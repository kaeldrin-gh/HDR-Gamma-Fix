# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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