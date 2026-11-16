; Автономний інсталятор RentSport. Перед компіляцією виконайте build-release.cmd.
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{EB47E82C-3199-4BB3-9F23-70319F6B7AC6}
AppName=RentSport
AppVersion=1.0.0
AppPublisher=Maksym Shostak
AppPublisherURL=https://github.com/mx111m/RentSport
AppSupportURL=https://github.com/mx111m/RentSport/issues
AppUpdatesURL=https://github.com/mx111m/RentSport/releases/latest
DefaultDirName={localappdata}\Programs\RentSport
PrivilegesRequired=lowest
UsePreviousAppDir=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Rentt.exe
OutputDir=..\dist
OutputBaseFilename=RentSport-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "rent.db,*.pdb,*.iss,*.bak"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\RentSport"; Filename: "{app}\Rentt.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\RentSport"; Filename: "{app}\Rentt.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Rentt.exe"; WorkingDir: "{app}"; Description: "Запустити RentSport"; Flags: nowait postinstall skipifsilent
