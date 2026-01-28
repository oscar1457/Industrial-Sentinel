#define MyAppName "Industrial Sentinel"
#define MyAppPublisher "Industrial Sentinel"
#define MyAppURL "https://github.com/your-org/industrial-sentinel"
#define MyAppExeName "IndustrialSentinel.App.exe"
#define MyAppVersion "1.0.0"
#define MyAppSourceDir "dist\\IndustrialSentinel"
#define MyAppOutputDir "dist"
#define MyAppArch "x64"
#define MyAppId "{{8C7C25D4-5C93-4D9C-9B5C-9C8E0B2C4B6A}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=IndustrialSentinel-Setup-{#MyAppVersion}-{#MyAppArch}
OutputDir={#MyAppOutputDir}
SetupIconFile=src\IndustrialSentinel.App\Resources\app_icon.png
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
