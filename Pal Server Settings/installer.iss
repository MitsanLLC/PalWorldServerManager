; Pal Server Hub - Inno Setup script
; Build the Release publish folder first with publish-release.ps1.

#define MyAppName "Pal Server Hub"
#define MyAppVersion "0.9.0"
#define MyAppPublisher "Mitsan LLC"
#define MyAppDeveloper "Stancean"
#define MyAppURL "https://palworldserverhub.com"
#define MyAppExeName "PalServerHub.exe"

[Setup]
AppId={{4B4E49AF-6E4D-4DC0-8B41-34F9823876B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Pal Server Hub
DefaultGroupName=Pal Server Hub
DisableProgramGroupPage=yes
OutputDir=release\installer
OutputBaseFilename=Pal-Server-Hub-v{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=Assets\PalServerHub.ico
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "release\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Pal Server Hub"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Pal Server Hub"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Pal Server Hub"; Flags: nowait postinstall skipifsilent
