using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Windows;
using System.Windows.Controls;
using System.Management;
using System.Net;
using System.Threading;
using System.IO;
using Ceen.Httpd;
using Ceen.Httpd.Handler;
using Ceen.Httpd.Logging;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Windows.Forms;
using System.Linq;
using System.Net.NetworkInformation;
using Serilog;
using System.Diagnostics.Eventing.Reader;


namespace SysmonConfigPusher

{




    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        

        //private object result;

        public MainWindow()
        {
            InitializeComponent();
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("SysmonConfigPusher.log", rollingInterval: RollingInterval.Month)
            .CreateLogger();

        }

        public object Controls { get; private set; }

        // Stuff that happens when you click the "Get Domain Computers" Button
       

        public void Button_Click(object sender, RoutedEventArgs e)
        {
            List<String> ComputerNames = GetComputers();
            // Clear the values in case the button is clicked twice
            ComputerList.Items.Clear();
            foreach (object Computer in ComputerNames)
            {
                ComputerList.Items.Add(Computer);
            }
            
        }

        private void ListBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {

        }

        // This part gets the list of computers from the domain
        public static List<string> GetComputers()
        {

            List<string> computerNames = new List<string>();

            string configDomainName = ConfigurationManager.AppSettings.Get("DomainName");
            
            using (DirectoryEntry entry = new DirectoryEntry("LDAP://"+configDomainName))
            
            {
                using (DirectorySearcher mySearcher = new DirectorySearcher(entry))
                {
                    mySearcher.Filter = ("(objectClass=computer)");

                    // No size limit, reads all objects
                    mySearcher.SizeLimit = 0;

                    // Read data in pages of 250 objects. Make sure this value is below the limit configured in your AD domain (if there is a limit)
                    mySearcher.PageSize = 250;

                    // Let searcher know which properties are going to be used, and only load those
                    mySearcher.PropertiesToLoad.Add("name");
                    //Ref: https://stackoverflow.com/questions/4094682/c-sharp-check-domain-is-live

                    Ping pingSender = new Ping();
                    PingReply reply = pingSender.Send(configDomainName);
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        Log.Information("Domain Ping Check Complete for " + configDomainName);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Error Contacting Domain, Please Check Config Settings and Log File for More Information");
                        Log.Information("Failed Domain Ping Check for " + configDomainName);
                    }
                    
                    foreach (SearchResult resEnt in mySearcher.FindAll())
                    {
                        // Note: Properties can contain multiple values.
                        if (resEnt.Properties["name"].Count > 0)
                        {
                            string computerName = (string)resEnt.Properties["name"][0];
                            computerNames.Add(computerName);
                        }
                    }
                }
            }

            return computerNames;

        }

        // This takes the selected items from the "ComputerList" ListBox, loops through them and puts them into another ListBox named "SelectedComputerList" - we want to perform whatever actions we need on this list

        public void SelectComputers_Click(object sender, RoutedEventArgs e)
        {
            //Need better logic for handling duplicates here
           SelectedComputerList.Items.Clear();
            Ping pingSender = new Ping();
            var SelectedComputers = ComputerList.SelectedItems;
            foreach (object SelectedComputer in SelectedComputers)
            {
                PingReply ComputerPingReply = pingSender.Send(SelectedComputer.ToString());
                if(ComputerPingReply.Status == IPStatus.Success)
                {
                    SelectedComputerList.Items.Add(SelectedComputer);
                    Log.Information(SelectedComputer + " Passed Ping Check");
                }
                else
                {
                    Log.Information(SelectedComputer + " Did not pass the Ping Check and was not added to the list");
                }
                
            }

        }

        public void ExecuteCommand_Click(object sender, RoutedEventArgs e)
        {
            if (Configs.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Error, Please Select a Configuration Value");
                return;
            }

            string selectedItem = Configs.Items[Configs.SelectedIndex].ToString();
            bool containsTag = selectedItem.Contains("Tag");

            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;

            }

            if (containsTag == true)
            {
                System.Windows.MessageBox.Show("Error, Select config value, not the tag");
                return;
            }

            string WebServerLabelContent = (string)WebServerLabel.Content;

            if (WebServerLabelContent == "Web Server Stopped")
            {
                System.Windows.MessageBox.Show("Start the Web Server First");
                return;
            }


        
            //RegEx: ([^\\]*)$

            Regex ConfigToDeployRegEx = new Regex(@"([^\\]*)$");
            Match MatchedSysmonConfig = ConfigToDeployRegEx.Match(selectedItem);

            System.Collections.IList
            //This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                //Selected Sysmon Config Variable Name = FinalSysmonMatchedConfig
                var FinalSysmonMatchedConfig = MatchedSysmonConfig.ToString();

                //Get Web Server IP Address from config
                string configWebServerIP = ConfigurationManager.AppSettings.Get("WebServerIP");

                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://"+ configWebServerIP +"/" + FinalSysmonMatchedConfig + " -OutFile C:\\SysmonFiles\\" + FinalSysmonMatchedConfig;
                inParams["CurrentDirectory"] = @"C:\";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                Log.Information("Pushing " + FinalSysmonMatchedConfig + " to " + SelectedComputer);
            }
            
        }

        private void ClearSelectedComputers_Click(object sender, RoutedEventArgs e)
        {
            SelectedComputerList.Items.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ComputerList.Items.Clear();
        }

        
        private void StartWebServer_Click(object sender, RoutedEventArgs e)
        {
            string configSysmonConfigLocation = ConfigurationManager.AppSettings.Get("SysmonConfigLocation");
            string configWebServerIP = ConfigurationManager.AppSettings.Get("WebServerIP");

            WebServerLabel.Content = "Web Server Stopped";

            var tcs = new CancellationTokenSource();
            var config = new ServerConfig()
                .AddLogger(new CLFStdOut())
                //This is the directory where the Sysmon files live
                
                .AddRoute(new FileHandler(configSysmonConfigLocation));
            var task = HttpServer.ListenAsync(
                new IPEndPoint(IPAddress.Any, 80),
                false,
                config,
                tcs.Token
            );
            WebServerLabel.Content = "Web Server Started";
            Log.Information("Web Server Started on " + configWebServerIP + ", serving configs from " + configSysmonConfigLocation);

        }

        public void LoadConfigs_Click(object sender, RoutedEventArgs e)
        {
            string configSysmonConfigLocation = ConfigurationManager.AppSettings.Get("SysmonConfigLocation");

            Configs.Items.Clear();

            Regex tagregex = new Regex(@"(?<=SCPTAG\: )((?<config_tag>.*))(?=\-\-\>)");

            //Needs to be in config
            var sourceDirectory =  configSysmonConfigLocation;
            var SysmonConfigs = Directory.EnumerateFiles(sourceDirectory, "*.xml");

            foreach(string currentSysmonConfig in SysmonConfigs)
            {
                using (StreamReader r = new StreamReader(currentSysmonConfig))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        Match match = tagregex.Match(line);
                        if (match.Success)
                        {
                            string matchedtag = match.Groups[1].Value;
                            Configs.Items.Add("[Tag]: " + matchedtag);
                            Configs.Items.Add(currentSysmonConfig);
                        }
                    }
                }
            }
        }

        private void CreateDirectories_Click(object sender, RoutedEventArgs e)
        {
    

            System.Collections.IList
            //This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command New-Item -Path C:\\ -Name SysmonFiles -ItemType Directory";
                inParams["CurrentDirectory"] = @"C:\";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                Log.Information("SysmonFiles Directory created on " + SelectedComputer);
            }

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Configs.Items.Clear();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            string selectedItem = Configs.Items[Configs.SelectedIndex].ToString();
            bool containsTag = selectedItem.Contains("Tag");
            if (containsTag == true)
            {
                System.Windows.MessageBox.Show("Error, Select config value, not the tag");
                return;
            }
            //UpdateConfigs
            Regex ConfigToDeployRegEx = new Regex(@"([^\\]*)$");
            Match MatchedSysmonConfig = ConfigToDeployRegEx.Match(selectedItem);

            System.Collections.IList
            //This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
           

            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;

            }

            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                //Selected Sysmon Config Variable Name = FinalSysmonMatchedConfig
                var FinalSysmonMatchedConfig = MatchedSysmonConfig.ToString();
                                
                inParams["CommandLine"] = "C:\\SysmonFiles\\Sysmon.exe -c " + FinalSysmonMatchedConfig;
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Updated " + SelectedComputer + " with " + FinalSysmonMatchedConfig);

                string logQuery = "*[System[(EventID = 16)]]";

                EventLogSession session = new EventLogSession(SelectedComputer.ToString());

                EventLogQuery query = new EventLogQuery("Microsoft-Windows-Sysmon/Operational", PathType.LogName, logQuery);

                query.Session = session;

                EventLogReader logReader = new EventLogReader(query);

                for(EventRecord eventdetail = logReader.ReadEvent(); eventdetail!=null; eventdetail = logReader.ReadEvent())
                {
                    string EventData = eventdetail.ToXml();

                    Regex SHA256 = new Regex(@"[A-Fa-f0-9]{64}");
                    Match SHA256Value = SHA256.Match(EventData);
                    Regex LoggedEventTime = new Regex(@"\d\d\d\d\-\d\d\-\d\d.\d\d\:\d\d\:\d\d\.\d\d\d");
                    Match MatchedLoggedEventTime = LoggedEventTime.Match(EventData);

                    Log.Information("Found Config Update Event on " + SelectedComputer + " Logged at " + MatchedLoggedEventTime + "." + " Updated with config file with the SHA256 Hash of: " + SHA256Value.ToString());
                    
                }
            }
        }

        private void PushExecutable_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;

            }
            System.Collections.IList
            //This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");


                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://live.sysinternals.com/Sysmon.exe -OutFile C:\\SysmonFiles\\Sysmon.exe";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Sysmon executable pushed to " + SelectedComputer);
            }
        }

        private void FromFile_Click(object sender, RoutedEventArgs e)
        {
            //Ref-https://www.c-sharpcorner.com/UploadFile/mahesh/openfiledialog-in-C-Sharp/
            //Ref-https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-read-from-a-text-file
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.ShowDialog();
            openFileDialog1.Title = "Browse to your list of Computers (.txt)";
            openFileDialog1.DefaultExt = "txt";
            openFileDialog1.Filter = "txt files (*.txt)|*.txt";
            string[] computers = System.IO.File.ReadAllLines(openFileDialog1.FileName);

            foreach (string computer in computers)
            {
                ComputerList.Items.Add(computer);
            }

            
           
        }

        private void UnInstallSysmon_Click(object sender, RoutedEventArgs e)
        {
            System.Collections.IList
            //This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                inParams["CommandLine"] = "Sysmon.exe -u";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                Log.Information("Sysmon removed from " + SelectedComputer);
            }
        }

        private void InstallSysmon_Click(object sender, RoutedEventArgs e)
        {
            System.Collections.IList
            //This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                inParams["CommandLine"] = "C:\\SysmonFiles\\Sysmon.exe -accepteula -i";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Sysmon installed on " + SelectedComputer);
            }
        }


        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            

            //Need to convert the list of computers from the list box into a list first: https://stackoverflow.com/questions/1565504/most-succinct-way-to-convert-listbox-items-to-a-generic-list

            //ComputersList is the string version of the ListBox contents ComputerList

            var ComputersList = ComputerList.Items.Cast<String>().ToList();

            //REF: https://stackoverflow.com/questions/29963240/textbox-textchange-to-update-an-onscreen-listbox-in-c-sharp

            //I'm not sure exactly what this line does
            System.Windows.Controls.TextBox s = (System.Windows.Controls.TextBox)sender;

            //Clear the results when searching
            ComputerList.Items.Clear();

            //Loop through newly created list of computers and compare the text, if there is a match, add it to the ListBox
            foreach (string value in ComputersList)
            {
                if (value.IndexOf(s.Text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ComputerList.Items.Add(value);
                }
            }
        }

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            txtSearch.AppendText("Filter for Computer...");
        }
    }
}
           

