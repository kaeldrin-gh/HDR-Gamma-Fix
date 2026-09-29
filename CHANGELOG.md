# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-09-29

### Added
- Built-in LUT generator (a port of dylanraga's web generator): each monitor's gamma curve is
  generated from its current Windows "SDR content brightness" and regenerated automatically when
  the slider moves, so `lut.cal` no longer has to be made and edited by hand. A new "Gamma Curve"
  menu shows each monitor's SDR brightness and sets gamma (2.2/2.4), black floor and GPU method
  (NVIDIA/AMD, detected automatically). "Use lut.cal File" keeps the previous behavior, and is
  selected automatically if `scripts/lut.cal` was hand-edited.
- "Only Apply When HDR Is On" option (on by default): the HDR-tuned LUT is skipped on displays
  in SDR mode, removed when HDR is switched off, and reapplied automatically when it's turned
  back on.
- "Revert on Exit" option (off by default) that clears the LUT when the app is closed.

### Changed
- Redesigned tray icons, drawn at the exact tray size so they stay sharp at any scaling. The "off"
  icon follows the taskbar theme (the old black one was nearly invisible on a dark taskbar), and a
  new "paused" icon shows when the fix is on but nothing is loaded (HDR off, or the selected
  monitor disconnected). The exe icon matches, with sharp images from 16 to 256 px (it was a
  single 256 px image that Windows had to shrink).
- README rewritten: tray icon states, menu reference, gamma curve settings, troubleshooting and
  uninstall steps.
- The watchdog now reads the loaded gamma ramp back and reapplies only when Windows actually
  replaced it, whatever the cause. Previously it guessed from whether Windows Settings was
  running and reapplied every 4 seconds for as long as `SystemSettings.exe` existed, which on
  Windows 11 can be long after the Settings window is closed. The guess is kept only as a
  fallback where the ramp can't be read, and is now capped at 60 seconds.

### Fixed
- The README described the bundled `lut.cal` as made for 40% SDR brightness; it is the
  generator's output for 300 nits (slider 55), gamma 2.2, NVIDIA method.
- The profile is reapplied after resume from sleep, session unlock and console connect, and
  checked again shortly after startup, where Windows' Calibration Loader task or the graphics
  driver can silently reset the gamma ramp.
- Events caused by the app's own apply/revert could trigger another reapply when the operation
  took longer than 2 seconds.
- "Run at Startup" is repointed at the current exe if the registered one no longer exists
  (e.g. after moving the app folder).
- Exiting during an "All Monitors" apply can no longer start `dispwin.exe` for the next monitor.

## [1.2.2] - 2026-09-11

### Changed
- Monitor detection no longer blocks the UI thread, so opening the tray menu or a display
  topology change can't freeze the app while `dispwin.exe` starts.
- Failures while applying/reverting on "All Monitors" now mark the profile as partially
  applied instead of silently leaving the app in the default state.
- Background apply/revert failures notify via balloon tip instead of intrusive modal dialogs.

### Fixed
- `dispwin.exe` monitor detection can no longer deadlock on its output pipes, and a hung
  helper process is terminated instead of being orphaned.
- The "session ending" suppression now expires, so a shutdown cancelled by another app can't
  leave the hotkeys and automatic recovery permanently disabled.
- Holding down a hotkey no longer queues repeated toggles (`MOD_NOREPEAT`).
- The hotkey configuration dialog now scales with the system font on high-DPI displays.
- Reverting a profile no longer stops the settings watchdog if the revert itself fails.

## [1.2.1] - 2026-08-28

### Changed
- Improved responsiveness and reliability when applying gamma profiles.
- Added configurable hotkeys and better support for everyday multi-monitor use.
- Monitor selections now stay accurate when displays are connected or disconnected.

### Fixed
- Prevented duplicate app instances and cleaned up stuck background processes.
- Improved error handling and high-DPI display support.

## [1.1.0] - 2025-09-26

### Added
- **Multi-Monitor Support**: Comprehensive support for multiple monitor setups
  - Automatic monitor detection using dispwin.exe output parsing
  - Individual monitor selection (Monitor 1, Monitor 2, etc.)
  - "All Monitors" option that applies profiles to every detected monitor
  - Monitor-specific profile application using `dispwin.exe -d [monitor]` commands
- **Notification Control**: Toggle balloon notifications on/off while retaining functionality
  - "Show Notifications" menu option with persistent setting
  - Registry-based preference storage for notification settings
- **Enhanced User Experience**:
  - Improved tray icon tooltips showing current monitor selection
  - Better error handling and fallback mechanisms
  - More informative notification messages with monitor context
  - Persistent monitor selection preferences across application restarts

### Fixed
- **Monitor Detection**: Replaced unreliable monitor testing with robust dispwin output parsing
- **All Monitors Logic**: Fixed issue where "All Monitors" only applied to single monitor
- **Null Reference Exception**: Fixed startup crash related to notification timer initialization
- **Menu Consistency**: Monitor submenu now shows even with single monitor for better UX

### Changed
- **Monitor Application Logic**: "All Monitors" now loops through each monitor individually instead of relying on batch files
- **Settings Storage**: All user preferences now stored in `HKCU\SOFTWARE\HDRGammaFix` registry key
- **Profile Application**: Enhanced dispwin.exe integration for more reliable monitor-specific operations

### Technical Improvements
- Better regex-based monitor parsing for more accurate detection
- Improved process execution with proper timeout handling
- Enhanced registry operations with better error handling
- More robust file path resolution for distributed applications

## [1.0.0] - 2025-04-09

### Added
- Initial release
- System tray icon with toggle functionality
- Keyboard shortcuts (Alt+F1 and Alt+F2)
- Start with Windows option
- Brief notification system
- Error handling for script execution