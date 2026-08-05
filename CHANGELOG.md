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
- **Async Profile Execution**: dispwin.exe now runs asynchronously so the tray UI and timers stay responsive instead of freezing up to 10 seconds per monitor.
- **Direct dispwin.exe Invocation**: Profiles are applied by calling `scripts/dispwin.exe` directly, removing the intermediate `cmd.exe`/batch-file hop. The `.bat` scripts remain in the distribution for reference but are no longer executed.
- **Watchdog Efficiency**: The settings watchdog timer only runs while the gamma profile is active, instead of polling `SystemSettings.exe` every second while idle in the default state.
- **Higher-DPI Rendering**: Switched from `SystemAware` to `PerMonitorV2` DPI handling for sharper rendering on mixed-DPI setups.

### Fixed
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