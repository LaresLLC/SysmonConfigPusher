# Intro

Sysmon is an amazing tool that gives you enhanced visibility on endpoints. Installing Sysmon is a fairly straightforward process, involving a few commands and a configuration file.

However, when scale is introduced to the equation, a Sysmon deployment becomes more complex and cumbersome. 

One of the more popular methods of maintaining a Sysmon deployment across a fleet of computers is with a scheduled task. This process is outlined here: 

https://www.syspanda.com/index.php/2017/02/28/deploying-sysmon-through-gpo/

This approach runs a batch file at a set interval on systems, the batch script pulls a Sysmon configuration file from a central share and updates an existing Sysmon installation on a host.

# Scale is Hard

There is nothing wrong with the Scheduled Task approach to managing a Sysmon deployment. However, what if you wanted to have a certain version of your Sysmon configuration file on a certain set of hosts. For example, you want to disable ImageLoad events on sensitive servers, but not on end-user workstations. The servers that you want the ImageLoad events disabled on would now need a new GPO, with a script that points these hosts to a different Sysmon configuration file. This can get cumbersome very quickly if you find yourself running into this situation more than a handful of times.

# Just Push It

This is the problem SysmonConfigPusher attempts to solve. Rather than having hosts check for a certain configuration file from a central share, SysmonConfigPusher will read a directory full of different Sysmon configs and will parse out a tag value that you enter. You can then choose which configuration file you want to deploy to which host or hosts. 

# Requirements 

- .NET 4.0
- Windows Host
- SysmonConfigPusher must be run with credentials that have administrative access to the hosts that configs are being pushed to
- Endpoints must be able to reach the host that SysmonConfigPusher is runnning on, over port 80

# Usage

## Edit SysmonConfigPusher.exe.config

The configuration for Sysmon Config Pusher has three main values that you will want to change:

- DomainName: This is the domain that SysmonConfigPusher will load computers from (You Can also load a list of computers via a text file)
- WebServerIP: SysmonConfigPusher has a built in web server which endpoints connect to in order to download the appropriate Sysmon configuration file, this value needs to be set to an IP address that is able to bind to port 80
- SysmonConfigLocation: This is the directory, on the host that is running SysmonConfigPusher, that holds your various Sysmon configs - NOTE: The "\\" need to be escaped here

An example: 

```xml
<!-- DomainName should be the value of the domain that you want to get a listing of computers from 
  Example values: larescf - this does NOT need to be the FQDN of the domain -->
<add key="DomainName" value="larescf" />
<!-- WebServerIP is the IPv4 address on which your web server will listen, on port 80 -->
<add key="WebServerIP" value="192.168.1.134" />
<!-- SysmonConfigLocation is the value where your local Sysmon configurations live, this value must be escaped: C:\\SysmonConfigs\\ NOT C:\SysmonConfigs\ -->
<add key="SysmonConfigLocation" value="C:\\Users\\Administrator\\Desktop\\SysmonConfigs\\" />
```

## Tag your Sysmon Configs

After editing the SysmonConfigPusher.exe.config, and setting the appropriate SysmonConfigLocation value, you can now go into that directory and create a Sysmon configuration file. I highly suggest taking a look at Carlos Perez's Sysmon extension for VSCode: https://marketplace.visualstudio.com/items?itemName=DarkOperator.sysmon for creating and working with Sysmon configurations. 

Once your Sysmon configuration file is created, you need to add the following comment: 

```xml
<!--SCPTAG: Silent Config-->
```

SysmonConfigParser will grab anything after the ":" symbol as the configuration tag, so in this case the configuration will be tagged as "Silent Config", so if the comment was:

```xml
<!--SCPTAG: Servers-->
```

The tag assigned will be "Servers"

It does not matter where in the file this comment is, but for ease of use and tracking it is easier to place it on the first line of your Sysmon configuration, above the schemaversion XML attribute. 

## Push Your Configs

Now that the configuration file is set and you have your Sysmon configurations tagged appropriatley, you can start SysmonConfigPusher - the first step from here is to give it a list of computers. This can be done via the "Get Domain Computers" button, which will pull all the computer objects in the domain set in SysmonConfigPusher.exe.config OR you can use the "Load Computers From File" button and select a text file with a list of computers in a format like so:

```
Computer1
Computer2
Computer3
```

At this point you should see the computers you selected in the "Computers in Domain" text box.

Next, highlight the computers you want to perform actions on, within the "Computers in Domain" window, and then click on the "Select Computers" button - the computers you selected show now appear in the "Click on computers you want to action" window.

Next, again highlight the computers you want to perform actions on, within the "Click on computers you want to action" window.

At this point you have loaded a list of computers and have selected particular computers from that list to action. Now you can click on the "Load Configs" button which will read the Sysmon Configuration files in the directory and will present them to you visually like so:

```
[Tag]: Silent Config
C:\\Users\\Administrator\\Desktop\\SysmonConfigs\\config_silent.xml
[Tag]: Server Config
C:\\Users\\Administrator\\Desktop\\SysmonConfigs\\config_servers.xml
```
As mentioned earlier, the tag value is whatever you enter in the comment of your Sysmon config and the file name is what you choose to give to your Sysmon config. SysmonConfigPusher just reads and parses these values, it does not set them.

Next, select the configuration file you want to deploy (Note: The config path needs to be selected here, not the tag value).

At this point you should have both the computer(s) you want to deploy to as well as the Sysmon config file path selected.

If not done so already, click on the "Start Web Server" button, this will open a web server, listening on the IP Address specified in SysmonConfigPusher.exe.config - machines will connect to this web server to download the selected configuration file. 

The buttons on the right side of the Sysmon Config Pusher UI are designed to walk you through the order of operations of deploying a configuration file for hosts that have not had Sysmon installed on them, the order is as follows: 

**Create Directories** - This creates a directory called SysmonFiles in the C:\ drive of the hosts you selected

**Push Newest Executable from Sysinternals** - This downloads the 64bit version of Sysmon from live.sysinternals.com and places it in C:\SysmonFiles on the selected hosts

**Install Sysmon On Selected Computers** - This initates the "Sysmon64.exe -accepteula -i" command on the selected computers for a base Sysmon installation

**Push Configs** - This initates a download of the selected configuration file on the selected computers 

**Update Config on Selected Computers** - This initiates the "Sysmon64.exe -c <config value.xml>" command on the selected computers

# Why So Many Buttons ? 

Sysmon Config Pusher has a lot of moving parts and logically seperating these tasks using graphical buttons helps me keep track of what I'm doing 😎 

The other, and more important reason, for so many buttons is that it seperates out functionality giving you flexibility. In our earlier example we went through a full blown flow for machines without Sysmon installed on them. What if the systems did have Sysmon installed and you just wanted to update the configuration file on them? This is doable with the interface: 

Get/Load Computers --> Select Computers --> Load Configs --> Start Web Server --> Push Configs --> Update Configs 

You also have the option of first staging Sysmon and it's configuration files, but not actually installing Sysmon or the config

Get/Load Computers --> Select Computers --> Load Configs --> Start Web Server --> Push Newst Executable --> Push Configs

If an upgraded version of Sysmon gets released, there is also a flow for that:

Get/Load Computers --> Select Computers --> Uninstall Sysmon from Selected Computers

and then

Get/Load Computers --> Select Computers --> Load Configs --> Start Web Server --> Push Newst Sysmon Executable From Sysinternals --> Install Sysmon on Selected Computers --> Push Configs --> Update Configs on Selected Computers

# Under the Hood

Sysmon Config Pusher relies on WMI and PowerShell to issue remote commands to the computers you select, here's an example snippet of what happens when you click the "Push Newst Executable From Sysinternals" button:

```csharp
ManagementClass processClass = new ManagementClass($@"\\{SelectedComputer}\root\cimv2:Win32_Process");
ManagementBaseObject inParams = processClass.GetMethodParameters("Create");


inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command \"Invoke-WebRequest -UseBasicParsing -Uri http://live.sysinternals.com/Sysmon64.exe -OutFile C:\\SysmonFiles\\Sysmon64.exe";
inParams["CurrentDirectory"] = @"C:\SysmonFiles";
```

The majority of the the other functionality within the app uses the same logic, just with different commands, another example of how the directory is created:

```csharp
inParams["CommandLine"] = "PowerShell -WindowStyle Hidden -Command New-Item -Path C:\\ -Name SysmonFiles -ItemType Directory";
```

# Security Concerns

- Uses Port 80
- Give a list of event IDs
