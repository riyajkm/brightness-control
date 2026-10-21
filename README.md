# Display Control

**Brightness, colour and settings for every screen, from the Windows taskbar.** External monitors and the laptop screen, all at once or one by one, without reaching for the monitor's buttons.

*Formerly **Brightness Control**. Updating keeps your settings.*

![Display Control in dark and light theme](docs/images/hero.png)

- **Every screen in one place.** External monitors are controlled over **DDC/CI**, the laptop screen through Windows.
- **All screens slider** and **Night / Evening / Day / Max** presets.
- **Monitor settings:** contrast, colour preset (sRGB, 6500 K, …) and input source, for monitors that support them.
- **Brightness matching:** one slider looks equally bright on every screen, even when their panels differ.
- **Colour matching:** make whites look the same on every screen.
- **Follow laptop brightness:** external monitors follow the laptop's brightness keys.
- **Doesn't lose monitors:** screens that don't answer stay listed and are checked again automatically.
- Light/dark theme, sharp on high-DPI screens, keyboard and screen-reader friendly.
- **Private:** collects no data and never goes online ([privacy policy](docs/privacy.md)).

## Install

Requires Windows 10 (version 1809 or later) or Windows 11.

### Installer (recommended)

1. Download **`DisplayControl-…-setup.exe`** from the [latest release](https://github.com/riyajkm/display-control/releases/latest). One installer works on every PC (Intel, AMD and ARM).
2. Run it. Windows may show **"Windows protected your PC"** because the download is new and not yet signed: click **More info > Run anyway**.
3. Choose **Install for me only** (no admin rights needed) or for all users, accept the licence and pick your options:

   <img src="docs/images/setup-tasks.png" alt="Installer options" width="460">

   - **Create a desktop shortcut**
   - **Start Display Control when I sign in to Windows**. Recommended: colour matching on laptop screens only applies while the app runs.
4. Click **Install**, then **Finish** to start Display Control.

   <img src="docs/images/setup-finish.png" alt="Installer finished" width="460">

The panel opens and the Display Control icon (a monitor with a sun) appears in the taskbar. If you don't see it, click **^** next to the clock and drag it onto the taskbar. Display Control is also in the Start menu.

**Updating:** run the newer setup. It closes the running app, updates it, and keeps all your settings. Coming from **Brightness Control 1.0.0**? The installer closes the old app; your calibrations, colour matching and *Start with Windows* carry over. You can then delete the old app's folder and `%LOCALAPPDATA%\BrightnessControl`.

### Uninstall

**Settings > Apps > Installed apps > Display Control > Uninstall.** Uninstalling also:

- puts every monitor's colours back exactly as they were before colour matching,
- removes the *Start with Windows* entry and all shortcuts,
- asks whether to delete your settings too (calibration, colour matching, log). Keep them if you might reinstall.

<img src="docs/images/uninstall-settings.png" alt="Uninstall asks about settings" width="426">

### Portable (no installer)

Prefer not to install? Download `DisplayControl-…-win-x64.zip` (most PCs) or `-win-arm64.zip` (ARM laptops; check Settings > System > About > *System type*), unzip it to a folder you'll keep and run **DisplayControl.exe**. Nothing else is needed; .NET is built in. Turn on **Start with Windows** from the tray menu if you like.

To remove the portable version: right-click the tray icon, choose **Reset colours** (if you used colour matching) and **Exit**, then delete the folder and `%LOCALAPPDATA%\DisplayControl`.

### Microsoft Store

Coming soon. The Store version installs and updates automatically.

### Before you start: let your monitors be controlled

Most monitors support this, but some have it switched off. In each external monitor's own on-screen menu:

1. Turn **DDC/CI** on (often under *Setup* or *System*).
2. Set **ECO mode** to *Standard* and turn off **Dynamic Contrast**. These lock brightness.
3. Connect the monitor directly to the PC if you can. Docks, USB display adapters and some KVM switches block DDC/CI.

## Use

### The panel

Click the Display Control icon in the taskbar to open the panel. Each screen gets a slider; **All screens** moves them together, and the presets set everything in one click.

<img src="docs/images/panel.png" alt="The brightness panel" width="340">

- Drag a slider, scroll on it with the mouse wheel, or use the keyboard: arrows, Page Up / Page Down, Home / End.
- Esc or clicking elsewhere closes the panel.
- Right-click the icon, or use **⋯** in the panel, for everything else:

<img src="docs/images/menu.png" alt="The menu" width="345">

### Monitor settings

Click the arrow next to a monitor's name for its **contrast**, **colour preset** and **input source**. Only the settings that monitor reports supporting are shown. Switching the input shows another device's picture on that screen.

<img src="docs/images/panel-settings.png" alt="Monitor settings" width="340">

### Make screens equally bright

The same percentage rarely looks the same on two screens. **Calibrate matching…** fixes that in two steps:

1. A grey square appears on every screen. Your laptop (or first screen) is set to a dim level.
2. Move each other screen's slider until its square looks as bright as the reference. Click **Next**.
3. Do the same at a bright level, then **Save**.

From then on, **All screens**, the presets and *Follow laptop brightness* keep every screen looking alike. Your screens go back to their previous brightness when the wizard closes. To switch matching off, untick **Match brightness across screens**.

<img src="docs/images/brightness-wizard.png" alt="Brightness matching wizard" width="456">

### Make screens show the same colours

**Match colours…** puts a white square on every screen.

1. Under **Reference screen**, pick the screen whose white looks right to you.
2. For each other screen, move **Cooler ↔ Warmer** and **Greener ↔ Magenta** until its square looks the same. Changes show immediately.
3. Tick **Grey squares** to double-check on grey, then **Save**.

<img src="docs/images/colour-wizard.png" alt="Colour matching wizard" width="516">

How it's applied:

- **Monitors that accept colour commands** are adjusted in their own settings. They usually switch to their *User* colour mode, and they keep the colour even when the app isn't running.
- **Laptop screens and monitors that ignore colour commands** are adjusted through Windows, while the app is running. Turn on **Start with Windows** to keep them matched.

**Reset colours** in the menu puts every screen back exactly as it was. Match brightness first: screens at different brightness never look the same colour. Whites and greys can match closely; strong colours may still differ a little, because each panel can show a different range of colour.

### Follow laptop brightness

Turn on **Follow laptop brightness**. When you press the laptop's brightness keys, or Windows changes the laptop's brightness by itself, external monitors follow, through brightness matching if you've calibrated it.

## Troubleshooting

- **A monitor is missing or says "Not answering yet".** Check the monitor menu steps above, then right-click the Display Control icon and choose **Refresh screens**. Opening the panel also re-checks.
- **A monitor ignores brightness changes.** Its ECO or picture mode may lock brightness. Switch it to *Standard* in the monitor's menu.
- **Find out what happened.** Right-click the Display Control icon and choose **Open detection log**. It lists every screen Windows reports and what each one answered. Attach it to an issue if you need help.
- **Colours of the laptop screen change back.** Windows Night Light and some other apps adjust the same colour output. Opening the panel re-applies your matched colours.

Questions or problems: [open an issue](https://github.com/riyajkm/display-control/issues).

## Build from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`winget install Microsoft.DotNet.SDK.10`).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1          # bin\DisplayControl.exe
powershell -ExecutionPolicy Bypass -File .\build-release.ps1  # dist\*.zip portable downloads
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 # dist\*-setup.exe installer (needs Inno Setup 6)
dotnet run --file tests/unit.cs -- bin/DisplayControl.dll   # unit tests
```

### Microsoft Store package

Also needs the Windows SDK (`winget install Microsoft.WindowsSDK.10.0.26100`) for `makeappx` and `makepri`.

```powershell
# Local test build with a placeholder identity
powershell -ExecutionPolicy Bypass -File .\build-msix.ps1

# Store build: identity values from Partner Center > Product management > Product identity
powershell -ExecutionPolicy Bypass -File .\build-msix.ps1 -IdentityName "<Package/Identity/Name>" `
    -Publisher "<Package/Identity/Publisher>" -PublisherDisplayName "<Publisher display name>" `
    -DisplayName "<the app name you reserved>"
```

Output: `dist\DisplayControl_<version>.msixbundle` (x64 + arm64, self-contained) to upload to Partner Center. The Store signs it. Listing text is in [docs/store-listing.md](docs/store-listing.md). Icons are generated by `dotnet run --file tools/make-assets.cs`.

## Roadmap

**1.0:** detection that doesn't drop monitors, monitor settings from capabilities, brightness matching, colour matching, follow laptop brightness, theme/DPI/accessibility, Store packaging, installer.

**Later:**

- [ ] Global hotkeys
- [ ] Schedules and sunrise/sunset
- [ ] Ambient light sensor, on laptops that have one
- [ ] Software dimming: "extra dim" below 0%, and control for screens with no DDC/CI
- [ ] More monitor settings: volume, power mode, monitor info (EDID)

Research notes: [docs/feature-research.md](docs/feature-research.md).

## Layout

```
src/DisplayControl/          app source (.NET 10, Windows Forms), one class per file
  Monitors.cs, Screen2.cs       detection and per-screen DDC/CI / WMI I/O
  Capabilities.cs               MCCS capabilities parser
  Matching.cs, MatchWizard.cs   brightness matching
  ColorBalance.cs, ColorWizard.cs  colour matching (RGB gain or gamma ramp)
  LaptopWatcher.cs              follow laptop brightness
  Panel.cs, FlatSlider.cs       the flyout UI
  App.cs, Program.cs            tray icon, menu, startup; --exit / --uninstall for the installer
packaging/                      MSIX manifest and icons
installer/                      Inno Setup script and wizard images
tests/unit.cs                   unit tests
tools/make-assets.cs            icon generator
docs/                           privacy policy, Store listing, research, screenshots
build.ps1, build-release.ps1, build-installer.ps1, build-msix.ps1   build scripts
```

## License

[MIT](LICENSE)
