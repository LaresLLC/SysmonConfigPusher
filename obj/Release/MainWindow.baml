   M S B A M L   `   `   `  ���� M  ISysmonConfigPusher, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null7 clr-namespace:SysmonConfigPusherSysmonConfigPusher  #    SysmonConfigPusher.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     2local clr-namespace:SysmonConfigPusher     -     �Name$  MainWindow1�� � Title$ Sysmon Config Pusher��$��899.289��$��1176.267�� � WindowState$ Normal=�.���  � RowDefinitions �� $	��41* ��� $	��43* � � ColumnDefinitions �� $��113.318 ��� $��655.482 ��� $
��Auto �.+��� -    ��Name$ GetDomainComputers��$��Get Domain Computers��$
��Left=�$��	36,54,0,0q�$	��Top=�$	��172��$��31��$��2���� -    ��Name$ ComputerList��$
��Left=�$	��426��$��
20,230,0,0q�$	��Top=�$	��230�� �� SelectionMode$ Multiple=�$��2��$��2���� -    ��Name$ DomainListLabel��$��Computer List:��$
��Left=�$��
20,199,0,0q�$	��Top=�$	��230��$��2��$
��Bold"��� -   $ SelectComputers��$��Select Computers��$
��Left=�$��177.6,54,0,0q�$	��Top=�$	��175��$��31��$��1���� -   $ SelectedComputerList��$��1��$
��Left=�$	��426��$��149,230,0,0q�$	��Top=�$	��240��$��2��$ Multiple=��� -   $ ComputerSelection��$,��&Click on computers you want to action:��$
��Left=�$��150.6,199,0,0q�$	��Top=�$	��239��$��1��$
��Bold"��� -   $ PushConfigs��$��Push Configs
��$��2��$��Center=�$��131.8,160.6,-67.2,0q�$	��Top=�$	��165��$��1���� -	   $ ClearSelectedComputers��$��Clear Selected Computers��$��1��$
��Left=�$��150,259,0,0q�$	��Top=�$	��239��$��1���� -
   $$��Clear Computers in Domain List��$
��Left=�$��
20,259,0,0q�$	��Top=�$	��230��$��1��$��2���� -   $ StartWebServer��$��Start Web Server��$��Center=�$��136.6,75,-82.8,0q�$	��Top=�$	��167��$��35��$��2���� -   $ WebServerLabel��$��Web Server Stopped��$
��Left=�$��103.6,40,-50.8,0q�$	��Top=�$	��168��$��2���� -   $ LoadConfigs��$��Load Configs��$��1��$
��Left=�$��447.6,52,0,0q�$	��Top=�$	��175��$��33��	 J� RenderTransformOrigin$	 -0.216,0.296&��� -   $ Configs��$��1��$
��Left=�$	��426��$��406.6,230,0,0q�$	��Top=�$	��239��$��2���� $'��!Pick the Sysmon Config To Deploy:��$��1��$
��Left=�$��405.6,199,0,0q�$	��Top=�$	��239��$
��Bold"��� -   $ CreateDirectories��$��Create Directories
��$��2��$��Center=�$��134.6,198,-64.6,0q�$	��Top=�$	��166��$��35��$��Center=��� -   $��Clear Configs��$��1��$
��Left=�$��405.6,258.6,0,0q�$	��Top=�$	��240��$��1���� -   $ UpdateConfigs��$+��%Update Config on 
Selected Computers��$��2��$
��Left=�$��132.6,282.6,-65.6,0q�$	��Top=�$	��165��$��36��$	 0.497,3.118&�$��1���� -   $ PushExecutable��$/��)Push Newest Executable
From Sysinternals��$��2��$
��Left=�$��131.6,329,-76.8,0q�$	��Top=�$	��166��$��36���� -   $ UnInstallSysmon��$/��)UnInstall Sysmon from
Selected Computers��$��1��$
��Left=�$��447.6,355.2,0,0q�$	��Top=�$	��168��$��36��$��1���� -   $ InstallSysmon��$+��%Install Sysmon On
Selected Computers��$��2��$
��Left=�$��131.6,36.6,-69.4,0q�$	��Top=�$	��165��$��36��$��1���� -   $ FromFile��$��Load Computers From File��$
��Left=�$��
36,100,0,0q�$	��Top=�$	��172��$��29��$��2���� $��2��$ ��Step 1: Load Computer List��$
��Left=�$	��123��$��	10,17,0,0q�$	��Top=�$	��240���� $��Step 2: Select Computers��$
��Left=�$	��123��$��149.6,17,0,0q�$	��Top=�$	��240��$��1���� $!��Step 2: Load Sysmon Configs��$
��Left=�$	��123��$��415.6,17,0,0q�$	��Top=�$	��240��$��1���� $��Deploy Step 0: Web Server��$
��Left=�$	��123��$��75.6,21,-125.8,0q�$	��Top=�$	��271��$��2��$
��Bold"��� $'��!Deploy Step 1: Create Directories��$
��Left=�$	��123��$��75.6,149,-109,0q�$	��Top=�$	��271��$��2��$
��Bold"��� $+��%Deploy Step 2: Push Sysmon Executable��$
��Left=�$	��123��$��76.6,277,-124.4,0q�$	��Top=�$	��271��$��2��$
��Bold"��� $#��Deploy Step 3: Install Sysmon��$
��Left=�$	��123��$��76.6,405,-119.6,0q�$	��Top=�$	��271��$��2��$
��Bold"�$��2���� $'��!Deploy Step 4: Push Sysmon Config��$
��Left=�$	��123��$��76.6,114.6,-117.2,0q�$	��Top=�$	��271��$��2��$��1��$
��Bold"��� $"��Deploy Step 5: Update Config��$
��Left=�$	��123��$��76.6,242.6,-114.8,0q�$	��Top=�$	��271��$��2��$��1��$
��Bold"��� $��Uninstall / Upgrade��$
��Left=�$	��101��$��415.6,323.2,0,0q�$	��Top=�$	��240��$��1��$��1��$
��Bold"��� -   
 ��Name$
 	txtSearch��$��2��$
��Left=�$��48��$��
10,150,0,0q� �� TextWrapping$
 Wrap=�$��Filter for Computer...��$	��Top=�$	��240���� $��1��$
��Left=�$	��100��$��283.6,382,-205.8,0q�$	��Top=�$	��807��$	 0.5,0.5&�$��2��$��2��z�V� .~��� �� ��  �� Angle$ 89.967Q�T� �� -   $ SelectAllComputers��$��
Select All��$
��Left=�$��20,283.2,0,0q�$��1��$	��Top=�$	��230��$��2��� -    �Name$ myprogressDialog��$
��Left=�$��20��$��150.6,164,0,0q�$	��Top=�$	��239��$��1���� -   $ StatusLabel��$�� ��$��1��$
��Left=�$��415.6,150,0,0q�$	��Top=�$��49��$	��240���� -   $ RestartButton��$��	Restart
��$��2��$
��Left=�$��76.6,370.2,0,0q�$��1��$	��Top=�$��75��$��21�����	#FFFF0404���	#FFCB9393 J� OpacityMask �	#FF9F7777�� -   $ SelectAllActionComputers��$��
Select All��$��1��$
��Left=�$��150.6,283.2,0,0q�$��1��$	��Top=�$	��238���� -   $ 
ExitButton��$
��Exit��$��2��$
��Left=�$��272.6,370.2,-108.4,0q�$��1��$	��Top=�$��75��$��21�����	#FFCB9393$
��Bold"��� -   $ ConfigCleanup��$��Config Cleanup��$��1��$
��Left=�$��469.6,164,0,0q�$	��Top=�$	��175��