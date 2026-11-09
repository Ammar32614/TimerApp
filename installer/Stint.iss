; Inno Setup script for Stint. Built by .github/workflows/release.yml:
;   ISCC.exe /DMyAppVersion=1.2.3 installer\Stint.iss
; Expects the single-file build at publish\Stint.exe.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{7E2C4B1A-5D3F-4C8E-9A61-2B7F0D4E9C35}
AppName=Stint
AppVersion={#MyAppVersion}
AppVerName=Stint {#MyAppVersion}
AppPublisher=Ammar32614
AppPublisherURL=https://github.com/Ammar32614/TimerApp
AppSupportURL=https://github.com/Ammar32614/TimerApp/issues
AppUpdatesURL=https://github.com/Ammar32614/TimerApp/releases
; Per-user install: no admin prompt, lands in %LocalAppData%\Programs\Stint
PrivilegesRequired=lowest
DefaultDirName={autopf}\Stint
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=..\dist
OutputBaseFilename=Stint-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\stint.ico
UninstallDisplayIcon={app}\Stint.exe
UninstallDisplayName=Stint
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Ask the user to close a running copy (the app holds this mutex)
AppMutex=Stint.SingleInstance
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start Stint when I sign in"; Flags: unchecked

[Files]
Source: "..\publish\Stint.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Stint"; Filename: "{app}\Stint.exe"
Name: "{autodesktop}\Stint"; Filename: "{app}\Stint.exe"; Tasks: desktopicon

[Registry]
; Same value the in-app "Start Stint when I sign in" switch writes.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Stint"; \
    ValueData: """{app}\Stint.exe"" --minimized"; Tasks: startup
; Always remove it on uninstall, even if it was switched on from inside the app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Stint"; \
    Flags: uninsdeletevalue

[Run]
Filename: "{app}\Stint.exe"; Description: "Open Stint now"; Flags: nowait postinstall skipifsilent

; Your study log in %AppData%\Stint is deliberately kept on uninstall.
