using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SystemTrayApp
{
    /// <summary>
    /// Reports which program's window is in the foreground, by exe file name (e.g.
    /// "Cyberpunk2077.exe"). Uses a WinEvent hook, so Windows notifies us on each switch instead of
    /// us polling. Changes are debounced so an Alt+Tab flurry or a game's launcher handing over
    /// to the game results in a single report. Must be created on the UI thread, whose message
    /// loop delivers the hook's callbacks.
    /// </summary>
    internal sealed class ForegroundAppWatcher : IDisposable
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int DebounceMilliseconds = 750;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder exeName, ref uint size);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private readonly WinEventDelegate _callback; // Kept referenced so the GC can't collect it while hooked
        private readonly IntPtr _hook;
        private readonly System.Windows.Forms.Timer _debounceTimer;
        private string? _lastReported;
        private bool _hasReported;

        /// <summary>Raised on the UI thread with the new foreground exe name (null if unknown).</summary>
        public event Action<string?>? ForegroundAppChanged;

        public ForegroundAppWatcher()
        {
            _debounceTimer = new System.Windows.Forms.Timer { Interval = DebounceMilliseconds };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                string? current = GetForegroundExeName();
                if (!_hasReported || !string.Equals(current, _lastReported, StringComparison.OrdinalIgnoreCase))
                {
                    _hasReported = true;
                    _lastReported = current;
                    ForegroundAppChanged?.Invoke(current);
                }
            };

            _callback = (hook, eventType, hwnd, idObject, idChild, thread, time) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            // Skip our own windows (tray menu, dialogs): opening the menu shouldn't count as
            // leaving the game.
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _callback,
                0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (_hook == IntPtr.Zero)
            {
                Debug.WriteLine("SetWinEventHook failed; foreground app detection is unavailable.");
            }
        }

        /// <summary>The last reported foreground exe name, re-read now if nothing was reported yet.</summary>
        public string? Current => _hasReported ? _lastReported : GetForegroundExeName();

        /// <summary>Re-evaluates now (e.g. after the app list changed), without waiting for a switch.</summary>
        public void ReportNow()
        {
            _debounceTimer.Stop();
            _hasReported = true;
            _lastReported = GetForegroundExeName();
            ForegroundAppChanged?.Invoke(_lastReported);
        }

        /// <summary>File name of the exe that owns a window's process, e.g. "game.exe"; null if unknown.</summary>
        public static string? GetExeNameForProcess(uint processId)
        {
            if (processId == 0)
            {
                return null;
            }

            // Limited query rights work even for most elevated processes (many games run elevated
            // because of their launcher or anti-cheat).
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process != IntPtr.Zero)
            {
                try
                {
                    var buffer = new StringBuilder(1024);
                    uint size = (uint)buffer.Capacity;
                    if (QueryFullProcessImageName(process, 0, buffer, ref size))
                    {
                        return Path.GetFileName(buffer.ToString());
                    }
                }
                finally
                {
                    CloseHandle(process);
                }
            }

            try
            {
                using var fallback = Process.GetProcessById((int)processId);
                return fallback.ProcessName + ".exe";
            }
            catch
            {
                return null;
            }
        }

        private static string? GetForegroundExeName()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return null;
            }
            GetWindowThreadProcessId(window, out uint processId);
            return GetExeNameForProcess(processId);
        }

        public void Dispose()
        {
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
            }
        }
    }
}
