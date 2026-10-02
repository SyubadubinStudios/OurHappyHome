; Our Happy Home - Inno Setup 6 script (compiled by build-windows.ps1 when ISCC is available).
; Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif
#ifndef RootDir
  #define RootDir "..\.."
#endif

[Setup]
AppId={{6C1C8A52-5B8E-4E0B-9B3C-0A4F1E2D7B11}
AppName=Our Happy Home
AppVersion={#AppVersion}
AppPublisher=Syubadubin Studios
AppPublisherURL=https://github.com/SyubadubinStudios/OurHappyHome
DefaultDirName={autopf}\Our Happy Home
DefaultGroupName=Our Happy Home
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=OurHappyHome-{#AppVersion}-win-x64-setup
SetupIconFile={#RootDir}\src\OurHappyHome\Assets\icon.ico
UninstallDisplayIcon={app}\OurHappyHome.exe
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "indonesian"; MessagesFile: "compiler:Languages\Indonesian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Our Happy Home"; Filename: "{app}\OurHappyHome.exe"
Name: "{group}\{cm:UninstallProgram,Our Happy Home}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Our Happy Home"; Filename: "{app}\OurHappyHome.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\OurHappyHome.exe"; Description: "{cm:LaunchProgram,Our Happy Home}"; Flags: nowait postinstall skipifsilent
