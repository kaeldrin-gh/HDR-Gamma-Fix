# HDR Gamma Fix

A small Windows tray app that fixes washed-out SDR content on HDR displays, with one click or hotkey.

<div align="center">
  <img src="ui.png" alt="HDR Gamma Fix tray menu" />
</div>

## Why

With HDR enabled, Windows displays SDR content (the desktop, most apps, videos and games that aren't HDR) using the piecewise sRGB curve instead of the pure gamma 2.2 that SDR content is usually mastered for. Shadows come out lifted and grayish, and the whole picture looks washed out compared with HDR off.

HDR Gamma Fix loads a correction curve (a 1D LUT) into the GPU that maps SDR content from sRGB to gamma 2.2. It's based on [win11hdr-srgb-to-gamma2.2-icm](https://github.com/dylanraga/win11hdr-srgb-to-gamma2.2-icm) by Dylan Raga, wrapped in a tray app that keeps the curve applied.

## Features

- **One-click toggle:** left-click the tray icon, or use the global hotkeys (Alt+F1 / Alt+F2 by default, configurable)
- **Curve matched to your brightness:** the curve is generated per monitor from Windows' SDR content brightness slider and follows it automatically when you move it
- **Stays applied:** reapplies automatically when Windows resets the curve, e.g. after opening Display Settings, signing in, unlocking, or waking from sleep
- **HDR-aware:** only affects displays with HDR on, and pauses by itself when you turn HDR off
- **Pauses for HDR games:** list your HDR games and the fix steps aside while one is in front, then comes back when you switch away
- **Multi-monitor:** apply to all monitors or pick one; the choice follows the physical monitor even when Windows renumbers displays
- **Lightweight:** checks run in-process, with no background polling of external tools

## Requirements

- Windows 10 or 11 with an HDR display and HDR enabled
- [.NET 9 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Installation

1. Download the latest zip from the [Releases](../../releases) page
2. Extract it to a folder of your choice, keeping the files together
3. Run `HDRGammaFix.exe`; the fix is applied straight away
4. Optionally, enable **Run at Startup** from the tray menu

The zip contains `HDRGammaFix.exe` and the `scripts` folder (`dispwin.exe` and `lut.cal`), which are required. `Resources` holds fallback icons, `HDRGammaFix.pdb` is only needed to diagnose crashes, and the `.bat` files in `scripts` are for reference only.

## Usage

### Tray icon

| Icon | Meaning |
|------|---------|
| Monitor outline | Off: Windows' default curve |
| Blue monitor | On: the gamma curve is applied |
| Blue outline with pause bars | On but paused: HDR is off, the selected monitor isn't connected, or an app from **Pause for Apps** is in front. It resumes by itself. |

Hover over the icon for details, including which monitor is targeted. Left-click toggles the fix on and off.

### Menu

| Item | What it does |
|------|--------------|
| Apply sRGB to Gamma / Revert to Default | Turn the fix on or off (hotkeys shown next to them) |
| Run at Startup | Start with Windows |
| Apply to Monitor | All monitors, or one specific monitor |
| Show Notifications | Brief notifications when the state changes |
| Only Apply When HDR Is On | Skip displays in SDR mode (on by default) |
| Revert on Exit | Restore the default curve when you exit (off by default) |
| Gamma Curve | Current SDR brightness per monitor, and the curve settings below |
| Pause for Apps... | Apps (such as HDR games) that pause the fix while they're in front |
| Configure Hotkeys... | Change the two global hotkeys |

### Gamma curve

The right curve depends on the **SDR content brightness** slider in Windows HDR settings. By default the app generates the curve itself for each monitor from that slider, using the same formula as Dylan Raga's [LUT generator](https://dylanraga.github.io/gen-srgb-to-gamma-lut/), and regenerates it within a couple of seconds when you move the slider.

Under **Gamma Curve** you can:

- choose **Gamma 2.2** (default) or **Gamma 2.4** (darker; suits a dim room)
- raise the **Black Floor** if dark detail is crushed to black
- override the **GPU Method** (NVIDIA or AMD), which is normally detected from your graphics card
- choose **Use lut.cal File** to load `scripts/lut.cal` instead, e.g. a curve you made with the web generator

The bundled `lut.cal` is the generator's output for 300 nits (slider 55), gamma 2.2 and the NVIDIA method. If you've edited `lut.cal`, the app keeps using it after upgrading.

### HDR games and videos

Native HDR games and videos don't have the washed-out problem, but the correction curve applies to everything on screen: it leaves highlights above SDR white alone but darkens HDR shadows and mid-tones. Add your HDR games under **Pause for Apps...**, either picking them while they're running or browsing to their `.exe`. The fix then pauses (removing the curve) while one of them is in front, and comes back when you Alt+Tab out or quit. To keep the fix on in a listed game anyway, press the apply hotkey while you're in it; it stays on until you switch away. Switching the curve takes a moment, so a brief flicker when a listed game gains or loses focus is normal.

## Troubleshooting

- **Shadows look crushed or too dark:** raise **Gamma Curve → Black Floor**, or switch to Gamma 2.2 if you chose 2.4.
- **No visible change:** make sure HDR is on for that monitor (the icon shows paused if it isn't) and that the right monitor is selected under **Apply to Monitor**.
- **AMD graphics:** check that **Gamma Curve → GPU Method** is AMD. The AMD method follows the generator but has had less testing than NVIDIA.
- **Colors look wrong with HDR off:** keep **Only Apply When HDR Is On** checked; the curve is designed for HDR mode.
- **One app's window flickers while the fix is on (NVIDIA):** this mostly affects Chromium/Electron apps such as Chrome, Discord or Claude. It's caused by the loaded gamma curve itself, not by HDR Gamma Fix's activity: it happens with any curve loaded through `dispwin`, even with the app closed. The likely cause is the driver's multi-plane overlay (MPO) handling, where the window switches between rendering paths that apply the curve differently. In order of preference:
  1. Update the NVIDIA driver.
  2. Turn off hardware acceleration in the flickering app, if it has that setting.
  3. Add the app under **Pause for Apps...**. It won't flicker while it's in front, but its SDR content looks washed out again there.
  4. Disable MPO system-wide with NVIDIA's workaround: create the DWORD `OverlayTestMode` = `5` under `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm` and reboot. Delete the value and reboot to undo. It may not take effect on the newest Windows 11 builds.
- **Monitor missing from the menu:** open the menu again after connecting it (the list refreshes), and check that `scripts/dispwin.exe` is present.
- **Hotkey doesn't work:** another app may already use it; pick another under **Configure Hotkeys...**

## Settings and uninstalling

Settings are stored in the registry under `HKEY_CURRENT_USER\SOFTWARE\HDRGammaFix`. Generated curves are written to `%LOCALAPPDATA%\HDRGammaFix\luts`.

To uninstall, turn off **Run at Startup**, choose **Revert to Default** and exit, then delete the app folder. Optionally delete the registry key and the `%LOCALAPPDATA%\HDRGammaFix` folder too.

## License

MIT License. See [LICENSE](LICENSE).

## Acknowledgements

- [Dylan Raga](https://github.com/dylanraga) for the sRGB-to-gamma research, the method and the LUT generator this app builds on

## Third-Party Notices

- `scripts/dispwin.exe` is part of [Argyll CMS](https://github.com/argyllcms/argyllcms) v3.1.0 by Graeme W. Gill, licensed under the [GNU Affero General Public License v3](https://www.gnu.org/licenses/agpl-3.0.html). This project bundles it unmodified; you can get the corresponding source from the upstream repository. The full license text is in `AGPL-3.0.txt`, which is included in the release zip.
