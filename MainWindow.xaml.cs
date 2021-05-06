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
            //This sets up logging - logs to SysmonConfigPusher.log, to log other things: Log.Information("stuff")

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
            //No actions here for when list box is changed, don't delete 
        }

        // This part gets the list of computers from the domain
        public static List<string> GetComputers()
        {

            List<string> computerNames = new List<string>();

            //Get the DomainName from the config file, needs to be a FQDN
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

                    // Doing a quick ping check on the domain to see if it's alive
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
            // Looping through the computers that are in the domain or populated via a text file - this logic performs a ping check on the computers and adds them to the middle computers you want to action listbox, these are the computers that will have various commands issued to them
            foreach (object SelectedComputer in SelectedComputers)
            {
                
                // Want to test for a ping response before adding the computer to the list, if it passed the ping check, add it to the listbox, if it does not pass the ping check, don't add it to the listbox and log it 
                // This part is very slow, I tried playing with ping options like buffer, ttl and timeouts with no noticeable impact, this part needs optimization for large computer lists
                try
                {
                    PingReply ComputerPingReply = pingSender.Send(SelectedComputer.ToString());
                    if (ComputerPingReply.Status == IPStatus.Success)
                    {
                        SelectedComputerList.Items.Add(SelectedComputer);
                        Log.Information(SelectedComputer + " Passed Ping Check");
                    }
                    else
                    {
                        Log.Information(SelectedComputer + " Did not pass the Ping Check and was not added to the list");
                    }
                }
                catch (Exception pingexception)
                {
                    Log.Information(pingexception.Message);
                }
            }
        }

        // This stuff happens when you click the "Push Configs" Button
        public void ExecuteCommand_Click(object sender, RoutedEventArgs e)
        {
            // If there is no config selected, pop up an error message and let the user try again - this isn't logged to the log file
            if (Configs.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Error, Please Select a Configuration Value");
                return;
            }

            string selectedItem = Configs.Items[Configs.SelectedIndex].ToString();
            bool containsTag = selectedItem.Contains("Tag");

            // If there is no computer selected, pop up an error message and let the user try again - this isn't logged to the log file
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }

            // The tag value is there for display only, so you can see which Sysmon config has what tag applied, but this can be a little confusing, so an error message is shown when the tag value is selected instead of the config value - the config value itself needs to be selected as we pass that value to a command later
            if (containsTag == true)
            {
                System.Windows.MessageBox.Show("Error, Select config value, not the tag");
                return;
            }

            string WebServerLabelContent = (string)WebServerLabel.Content;

            // If we try to push configs without the web server started, an error message is popped up as the web server needs to be active
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

                // This is the command ran on the remote computer, the idea here is that the remote computer runs a PowerShell Invoke-WebRequest for the config that is hosted on the host on which SysmonConfigPusher is running on
                // For some reason, I've experienced some quirks qith the CurrentDirectory setting so where possible I hardcode the path to C:\SysmonFiles - to do item to make the directory location a configurable variable

                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://"+ configWebServerIP +"/" + FinalSysmonMatchedConfig + " -OutFile C:\\SysmonFiles\\" + FinalSysmonMatchedConfig;
                inParams["CurrentDirectory"] = @"C:\\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                Log.Information("Pushing " + FinalSysmonMatchedConfig + " to " + SelectedComputer);
            }
            
        }

        // These clear the values for selected computers and computers in the domain respectively 
        private void ClearSelectedComputers_Click(object sender, RoutedEventArgs e)
        {
            SelectedComputerList.Items.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ComputerList.Items.Clear();
        }

        // This is stuff that happens when you click the Start WebServer button
        private void StartWebServer_Click(object sender, RoutedEventArgs e)
        {
            // Getting values from the configuration of the IP address of the web server as well as where the local bank of Sysmon configuration files are 
            string configSysmonConfigLocation = ConfigurationManager.AppSettings.Get("SysmonConfigLocation");
            string configWebServerIP = ConfigurationManager.AppSettings.Get("WebServerIP");

            WebServerLabel.Content = "Web Server Stopped";

            var tcs = new CancellationTokenSource();
            var config = new ServerConfig()
                .AddLogger(new CLFStdOut())
                //This is the directory where the local Sysmon files live, these will be pushed to the user-selected systems
                
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

        //This is what happens when you click the "Load Configs" button
        public void LoadConfigs_Click(object sender, RoutedEventArgs e)
        {
            string configSysmonConfigLocation = ConfigurationManager.AppSettings.Get("SysmonConfigLocation");

            Configs.Items.Clear();

            // This is the regex used to grab the tag value that you set within your Sysmon config, if you want to use a different prefix change the "SCPTAG" value here
            Regex tagregex = new Regex(@"(?<=SCPTAG\: )((?<config_tag>.*))(?=\-\-\>)");

            //Needs to be in config
            var sourceDirectory =  configSysmonConfigLocation;
            //To do: add the .smc extension here as well 
            var SysmonConfigs = Directory.EnumerateFiles(sourceDirectory, "*.xml");
            //We loop through the XML files of where our Sysmon configurations live, and we extract the tag value as well as the name of the config, both values get populated to the list box
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

        // This stuff happens when you click the "Create Directories" button
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

                // Creating a directory named "SysmonFiles" in the C: drive of the remote host
                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command New-Item -Path C:\\ -Name SysmonFiles -ItemType Directory";
                inParams["CurrentDirectory"] = @"C:\";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                Log.Information("SysmonFiles Directory created on " + SelectedComputer);
            }

        }
        // This clears the configuration files listbox
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Configs.Items.Clear();
        }
        // This stuff happens when you click the "Update config on selected computers button"
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            // If the selected listbox item is the tag of the config, rather than the configuration itself, pop up an error and let the user try again
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
           
            // If there is no computer selected to update the configuration on, pop up an error and let the user try again
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }

            //Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc - this is a pretty funky loop as we are doing some validation here as well
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                //Selected Sysmon Config Variable Name = FinalSysmonMatchedConfig
                var FinalSysmonMatchedConfig = MatchedSysmonConfig.ToString();
                
                //Commands ran on the remote host to update the configuration file
                inParams["CommandLine"] = "C:\\SysmonFiles\\Sysmon.exe -c " + FinalSysmonMatchedConfig;
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Updated " + SelectedComputer + " with " + FinalSysmonMatchedConfig);

                // XPath Query for Event ID 16s only, this is the "Sysmon config state changed" event - later we specify the log channel and extract the SHA256 value of the configuration file hash as it exists on the remote host
                string logQuery = "*[System[(EventID = 16)]]";

                //Establish a remote event log session on the computer in this for loop
                EventLogSession session = new EventLogSession(SelectedComputer.ToString());
                EventLogQuery query = new EventLogQuery("Microsoft-Windows-Sysmon/Operational", PathType.LogName, logQuery);
                query.Session = session;
                EventLogReader logReader = new EventLogReader(query);

                // Loop through the events that were returned in the above query
                for(EventRecord eventdetail = logReader.ReadEvent(); eventdetail!=null; eventdetail = logReader.ReadEvent())
                {
                    // EventData variable contains the detail of each event in XML format, I tried to use LINQ to extract the XML elements instead of regex but found regex to be simpler, please don't hate me for the upcoming dirty regexes
                    string EventData = eventdetail.ToXml();
                    // RegEx used to extract just the SHA256 hash from Event ID 16
                    Regex SHA256 = new Regex(@"[A-Fa-f0-9]{64}");
                    // Put the matched regex (the SHA256) hash into a variable called SHA256Value
                    Match SHA256Value = SHA256.Match(EventData);
                    /// Another awful regex to extract the time stamp from Event ID 16 - the SHA256 value of the updated config as well as the time stamp get logged, this way you can validate that the right configuration file got pushed to the right computer
                    Regex LoggedEventTime = new Regex(@"\d\d\d\d\-\d\d\-\d\d.\d\d\:\d\d\:\d\d\.\d\d\d");
                    Match MatchedLoggedEventTime = LoggedEventTime.Match(EventData);
                    //Log showing that we found an Event ID 16 on the selected remote host, and we log the time and SHA256 value of the configuration file pushed
                    Log.Information("Found Config Update Event on " + SelectedComputer + " Logged at " + MatchedLoggedEventTime + "." + " Updated with config file with the SHA256 Hash of: " + SHA256Value.ToString());                    
                }
            }
        }
        // Stuff that happens when you click "Push latest Sysmon executable from sysinternals" button
        private void PushExecutable_Click(object sender, RoutedEventArgs e)
        {
            // If there is no computer selected to push the config to, pop up an error and let the user try again
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

                // This is the command that gets executed on the remote host, the Sysmon executable is downloaded directly from live.sysinternals.com 
                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://live.sysinternals.com/Sysmon.exe -OutFile C:\\SysmonFiles\\Sysmon.exe";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Sysmon executable pushed to " + SelectedComputer);
            }
        }
        // This is what happens when you click the load computers from file button, no ping checks here as we assume the user has done that work prior to building the list of computers - may be a wrong assumption :) 
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

        // Stuff that happens when you click the uninstall Sysmon button
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

                // Command that executed on the remote host, can tinker with this to add the -force flag as well
                inParams["CommandLine"] = "C:\\SysmonFiles\\Sysmon.exe -u";
                
                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                Log.Information("Sysmon removed from " + SelectedComputer);
            }
        }
        // Stuff that happens when you click the "Install Sysmon on Selected Computers" button
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

                //Command that gets executed on the remote host, here we are just installing Sysmon, not giving it a configuration file yet, wanted to make these steps as modular as possible to accomodate different usecases
                inParams["CommandLine"] = "C:\\SysmonFiles\\Sysmon.exe -accepteula -i";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Sysmon installed on " + SelectedComputer);
            }
        }

        // Code for the search bar, clobbered together from various stack overflow posts - the searchbar works but does not behave very well and is a but cumbersome
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
            //This breaks the selection in the listbox, so it's commented out for now
            //txtSearch.AppendText("Filter for Computer...");
        }
        // Stuff that happens when you click "Select All"
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            ComputerList.SelectAll();
        }
    }
}