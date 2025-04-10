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

        // --- Notification Debouncing ---
        private System.Windows.Forms.Timer _notificationTimer;
        private const int NotificationDelayMilliseconds = 500; // Delay before showing notification (adjust as needed)
        private (string Title, string Message, ToolTipIcon Icon)? _pendingNotificationDetails = null; // Tuple to hold pending details

        public TrayApplicationContext()
        {
            InitializeComponent(); // Initialize UI elements first
            RegisterHotkeys();     // Register hotkeys
            InitializeNotificationTimer(); // Initialize the notification timer

            // Apply the profile on startup (notification will be queued)
            ApplySrgbToGamma();
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

        // --- Initialize Notification Timer ---
        private void InitializeNotificationTimer()
        {
            _notificationTimer = new System.Windows.Forms.Timer
            {
                Interval = NotificationDelayMilliseconds
            };
            _notificationTimer.Tick += NotificationTimer_Tick;
        }

        // --- Timer Tick Event Handler ---
        private void NotificationTimer_Tick(object sender, EventArgs e)
        {
            _notificationTimer.Stop(); // Stop the timer

            // If there are pending details, show the notification now
            if (_pendingNotificationDetails.HasValue)
            {
                var details = _pendingNotificationDetails.Value;
                ShowBalloonTipInternal(details.Title, details.Message, details.Icon); // Call the internal method
                _pendingNotificationDetails = null; // Clear pending details
            }
        }

        // --- Queue Notification Method ---
        /// <summary>
        /// Queues a notification to be shown after a short delay.
        /// If another notification is queued before the delay expires, only the latest one will be shown.
        /// </summary>
        private void QueueBalloonTip(string title, string message, ToolTipIcon icon)
        {
            // Store the details of the notification to be shown
            _pendingNotificationDetails = (title, message, icon);

            // Stop the timer if it's already running (resets the delay)
            _notificationTimer.Stop();
            // Start the timer (again)
            _notificationTimer.Start();
        }

        // --- Internal Method to Show Balloon Tip Immediately ---
        /// <summary>
        /// Shows the balloon tip immediately. Use QueueBalloonTip for debounced notifications.
        /// </summary>
        private void ShowBalloonTipInternal(string title, string message, ToolTipIcon icon)
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

            // Show for a standard duration
            _notifyIcon.ShowBalloonTip(1500); // Adjusted duration
        }


        private void RegisterHotkeys()
        {
            _messageHandler = new HotkeyMessageHandler();
            _messageHandler.HotkeyPressed += OnHotkeyPressed;

            if (!RegisterHotKey(_messageHandler.Handle, HOTKEY_ID_GAMMA, MOD_ALT, VK_F1))
            {
                // Use the queue method for errors too
                QueueBalloonTip("Hotkey Registration Failed",
                              "Could not register Alt+F1 hotkey. It may be in use by another application.",
                              ToolTipIcon.Warning);
            }

            if (!RegisterHotKey(_messageHandler.Handle, HOTKEY_ID_DEFAULT, MOD_ALT, VK_F2))
            {
                 // Use the queue method for errors too
                QueueBalloonTip("Hotkey Registration Failed",
                              "Could not register Alt+F2 hotkey. It may be in use by another application.",
                              ToolTipIcon.Warning);
            }
        }

        private void UnregisterHotkeys()
        {
            if (_messageHandler != null && !_messageHandler.IsDisposed)
            {
                UnregisterHotKey(_messageHandler.Handle, HOTKEY_ID_GAMMA);
                UnregisterHotKey(_messageHandler.Handle, HOTKEY_ID_DEFAULT);
                _messageHandler.HotkeyPressed -= OnHotkeyPressed;
                _messageHandler.Dispose();
                _messageHandler = null;
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
            string defaultIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "DefaultIcon.ico");
            string gammaIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "GammaIcon.ico");

            try
            {
                _defaultIcon = File.Exists(defaultIconPath) ? new Icon(defaultIconPath) : SystemIcons.Application;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading default icon: {ex.Message}");
                _defaultIcon = SystemIcons.Application;
            }

            try
            {
                 _gammaIcon = File.Exists(gammaIconPath) ? new Icon(gammaIconPath) : SystemIcons.Information;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading gamma icon: {ex.Message}");
                _gammaIcon = SystemIcons.Information;
            }
        }

        private void ToggleProfile()
        {
            if (_isDefaultProfile)
            {
                ApplySrgbToGamma();
            }
            else
            {
                RevertToDefault();
            }
        }

        private void UpdateIcon()
        {
            if (_notifyIcon == null) return;

            _notifyIcon.Icon = _isDefaultProfile ? _defaultIcon : _gammaIcon;
            _notifyIcon.Text = _isDefaultProfile ? "Color Profile: Default" : "Color Profile: sRGB to Gamma";
        }

        private void ApplySrgbToGamma()
        {
            if (ExecuteBatchFile("srgb-to-gamma.bat"))
            {
                _isDefaultProfile = false;
                UpdateIcon();

                // Queue the notification instead of showing immediately
                QueueBalloonTip("Profile Changed",
                              "Applied sRGB to Gamma profile",
                              ToolTipIcon.Info);
            }
        }

        private void RevertToDefault()
        {
            if (ExecuteBatchFile("revert.bat"))
            {
                _isDefaultProfile = true;
                UpdateIcon();

                // Queue the notification instead of showing immediately
                QueueBalloonTip("Profile Changed",
                              "Reverted to Default profile",
                              ToolTipIcon.Info);
            }
        }

        private void OnApplySrgbToGamma(object sender, EventArgs e) => ApplySrgbToGamma();
        private void OnRevertToDefault(object sender, EventArgs e) => RevertToDefault();


        private bool ExecuteBatchFile(string fileName)
        {
            string foundPath = null;
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] possibleRelativePaths = new string[]
                {
                    fileName,
                    Path.Combine("scripts", fileName)
                };

                foundPath = possibleRelativePaths
                                .Select(relativePath => Path.Combine(baseDir, relativePath))
                                .FirstOrDefault(File.Exists);

                if (string.IsNullOrEmpty(foundPath))
                {
                    string attemptedPaths = string.Join(Environment.NewLine + "  - ",
                        possibleRelativePaths.Select(p => Path.Combine(baseDir, p)));
                    string errorMsg = $"Could not find '{fileName}'. Tried the following locations:{Environment.NewLine}  - {attemptedPaths}";

                    MessageBox.Show(errorMsg, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Use QueueBalloonTip for consistency, even for errors shown via MessageBox
                    QueueBalloonTip("Error", $"Could not find {fileName}", ToolTipIcon.Error);
                    return false;
                }

                string workingDirectory = Path.GetDirectoryName(foundPath) ?? baseDir;

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = foundPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workingDirectory
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        string errorMsg = $"Failed to start process for '{fileName}'.";
                        MessageBox.Show(errorMsg, "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        QueueBalloonTip("Error", $"Failed to execute {fileName}", ToolTipIcon.Error);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error executing '{fileName}': {ex.Message}";
                if (!string.IsNullOrEmpty(foundPath)) {
                    errorMsg += $"\nPath: {foundPath}";
                }
                MessageBox.Show(errorMsg, "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                QueueBalloonTip("Error", $"Error executing {fileName}", ToolTipIcon.Error);
                Debug.WriteLine($"Execution Error: {errorMsg}\n{ex.StackTrace}");
                return false;
            }
        }


        private void OnExit(object sender, EventArgs e)
        {
            if (_notifyIcon != null)
            {
                 _notifyIcon.Visible = false;
            }
            UnregisterHotkeys();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
                _notificationTimer?.Stop(); // Stop the timer before disposing
                _notificationTimer?.Dispose();
                _defaultIcon?.Dispose();
                _gammaIcon?.Dispose();
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose();
                UnregisterHotkeys(); // Ensure hotkeys are unregistered
                 _messageHandler?.Dispose();
            }
            base.Dispose(disposing);
        }

        // --- Startup Configuration ---
        private const string AppRegistryName = "HDRGammaFix";

        private void SetStartup(bool enable)
        {
            try
            {
                string appPath = Application.ExecutablePath;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null)
                    {
                         QueueBalloonTip("Registry Error", "Could not open startup registry key.", ToolTipIcon.Error);
                         return;
                    }

                    if (enable)
                    {
                        key.SetValue(AppRegistryName, $"\"{appPath}\"");
                        QueueBalloonTip("Startup Setting Changed",
                                      "Application will now run at Windows startup.",
                                      ToolTipIcon.Info);
                    }
                    else
                    {
                        if (key.GetValue(AppRegistryName) != null)
                        {
                            key.DeleteValue(AppRegistryName, false);
                            QueueBalloonTip("Startup Setting Changed",
                                          "Application will no longer run at Windows startup.",
                                          ToolTipIcon.Info);
                        }
                        // Optional: Show a notification even if it wasn't set? Probably not needed.
                    }
                }
            }
            catch (Exception ex)
            {
                 QueueBalloonTip("Registry Error", $"Failed to update startup setting: {ex.Message}", ToolTipIcon.Error);
                 Debug.WriteLine($"Registry Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private bool IsConfiguredToRunAtStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue(AppRegistryName) != null;
                }
            }
             catch (Exception ex)
            {
                 Debug.WriteLine($"Error checking startup registry: {ex.Message}");
                 return false;
            }
        }
    }

    // HotkeyMessageHandler class remains the same as before
    public class HotkeyMessageHandler : Form
    {
        private const int WM_HOTKEY = 0x0312;

        public event Action<int> HotkeyPressed;

        public HotkeyMessageHandler()
        {
            this.CreateHandle();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                cp.Style = 0;
                cp.Width = 0;
                cp.Height = 0;
                cp.X = -2000;
                cp.Y = -2000;
                return cp;
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();
                HotkeyPressed?.Invoke(hotkeyId);
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            // No specific managed resources added here in this example
            base.Dispose(disposing);
        }
    }
}
