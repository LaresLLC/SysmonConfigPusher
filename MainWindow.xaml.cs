//   _______ __   __ _______ __   __ _______ __    _ _______ _______ __    _ _______ ___ _______ _______ __   __ _______ __   __ _______ ______   
//  |       |  | |  |       |  |_|  |       |  |  | |       |       |  |  | |       |   |       |       |  | |  |       |  | |  |       |    _ |  
//  |  _____|  |_|  |  _____|       |   _   |   |_| |       |   _   |   |_| |    ___|   |    ___|    _  |  | |  |  _____|  |_|  |    ___|   | ||  
//  | |_____|       | |_____|       |  | |  |       |       |  | |  |       |   |___|   |   | __|   |_| |  |_|  | |_____|       |   |___|   |_||_ 
//  |_____  |_     _|_____  |       |  |_|  |  _    |      _|  |_|  |  _    |    ___|   |   ||  |    ___|       |_____  |       |    ___|    __  |
//   _____| | |   |  _____| | ||_|| |       | | |   |     |_|       | | |   |   |   |   |   |_| |   |   |       |_____| |   _   |   |___|   |  | |
//  |_______| |___| |_______|_|   |_|_______|_|  |__|_______|_______|_|  |__|___|   |___|_______|___|   |_______|_______|__| |__|_______|___|  |_|
//     _     _   
//    (c).-.(c)  
//     / ._. \   
//   __\( Y )/__ 
//  (_.-/'-'\-._)
//    || SCP ||   
//   _.' `-' '._ 
//  (.-./`-`\.-.)
//   `-'     `-' 


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
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections;


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
            // This sets up logging - logs to SysmonConfigPusher.log, to log other things: Log.Information("stuff")
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
            // Get list of computers for the domain and add them to the listbox
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
            //REF: https://stackoverflow.com/questions/1605567/list-all-computers-in-active-directory?rq=1
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
                    try
                    {
                        PingReply reply = pingSender.Send(configDomainName);
                        if (reply.Status == IPStatus.Success)
                        {
                            // If the ping is successful, log it
                            Log.Information("Domain Ping Check Complete for " + configDomainName);
                        }
                        else
                        {
                            // If ping failes, pop up a message box and log it
                            System.Windows.MessageBox.Show("Error Contacting Domain, Please Check Config Settings and Log File for More Information");
                            Log.Information("Failed Domain Ping Check for " + configDomainName);
                        }
                    }

                    catch (Exception domainpingexception)
                    {
                        // Catch the exception and log it when there is something wrong with the domain ping
                        System.Windows.MessageBox.Show("Error Contacting Domain, Please Check Config Settings and Log File for More Information");
                        Log.Information(domainpingexception.Message);
                    }
                    // Loop through computers in the domai and add them, we populate the listbox with the code above
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
            // If we click on select computers with no actual computers selected pop up a message box since the GUI is a bit confusing
            if (ComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer from the Live Computers in Domain List");
                return;
            }
            // Need better logic for handling duplicates here
            SelectedComputerList.Items.Clear();

            // Put the computers the user selected in the GUI into a variable
            var SelectedComputers = ComputerList.SelectedItems;

            // Set parallel options
            int computercount = ((IList)SelectedComputers).Count;
            var options = new ParallelOptions { MaxDegreeOfParallelism = 100 };

            // Need to put the list of computers into a blocking collection for parallel for each
            BlockingCollection<string> SelectedComputersCollection = new BlockingCollection<string>();

            // Loop through the list of selected computers and add them to the new blocking list
            foreach(object SelectedComputer in SelectedComputers)
            {
                SelectedComputersCollection.Add((string)SelectedComputer);
            }

            // Looping through the computers that are in the domain or populated via a text file - this logic performs a ping check on the computers and adds them to the middle computers you want to action listbox, these are the computers that will have various commands issued to them

            //REF: http://hk.uwenku.com/question/p-dyussklc-gg.html (sketchy site)


            // Setting the value for ping timeout, 5000ms 
            int pingtimeout = 5000;
            // Setting the current variable to 0, this is used for the progress bar
            int current = 0;
            // Setting the max value of the progress bar equtal to the amount of computers 
            myprogressDialog.Maximum = computercount;
            // Initailizing the percentcomplete variable, used for the progress bar
            int percentcomplete = 0;
            
            // Start the task for the foreach loop, this loops through a list of computers and adds only the live ones
            Task looper = new Task(() =>
            {
                Parallel.ForEach(SelectedComputersCollection, options, SelectedComputer =>
                // foreach (object SelectedComputer in SelectedComputers) -- this is the old for loop, leaving here just in case
                {
                    // Need to use the Dispatcher method to update GUI
                    Dispatcher.Invoke(async () =>
                    {
                        StatusLabel.Content = "Working...";
                       
                        // This stuff updates the progress bar, needs a bit of work
                        current++;
                        percentcomplete = (current / computercount) * 100;
                        myprogressDialog.Value = percentcomplete;
                        // End of progress bar update

                        // Within the loop, try, catch - try the pings and log successes, if it fails log the error message

                        Ping pingSender = new Ping();
                        //PingReply ComputerPingReply = pingSender.Send(SelectedComputer.ToString(), pingtimeout); -- this is the old single threaded ping, leaving here just in case                     
                        try
                        {                          
                            PingReply ComputerPingReply = await pingSender.SendPingAsync(SelectedComputer.ToString(), pingtimeout);
                            if (ComputerPingReply.Status == IPStatus.Success)
                            {
                                myprogressDialog.Value = current;
                                SelectedComputerList.Items.Add(SelectedComputer);
                                Log.Information(SelectedComputer + " Passed Ping Check");
                            }
                            if (ComputerPingReply.Status == IPStatus.DestinationHostUnreachable)
                            { 
                                myprogressDialog.Value = current;
                                Log.Information(SelectedComputer + " Was Unreachable");
                            }
                        }
                        catch (Exception pingexception)
                        {
                            Log.Debug(SelectedComputer + " : " + pingexception.InnerException.Message);
                        }
                    });
                } );
            } ); // closing brackets for the Task
            // Start our looper task
            looper.Start();
            // When the task is done, update the status label and dispose of the task
            looper.GetAwaiter().OnCompleted(() =>
            {
                StatusLabel.Content = "Done!";
                looper.Dispose();   
            } );
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
            // This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            // Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                // Selected Sysmon Config Variable Name = FinalSysmonMatchedConfig
                var FinalSysmonMatchedConfig = MatchedSysmonConfig.ToString();

                // Get Web Server IP Address from config
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
            myprogressDialog.Value = 0;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ComputerList.Items.Clear();
        }

        // This is stuff that happens when you click the Start WebServer button
        public void StartWebServer_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => {
                // Getting values from the configuration of the IP address of the web server as well as where the local bank of Sysmon configuration files are 
                string configSysmonConfigLocation = ConfigurationManager.AppSettings.Get("SysmonConfigLocation");
                string configWebServerIP = ConfigurationManager.AppSettings.Get("WebServerIP");

                WebServerLabel.Content = "Web Server Stopped";

                var tcs = new CancellationTokenSource();
                var config = new ServerConfig()
                    .AddLogger(new CLFStdOut())
                    //This is the directory where the local Sysmon files live, these will be pushed to the user-selected systems

                    .AddRoute(new FileHandler(configSysmonConfigLocation));
                var webservertask = HttpServer.ListenAsync(
                    new IPEndPoint(IPAddress.Any, 80),
                    false,
                    config,
                    tcs.Token
                );

                WebServerLabel.Content = "Web Server Started";
                Log.Information("Web Server Started on " + configWebServerIP + ", serving configs from " + configSysmonConfigLocation);
                return Task.CompletedTask;
            });
        }

        // This is what happens when you click the "Load Configs" button
        public void LoadConfigs_Click(object sender, RoutedEventArgs e)
        {
            string configSysmonConfigLocation = ConfigurationManager.AppSettings.Get("SysmonConfigLocation");

            Configs.Items.Clear();

            // This is the regex used to grab the tag value that you set within your Sysmon config, if you want to use a different prefix change the "SCPTAG" value here
            Regex tagregex = new Regex(@"(?<=SCPTAG\: )((?<config_tag>.*))(?=\-\-\>)");

            // Needs to be in config
            var sourceDirectory =  configSysmonConfigLocation;
            // To do: add the .smc extension here as well 
            var SysmonConfigs = Directory.EnumerateFiles(sourceDirectory, "*.xml");
            // We loop through the XML files of where our Sysmon configurations live, and we extract the tag value as well as the name of the config, both values get populated to the list box
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

            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }

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
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }
            // If the selected listbox item is the tag of the config, rather than the configuration itself, pop up an error and let the user try again
            string selectedItem = Configs.Items[Configs.SelectedIndex].ToString();
            bool containsTag = selectedItem.Contains("Tag");
            if (containsTag == true)
            {
                System.Windows.MessageBox.Show("Error, Select config value, not the tag");
                return;
            }
            // UpdateConfigs
            Regex ConfigToDeployRegEx = new Regex(@"([^\\]*)$");
            Match MatchedSysmonConfig = ConfigToDeployRegEx.Match(selectedItem);

            System.Collections.IList
            // This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
           
            // If there is no computer selected to update the configuration on, pop up an error and let the user try again
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }

            // Run command on whatever computers we selected - probably need a better way to do this at some point, with multiple threads etc - this is a pretty funky loop as we are doing some validation here as well
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
                try
                {
                    EventLogReader logReader = new EventLogReader(query);
                    // Loop through the events that were returned in the above query
                    for (EventRecord eventdetail = logReader.ReadEvent(); eventdetail != null; eventdetail = logReader.ReadEvent())
                    {
                        // EventData variable contains the detail of each event in XML format, I tried to use LINQ to extract the XML elements instead of regex but found regex to be simpler, please don't hate me for the upcoming dirty regexes
                        string EventData = eventdetail.ToXml();
                        // RegEx used to extract just the SHA256 hash from Event ID 16
                        Regex SHA256 = new Regex(@"[A-Fa-f0-9]{64}");
                        // Put the matched regex (the SHA256) hash into a variable called SHA256Value
                        Match SHA256Value = SHA256.Match(EventData);
                        // Another awful regex to extract the time stamp from Event ID 16 - the SHA256 value of the updated config as well as the time stamp get logged, this way you can validate that the right configuration file got pushed to the right computer
                        Regex LoggedEventTime = new Regex(@"\d\d\d\d\-\d\d\-\d\d.\d\d\:\d\d\:\d\d\.\d\d\d");
                        Match MatchedLoggedEventTime = LoggedEventTime.Match(EventData);
                        // Log showing that we found an Event ID 16 on the selected remote host, and we log the time and SHA256 value of the configuration file pushed
                        Log.Information("Found Config Update Event on " + SelectedComputer + " Logged at " + MatchedLoggedEventTime + "." + " Updated with config file with the SHA256 Hash of: " + SHA256Value.ToString());
                    }
                }
                catch(Exception eventlogexception)
                {
                    Log.Debug(eventlogexception.Message + ": You may have hit the update configs button on a host with Sysmon not installed");
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
            // This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            // Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
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
            try
            {
                string[] computers = System.IO.File.ReadAllLines(openFileDialog1.FileName);
                foreach (string computer in computers)
                {
                    ComputerList.Items.Add(computer);
                }
            }
            catch (Exception openfilesexception)
            {
                Log.Information(openfilesexception.Message + " - this is logged when no computer list is selected");
            }
        }

        // Stuff that happens when you click the uninstall Sysmon button
        private void UnInstallSysmon_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }
            System.Collections.IList
            // This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            // Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
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
            if (SelectedComputerList.SelectedIndex < 0)
            {
                System.Windows.MessageBox.Show("Please Select a Computer");
                return;
            }
            System.Collections.IList
            // This grabs the selected computer variable
            ComputerSelected = SelectedComputerList.SelectedItems;
            // Run command on whatever computers we selected - probably need a beter way to do this at some point, with multiple threads etc
            foreach (object SelectedComputer in ComputerSelected)
            {
                ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");

                // Command that gets executed on the remote host, here we are just installing Sysmon, not giving it a configuration file yet, wanted to make these steps as modular as possible to accomodate different usecases
                inParams["CommandLine"] = "C:\\SysmonFiles\\Sysmon.exe -accepteula -i";
                inParams["CurrentDirectory"] = @"C:\SysmonFiles";

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                Log.Information("Sysmon installed on " + SelectedComputer);
            }
        }

        // Code for the search bar, clobbered together from various stack overflow posts - the searchbar works but does not behave very well and is a but cumbersome
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            
            // Need to convert the list of computers from the list box into a list first: https://stackoverflow.com/questions/1565504/most-succinct-way-to-convert-listbox-items-to-a-generic-list
            // ComputersList is the string version of the ListBox contents ComputerList
            var ComputersList = ComputerList.Items.Cast<String>().ToList();
            // REF: https://stackoverflow.com/questions/29963240/textbox-textchange-to-update-an-onscreen-listbox-in-c-sharp
            // I'm not sure exactly what this line does
            System.Windows.Controls.TextBox s = (System.Windows.Controls.TextBox)sender;
            // Clear the results when searching
            ComputerList.Items.Clear();

            // Loop through newly created list of computers and compare the text, if there is a match, add it to the ListBox
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
            // The below code breaks the selection in the listbox, so it's commented out for now
            //txtSearch.AppendText("Filter for Computer...");
        }
        // Stuff that happens when you click "Select All" under the "Computers in Domain" list box
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            ComputerList.SelectAll();
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Nothing here
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.Application.Restart();
            Environment.Exit(0);
        }

        // Stuff that happens when you click "Select All" under the "Click on computers you want to action" listbox
        private void SelectAllActionComputers_Click(object sender, RoutedEventArgs e)
        {
            SelectedComputerList.SelectAll();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            // When the user clicks exit, we log it, close and flush the log file and exit the app
            Log.Debug("Application Exit");
            Log.CloseAndFlush();
            Environment.Exit(0);
        }
    }
}