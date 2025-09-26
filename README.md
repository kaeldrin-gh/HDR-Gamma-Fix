# HDR Gamma Fix

A Windows system tray utility to fix gamma issues with SDR content when using HDR displays with comprehensive multi-monitor support.

## Overview

Windows HDR implementation often causes SDR content to appear washed out due to incorrect gamma handling. This utility provides a simple way to toggle between default Windows color management and a corrected gamma profile for a better viewing experience.

This tool is designed to stay in your system tray, allowing you to quickly switch profiles with keyboard shortcuts or by clicking the tray icon.

**Based on:** [win11hdr-srgb-to-gamma2.2-icm](https://github.com/dylanraga/win11hdr-srgb-to-gamma2.2-icm) by dylanraga. This project provides a convenient interface for applying the color calibration approach described in that repository. The brightness values can be adjusted to suit your monitor's needs by modifying the 'lut.cal' file in the scripts folder. Refer to the guide in dylanraga's repository for detailed instructions.

### Current Configuration
Current configuration for the Xiaomi Pro G27i MiniLed Monitor with 40% SDR Brightness. You may need to experiment with these settings to achieve optimal results for your display. Refer to the guide in dylanraga's repository for detailed instructions.

## Features

### Core Functionality
- **Quick Toggle:** Single left-click the tray icon to switch between default and gamma-corrected profiles
- **Keyboard Shortcuts:**
  - Alt+F1: Apply sRGB to Gamma profile
  - Alt+F2: Revert to Default profile
- **Minimal Footprint:** Lightweight system tray application that uses minimal resources
- **Visual Feedback:** Different icons for each profile state and brief notifications

### Multi-Monitor Support
- **Automatic Monitor Detection:** Detects all available monitors using dispwin.exe
- **Selective Application:** Choose to apply profiles to specific monitors or all monitors
- **Monitor-Specific Control:** Apply different profiles to different monitors as needed
- **Smart Menu Display:** Context menu shows all available monitors with clear labels

### User Experience
- **Notification Control:** Toggle balloon notifications on/off while retaining all functionality
- **Startup Management:** Configure the application to run automatically at Windows startup
- **Settings Persistence:** All preferences (monitor selection, notification settings) are saved and restored
- **Visual Status:** Tray icon tooltip shows current profile and selected monitor(s)

### Menu Options
- Apply sRGB to Gamma (Alt+F1)
- Revert to Default (Alt+F2)
- Run at Startup (toggleable)
- Apply to Monitor (submenu with all detected monitors)
  - All Monitors (applies to every detected monitor)
  - Individual monitor selection (Monitor 1, Monitor 2, etc.)
- Show Notifications (toggleable)

## Requirements

- Windows 10/11 with HDR capability
- .NET 9 Runtime

## Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract the files to a location of your choice (ensure all files stay together)
3. Run HDRGammaFix.exe
4. Optionally, enable "Run at Startup" from the context menu

### Required Files for Distribution
When moving or sharing the application, ensure these files stay together:
- HDRGammaFix.exe
- HDRGammaFix.dll
- HDRGammaFix.deps.json
- HDRGammaFix.runtimeconfig.json
- scripts/dispwin.exe
- scripts/lut.cal
- scripts/srgb-to-gamma.bat
- scripts/revert.bat
- Resources/DefaultIcon.ico
- Resources/GammaIcon.ico

## Usage

### Basic Operation
- **Left-click** the tray icon to toggle between profiles
- Use **Alt+F1** to apply the gamma-corrected profile
- Use **Alt+F2** to revert to the default Windows profile
- **Right-click** for additional options and settings

### Multi-Monitor Setup
1. Right-click the tray icon to open the context menu
2. Navigate to "Apply to Monitor" submenu
3. Select either:
   - **All Monitors**: Applies the profile to every detected monitor
   - **Individual Monitor**: Choose a specific monitor (e.g., Monitor 1, Monitor 2)
4. The selected option is remembered between application restarts

### Notifications
- Toggle balloon notifications on/off via "Show Notifications" in the context menu
- When enabled, you'll see brief notifications when profiles are applied
- When disabled, the application works silently while maintaining all functionality

## Troubleshooting

### Monitor Detection Issues
- Ensure dispwin.exe is in the scripts folder
- Try restarting the application to refresh monitor detection
- Check that all monitors are properly connected and recognized by Windows

### Profile Not Applied
- Verify all required files are present in the application folder
- Ensure HDR is enabled in Windows Display Settings for the target monitor
- Try applying to individual monitors if "All Monitors" isn't working

## License

MIT License - See [LICENSE](LICENSE) file for details.

## Acknowledgements

- Based on the research and color profile work by [Dylan Raga](https://github.com/dylanraga)
- Icons adapted from standard system resources for clarity
