using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemTrayApp
{
    /// <summary>
    /// In-process queries of the live display state: whether HDR is on for each display, and which
    /// gamma ramp is currently loaded. These are cheap enough to poll, unlike launching dispwin.exe.
    /// Displays are keyed by their GDI adapter name without the "\\.\" prefix (e.g. "DISPLAY1"),
    /// matching what dispwin reports.
    /// </summary>
    internal static class DisplayState
    {
        private const int GammaRampLength = 256 * 3;

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool GetDeviceGammaRamp(IntPtr hdc, ushort[] lpRamp);

        /// <summary>
        /// Reads the gamma ramp currently loaded for a display (256 red, then green, then blue
        /// entries). Returns null if it can't be read.
        /// </summary>
        public static ushort[]? ReadGammaRamp(string adapterName)
        {
            if (string.IsNullOrEmpty(adapterName))
            {
                return null;
            }

            IntPtr hdc = CreateDC("DISPLAY", @"\\.\" + adapterName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var ramp = new ushort[GammaRampLength];
                return GetDeviceGammaRamp(hdc, ramp) ? ramp : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading gamma ramp for {adapterName}: {ex}");
                return null;
            }
            finally
            {
                DeleteDC(hdc);
            }
        }

        public static bool RampsEqual(ushort[] a, ushort[] b) => a.AsSpan().SequenceEqual(b);

        /// <summary>
        /// True if the ramp is the default straight line. The gamma LUT is never that, so reading
        /// it back after applying the LUT means reads don't reflect what dispwin loaded and can't
        /// be used to verify it.
        /// </summary>
        public static bool IsIdentityRamp(ushort[] ramp)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (Math.Abs(ramp[channel * 256 + i] - i * 257) > 512)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // --- HDR state via the DisplayConfig API ---

        private const uint QDC_ONLY_ACTIVE_PATHS = 0x2;
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 = 15; // Windows 11 24H2+
        private const int DISPLAYCONFIG_ADVANCED_COLOR_MODE_HDR = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public int outputTechnology;
            public int rotation;
            public int scaling;
            public uint refreshRateNumerator;
            public uint refreshRateDenominator;
            public int scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        // Only needed as a correctly sized buffer for QueryDisplayConfig (64 bytes); the mode
        // union's contents are never read.
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct DISPLAYCONFIG_MODE_INFO
        {
            public int infoType;
            public uint id;
            public LUID adapterId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public int type;
            public int size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value; // bit 1: advancedColorEnabled, bit 2: wideColorEnforced
            public int colorEncoding;
            public uint bitsPerColorChannel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public int colorEncoding;
            public uint bitsPerColorChannel;
            public int activeColorMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SDR_WHITE_LEVEL
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint SDRWhiteLevel; // In units of 80 nits / 1000, i.e. 1000 = 80 nits
        }

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SDR_WHITE_LEVEL requestPacket);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 requestPacket);

        /// <summary>
        /// Live color state of one display. Either value is null if Windows couldn't report it.
        /// </summary>
        public readonly record struct DisplayColorInfo(bool? IsHdr, double? SdrWhiteNits);

        /// <summary>
        /// Returns the HDR state and SDR white level ("SDR content brightness") of each active
        /// display. Displays that couldn't be queried at all are left out, so callers can treat
        /// "missing" as "unknown".
        /// </summary>
        public static Dictionary<string, DisplayColorInfo> GetColorInfoByAdapter()
        {
            var result = new Dictionary<string, DisplayColorInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                DISPLAYCONFIG_PATH_INFO[] paths;
                uint pathCount;
                int status;
                do
                {
                    status = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out uint modeCount);
                    if (status != ERROR_SUCCESS)
                    {
                        return result;
                    }

                    paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                    var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                    status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                }
                while (status == ERROR_INSUFFICIENT_BUFFER); // Topology changed between the two calls

                if (status != ERROR_SUCCESS)
                {
                    return result;
                }

                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];

                    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    sourceName.header.size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    sourceName.header.adapterId = path.sourceInfo.adapterId;
                    sourceName.header.id = path.sourceInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref sourceName) != ERROR_SUCCESS)
                    {
                        continue;
                    }

                    bool? isHdr = QueryIsHdr(path.targetInfo.adapterId, path.targetInfo.id);
                    double? sdrWhiteNits = QuerySdrWhiteNits(path.targetInfo.adapterId, path.targetInfo.id);
                    if (isHdr == null && sdrWhiteNits == null)
                    {
                        continue;
                    }

                    string adapterName = sourceName.viewGdiDeviceName.StartsWith(@"\\.\", StringComparison.Ordinal)
                        ? sourceName.viewGdiDeviceName.Substring(4)
                        : sourceName.viewGdiDeviceName;

                    // A cloned desktop has several targets per source; it counts as HDR if any
                    // does. The first target's white level wins, since one ramp serves them all.
                    if (result.TryGetValue(adapterName, out var existing))
                    {
                        isHdr = existing.IsHdr == true || isHdr == true ? true : existing.IsHdr ?? isHdr;
                        sdrWhiteNits = existing.SdrWhiteNits ?? sdrWhiteNits;
                    }
                    result[adapterName] = new DisplayColorInfo(isHdr, sdrWhiteNits);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying HDR state: {ex}");
            }

            return result;
        }

        private static double? QuerySdrWhiteNits(LUID adapterId, uint targetId)
        {
            var info = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
            info.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
            info.header.size = Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
            info.header.adapterId = adapterId;
            info.header.id = targetId;
            if (DisplayConfigGetDeviceInfo(ref info) != ERROR_SUCCESS || info.SDRWhiteLevel == 0)
            {
                return null;
            }
            return info.SDRWhiteLevel / 1000.0 * 80;
        }

        /// <summary>
        /// Picks the LUT method for the GPU driving a display, from its adapter description
        /// (e.g. "NVIDIA GeForce RTX 5070 Ti"). AMD needs its own method; everything else uses
        /// the NVIDIA one, which is also the generator's default.
        /// </summary>
        public static LutMethod DetectLutMethod(string adapterName)
        {
            try
            {
                for (uint i = 0; ; i++)
                {
                    var adapter = new DISPLAY_DEVICE();
                    adapter.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
                    if (!EnumDisplayDevices(null, i, ref adapter, 0))
                    {
                        break;
                    }

                    if (string.Equals(adapter.DeviceName, @"\\.\" + adapterName, StringComparison.OrdinalIgnoreCase))
                    {
                        string gpu = adapter.DeviceString ?? "";
                        return gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                            || gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                            ? LutMethod.Amd
                            : LutMethod.Nvidia;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting GPU vendor for {adapterName}: {ex}");
            }
            return LutMethod.Nvidia;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        private static bool? QueryIsHdr(LUID adapterId, uint targetId)
        {
            // Windows 11 24H2+ reports the active color mode directly. Prefer it, because there
            // "advanced color enabled" is also true for SDR displays using Auto Color Management.
            var info2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2();
            info2.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2;
            info2.header.size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>();
            info2.header.adapterId = adapterId;
            info2.header.id = targetId;
            if (DisplayConfigGetDeviceInfo(ref info2) == ERROR_SUCCESS)
            {
                return info2.activeColorMode == DISPLAYCONFIG_ADVANCED_COLOR_MODE_HDR;
            }

            // Older Windows: advanced color on and not merely forced wide gamut means HDR.
            var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
            info.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
            info.header.size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
            info.header.adapterId = adapterId;
            info.header.id = targetId;
            if (DisplayConfigGetDeviceInfo(ref info) == ERROR_SUCCESS)
            {
                bool advancedColorEnabled = (info.value & 0x2) != 0;
                bool wideColorEnforced = (info.value & 0x4) != 0;
                return advancedColorEnabled && !wideColorEnforced;
            }

            return null;
        }
    }
}
