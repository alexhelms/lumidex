#define MyAppName "Lumidex"
#define MyAppPublisher "Alex Helms"
#define MyAppURL "https://github.com/alexhelms/lumidex"
#define MyAppSourceFolder "publish"
#define MyAppExeName MyAppName + ".Desktop.exe"
#define MyAppFileVersion GetStringFileInfo(MyAppSourceFolder + '\' + MyAppExeName, FILE_VERSION)
#define MyAppProductVersion GetStringFileInfo(MyAppSourceFolder + '\' + MyAppExeName, PRODUCT_VERSION)

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
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename={#MyAppName}-{#MyAppProductVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSourceFolder}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
