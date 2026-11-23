#define MyAppName "Memory Cleaner"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "DmitriyOMS"
#define MyAppURL "https://github.com/DmitriyOMS/memory-cleaner"
#define MyAppExeName "MemoryCleaner.exe"

[Setup]
AppId={{B4D37E0A-8A4D-4A66-9F7A-2E5C96A4B701}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\Memory Cleaner
DefaultGroupName=Memory Cleaner
AllowNoIcons=yes
DisableDirPage=no
DisableProgramGroupPage=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=output
OutputBaseFilename=MemoryCleaner-Setup-{#MyAppVersion}
SetupIconFile=..\native\assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Установщик {#MyAppName}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать значок на &рабочем столе"; GroupDescription: "Расположение значков:"; Flags: unchecked

[Files]
Source: "..\MemoryCleaner.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "..\MemoryCleaner-UserGuide.pdf"; DestDir: "{app}"; DestName: "MemoryCleaner-UserGuide.pdf"; Flags: ignoreversion

[Icons]
Name: "{group}\Memory Cleaner"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Инструкция пользователя"; Filename: "{app}\MemoryCleaner-UserGuide.pdf"; WorkingDir: "{app}"
Name: "{autodesktop}\Memory Cleaner"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить {#MyAppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
