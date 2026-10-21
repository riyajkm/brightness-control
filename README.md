# Brightness Control

A small Windows tray app for changing monitor brightness without using the monitor's buttons.

- External monitors are controlled over **DDC/CI** (Windows `dxva2.dll`).
- The laptop's built-in screen is controlled through **WMI**.
- One slider per screen, an **All screens** slider, and Night / Evening / Day / Max presets.
- If a monitor ignores a change, the app shows a warning on that screen's card.

## Use

Click the sun icon in the taskbar to open the panel. Right-click it for **Refresh screens**, **Start with Windows** and **Exit**.

Sliders also respond to the mouse wheel and arrow keys.

## Build

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`winget install Microsoft.DotNet.SDK.10`). Then:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `bin\BrightnessControl.exe`

## If a monitor doesn't respond

The monitor itself has to allow software control. In the monitor's on-screen menu:

1. Turn **DDC/CI** on (often under Setup / System).
2. Set **ECO Mode** to **Standard** and turn off **Dynamic Contrast** — these modes lock brightness.
3. Connect directly to the computer (docks and USB display adapters usually block DDC/CI).

Screens that don't answer stay in the panel and are re-checked each time you open it, after display changes and after the PC wakes up. To see why a screen isn't working, right-click the tray icon and choose **Open detection log** (`%LOCALAPPDATA%\BrightnessControl\detect.log`).

## Roadmap

Working toward a Microsoft Store release:

- [x] Move to an SDK-style .NET 10 WinForms project
- [x] Detect every monitor reliably: retries, never drop a screen, re-check on open / display change / wake, detection log
- [ ] Software dimming fallback for screens with no DDC/CI or WMI control
- [ ] Monitor controls read from each monitor's capabilities: contrast, colour preset, input source (see [docs/feature-research.md](docs/feature-research.md))
- [ ] Follow the laptop's brightness keys, perceptual matching across monitors, schedules / sunrise-sunset
- [ ] Per-monitor DPI support (enabled in the project; UI layout still needs scaling work), light/dark theme that follows Windows
- [ ] Open the panel on first launch and when launched again
- [ ] Advanced options: global hotkeys, schedules, custom presets, contrast and input switching
- [ ] Screen-reader support for the sliders
- [ ] MSIX packaging with a startup task, then Store submission

## Layout

```
BrightnessControl.slnx                       solution (open in Visual Studio / VS Code / Rider)
src/BrightnessControl/BrightnessControl.cs    app source (.NET 10, Windows Forms)
src/BrightnessControl/BrightnessControl.csproj
build.ps1                                     build script (dotnet publish -> bin/)
bin/                                          compiled app (not committed)
```
