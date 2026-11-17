; Per-user Buntly installer. No administrator password.
; Build it only after `dotnet publish` has filled the folder named by SourceDir.
; The GitHub workflow passes /DSourceDir=... . A local build from this folder
; expects the publish output at ..\..\dist\windows.

#ifndef SourceDir
  #define SourceDir "..\..\dist\windows"
#endif

#define AppVersion "1.0.0"

[Setup]
AppId={{7C4E9A21-5B18-4F6D-9A30-B1E2D4C8F067}
AppName=Buntly
AppVersion={#AppVersion}
AppPublisher=Nithin
DefaultDirName={localappdata}\Programs\Buntly
DisableDirPage=yes
DisableProgramGroupPage=yes
DefaultGroupName=Buntly
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=Buntly-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Buntly.exe
UninstallDisplayName=Buntly
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
; Same name Program.cs waits on, so an upgrade can ask the person to quit first.
AppMutex=Local\Buntly.Windows
InfoBeforeFile=..\..\WINDOWS-ONBOARDING.md
SetupLogging=yes

[Files]
; The published folder already contains "Read Me First.txt".
; Whisper.net also copies Mac and Linux libraries into a net8.0 build.
; The friend is on 64-bit Windows, so those stay out of the installer.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,runtimes\win-x86\*,runtimes\win-arm64\*,runtimes\linux-*\*,runtimes\macos-*\*,ggml-metal.metal"

[Icons]
Name: "{autoprograms}\Buntly"; Filename: "{app}\Buntly.exe"; Comment: "Local dictation"

[Run]
Filename: "{app}\Buntly.exe"; Description: "Start Buntly"; Flags: nowait postinstall skipifsilent

[Messages]
FinishedLabel=Buntly is installed. The rings icon sits by the clock. The speech model downloads the first time you open it, about 1.6 GB.
