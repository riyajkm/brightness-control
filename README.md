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

No Visual Studio needed — it uses the compiler built into Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `bin\BrightnessControl.exe`

## If a monitor doesn't respond

The monitor itself has to allow software control. In the monitor's on-screen menu:

1. Turn **DDC/CI** on (often under Setup / System).
2. Set **ECO Mode** to **Standard** and turn off **Dynamic Contrast** — these modes lock brightness.
3. Connect directly to the computer (docks and USB display adapters usually block DDC/CI).

## Roadmap

Working toward a Microsoft Store release:

- [ ] Move to an SDK-style .NET 8 WinForms project
- [ ] Detect every monitor reliably (DDC/CI, WMI, and fallbacks for docks, adapters and laptops)
- [ ] Per-monitor DPI support, light/dark theme that follows Windows
- [ ] Open the panel on first launch and when launched again
- [ ] Advanced options: global hotkeys, schedules, custom presets, contrast and input switching
- [ ] Screen-reader support for the sliders
- [ ] MSIX packaging with a startup task, then Store submission

## Layout

```
src/BrightnessControl.cs   app source (C# 5, .NET Framework 4.x)
build.ps1                  build script
bin/                       compiled exe (not committed)
```
