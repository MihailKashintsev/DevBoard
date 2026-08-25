[Setup]
AppName=DevBoard
AppVersion={#AppVersion}
AppPublisher=MihailKashintsev
DefaultDirName={autopf}\DevBoard
DefaultGroupName=DevBoard
OutputBaseFilename=DevBoard-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=no
AppendDefaultDirName=yes
AlwaysShowDirOnReadyPage=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DevBoard"; Filename: "{app}\DevBoard.exe"
Name: "{group}\Uninstall DevBoard"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DevBoard"; Filename: "{app}\DevBoard.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Run]
Filename: "{app}\DevBoard.exe"; Description: "Запустить DevBoard"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
