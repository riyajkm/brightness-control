# Microsoft Store listing: Brightness Control 1.0.0

Copy-paste text for Partner Center (Store listings > English). Text inside the grey blocks goes into the fields as-is. Partner Center fields are plain text: no Markdown, HTML or URLs in the description, and no manual bullets in Product features or Additional system requirements (the Store adds bullets itself).

Limits below are from Microsoft Learn, checked 21 October 2026:
- [Add and edit Store listing info (MSIX)](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)
- [App screenshots, images, and trailers (MSIX)](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)

---

## Product name

```text
Brightness Control
```

The name has to be reserved in Partner Center first (Apps and games > New product > MSIX or PWA app), and it must match the `DisplayName` in the package manifest. "Brightness Control" is a generic phrase and may already be reserved by someone else. If it is, try these in order:

1. `Brightness Control for Monitors`
2. `Screen Brightness Control`
3. `Brightness Control - DDC/CI`

If you reserve a different name, update the manifest `DisplayName` to match before uploading the package.

---

## Short description

Limit: 1,000 characters. Some Store views show only the first 270, so this is kept under that (about 200).

```text
Change the brightness of every screen from the system tray: external monitors over DDC/CI and your laptop screen, with one slider each or one for all. No monitor buttons, no account, no data collection.
```

---

## Description

Limit: 10,000 characters (this is about 2,200).

```text
Changing the brightness of an external monitor usually means reaching behind it and fiddling with tiny buttons and on-screen menus, once per monitor. Brightness Control puts a slider for every screen in the Windows system tray instead.

Click the tray icon and you get one slider per screen plus an "All screens" slider. External monitors are controlled over DDC/CI, the standard most monitors use for settings over the display cable. Laptop screens are controlled through Windows. Night, Evening, Day and Max presets set everything in one click.

Monitor settings without the menus
If a monitor reports that it supports them, Brightness Control also shows its contrast, colour preset (such as sRGB or 6500K) and input source. Settings a monitor doesn't support are simply not shown.

Screens that look equally bright
Two monitors at "50%" rarely look the same. Brightness matching fixes that: a short two-step wizard shows a grey square on each screen and you adjust until they look the same by eye. After that, one slider makes every screen look equally bright.

Follow laptop brightness
Turn this on and your external monitors follow your laptop screen, whether you use the brightness keys or Windows adaptive brightness.

Built to keep working
Monitors that don't answer straight away stay in the list and are checked again when you open the panel, after display changes and after your PC wakes from sleep. If a monitor still won't respond, a detection log shows what happened so the problem can be tracked down.

Fits in with Windows
- Light and dark theme that follows your Windows setting
- Sharp on high-DPI displays
- Sliders work with the keyboard and with screen readers
- Optional start with Windows

Private by design
Brightness Control collects no data, has no ads and no account, and works fully offline. Settings stay on your PC.

If a monitor doesn't respond
- Turn on DDC/CI in the monitor's own on-screen menu (it is sometimes off by default).
- Set ECO or power-saving mode to Standard; some eco modes lock brightness.
- Connect the monitor directly to the PC. Docks, USB display adapters and some KVM switches don't pass DDC/CI through.
Then choose Refresh screens from the tray menu.
```

---

## What's new in this version

Limit: 1,500 characters. Microsoft's guidance is to leave this field **blank on the first submission**. Keep the text below for the GitHub release notes, or use it once 1.0.x updates start.

```text
First release.
- One slider per screen plus an "All screens" slider, with Night, Evening, Day and Max presets
- External monitors over DDC/CI, laptop screens through Windows
- Contrast, colour preset and input source for monitors that support them
- Brightness matching across screens with a two-step by-eye wizard
- Optional "Follow laptop brightness"
- Detection that retries unresponsive monitors and a detection log for troubleshooting
```

---

## Product features

Limit: up to 20 features, 200 characters each. Enter one per field, without bullets.

```text
One brightness slider per screen, plus an "All screens" slider
Night, Evening, Day and Max presets
External monitors over DDC/CI, laptop screens through Windows
Contrast, colour preset and input source for monitors that support them
Brightness matching so one slider looks equally bright on every screen
Two-step by-eye matching wizard using grey squares on each screen
Optional: external monitors follow your laptop's brightness keys and adaptive brightness
Unresponsive monitors stay listed and are re-checked after display changes and sleep
Detection log for troubleshooting
Light and dark theme follows Windows, sharp on high-DPI displays
Keyboard and screen-reader accessible sliders
Optional start with Windows
No data collection, no account, works offline
```

---

## Search terms

The current Microsoft Learn listing pages (above) no longer document a Search terms field. If your Partner Center listing page still shows one, the long-standing limits have been 7 terms, 30 characters each, and 21 words across all terms (not re-verified against current docs). These fit those limits (13 words total):

```text
brightness
monitor brightness
external monitor
DDC/CI
dimmer
multi monitor
screen brightness
```

If the field isn't there, nothing is lost: Store search also uses the name, description and features, which already contain these words.

---

## Category

- Category: **Utilities & tools**
- Subcategory: if offered, choose the closest system/utility option (for example "Other" or "System tools" depending on what Partner Center lists for Utilities & tools). Partner Center only shows subcategories for some categories.

---

## Age rating

Complete the IARC questionnaire under Age ratings. Answers for 1.0:

- App type: utility / not a game.
- No violence, sexual content, profanity, gambling, drugs or alcohol references.
- No user-to-user interaction, chat or user-generated content.
- Does not share the user's location or personal information.
- No in-app purchases, no ads, no unrestricted web browsing (the About box only offers to open the project page in the user's own browser).

Expected result: 3+ (PEGI 3 / ESRB Everyone, and equivalents).

---

## Privacy policy URL

```text
https://github.com/riyajkm/brightness-control/blob/main/docs/privacy.md
```

Website and support contact can both point to the repository and its Issues page.

---

## System requirements

In **Properties > System requirements**, no special hardware is required beyond a display. Minimum OS is set by the package manifest (the app targets Windows 10 version 1809 / build 17763 or later).

**Additional system requirements** (Store listing, Minimum hardware; up to 11 items, 200 characters each):

```text
Windows 10 version 1809 or later, or Windows 11
External monitors must support DDC/CI with it turned on in the monitor's menu
Monitors connected through docks, USB display adapters or KVM switches may not respond
Laptop screen control requires a built-in display whose brightness Windows can set
```

---

## Screenshots

Requirements: PNG, at least 1366 x 768 (up to 3840 x 2160), 50 MB max each, up to 10 desktop screenshots; caption up to 200 characters each. Keep the important part in the top two-thirds of the image, and don't add logos or marketing text on top. Take them on a clean desktop with a calm wallpaper, at 100% or 150% scaling, and crop or compose so the tray panel isn't a tiny speck in a huge image (for example, take a 1920 x 1080 shot and place the panel in the upper part of the frame).

1. **Main panel, multiple screens.** Tray panel open showing the "All screens" slider and sliders for a laptop screen and two external monitors (with real model names), presets row visible. Light theme.
   Caption: `One slider per screen, plus one for all of them`
2. **Monitor settings.** Panel with one external monitor's settings expanded: contrast slider, colour preset (sRGB/6500K) and input source. Dark theme, to show the theme follows Windows.
   Caption: `Contrast, colour preset and input source, when your monitor supports them`
3. **Matching wizard.** The matching wizard in progress, with its grey square on the screen. If possible, capture across two screens so a grey square shows on each.
   Caption: `Match screens by eye so one slider looks the same everywhere`
4. **Tray menu.** The right-click tray menu open, showing Match brightness across screens, Calibrate matching, Follow laptop brightness (checked), Start with Windows, Open detection log.
   Caption: `Follow your laptop's brightness keys on external monitors`
5. **Unresponsive monitor.** Panel showing a monitor that didn't answer still listed, next to working screens (only if the app shows this clearly; otherwise skip this shot). Optionally the detection log open in Notepad beside it (check it shows nothing you'd rather not publish).
   Caption: `Monitors that don't answer stay listed and are re-checked automatically`

---

## Store listing notes and reviewer notes

**Notes for certification** (Submission options > Notes for certification), suggested text:

```text
Brightness Control runs in the system tray; click the tray icon to open the brightness panel. External monitor controls appear only on monitors that support DDC/CI (most desktop monitors, often needs DDC/CI enabled in the monitor menu). On a VM or a PC without such a monitor, the panel shows only the screens Windows can control, which is expected. The app makes no network connections and collects no data.
```
