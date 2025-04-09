using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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
            InitializeComponent();
            RegisterHotkeys();
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
                Icon = _defaultIcon,
                ContextMenuStrip = _contextMenu,
                Text = "Color Profile: Default",
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
            if (_messageHandler != null && !_messageHandler.IsDisposed)
            {
                UnregisterHotKey(_messageHandler.Handle, HOTKEY_ID_GAMMA);
                UnregisterHotKey(_messageHandler.Handle, HOTKEY_ID_DEFAULT);
                _messageHandler.HotkeyPressed -= OnHotkeyPressed;
                _messageHandler.Dispose();
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
            if (File.Exists(defaultIconPath))
            {
                _defaultIcon = new Icon(defaultIconPath);
            }
            else
            {
                // Fallback to system icon
                _defaultIcon = SystemIcons.Application;
            }
            
            // Load gamma icon
            if (File.Exists(gammaIconPath))
            {
                _gammaIcon = new Icon(gammaIconPath);
            }
            else
            {
                // Fallback to information icon if gamma icon is missing
                _gammaIcon = SystemIcons.Information;
            }
        }
        
        private void ShowBalloonTip(string title, string message, ToolTipIcon icon, bool briefNotification = false)
        {
            // Cancel any existing notification
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
            _notifyIcon.Icon = _isDefaultProfile ? _defaultIcon : _gammaIcon;
            _notifyIcon.Text = _isDefaultProfile ? "Color Profile: Default" : "Color Profile: sRGB to Gamma";
        }

        private void ApplySrgbToGamma()
        {
            if (ExecuteBatchFile("srgb-to-gamma.bat"))
            {
                _isDefaultProfile = false;
                UpdateIcon();
                
                // Show notification
                ShowBalloonTip("Profile Changed", 
                              "Applied sRGB to Gamma profile", 
                              ToolTipIcon.Info, true);
            }
        }
        
        private void RevertToDefault()
        {
            if (ExecuteBatchFile("revert.bat"))
            {
                _isDefaultProfile = true;
                UpdateIcon();
                
                // Show notification
                ShowBalloonTip("Profile Changed", 
                              "Reverted to Default profile", 
                              ToolTipIcon.Info, true);
            }
        }

        private void OnApplySrgbToGamma(object? sender, EventArgs e)
        {
            ApplySrgbToGamma();
        }

        private void OnRevertToDefault(object? sender, EventArgs e)
        {
            RevertToDefault();
        }

        private bool ExecuteBatchFile(string fileName)
        {
            try
            {
                // Try multiple possible locations for the batch file
                string[] possiblePaths = new string[]
                {
                    // Direct in application directory
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
                    // In scripts subdirectory
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", fileName)
                };

                string path = possiblePaths.FirstOrDefault(File.Exists);
                    
                if (string.IsNullOrEmpty(path))
                {
                    string attemptedPaths = string.Join(Environment.NewLine, possiblePaths);
                    MessageBox.Show($"Could not find {fileName}. Tried the following paths:\n{attemptedPaths}", 
                                   "File Not Found", 
                                   MessageBoxButtons.OK, 
                                   MessageBoxIcon.Error);
                    
                    // Show notification for error
                    ShowBalloonTip("Error", 
                                  $"Could not find {fileName}", 
                                  ToolTipIcon.Error);
                    return false;
                }

                // Get the directory where the batch file is located
                string workingDirectory = Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory;

                // Launch a command prompt and execute the batch file
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{path}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = workingDirectory  // Set the working directory
                };
                
                Process process = Process.Start(psi);
                if (process == null)
                {
                    MessageBox.Show($"Failed to start process for {fileName}", 
                                  "Error", 
                                  MessageBoxButtons.OK, 
                                  MessageBoxIcon.Error);
                    
                    // Show notification for error
                    ShowBalloonTip("Error", 
                                  $"Failed to execute {fileName}", 
                                  ToolTipIcon.Error);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing {fileName}: {ex.Message}", 
                               "Error", 
                               MessageBoxButtons.OK, 
                               MessageBoxIcon.Error);
                
                // Show notification for error
                ShowBalloonTip("Error", 
                              $"Error executing {fileName}: {ex.Message}", 
                              ToolTipIcon.Error);
                return false;
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            // Hide tray icon before exiting
            _notifyIcon.Visible = false;
            
            // Unregister hotkeys
            UnregisterHotkeys();
            
            Application.Exit();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _defaultIcon?.Dispose();
                _gammaIcon?.Dispose();
                _notifyIcon?.Dispose();
                UnregisterHotkeys();
            }
            
            base.Dispose(disposing);
        }

        private void SetStartup(bool enable)
        {
            string appName = "HDRGammaFix";  // Changed from SystemTrayApp
            string appPath = Application.ExecutablePath;
            
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            
            if (enable)
            {
                key?.SetValue(appName, appPath);
                
                // Show notification
                ShowBalloonTip("Startup Setting Changed", 
                              "Application will run at Windows startup", 
                              ToolTipIcon.Info);
            }
            else
            {
                key?.DeleteValue(appName, false);
                
                // Show notification
                ShowBalloonTip("Startup Setting Changed", 
                              "Application will not run at Windows startup", 
                              ToolTipIcon.Info);
            }
        }

        private bool IsConfiguredToRunAtStartup()
        {
            string appName = "HDRGammaFix";  // Changed from SystemTrayApp
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            
            return key?.GetValue(appName) != null;
        }
    }
    
    // This invisible form is used to receive hotkey messages
    public class HotkeyMessageHandler : Form
    {
        private const int WM_HOTKEY = 0x0312;
        
        public event Action<int>? HotkeyPressed;
        
        public HotkeyMessageHandler()
        {
            // Make this form invisible
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Minimized;
            this.Opacity = 0;
        }
        
        protected override void SetVisibleCore(bool value)
        {
            // Make sure the form never becomes visible
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
    }
}