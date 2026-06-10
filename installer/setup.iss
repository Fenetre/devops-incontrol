; DevOps InControl — Inno Setup Script
; Produces a single DevOpsInControl-Setup.exe installer.
; Prerequisites: .NET 8 Desktop Runtime must be installed on the target machine.

#define MyAppName "DevOps InControl"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Fenêtre"
#define MyAppURL "https://fenetre.nl"
#define MyAppExeName "DashboardApi.exe"

[Setup]
AppId={{7B2F4A8E-3C1D-4E5F-9A6B-8D7C2E1F0A3B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\DevOpsInControl
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..
OutputBaseFilename=DevOpsInControl-{#MyAppVersion}-Setup
SetupIconFile=..\dashboard\frontend\public\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start automatically at Windows logon"; GroupDescription: "Additional options:"

[Files]
; Published application (backend + frontend wwwroot)
Source: "..\app\*"; DestDir: "{app}"; Excludes: "data\config.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; Release notes
Source: "..\dashboard\release-notes\*"; DestDir: "{app}\release-notes"; Flags: ignoreversion recursesubdirs createallsubdirs
; Launcher script
Source: "..\scripts\Launch-DevOpsInControl.ps1"; DestDir: "{app}"; Flags: ignoreversion
; Icon
Source: "..\dashboard\frontend\public\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; Ensure data directory exists for config on first run
Name: "{app}\data"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "pwsh.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Launch-DevOpsInControl.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Comment: "Start DevOps InControl"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "pwsh.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Launch-DevOpsInControl.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Registry]
; Auto-start via Run key (lightweight, no elevation needed)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DevOpsInControl"; ValueData: "pwsh.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{app}\Launch-DevOpsInControl.ps1"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "pwsh.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Launch-DevOpsInControl.ps1"""; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM DashboardApi.exe"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\data"
Type: filesandordirs; Name: "{app}\logs"

[Code]
function IsDotNet8Installed(): Boolean;
var
  FindRec: TFindRec;
  RuntimePath: String;
begin
  Result := False;
  RuntimePath := ExpandConstant('{pf}\dotnet\shared\Microsoft.AspNetCore.App\');
  if FindFirst(RuntimePath + '8.*', FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    if MsgBox('DevOps InControl requires the .NET 8 Desktop Runtime.' + #13#10 + #13#10 +
              'Please install it from:' + #13#10 +
              'https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10 + #13#10 +
              'Do you want to continue anyway?',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

procedure MigrateOldConfig();
var
  OldInstallLocation, OldConfigPath, NewConfigPath: String;
begin
  NewConfigPath := ExpandConstant('{app}\data\config.json');
  // Only migrate if target config does not already exist
  if FileExists(NewConfigPath) then
    Exit;

  // Check old install.ps1 registry entry for previous install location
  if not RegQueryStringValue(HKEY_CURRENT_USER,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\DevOpsInControl',
    'InstallLocation', OldInstallLocation) then
    Exit;

  OldConfigPath := OldInstallLocation + '\dashboard\data\config.json';
  if not FileExists(OldConfigPath) then
    Exit;

  if MsgBox('An existing configuration was found from a previous installation:' + #13#10 +
            OldConfigPath + #13#10 + #13#10 +
            'Would you like to import it?',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    FileCopy(OldConfigPath, NewConfigPath, False);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    MigrateOldConfig();
end;
