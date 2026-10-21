# Brightness Control privacy policy

Effective date: 21 October 2026

Brightness Control is a Windows tray app that changes the brightness of your screens. This policy covers the app as installed from the Microsoft Store and the standalone download from this repository.

## The short version

Brightness Control does not collect, send or sell any information about you. It has no telemetry, no analytics, no ads and no accounts, and it never connects to the internet.

## What the app does not do

- It does not collect personal data.
- It does not send usage statistics, crash reports or diagnostics anywhere.
- It does not show ads or use advertising IDs.
- It does not require an account or sign-in.
- It makes no network connections of its own.

The only web link in the app is in **About**. If you choose to open the project page, Windows opens it in your default browser. That visit is between you and GitHub; the app is not involved after it hands over the link.

## What the app stores on your PC

The app keeps two files in its own data folder. When installed from the Microsoft Store, this is the package's `LocalState` folder. The standalone version uses `%LOCALAPPDATA%\BrightnessControl`.

- **settings.json** holds your preferences (for example, whether brightness matching or "Follow laptop brightness" is on), plus each monitor's capabilities string, brightness-matching curve and colour-matching values (including the monitor's original colour preset, so it can be restored). Monitors are identified by their Windows display device IDs.
- **detect.log** records what happened while the app looked for monitors: monitor model names, display device IDs and error codes. It exists only to help troubleshoot a monitor that isn't detected. The file is capped at 256 KB and is cleared when it grows past that.

Neither file is ever uploaded or sent anywhere by the app. You can open the log from the tray menu (**Open detection log**). If you report a problem, you can choose to paste or attach it to a GitHub issue; that is always your decision, and you can read or edit it first.

## How the app talks to your hardware

- **External monitors:** the app sends brightness, contrast, colour preset, input source and (for colour matching) red/green/blue level commands directly to the monitor over the display cable, using the DDC/CI standard.
- **Colour matching on other screens:** for the laptop screen and monitors that ignore colour commands, the app adjusts that screen's colour output in Windows (its gamma ramp). This stays on your PC.
- **Laptop screens:** the app reads and sets brightness through Windows Management Instrumentation (WMI), the same interface Windows itself uses.
- **Follow laptop brightness:** when you turn this on, the app listens for Windows brightness-change events on your PC so your external monitors can follow the laptop screen. These events stay on your PC.

All of this happens locally between your PC and your screens.

## Start with Windows

If you turn on **Start with Windows**, the app registers itself to start when you sign in (a startup task for the Store version, or an entry in your user's Run key for the standalone version). Turning the option off removes it.

## Removing your data

Uninstalling the Microsoft Store version removes the app together with its `LocalState` folder, including settings.json and detect.log. For the standalone version, delete the `%LOCALAPPDATA%\BrightnessControl` folder after removing the app.

## Children

The app collects no data from anyone, including children.

## Changes to this policy

If the app's handling of data ever changes, this page will be updated and the effective date above will change. The history of this file is visible on GitHub.

## Contact

Questions about privacy can be raised as an issue at https://github.com/riyajkm/brightness-control/issues.
