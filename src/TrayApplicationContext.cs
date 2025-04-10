using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32; // Added for Registry access clarity
using System.Drawing; // Added for Icon/SystemIcons clarity

namespace SystemTrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private bool _isDefaultProfile = true;  // Track current state
        private Icon _defaultIcon;
        private Icon _gammaIcon;

        // P/Invoke declarations for global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Hotkey constants
        private const int HOTKEY_ID_GAMMA = 1;
        private const int HOTKEY_ID_DEFAULT = 2;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_F1 = 0x70;
        private const uint VK_F2 = 0x71;

        // Message handler form for hotkeys
        private HotkeyMessageHandler _messageHandler;

        public TrayApplicationContext()
        {
            InitializeComponent(); // Initialize UI elements first
            RegisterHotkeys();     // Register hotkeys

            // *** Add this line to apply the profile on startup ***
            ApplySrgbToGamma();
            // *****************************************************
        }

        private void InitializeComponent()
        {
            // Create the context menu with our options
            _contextMenu = new ContextMenuStrip();

            // Add our action buttons
            _contextMenu.Items.Add("Apply sRGB to Gamma (Alt+F1)", null, OnApplySrgbToGamma);
            _contextMenu.Items.Add("Revert to Default (Alt+F2)", null, OnRevertToDefault);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Add start with Windows option
            var startupItem = new ToolStripMenuItem("Run at Startup");
            startupItem.Checked = IsConfiguredToRunAtStartup();
            startupItem.Click += (s, e) => {
                startupItem.Checked = !startupItem.Checked;
                SetStartup(startupItem.Checked);
            };
            _contextMenu.Items.Add(startupItem);

            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Exit", null, OnExit);

            // Load icons
            LoadIcons();

            // Create the tray icon with the default icon
            // Note: The icon will be updated immediately by ApplySrgbToGamma if successful
            _notifyIcon = new NotifyIcon
            {
                Icon = _defaultIcon, // Start with default, will be updated
                ContextMenuStrip = _contextMenu,
                Text = "Color Profile: Default", // Start with default, will be updated
                Visible = true
            };

            // Add left-click handler for toggling profiles
            _notifyIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ToggleProfile();
                }
            };
        }

        private void RegisterHotkeys()
        {
            // Create a message handler form to receive hotkey messages
            _messageHandler = new HotkeyMessageHandler();
            _messageHandler.HotkeyPressed += OnHotkeyPressed;

            // Register Alt+F1 for Gamma profile
            if (!RegisterHotKey(_messageHandler.Handle, HOTKEY_ID_GAMMA, MOD_ALT, VK_F1))
            {
                ShowBalloonTip("Hotkey Registration Failed",
                              "Could not register Alt+F1 hotkey. It may be in use by another application.",
                              ToolTipIcon.Warning);
            }

            // Register Alt+F2 for Default profile
            if (!RegisterHotKey(_messageHandler.Handle, HOTKEY_ID_DEFAULT, MOD_ALT, VK_F2))
            {
                ShowBalloonTip("Hotkey Registration Failed",
                              "Could not register Alt+F2 hotkey. It may be in use by another application.",
                              ToolTipIcon.Warning);
            }
        }

        private void UnregisterHotkeys()
        {
            // Use null conditional operator ?. for safer access
            if (_messageHandler != null && !_messageHandler.IsDisposed)
            {
                UnregisterHotKey(_messageHandler.Handle, HOTKEY_ID_GAMMA);
                UnregisterHotKey(_messageHandler.Handle, HOTKEY_ID_DEFAULT);
                _messageHandler.HotkeyPressed -= OnHotkeyPressed;
                _messageHandler.Dispose();
                _messageHandler = null; // Set to null after disposal
            }
        }

        private void OnHotkeyPressed(int hotkeyId)
        {
            switch (hotkeyId)
            {
                case HOTKEY_ID_GAMMA:
                    ApplySrgbToGamma();
                    break;

                case HOTKEY_ID_DEFAULT:
                    RevertToDefault();
                    break;
            }
        }

        private void LoadIcons()
        {
            // Default icon (for default profile)
            string defaultIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "DefaultIcon.ico");
            string gammaIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "GammaIcon.ico");

            // Load default icon
            try
            {
                _defaultIcon = File.Exists(defaultIconPath) ? new Icon(defaultIconPath) : SystemIcons.Application;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading default icon: {ex.Message}");
                _defaultIcon = SystemIcons.Application; // Fallback on error
            }


            // Load gamma icon
            try
            {
                 _gammaIcon = File.Exists(gammaIconPath) ? new Icon(gammaIconPath) : SystemIcons.Information;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading gamma icon: {ex.Message}");
                _gammaIcon = SystemIcons.Information; // Fallback on error
            }
        }

        private void ShowBalloonTip(string title, string message, ToolTipIcon icon, bool briefNotification = false)
        {
            // Ensure notify icon exists and is visible before showing tip
            if (_notifyIcon == null || !_notifyIcon.Visible) return;

            // Cancel any existing notification by briefly hiding and showing
            // This is a common workaround for notification update issues
            _notifyIcon.Visible = false;
            _notifyIcon.Visible = true;

            // Set notification properties
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;

            // Show for very brief duration if briefNotification is true
            _notifyIcon.ShowBalloonTip(briefNotification ? 500 : 2000);
        }

        private void ToggleProfile()
        {
            if (_isDefaultProfile)
            {
                // Currently default, switch to gamma
                ApplySrgbToGamma();
            }
            else
            {
                // Currently gamma, switch to default
                RevertToDefault();
            }
        }

        private void UpdateIcon()
        {
            if (_notifyIcon == null) return; // Prevent NullReferenceException if called too early

            _notifyIcon.Icon = _isDefaultProfile ? _defaultIcon : _gammaIcon;
            _notifyIcon.Text = _isDefaultProfile ? "Color Profile: Default" : "Color Profile: sRGB to Gamma";
        }

        private void ApplySrgbToGamma()
        {
            if (ExecuteBatchFile("srgb-to-gamma.bat"))
            {
                _isDefaultProfile = false;
                UpdateIcon(); // Update icon and text

                // Show notification (only if not during initial startup maybe? Optional)
                // Consider if you want this notification every single time it starts
                ShowBalloonTip("Profile Changed",
                              "Applied sRGB to Gamma profile",
                              ToolTipIcon.Info, true);
            }
            // No else needed here, ExecuteBatchFile handles showing errors
        }

        private void RevertToDefault()
        {
            if (ExecuteBatchFile("revert.bat"))
            {
                _isDefaultProfile = true;
                UpdateIcon(); // Update icon and text

                // Show notification
                ShowBalloonTip("Profile Changed",
                              "Reverted to Default profile",
                              ToolTipIcon.Info, true);
            }
             // No else needed here, ExecuteBatchFile handles showing errors
        }

        // Event handlers for menu items just call the main methods
        private void OnApplySrgbToGamma(object sender, EventArgs e) => ApplySrgbToGamma();
        private void OnRevertToDefault(object sender, EventArgs e) => RevertToDefault();


        private bool ExecuteBatchFile(string fileName)
        {
            string foundPath = null;
            try
            {
                // Define potential locations relative to the application's base directory
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] possibleRelativePaths = new string[]
                {
                    fileName,             // Direct in application directory
                    Path.Combine("scripts", fileName) // In scripts subdirectory
                    // Add more relative paths here if needed
                };

                // Find the first existing file path
                foundPath = possibleRelativePaths
                                .Select(relativePath => Path.Combine(baseDir, relativePath))
                                .FirstOrDefault(File.Exists);

                if (string.IsNullOrEmpty(foundPath))
                {
                    string attemptedPaths = string.Join(Environment.NewLine + "  - ",
                        possibleRelativePaths.Select(p => Path.Combine(baseDir, p)));
                    string errorMsg = $"Could not find '{fileName}'. Tried the following locations:{Environment.NewLine}  - {attemptedPaths}";

                    MessageBox.Show(errorMsg, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ShowBalloonTip("Error", $"Could not find {fileName}", ToolTipIcon.Error);
                    return false;
                }

                // Get the directory where the batch file is located
                string workingDirectory = Path.GetDirectoryName(foundPath) ?? baseDir;

                // Launch a command prompt and execute the batch file
                // UseShellExecute = true allows running batch files directly without cmd.exe /c
                // CreateNoWindow = true hides the command prompt window
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = foundPath, // Execute the batch file directly
                    // Arguments = $"/c \"{foundPath}\"", // Not needed if FileName is the .bat and UseShellExecute=true
                    UseShellExecute = true, // Important for running .bat files easily
                    CreateNoWindow = true, // Hide the cmd window
                    WindowStyle = ProcessWindowStyle.Hidden, // Reinforce hiding
                    WorkingDirectory = workingDirectory  // Set the working directory
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        string errorMsg = $"Failed to start process for '{fileName}'.";
                        MessageBox.Show(errorMsg, "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ShowBalloonTip("Error", $"Failed to execute {fileName}", ToolTipIcon.Error);
                        return false;
                    }
                    // Optional: Wait for the process to exit if needed, though likely not for this use case
                    // process.WaitForExit();
                }

                return true; // Assume success if process started without immediate error
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error executing '{fileName}': {ex.Message}";
                // Include foundPath in error if available
                if (!string.IsNullOrEmpty(foundPath)) {
                    errorMsg += $"\nPath: {foundPath}";
                }
                MessageBox.Show(errorMsg, "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowBalloonTip("Error", $"Error executing {fileName}", ToolTipIcon.Error); // Keep balloon tip concise
                Debug.WriteLine($"Execution Error: {errorMsg}\n{ex.StackTrace}"); // Log more details for debugging
                return false;
            }
        }


        private void OnExit(object sender, EventArgs e)
        {
            // Hide tray icon before exiting to avoid it lingering
            if (_notifyIcon != null)
            {
                 _notifyIcon.Visible = false;
            }

            // Unregister hotkeys cleanly
            UnregisterHotkeys();

            // Request application exit
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
                _defaultIcon?.Dispose();
                _gammaIcon?.Dispose();
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose(); // Dispose context menu too
                UnregisterHotkeys(); // Ensure hotkeys are unregistered if not already done by OnExit
                 _messageHandler?.Dispose(); // Ensure message handler is disposed
            }

            base.Dispose(disposing);
        }

        // --- Startup Configuration ---
        private const string AppRegistryName = "HDRGammaFix"; // Use a constant for the name

        private void SetStartup(bool enable)
        {
            try
            {
                string appPath = Application.ExecutablePath;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)) // Use @ for verbatim string
                {
                    if (key == null)
                    {
                         ShowBalloonTip("Registry Error", "Could not open startup registry key.", ToolTipIcon.Error);
                         return; // Exit if key cannot be opened
                    }

                    if (enable)
                    {
                        key.SetValue(AppRegistryName, $"\"{appPath}\""); // Quote the path just in case
                        ShowBalloonTip("Startup Setting Changed",
                                      "Application will now run at Windows startup.",
                                      ToolTipIcon.Info);
                    }
                    else
                    {
                        // Check if the value exists before trying to delete
                        if (key.GetValue(AppRegistryName) != null)
                        {
                            key.DeleteValue(AppRegistryName, false); // false = do not throw if not found
                            ShowBalloonTip("Startup Setting Changed",
                                          "Application will no longer run at Windows startup.",
                                          ToolTipIcon.Info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 ShowBalloonTip("Registry Error", $"Failed to update startup setting: {ex.Message}", ToolTipIcon.Error);
                 Debug.WriteLine($"Registry Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private bool IsConfiguredToRunAtStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false)) // Open for read-only
                {
                    // Check if key exists and the value exists
                    return key?.GetValue(AppRegistryName) != null;
                }
            }
             catch (Exception ex)
            {
                 // Log the error, but return false as we can't confirm it's configured
                 Debug.WriteLine($"Error checking startup registry: {ex.Message}");
                 return false;
            }
        }
    }

    // This invisible form is used to receive global hotkey messages
    public class HotkeyMessageHandler : Form
    {
        private const int WM_HOTKEY = 0x0312;

        public event Action<int> HotkeyPressed; // Non-nullable delegate type

        public HotkeyMessageHandler()
        {
            // Create a minimal, invisible window
            this.CreateHandle(); // Ensure the handle is created
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Hide the window effectively
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                cp.Style = 0; // No border or caption
                cp.Width = 0;
                cp.Height = 0;
                cp.X = -2000; // Position off-screen
                cp.Y = -2000;
                // Ensure it's a message-only window if possible, though CreateHandle approach is common
                // cp.Parent = (IntPtr)(-3); // HWND_MESSAGE
                return cp;
            }
        }


        protected override void SetVisibleCore(bool value)
        {
             // Prevent the window from ever becoming visible
            base.SetVisibleCore(false);
        }


        protected override void WndProc(ref Message m)
        {
            // Listen for the hotkey message
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();
                // Safely invoke the event
                HotkeyPressed?.Invoke(hotkeyId);
            }

            base.WndProc(ref m);
        }

        // Override Dispose to ensure cleanup if needed, though base Dispose should handle handle release
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                 // Dispose managed resources if any were added
            }
            base.Dispose(disposing);
        }
    }
}
