# Feature research (October 2026)

Feasibility notes for the advanced options planned for the Store release. Measurements are from a development machine: a Lenovo 82SA laptop (built-in LEN9153 panel), an MSI MD342CQP (DisplayPort) and a ViewSonic VA2759 (HDMI). All checks were read-only and ran unpackaged.

## 1. Monitor controls beyond brightness (Medium)

These use the same DDC/CI get/set VCP calls as brightness. The work is in parsing each monitor's MCCS capabilities string and offering only what it lists.

| VCP | Feature |
|---|---|
| 0x12 | Contrast |
| 0x14 | Colour preset (01 sRGB, 04 5000K, 05 6500K, 06 7500K, 08 9300K, 0B/0C User 1/2) |
| 0x16 / 0x18 / 0x1A | Red / green / blue gain |
| 0x6C / 0x6E / 0x70 | Black levels |
| 0x60 | Input source (0F DP1, 10 DP2, 11 HDMI1, 12 HDMI2) |
| 0x62 / 0x8D | Volume / mute |
| 0xD6 | Power mode |
| 0xDC | Display mode |

Lessons from the test monitors:

- **A successful read doesn't mean the feature exists.** The MSI returned believable values for codes missing from its capabilities string (0x6C/6E/70, 0x62, 0x8D). Offer features from the parsed `vcp(...)` list only.
- **The reported maximum is meaningless for list-type codes.** For example, the ViewSonic reports input 0x60 as current 17, max 3. Use the allowed values listed in brackets in the capabilities string.
- **The parser must tolerate messy strings:** spaces inside brackets (`60( 11 12 0F 10)`, `D6( 05 )`), nested brackets, and tags without separators (`MStarcmds`).
- **DDC/CI is slow:** about 40–50 ms per call, and over 1 s to fetch the capabilities string. Cache it per monitor.
- **Input and power-mode changes can make the monitor disappear from DDC/CI.**

**Software gamma ramps (`SetDeviceGammaRamp`)** are fragile:
- Windows quietly ignores ramps far from normal, which limits how dark they can go. Lifting that limit needs an admin-only, machine-wide registry key, which a Store app can't rely on.
- They reset on display changes and conflict with Night Light.
- Use them only as an optional "extra dim", or use a dimming overlay instead.

## 2. Understanding the monitor (Easy)

- **EDID** (registry `Enum\DISPLAY\...\Device Parameters\EDID`, or WMI `WmiMonitorID` / `WmiMonitorDescriptorMethods`) gives:
  - manufacturer, model and serial
  - manufacture date
  - size, gamma and colour primaries
- **WinRT `Windows.Devices.Display.DisplayMonitor`** works from `net10.0-windows10.0.19041.0` in an unpackaged app (verified). Packaged use is not yet tested.
- **Luminance:** trust `MaxLuminanceInNits` only when the EDID has an HDR or DisplayID block. On this machine:
  - MSI: 375 nits peak, 277 full-frame.
  - Laptop and ViewSonic: report a 270 / 0.5 nits placeholder. Treat that as unknown.

## 3. Time-based brightness (Easy)

- **Schedule:** fixed points plus sunrise/sunset, calculated locally with the NOAA solar algorithm, so no network is needed.
- **Transitions:** small steps, with DDC/CI writes throttled to about 1 per second per monitor.
- **Location:** Windows location access needs a capability and the user's consent. Default to a city picker, or a rough position from the time zone.

## 4. Auto brightness from the laptop

- **Ambient light sensor:** the test laptop has none (`LightSensor.GetDefault()` returns null). Offer it as an optional feature that hides when no sensor exists.
- **Following the laptop instead (Medium):** subscribe to the WMI `WmiMonitorBrightnessEvent`. When the brightness keys, adaptive brightness or battery saver change the laptop screen, the external monitors follow through the mapping in section 5.
  - Ignore events caused by the app's own writes, to avoid a feedback loop.
- **Webcam-based light estimate:** not recommended (privacy concerns, extra Store review).

## 5. Matching brightness across monitors (Medium)

Screens set to the same percentage don't look the same. On the test desk they range from about 270 to 375 nits at 100%.

- **Model:** nits ≈ Lmin + (Lmax − Lmin) · (p/100)^γ. Pick one target brightness in nits and solve for each monitor's percentage.
- **Lmax source:** the EDID HDR block when present, otherwise a typical value that the user can override.
- **What sets us apart:** a "match" wizard where the user lines up 2–3 grey patches by eye, giving a calibration curve per monitor.
- **Competitors:** Twinkle Tray and Monitorian only offer a linear min/max range per monitor.

## Recommended order

1. Parse and cache capabilities; add contrast, colour preset and input source, offered only where supported.
2. Follow the laptop's brightness changes.
3. Perceptual matching with the by-eye calibration wizard.
4. Schedules and sunrise/sunset.
5. Monitor info panel (EDID / DisplayMonitor).
6. Volume, power mode, RGB gain and black level.
7. Ambient light sensor (only when present).
8. Software dimming overlay / gamma ("extra dim", and the fallback for uncontrollable screens).

A full-trust MSIX package (`runFullTrust`) keeps dxva2, WMI and WinRT available. Twinkle Tray and Monitorian ship on the Store this way.

## Sources

- [SetDeviceGammaRamp](https://learn.microsoft.com/en-us/windows/desktop/api/wingdi/nf-wingdi-setdevicegammaramp)
- [ReactOS ICM gamma range](https://doxygen.reactos.org/db/db6/win32ss_2gdi_2ntgdi_2icm_8c.html)
- [DisplayMonitor (WinRT)](https://learn.microsoft.com/en-us/uwp/api/windows.devices.display.displaymonitor?view=winrt-19041)
- [AdvancedColorInfo.MaxLuminanceInNits](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.display.advancedcolorinfo.maxluminanceinnits?view=winrt-19041)
- [Twinkle Tray 1.16.0 beta](https://www.neowin.net/software/twinkle-tray-1160-beta/)
- [Twinkle Tray 1.16.4](https://www.warp2search.net/story/twinkle-tray-brightness-slider-1164/)
