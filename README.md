# HDR Gamma Fix

A Windows system tray utility to fix gamma issues with SDR content when using HDR.

## Overview

Windows HDR implementation often causes SDR content to appear washed out due to incorrect gamma handling. This utility provides a simple way to toggle between default Windows color management and a corrected gamma profile for a better viewing experience.

This tool is designed to stay in your system tray, allowing you to quickly switch profiles with keyboard shortcuts or by clicking the tray icon.

**Based on:** [win11hdr-srgb-to-gamma2.2-icm](https://github.com/dylanraga/win11hdr-srgb-to-gamma2.2-icm) by dylanraga. This project provides a convenient interface for applying the color calibration approach described in that repository. The brightness values can be adjusted to suit your monitor's needs by modifying the 'lut.cal' file in the scripts folder. Refer to the guide in dylanraga's repository for detailed instructions.

### Current Configuration
Current configuration for the Xiaomi Pro G27i MiniLed Monitor with 40% SDR Brightness. You may need to experiment with these settings to achieve optimal results for your display. Refer to the guide in dylanraga's repository for detailed instructions.

## Features

- **Quick Toggle:** Left-click the tray icon to switch between default and gamma-corrected profiles
- **Keyboard Shortcuts:**
  - Alt+F1: Apply sRGB to Gamma profile
  - Alt+F2: Revert to Default profile
- **Minimal Footprint:** Lightweight system tray application that uses minimal resources
- **Startup Option:** Configure the application to run automatically at Windows startup
- **Visual Feedback:** Different icons for each profile state and brief notifications

## Requirements

- Windows 10/11 with HDR capability
- .NET 9 Runtime

## Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract the files to a location of your choice
3. Run HDRGammaFix.exe
4. Optionally, enable "Run at Startup" from the context menu

## Usage

- **Left-click** the tray icon to toggle between profiles
- Use **Alt+F1** to apply the gamma-corrected profile
- Use **Alt+F2** to revert to the default Windows profile
- **Right-click** for additional options including startup configuration

## Building from Source

### Prerequisites
- Visual Studio 2022 or newer
- .NET 9 SDK

### Steps
1. Clone this repository
2. Open SystemTrayApp.sln in Visual Studio
3. Build the solution
4. The compiled output will be in the `bin\Release\net9.0-windows\publish` folder

## License

MIT License - See [LICENSE](LICENSE) file for details.

## Acknowledgements

- Based on the research and color profile work by [Dylan Raga](https://github.com/dylanraga)
- Icons adapted from standard system resources for clarity