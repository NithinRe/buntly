# Building the Windows app

The download people use is `Buntly-Setup.exe` on the Releases page.
GitHub Actions workflow **Windows installer** builds it. The workflow is
manual (`workflow_dispatch`). It runs the tests, publishes the app, and
compiles the installer with Inno Setup.

The publish is a folder, not one file. Whisper.net loads its native
libraries from `runtimes/win-x64/` inside that folder. A single-file
publish drops that folder and the speech model will not load.

Release builds turn symbols off so the installer does not carry a
developer machine path.

## Speed check

On the Windows laptop, from the published speech-check folder:

```text
Buntly.SpeechCheck.exe --seconds 10
```

It downloads the same model the app uses and prints how long a
ten-second clip took to transcribe. If a warm run is slower than 5
seconds, switch both projects from `Whisper.net.Runtime` to
`Whisper.net.Runtime.Vulkan` and publish again. Vulkan needs a Vulkan
driver. CPU is the default because a laptop without that driver would
fail to start.

## Local publish

With the .NET 8 SDK, from this repository root:

```bash
dotnet test windows/Buntly.Windows.sln -c Release
dotnet publish windows/src/Buntly.Windows/Buntly.Windows.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=false \
  -p:DebugType=none -p:DebugSymbols=false \
  -o dist/windows
```

The tray, the dictation key, the microphone, Windows Hello, and the pill
run on Windows. `Buntly.exe` does not run on macOS.
