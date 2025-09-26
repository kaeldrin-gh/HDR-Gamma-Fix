# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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