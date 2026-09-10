; ---------------------------------------------------------------------------
; Inno Setup script untuk Suarakata (Speech to Text)
; Build: & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer\Suarakata.iss
; ---------------------------------------------------------------------------

#define MyAppName "Suarakata"
#define MyAppVersion "1.2.1"
#define MyAppPublisher "Cyserrex"
#define MyAppExeName "Suarakata.exe"
#define MyAppUrl "https://github.com/Cyserrex/Suarakata"
#define SourceDir "..\bin\Release"

[Setup]
AppId={{B0C3A1D2-1111-4A2B-9C3D-0123456789AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=Suarakata-Setup-{#MyAppVersion}
OutputDir=.\Output
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
VersionInfoVersion={#MyAppVersion}
LicenseFile=..\LICENSE
PrivilegesRequired=admin
MinVersion=10.0
SetupIconFile=..\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Buat ikon di Desktop"; GroupDescription: "Ikon tambahan:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Suarakata.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\ggml-metal.metal"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Hanya runtime win-x64 yang disertakan; aplikasi ini x64-only, dan di Windows ARM64
; berkas x64 tetap dipakai lewat emulasi. Runtime Linux/macOS dari paket NuGet dilewati.
Source: "{#SourceDir}\runtimes\win-x64\*"; DestDir: "{app}\runtimes\win-x64"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Model besar diunduh saat aplikasi dipakai; saat uninstall folder models di direktori
; aplikasi ikut dibersihkan (model di %LocalAppData% sengaja dibiarkan agar tidak
; hilang bila pengguna hanya memasang ulang).
[UninstallDelete]
Type: filesandordirs; Name: "{app}\models"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Jalankan {#MyAppName}"; Flags: nowait postinstall skipifsilent
