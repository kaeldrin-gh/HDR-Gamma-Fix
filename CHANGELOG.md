# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Single-Instance Protection**: Launching the app while another instance is running now shows a message and exits instead of running twice (which caused conflicting gamma state and duplicate dispwin.exe processes).
- **Configurable Hotkeys**: New "Configure Hotkeys..." menu option lets you change the global apply/revert hotkeys (modifiers + key) from the default Alt+F1 / Alt+F2. Choices are persisted in the registry, shown in the tray menu, and roll back if the chosen combination can't be registered.
- **Assembly Version Metadata**: Executable now reports version 1.2.0 in file properties.

### Changed
- **Sticky Monitor Selection**: A monitor that is temporarily disconnected is no longer dropped from the selection. It stays selected (shown as "not connected" in the menu), apply/revert become no-ops while it is away, and the profile is reapplied automatically once it comes back.
- **Clearer Monitor Labels**: The "Apply to Monitor" entries now include resolution and the monitor's EDID id, e.g. `Monitor 1 - 2560x1440 [XMI27B2] (Primary)`, so a monitor stays recognisable even when Windows renumbers it.
- **Async Profile Execution**: dispwin.exe now runs asynchronously so the tray UI and timers stay responsive instead of freezing up to 10 seconds per monitor.
- **Direct dispwin.exe Invocation**: Profiles are applied by calling `scripts/dispwin.exe` directly, removing the intermediate `cmd.exe`/batch-file hop. The `.bat` scripts remain in the distribution for reference but are no longer executed.
- **Watchdog Efficiency**: The settings watchdog timer only runs while the gamma profile is active, instead of polling `SystemSettings.exe` every second while idle in the default state.
- **Higher-DPI Rendering**: Switched from `SystemAware` to `PerMonitorV2` DPI handling for sharper rendering on mixed-DPI setups.

### Fixed
- **Stale Monitor List**: The monitor list was detected once at startup and never refreshed, so disconnecting a monitor or switching between extended/single-display mode left the app targeting a display that no longer existed - Apply/Revert would silently do nothing until the app was restarted. The list is now re-detected whenever Windows reports a display change, and again (throttled) when the tray menu is opened.
- **Monitor Selection Following the Wrong Display**: A saved "Apply to Monitor" choice was stored as a display index, which is just a position in dispwin's list of currently attached displays. Enabling or disabling a monitor renumbered that list, so the profile silently jumped to a different physical monitor. The selection is now keyed on the monitor's stable Windows hardware ID (EDID-derived), so it stays locked to the same panel across topology changes and reboots.
- **Wrong Monitor During Display Transitions**: The target display index was resolved when the display-change event arrived, but dispwin.exe only ran ~1.5s later, by which point Windows may have renumbered the displays. Monitors are now re-detected immediately before each apply/revert so the index can't go stale.
- **Overlapping Launch Guard**: Rapid hotkey/menu presses can no longer start concurrent dispwin.exe processes; the operation in progress wins and subsequent requests are ignored.
- **Hung Process Cleanup**: If dispwin.exe fails to exit within the timeout, it is now killed instead of being left running in the background.
- **Silent Failures**: Previously empty `catch` blocks (monitor detection, registry load/save, process checks) now log diagnostics for easier troubleshooting.

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