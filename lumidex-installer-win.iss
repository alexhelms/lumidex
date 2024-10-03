#define MyAppName "Lumidex"
#define MyAppPublisher "Alex Helms"
#define MyAppURL "https://github.com/alexhelms/lumidex"
#define MyAppSourceFolder "publish-win"
#define MyAppExeName MyAppName + ".Desktop.exe"
#define MyAppFileVersion GetStringFileInfo(MyAppSourceFolder + '\' + MyAppExeName, FILE_VERSION)

#ifndef MyAppProductVersion
	#define MyAppProductVersion GetStringFileInfo(MyAppSourceFolder + '\' + MyAppExeName, PRODUCT_VERSION)
#endif

[Setup]
AppId={{2B380828-6D33-457B-B562-1A84B8043FD0}
AppName={#MyAppName}
AppVersion={#MyAppProductVersion}
AppVerName={#MyAppName} {#MyAppProductVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright (c) 2024 {#MyAppPublisher}
VersionInfoVersion={#MyAppFileVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=dist/win
OutputBaseFilename={#MyAppName}-{#MyAppProductVersion}-win
SetupIconFile=Lumidex\Assets\lumidex-icon.ico
Compression=lzma2
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=6
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MyAppSourceFolder}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "installer\VC_redist.x64.exe"; DestDir: {tmp}; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; 
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "{tmp}\VC_redist.x64.exe"; \
  StatusMsg: "Installing VC++ 14.40.33810.0 x64"; \
  Parameters: "/q /passive /Q:a /c:""msiexec /q /norestart /i vcredist.msi"""; Flags: waituntilterminated
 