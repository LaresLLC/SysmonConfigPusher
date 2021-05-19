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

- DomainName: This is the domain that SysmonConfigPusher will load computers from (You can also load a list of computers via a text file)

- WebServerIP: SysmonConfigPusher has a built in web server which endpoints connect to in order to download the appropriate Sysmon configuration file, this value needs to be set to an IP address that is able to bind to port 80 using the HTTP protocol.

- SysmonConfigLocation: This is the directory, on the host that is running SysmonConfigPusher, that holds your various Sysmon configs - NOTE: The "\\" need to be escaped here

An example: 

```xml
<!-- DomainName should be the value of the domain that you want to get a listing of computers from 
  Example values: larescf - this is the FQDN of the domain -->
<add key="DomainName" value="larescf.local" />
<!-- WebServerIP is the IPv4 address on which your web server will listen, on port 80 -->
<add key="WebServerIP" value="192.168.1.134" />
<!-- SysmonConfigLocation is the value where your local Sysmon configurations live, this value must be escaped: C:\\SysmonConfigs\\ NOT C:\SysmonConfigs\ -->
<add key="SysmonConfigLocation" value="C:\\Users\\Administrator\\Desktop\\SysmonConfigs\\" />
```

## Tag your Sysmon Configs

After editing the SysmonConfigPusher.exe.config, and setting the appropriate SysmonConfigLocation value, you can now go into that configured directory location and create or place an existing Sysmon configuration file within it. 

I highly suggest taking a look at Carlos Perez's Sysmon extension for VSCode: https://marketplace.visualstudio.com/items?itemName=DarkOperator.sysmon for creating and working with Sysmon configurations. 

Once your Sysmon configuration file is created, you need to add the following comment somewhere within your Sysmon configuration file: 

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

Next, highlight the computers you want to perform actions on, within the "Computers in Domain" window, and then click on the "Select Computers" button - the computers you selected show now appear in the "Click on computers you want to action" window. SysmonConfigPusher will perform a quick ping check on these machines and will only add live ones to the "Click on computers you want to action" window.

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

**Uninstall Sysmon from Selected Computers** - Uninstalls Sysmon from the selected computers using the -u flag

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

Sysmon Config Pusher relies on WMI and PowerShell to issue remote commands to the computers you select, here's an example snippet of what happens when you click the "Push Newest Executable From Sysinternals" button:

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

# Security & Artifacts
  
To keep the setup of Sysmon Config Pusher simple, the web server used runs over HTTP not HTTPS - this means that your Sysmon configuration file is transferred over the network in plain text. This introduces some risk, but the assumption is made that an attacker is not in your network in a MITM position. If this becomes a large issue for users, HTTPS support can be added in the future.

Because SysmonConfigPusher uses a privileged account, it's a good idea to monitor exactly what this account is doing and ensuring that it is only doing things that SysmonConfigPusher is configured to do and that is logging in from the server that SysmonConfigPusher is being ran on. There are only a few commands issued by SysmonConfigPusher to remote systems, so baselining this activity should be straight forward.

## Create Directories

- 4624 Type 3 Event With the account you use to run SysmonConfigPusher with. The IpAddress field should contain the IP address that is running SysmonConfigPusher
- 4627 Special privileges assigned to new login for the account that is used to run SysmonConfigPusher with
- 4688 Process Creation Event 1:
```xml
  <Data Name="SubjectUserSid">S-1-5-20</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e4</Data> 
  <Data Name="NewProcessId">0x12ac</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0xb0c</Data> 
  <Data Name="CommandLine">PowerShell -WindowStyle Hidden -Command New-Item -Path C:\ -Name SysmonFiles -ItemType Directory</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">Administrator</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x550fcef</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\wbem\WmiPrvSE.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
- 4688 Process Creation Event 2: 
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x550fcef</Data> 
  <Data Name="NewProcessId">0xa6c</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\conhost.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x12ac</Data> 
  <Data Name="CommandLine">\??\C:\Windows\system32\conhost.exe 0xffffffff -ForceV1</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```

A Parent or Creator Process of WmiPrvSE.exe is common among all the commands issued by SysmonConfigPusher - the Creator Subject Security ID will be of the NETWORK SERVICE account and the Target Subject Account Name will be the service account used to run SysmonConfigPusher

## Push Newst Executable From Sysinternals

- 4624 Type 3 Event With the account you use to run SysmonConfigPusher with. The IpAddress field should contain the IP address that is running SysmonConfigPusher
- 4627 Special privileges assigned to new login for the account that is used to run SysmonConfigPusher with
- 4688 Process Creation Event 1:
```xml
<Data Name="SubjectUserSid">S-1-5-20</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e4</Data> 
  <Data Name="NewProcessId">0x1a30</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0xb0c</Data> 
  <Data Name="CommandLine">PowerShell -WindowStyle Hidden -Command "Invoke-WebRequest -UseBasicParsing -Uri http://live.sysinternals.com/Sysmon.exe -OutFile C:\SysmonFiles\Sysmon.exe</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">Administrator</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x5520955</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\wbem\WmiPrvSE.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
- 4688 Process Creation Event 2: 
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x5520955</Data> 
  <Data Name="NewProcessId">0x15c4</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\conhost.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x1a30</Data> 
  <Data Name="CommandLine">\??\C:\Windows\system32\conhost.exe 0xffffffff -ForceV1</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```

## Install Sysmon on Selected Computers

- 4624 Type 3 Event With the account you use to run SysmonConfigPusher with. The IpAddress field should contain the IP address that is running SysmonConfigPusher
- 4627 Special privileges assigned to new login for the account that is used to run SysmonConfigPusher with
- 4688 Process Creation Event 1:
```xml
  <Data Name="SubjectUserSid">S-1-5-18</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e7</Data> 
  <Data Name="NewProcessId">0x180</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\sppsvc.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x208</Data> 
  <Data Name="CommandLine">C:\Windows\system32\sppsvc.exe</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">SERVER$</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x3e4</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\services.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-16384</Data> 
```
- 4688 Process Creation Event 2: 
```xml
  <Data Name="SubjectUserSid">S-1-5-20</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e4</Data> 
  <Data Name="NewProcessId">0x8c8</Data> 
  <Data Name="NewProcessName">C:\SysmonFiles\Sysmon.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0xb0c</Data> 
  <Data Name="CommandLine">C:\SysmonFiles\Sysmon.exe -accepteula -i</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">Administrator</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x555b61a</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\wbem\WmiPrvSE.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
-4688 Process Creation Event 3:
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x555b61a</Data> 
  <Data Name="NewProcessId">0x6c8</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\conhost.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x8c8</Data> 
  <Data Name="CommandLine">\??\C:\Windows\system32\conhost.exe 0xffffffff -ForceV1</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\SysmonFiles\Sysmon.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
- 4688 Process Creation Event 4:
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x555b61a</Data> 
  <Data Name="NewProcessId">0x1114</Data> 
  <Data Name="NewProcessName">C:\Users\ADMINI~1\AppData\Local\Temp\Sysmon.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x8c8</Data> 
  <Data Name="CommandLine">C:\SysmonFiles\Sysmon.exe -accepteula -i</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\SysmonFiles\Sysmon.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```

- Note: You will also see process creation events with wevtutil and services.exe - these are the same events that get logged when a Sysmon installation occurs

## Push Configs

- 4624 Type 3 Event With the account you use to run SysmonConfigPusher with. The IpAddress field should contain the IP address that is running SysmonConfigPusher
- 4627 Special privileges assigned to new login for the account that is used to run SysmonConfigPusher with
- 4688 Process Creation Event 1:
```xml
  <Data Name="SubjectUserSid">S-1-5-20</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e4</Data> 
  <Data Name="NewProcessId">0xf6c</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0xb0c</Data> 
  <Data Name="CommandLine">PowerShell -WindowStyle Hidden -Command "Invoke-WebRequest -UseBasicParsing -Uri http://192.168.1.134/config_silent.xml -OutFile C:\SysmonFiles\config_silent.xml</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">Administrator</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x5569039</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\wbem\WmiPrvSE.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
- 4688 Process Creation Event 2: 
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x5520955</Data> 
  <Data Name="NewProcessId">0x15c4</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\conhost.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x1a30</Data> 
  <Data Name="CommandLine">\??\C:\Windows\system32\conhost.exe 0xffffffff -ForceV1</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
## Update Config on Selected Computers

- 4624 Type 3 Event With the account you use to run SysmonConfigPusher with. The IpAddress field should contain the IP address that is running SysmonConfigPusher
- 4627 Special privileges assigned to new login for the account that is used to run SysmonConfigPusher with
- 4688 Process Creation Event 1:
```xml
  <Data Name="SubjectUserSid">S-1-5-20</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e4</Data> 
  <Data Name="NewProcessId">0x1258</Data> 
  <Data Name="NewProcessName">C:\SysmonFiles\Sysmon.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0xb0c</Data> 
  <Data Name="CommandLine">C:\SysmonFiles\Sysmon.exe -c config_silent.xml</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">Administrator</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x556dbef</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\wbem\WmiPrvSE.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
- 4688 Process Creation Event 2: 
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x5520955</Data> 
  <Data Name="NewProcessId">0x15c4</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\conhost.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x1a30</Data> 
  <Data Name="CommandLine">\??\C:\Windows\system32\conhost.exe 0xffffffff -ForceV1</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
## Uninstall Sysmon from Selected Computers

- 4624 Type 3 Event With the account you use to run SysmonConfigPusher with. The IpAddress field should contain the IP address that is running SysmonConfigPusher
- 4627 Special privileges assigned to new login for the account that is used to run SysmonConfigPusher with
- 4688 Process Creation Event 1:
```xml
  <Data Name="SubjectUserSid">S-1-5-20</Data> 
  <Data Name="SubjectUserName">SERVER$</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x3e4</Data> 
  <Data Name="NewProcessId">0xe58</Data> 
  <Data Name="NewProcessName">C:\SysmonFiles\Sysmon.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0xb0c</Data> 
  <Data Name="CommandLine">C:\SysmonFiles\Sysmon.exe -u</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">Administrator</Data> 
  <Data Name="TargetDomainName">LARESCF</Data> 
  <Data Name="TargetLogonId">0x5571bd3</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\wbem\WmiPrvSE.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```
- 4688 Process Creation Event 2: 
```xml
  <Data Name="SubjectUserSid">S-1-5-21-718865306-1039695286-1856044604-500</Data> 
  <Data Name="SubjectUserName">Administrator</Data> 
  <Data Name="SubjectDomainName">LARESCF</Data> 
  <Data Name="SubjectLogonId">0x5520955</Data> 
  <Data Name="NewProcessId">0x15c4</Data> 
  <Data Name="NewProcessName">C:\Windows\System32\conhost.exe</Data> 
  <Data Name="TokenElevationType">%%1936</Data> 
  <Data Name="ProcessId">0x1a30</Data> 
  <Data Name="CommandLine">\??\C:\Windows\system32\conhost.exe 0xffffffff -ForceV1</Data> 
  <Data Name="TargetUserSid">S-1-0-0</Data> 
  <Data Name="TargetUserName">-</Data> 
  <Data Name="TargetDomainName">-</Data> 
  <Data Name="TargetLogonId">0x0</Data> 
  <Data Name="ParentProcessName">C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Data> 
  <Data Name="MandatoryLabel">S-1-16-12288</Data> 
```

# The Sysmon Config Pusher Log File
  
The log file will contain information regarding which computers passed and did not pass the ping check: 

```2021-05-14 12:32:36.989 -04:00 [INF] WIN10-1 Passed Ping Check```
  
```2021-05-14 12:33:21.475 -04:00 [DBG] win10-7 : No such host is known```
  
The log file will also contain information regarding the web server, what IP it used and where it is serving configuration files from:
  
```2021-05-14 12:32:40.849 -04:00 [INF] Web Server Started on 192.168.1.134, serving configs from C:\\Users\\Administrator\\Desktop\\SysmonConfigs\\```

  When you click the "Update Config on Selected Computers" button, Sysmon Config Pusher will try to connect to the remote machine and look for the Sysmon Configuration State Change event (Event ID 16) and extract the SHA256 hash of the configuration value from the remote system so that you can verify that the machine did indeed perform a config update. 
  
```2021-05-19 12:48:14.332 -04:00 [INF] Found Config Update Event on WIN10-1 Logged at 2021-05-19T16:47:58.727. Updated with config file with the SHA256 Hash of: B24D08B5CC436B5FEA2A7481E911376E8FE88031B3F42D3F4CBDA0AE6FA94B6C```
  
If you try to update the config file on a host that does not have Sysmon installed yet, you may see the following in the log file: 

```2021-05-19 12:47:35.672 -04:00 [DBG] The specified channel could not be found: You may have hit the update configs button on a host with Sysmon not installed```

This means that Sysmon Config Pusher attempted to read the Sysmon log channel, but could not find it.
  
You may also see a "Found Config Update Event" with no hash:
 
```2021-05-19 12:48:14.332 -04:00 [INF] Found Config Update Event on WIN10-1 Logged at 2021-05-19T16:47:54.431. Updated with config file with the SHA256 Hash of: ```

This occurs if Sysmon Config Pusher looks for the event before it is logged, this may occur during fresh Sysmon installations.

  
