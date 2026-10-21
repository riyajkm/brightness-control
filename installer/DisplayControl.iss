; Display Control installer (Inno Setup 6). Built by build-installer.ps1, which passes:
;   /DAppVersion=1.0.2  /DStage=<folder with DisplayControl-x64.exe and DisplayControl-arm64.exe>
; One setup.exe for both x64 and ARM64 PCs; it installs the matching exe.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Stage
  #define Stage "..\obj\installer"
#endif

[Setup]
; Never change AppId: Windows uses it to recognise updates and the uninstall entry.
AppId={{67A6CAD9-6110-4A7C-B3CC-195E3A18830F}
AppName=Display Control
AppVersion={#AppVersion}
AppVerName=Display Control {#AppVersion}
AppPublisher=Riyaj Kafardeen
AppPublisherURL=https://github.com/riyajkm/display-control
AppSupportURL=https://github.com/riyajkm/display-control/issues
AppUpdatesURL=https://github.com/riyajkm/display-control/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\Display Control
DisableProgramGroupPage=yes
; "Install for me only" needs no admin rights; the user can choose "all users" instead.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible or arm64
ArchitecturesInstallIn64BitMode=x64compatible or arm64
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=DisplayControl-{#AppVersion}-setup
SetupIconFile=..\src\DisplayControl\app.ico
WizardSmallImageFile=wizard-small-55.bmp,wizard-small-110.bmp
WizardImageFile=wizard-large-164.bmp,wizard-large-328.bmp
UninstallDisplayIcon={app}\DisplayControl.exe
UninstallDisplayName=Display Control
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; We close the app ourselves (cleanly, see PrepareToInstall) rather than via Restart Manager.
CloseApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "&Start Display Control when I sign in to Windows"

[Files]
Source: "{#Stage}\DisplayControl-x64.exe"; DestDir: "{app}"; DestName: "DisplayControl.exe"; Check: not IsArm64; Flags: ignoreversion
Source: "{#Stage}\DisplayControl-arm64.exe"; DestDir: "{app}"; DestName: "DisplayControl.exe"; Check: IsArm64; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Display Control"; Filename: "{app}\DisplayControl.exe"; Comment: "Brightness, colour and settings for every screen"
Name: "{autodesktop}\Display Control"; Filename: "{app}\DisplayControl.exe"; Tasks: desktopicon

[Registry]
; Same entry the app's own "Start with Windows" menu item uses, so the two stay in step.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplayControl"; ValueData: """{app}\DisplayControl.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\DisplayControl.exe"; Description: "Launch Display Control"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Puts matched monitor colours back, removes the startup entry and closes the running app, before files go.
Filename: "{app}\DisplayControl.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "ResetMonitors"

[Code]
// Before installing or updating: close a running Display Control cleanly (it removes its software colour
// adjustments on exit and re-applies them when it starts again).
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Helper: String;
begin
  Result := '';
  if CheckForMutexes('DisplayControl.SingleInstance') then
  begin
    // Any copy of the exe can ask the running one to close; use the installed one if there is one.
    // Otherwise extract ours (found by its installed name; that's the x64 build, which ARM64 Windows 11 also runs).
    Helper := ExpandConstant('{app}\DisplayControl.exe');
    if not FileExists(Helper) then
    begin
      ExtractTemporaryFile('DisplayControl.exe');
      Helper := ExpandConstant('{tmp}\DisplayControl.exe');
    end;
    if not Exec(Helper, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or
       CheckForMutexes('DisplayControl.SingleInstance') then
      // Couldn't close it politely: stop it so its files can be replaced. It re-applies colours on next start.
      Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM DisplayControl.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
  // The app was called "Brightness Control" before 1.0.1; a copy still running would fight over the monitors.
  // Display Control takes over its settings on first start.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM BrightnessControl.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// After uninstalling: offer to remove settings too (kept by default, and always kept in a silent uninstall).
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data: String;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  Data := ExpandConstant('{localappdata}\DisplayControl');
  if not DirExists(Data) then Exit;
  if UninstallSilent then Exit;
  if MsgBox('Also delete your Display Control settings?' + #13#10 + #13#10 +
            'This removes your brightness calibration, colour matching and the detection log. ' +
            'Keep them if you plan to install Display Control again.',
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(Data, True, True, True);
end;
