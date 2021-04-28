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

        }

        public object Controls { get; private set; }

        // Stuff that happens when you click the "Get Domain Computers" Button
        private void Button_Click(object sender, RoutedEventArgs e)
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
            //SelectedComputerList.Items.Clear();

            var SelectedComputers = ComputerList.SelectedItems;
            foreach (object SelectedComputer in SelectedComputers)
            {
                SelectedComputerList.Items.Add(SelectedComputer);
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

                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command New-Item -Path C:\\ -Name SysmonFiles -ItemType Directory";
                
                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://"+ configWebServerIP +"/" + FinalSysmonMatchedConfig + " -OutFile C:\\SysmonFiles\\" + FinalSysmonMatchedConfig;
                inParams["CurrentDirectory"] = @"C:\";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
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

                
                inParams["CommandLine"] = "Sysmon64.exe -c " + FinalSysmonMatchedConfig;
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
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


                inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://live.sysinternals.com/Sysmon64.exe -OutFile C:\\SysmonFiles\\Sysmon64.exe";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
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

                inParams["CommandLine"] = "Sysmon64.exe -u";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
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

                inParams["CommandLine"] = "Sysmon64.exe -accepteula -i";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
            }
        }
    }
}
           

