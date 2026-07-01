; Script Inno Setup Script Wizard pour SaveMail Converter.
; Lukas
; 01.07.2026

#define MyAppName "SaveMail Converter"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "Lukas"
#define MyAppURL "https://github.com/Hoferlukaslh/SaveMail"
#define MyAppExeName "SaveMail_v0.1.1_Win_x64.exe"
#define SetupLogo "..\SaveMail\Assets\Images\Logo_blanc_256.ico"
#define BuildPath "Executable"
#define LicensePath "..\..\LICENSE"

[Setup]
; Utilisez Tools > Generate GUID dans Inno Setup pour remplacer cet identifiant
AppId={{04F2AF89-7EBF-4DD5-91A2-8AA8C94B57A9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os

DisableProgramGroupPage=yes
LicenseFile={#LicensePath}
PrivilegesRequiredOverridesAllowed=dialog

; Emplacement de sortie et nom de l'installateur
OutputDir=Installeur
OutputBaseFilename=SaveMail_v{#MyAppVersion}_Installer_WIN_x64
SolidCompression=yes
WizardStyle=modern windows11

; Icone SaveMail
SetupIconFile={#SetupLogo}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Fichier spécifique pour Windows x64 (basé sur votre dossier "Executable")
Source: "{#BuildPath}\{#MyAppExeName}"; DestDir: "{app}"; Check: IsX64OS; Flags: ignoreversion

; Autres architectures plus tard :
; Source: "{#BuildPath}\WIN_x86\SaveMail_v0.1.0_Win_x86.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Check: IsX86; Flags: ignoreversion
; Source: "{#BuildPath}\WIN_ARM64\SaveMail_v0.1.0_Win_ARM64.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Check: IsArm64; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent