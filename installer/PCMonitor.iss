#define MyAppName "PC Monitor"
#define MyAppVersion "0.0.1-beta"
#define MyAppPublisher "LetsTiger"
#define MyAppExeName "PC Monitor.exe"

[Setup]
AppId={{3B1CBB14-83A5-4FE2-A8B2-20D43F103A8F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=0.0.1.0
DefaultDirName={localappdata}\Programs\PC Monitor
DefaultGroupName=PC Monitor
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=PC-Monitor-{#MyAppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Types]
Name: "compact"; Description: "PC Monitor ohne FPS-Unterstützung"
Name: "full"; Description: "PC Monitor mit optionaler PresentMon-FPS-Unterstützung"
Name: "custom"; Description: "Benutzerdefiniert"; Flags: iscustom

[Components]
Name: "app"; Description: "PC Monitor"; Types: compact full custom; Flags: fixed
Name: "presentmon"; Description: "PresentMon 2.5.1 – FPS- und Frametime-Erfassung (benötigt beim Verwenden erhöhte Rechte)"; Types: full

[InstallDelete]
Type: filesandordirs; Name: "{app}\Tools\PresentMon"; Components: not presentmon
Type: files; Name: "{app}\PcMonitor.App.exe"
Type: files; Name: "{app}\PcMonitor.App.dll"
Type: files; Name: "{app}\PcMonitor.App.deps.json"
Type: files; Name: "{app}\PcMonitor.App.runtimeconfig.json"
Type: files; Name: "{app}\PcMonitor.App.pdb"

[Files]
Source: "..\artifacts\installer-source\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: app
Source: "..\Tools\PresentMon\PresentMon.exe"; DestDir: "{app}\Tools\PresentMon"; DestName: "PresentMon.exe"; Flags: ignoreversion; Components: presentmon
Source: "..\Tools\PresentMon\LICENSE.txt"; DestDir: "{app}\Tools\PresentMon"; Flags: ignoreversion; Components: presentmon
Source: "..\Tools\PresentMon\THIRD_PARTY.txt"; DestDir: "{app}\Tools\PresentMon"; Flags: ignoreversion; Components: presentmon

[Icons]
Name: "{group}\PC Monitor"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PC Monitor"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Aufgaben:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "PC Monitor starten"; Flags: nowait postinstall skipifsilent
