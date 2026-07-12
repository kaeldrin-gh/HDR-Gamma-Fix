using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32; // Added for Registry access clarity
using System.Drawing; // Added for Icon/SystemIcons clarity
using System.Text.RegularExpressions; // Added for monitor detection parsing

namespace SystemTrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon = null!;
        private ContextMenuStrip _contextMenu = null!;
        private bool _isDefaultProfile = true;  // Track current state
        private Icon _defaultIcon = null!;
        private Icon _gammaIcon = null!;

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
        private HotkeyMessageHandler? _messageHandler;

        // --- Notification Debouncing ---
        private System.Windows.Forms.Timer _notificationTimer = null!;
        private const int NotificationDelayMilliseconds = 500; // Delay before showing notification (adjust as needed)
        private (string Title, string Message, ToolTipIcon Icon)? _pendingNotificationDetails = null; // Tuple to hold pending details

        // --- Profile Recovery ---
        private System.Windows.Forms.Timer _profileRecoveryTimer = null!;
        private System.Windows.Forms.Timer _settingsWatchdogTimer = null!;
        private const int ProfileRecoveryDelayMilliseconds = 1500;
        private const int SettingsWatchdogIntervalMilliseconds = 1000;
        private static readonly TimeSpan ProfileRecoverySuppressionWindow = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SettingsWatchdogReapplyInterval = TimeSpan.FromSeconds(4);
        private DateTime _ignoreProfileRecoveryEventsUntilUtc = DateTime.MinValue;
        private DateTime _lastSettingsWatchdogRecoveryUtc = DateTime.MinValue;
        private string? _pendingProfileRecoveryReason;
        private bool _pendingProfileRecoveryNotification;
        private bool _wasSystemSettingsRunning;
        private volatile bool _sessionEnding; // Suppresses dispwin launches once Windows shutdown/logoff begins

        // --- Monitor Selection ---
        private List<MonitorInfo> _availableMonitors = new List<MonitorInfo>();
        private int _selectedMonitorIndex = -1; // -1 means all monitors, 0+ means specific monitor
        
        // --- Notification Settings ---
        private bool _notificationsEnabled = true; // Default to enabled, loaded from registry
        
        // Monitor information structure
        public struct MonitorInfo
        {
            public int DisplayNumber;
            public string DisplayName;
            public bool IsWorking;
            
            public MonitorInfo(int displayNumber, string displayName, bool isWorking)
            {
                DisplayNumber = displayNumber;
                DisplayName = displayName;
                IsWorking = isWorking;
            }
        }

        public TrayApplicationContext()
        {
            InitializeNotificationTimer(); // Initialize the notification timer first
            InitializeProfileRecoveryTimer();
            InitializeSettingsWatchdogTimer();
            DetectAvailableMonitors(); // Detect monitors
            LoadMonitorSelection(); // Load user's monitor preference
            LoadNotificationSetting(); // Load user's notification preference
            InitializeComponent(); // Initialize UI elements
            RegisterHotkeys();     // Register hotkeys
            RegisterSystemEventHandlers();

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
            
            // Add monitor selection submenu
            AddMonitorSelectionMenu();
            
            // Add notification toggle option
            var notificationItem = new ToolStripMenuItem("Show Notifications");
            notificationItem.Checked = _notificationsEnabled;
            notificationItem.Click += (s, e) => {
                notificationItem.Checked = !notificationItem.Checked;
                SaveNotificationSetting(notificationItem.Checked);
            };
            _contextMenu.Items.Add(notificationItem);
            
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

        private void InitializeProfileRecoveryTimer()
        {
            _profileRecoveryTimer = new System.Windows.Forms.Timer
            {
                Interval = ProfileRecoveryDelayMilliseconds
            };
            _profileRecoveryTimer.Tick += ProfileRecoveryTimer_Tick;
        }

        private void InitializeSettingsWatchdogTimer()
        {
            _settingsWatchdogTimer = new System.Windows.Forms.Timer
            {
                Interval = SettingsWatchdogIntervalMilliseconds,
                Enabled = true
            };
            _settingsWatchdogTimer.Tick += SettingsWatchdogTimer_Tick;
        }

        // --- Timer Tick Event Handler ---
        private void NotificationTimer_Tick(object? sender, EventArgs e)
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

        private void ProfileRecoveryTimer_Tick(object? sender, EventArgs e)
        {
            _profileRecoveryTimer.Stop();

            if (_isDefaultProfile)
            {
                _pendingProfileRecoveryReason = null;
                return;
            }

            string recoveryReason = _pendingProfileRecoveryReason
                ?? "Windows reapplied the active ICC profile.";
            bool showRecoveryNotification = _pendingProfileRecoveryNotification;
            _pendingProfileRecoveryReason = null;
            _pendingProfileRecoveryNotification = false;

            ApplySrgbToGamma(
                showNotification: showRecoveryNotification,
                isAutomaticRecovery: true,
                recoveryReason: recoveryReason);
        }

        private void SettingsWatchdogTimer_Tick(object? sender, EventArgs e)
        {
            bool isSystemSettingsRunning = IsSystemSettingsRunning();

            if (_isDefaultProfile)
            {
                _wasSystemSettingsRunning = isSystemSettingsRunning;
                return;
            }

            if (isSystemSettingsRunning && DateTime.UtcNow >= _ignoreProfileRecoveryEventsUntilUtc)
            {
                bool shouldScheduleRecovery = !_wasSystemSettingsRunning
                    || DateTime.UtcNow - _lastSettingsWatchdogRecoveryUtc >= SettingsWatchdogReapplyInterval;

                if (shouldScheduleRecovery)
                {
                    _lastSettingsWatchdogRecoveryUtc = DateTime.UtcNow;
                    ScheduleProfileRecovery(
                        !_wasSystemSettingsRunning
                            ? "Windows Settings opened and may have reset the gamma ramp."
                            : "Windows Settings is still open and may have refreshed the gamma ramp again.",
                        showNotification: false);
                }
            }

            _wasSystemSettingsRunning = isSystemSettingsRunning;
        }

        // --- Queue Notification Method ---
        /// <summary>
        /// Queues a notification to be shown after a short delay.
        /// If another notification is queued before the delay expires, only the latest one will be shown.
        /// </summary>
        private void QueueBalloonTip(string title, string message, ToolTipIcon icon)
        {
            // Safety check - if timer is not initialized yet, skip notification
            if (_notificationTimer == null)
                return;
                
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
            // Check if notifications are enabled
            if (!_notificationsEnabled) return;
            
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

        private void RegisterSystemEventHandlers()
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.SessionEnding += OnSessionEnding;
        }

        private void UnregisterSystemEventHandlers()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
        }

        private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            // Windows is shutting down or logging off. Launching dispwin.exe now would get it
            // killed mid-run and pop a "dispwin.exe stopped working" error, so stop everything.
            _sessionEnding = true;

            RunOnUiThread(() =>
            {
                try { _profileRecoveryTimer?.Stop(); } catch (ObjectDisposedException) { }
                try { _settingsWatchdogTimer?.Stop(); } catch (ObjectDisposedException) { }
                _pendingProfileRecoveryReason = null;
                _pendingProfileRecoveryNotification = false;
            });
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            ScheduleProfileRecovery("Windows display settings changed.");
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (!ShouldRecoverProfileForPreferenceCategory(e.Category))
            {
                return;
            }

            ScheduleProfileRecovery($"Windows refreshed {e.Category.ToString().ToLowerInvariant()} settings.");
        }

        private static bool ShouldRecoverProfileForPreferenceCategory(UserPreferenceCategory category)
        {
            return category == UserPreferenceCategory.Color
                || category == UserPreferenceCategory.Desktop
                || category == UserPreferenceCategory.General
                || category == UserPreferenceCategory.VisualStyle
                || category == UserPreferenceCategory.Window;
        }

        private void ScheduleProfileRecovery(string reason, bool showNotification = true)
        {
            if (_sessionEnding || _isDefaultProfile || DateTime.UtcNow < _ignoreProfileRecoveryEventsUntilUtc)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                if (_sessionEnding || _isDefaultProfile || DateTime.UtcNow < _ignoreProfileRecoveryEventsUntilUtc)
                {
                    return;
                }

                _pendingProfileRecoveryReason = reason;
                _pendingProfileRecoveryNotification = _pendingProfileRecoveryNotification || showNotification;
                _profileRecoveryTimer.Stop();
                _profileRecoveryTimer.Start();
            });
        }

        private static bool IsSystemSettingsRunning()
        {
            try
            {
                return Process.GetProcessesByName("SystemSettings").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void SuppressProfileRecoveryEvents()
        {
            _ignoreProfileRecoveryEventsUntilUtc = DateTime.UtcNow.Add(ProfileRecoverySuppressionWindow);
        }

        private void RunOnUiThread(Action action)
        {
            if (_messageHandler != null && !_messageHandler.IsDisposed && _messageHandler.IsHandleCreated && _messageHandler.InvokeRequired)
            {
                _messageHandler.BeginInvoke(action);
                return;
            }

            action();
        }

        // --- Monitor Detection and Management ---
        private void DetectAvailableMonitors()
        {
            _availableMonitors.Clear();
            
            // Try to detect monitors using dispwin.exe help output
            string dispwinPath = FindDispwinExecutable();
            if (string.IsNullOrEmpty(dispwinPath))
            {
                // Fallback: assume at least one monitor
                _availableMonitors.Add(new MonitorInfo(1, "Primary Monitor", true));
                return;
            }

            // Parse dispwin help output to get actual monitor list
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = dispwinPath,
                    Arguments = "-?", // Help command shows available displays
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using Process? process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string errorOutput = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);
                    
                    // Parse both stdout and stderr for display information
                    string fullOutput = output + errorOutput;
                    ParseMonitorsFromDispwinOutput(fullOutput);
                }
            }
            catch
            {
                // If parsing fails, fall back to single monitor
            }
            
            // If no monitors detected, add primary as fallback
            if (_availableMonitors.Count == 0)
            {
                _availableMonitors.Add(new MonitorInfo(1, "Primary Monitor", true));
            }
        }
        
        private void ParseMonitorsFromDispwinOutput(string output)
        {
            // Look for lines like: "    1 = 'DISPLAY1, at 0, 0, width 2560, height 1440 (Primary Display)'"
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string line in lines)
            {
                // Match lines that start with spaces, then a number, then " = '"
                if (Regex.IsMatch(line.Trim(), @"^\d+\s*=\s*'.*'"))
                {
                    try
                    {
                        // Extract monitor number and description
                        var match = Regex.Match(line.Trim(), @"^(\d+)\s*=\s*'([^']+)'");
                        if (match.Success)
                        {
                            int monitorNum = int.Parse(match.Groups[1].Value);
                            string description = match.Groups[2].Value;
                            
                            // Create a cleaner display name
                            string displayName = $"Monitor {monitorNum}";
                            if (description.Contains("Primary"))
                                displayName += " (Primary)";
                            
                            _availableMonitors.Add(new MonitorInfo(monitorNum, displayName, true));
                        }
                    }
                    catch
                    {
                        // Skip malformed lines
                    }
                }
            }
        }
        
        
        private string FindDispwinExecutable()
        {
            string[] possiblePaths = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "dispwin.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dispwin.exe"),
            };
            
            return possiblePaths.FirstOrDefault(File.Exists) ?? "";
        }
        
        private void LoadMonitorSelection()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\HDRGammaFix", false);
                
                if (key?.GetValue("SelectedMonitor") is int selectedMonitor)
                {
                    _selectedMonitorIndex = selectedMonitor;
                }
                else
                {
                    _selectedMonitorIndex = -1; // Default to all monitors
                }
            }
            catch
            {
                _selectedMonitorIndex = -1; // Default to all monitors on error
            }
        }
        
        private void LoadNotificationSetting()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\HDRGammaFix", false);
                
                if (key?.GetValue("NotificationsEnabled") is int notificationValue)
                {
                    _notificationsEnabled = notificationValue != 0;
                }
                else
                {
                    _notificationsEnabled = true; // Default to enabled
                }
            }
            catch
            {
                _notificationsEnabled = true; // Default to enabled on error
            }
        }
        
        private void SaveMonitorSelection(int monitorIndex)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\HDRGammaFix");
                key?.SetValue("SelectedMonitor", monitorIndex);
                _selectedMonitorIndex = monitorIndex;
            }
            catch
            {
                // Ignore save errors
            }
        }
        
        private void SaveNotificationSetting(bool enabled)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\HDRGammaFix");
                key?.SetValue("NotificationsEnabled", enabled ? 1 : 0);
                _notificationsEnabled = enabled;
            }
            catch
            {
                // Ignore save errors
            }
        }
        
        private void AddMonitorSelectionMenu()
        {
            if (_availableMonitors.Count == 0)
            {
                // No monitors detected, skip adding menu
                return;
            }
            
            var monitorMenuItem = new ToolStripMenuItem("Apply to Monitor");
            
            // Add "All Monitors" option
            var allMonitorsItem = new ToolStripMenuItem("All Monitors");
            allMonitorsItem.Checked = (_selectedMonitorIndex == -1);
            allMonitorsItem.Click += (s, e) => {
                SaveMonitorSelection(-1);
                RefreshMonitorMenu();
                UpdateIconAndText();
            };
            monitorMenuItem.DropDownItems.Add(allMonitorsItem);
            
            // Add separator
            monitorMenuItem.DropDownItems.Add(new ToolStripSeparator());
            
            // Add individual monitor options
            foreach (var monitor in _availableMonitors)
            {
                var monitorItem = new ToolStripMenuItem(monitor.DisplayName);
                monitorItem.Checked = (_selectedMonitorIndex == monitor.DisplayNumber);
                monitorItem.Tag = monitor.DisplayNumber;
                monitorItem.Click += (s, e) => {
                    if (s is ToolStripMenuItem item && item.Tag is int displayNum)
                    {
                        SaveMonitorSelection(displayNum);
                        RefreshMonitorMenu();
                        UpdateIconAndText();
                    }
                };
                monitorMenuItem.DropDownItems.Add(monitorItem);
            }
            
            _contextMenu.Items.Add(monitorMenuItem);
        }
        
        private void RefreshMonitorMenu()
        {
            // Find and update the monitor menu items
            foreach (ToolStripItem item in _contextMenu.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Text == "Apply to Monitor")
                {
                    // Update all submenu items
                    foreach (ToolStripItem subItem in menuItem.DropDownItems)
                    {
                        if (subItem is ToolStripMenuItem subMenuItem)
                        {
                            if (subMenuItem.Text == "All Monitors")
                            {
                                subMenuItem.Checked = (_selectedMonitorIndex == -1);
                            }
                            else if (subMenuItem.Tag is int displayNum)
                            {
                                subMenuItem.Checked = (_selectedMonitorIndex == displayNum);
                            }
                        }
                    }
                    break;
                }
            }
        }


        private void RegisterHotkeys()
        {
            _messageHandler = new HotkeyMessageHandler();
            _messageHandler.HotkeyPressed += OnHotkeyPressed;
            _messageHandler.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
            _messageHandler.SystemSettingChanged += OnSystemSettingChanged;

            if (!RegisterHotKey(_messageHandler.Handle, HOTKEY_ID_GAMMA, MOD_ALT, VK_F1))
            {
                // Use the queue method for errors too - with null check
                QueueBalloonTip("Hotkey Registration Failed",
                              "Could not register Alt+F1 hotkey. It may be in use by another application.",
                              ToolTipIcon.Warning);
            }

            if (!RegisterHotKey(_messageHandler.Handle, HOTKEY_ID_DEFAULT, MOD_ALT, VK_F2))
            {
                 // Use the queue method for errors too - with null check
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
                _messageHandler.DisplayConfigurationChanged -= OnDisplayConfigurationChanged;
                _messageHandler.SystemSettingChanged -= OnSystemSettingChanged;
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

        private void OnDisplayConfigurationChanged()
        {
            ScheduleProfileRecovery("Windows refreshed the display configuration.");
        }

        private void OnSystemSettingChanged(string? settingName)
        {
            if (DateTime.UtcNow < _ignoreProfileRecoveryEventsUntilUtc)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(settingName))
            {
                string normalizedName = settingName.Trim();
                bool looksDisplayRelated = normalizedName.Contains("color", StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Contains("display", StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Contains("immersive", StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Contains("userpreferences", StringComparison.OrdinalIgnoreCase);

                if (!looksDisplayRelated)
                {
                    return;
                }

                ScheduleProfileRecovery($"Windows updated the '{normalizedName}' display setting.");
                return;
            }

            ScheduleProfileRecovery("Windows refreshed system display settings.");
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

        private void UpdateIconAndText()
        {
            if (_notifyIcon == null) return;

            _notifyIcon.Icon = _isDefaultProfile ? _defaultIcon : _gammaIcon;
            
            // Build tooltip text with monitor information
            string profileText = _isDefaultProfile ? "Default" : "sRGB to Gamma";
            string monitorText = GetMonitorDisplayText();
            
            _notifyIcon.Text = $"HDR Gamma Fix: {profileText}{monitorText}";
        }
        
        private string GetMonitorDisplayText()
        {
            if (_selectedMonitorIndex == -1)
            {
                return " (All Monitors)";
            }
            
            var selectedMonitor = _availableMonitors.FirstOrDefault(m => m.DisplayNumber == _selectedMonitorIndex);
            if (selectedMonitor.DisplayNumber > 0)
            {
                return $" ({selectedMonitor.DisplayName})";
            }
            
            return "";
        }

        private void ApplySrgbToGamma(bool showNotification = true, bool isAutomaticRecovery = false, string? recoveryReason = null)
        {
            if (_sessionEnding)
            {
                return;
            }

            SuppressProfileRecoveryEvents();

            if (ExecuteBatchFile("srgb-to-gamma.bat"))
            {
                _settingsWatchdogTimer.Start();
                _isDefaultProfile = false;
                UpdateIconAndText();

                if (isAutomaticRecovery)
                {
                    if (showNotification)
                    {
                        QueueBalloonTip("Profile Reapplied",
                                      $"{recoveryReason ?? "Windows reset the gamma ramp."} HDR Gamma Fix restored the active profile{GetMonitorDisplayText()}",
                                      ToolTipIcon.Info);
                    }
                }
                else if (showNotification)
                {
                    // Queue the notification instead of showing immediately
                    string monitorInfo = GetMonitorDisplayText();
                    QueueBalloonTip("Profile Changed",
                                  $"Applied sRGB to Gamma profile{monitorInfo}",
                                  ToolTipIcon.Info);
                }
            }
        }

        private void RevertToDefault(bool showNotification = true)
        {
            SuppressProfileRecoveryEvents();
            _profileRecoveryTimer.Stop();
            _pendingProfileRecoveryReason = null;
            _pendingProfileRecoveryNotification = false;
            _wasSystemSettingsRunning = false;

            if (ExecuteBatchFile("revert.bat"))
            {
                _isDefaultProfile = true;
                UpdateIconAndText();

                if (showNotification)
                {
                    // Queue the notification instead of showing immediately
                    string monitorInfo = GetMonitorDisplayText();
                    QueueBalloonTip("Profile Changed",
                                  $"Reverted to Default profile{monitorInfo}",
                                  ToolTipIcon.Info);
                }
            }
        }

        private void OnApplySrgbToGamma(object? sender, EventArgs e) => ApplySrgbToGamma();
        private void OnRevertToDefault(object? sender, EventArgs e) => RevertToDefault();


        private bool ExecuteBatchFile(string fileName)
        {
            // If "All Monitors" is selected (-1), apply to each monitor individually
            if (_selectedMonitorIndex == -1)
            {
                bool success = true;
                foreach (var monitor in _availableMonitors)
                {
                    if (!ExecuteBatchFileForMonitor(fileName, monitor.DisplayNumber))
                    {
                        success = false;
                    }
                }
                return success;
            }
            // If a specific monitor is selected, use monitor-specific execution
            else if (_selectedMonitorIndex > 0)
            {
                return ExecuteBatchFileForMonitor(fileName, _selectedMonitorIndex);
            }
            
            // Fallback: execute normally (should not normally reach here)
            return ExecuteBatchFileOriginal(fileName);
        }
        
        private bool ExecuteBatchFileForMonitor(string fileName, int monitorIndex)
        {
            try
            {
                // Find dispwin.exe path
                string dispwinPath = FindDispwinExecutable();
                if (string.IsNullOrEmpty(dispwinPath))
                {
                    MessageBox.Show("Could not find dispwin.exe", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    QueueBalloonTip("Error", "Could not find dispwin.exe", ToolTipIcon.Error);
                    return false;
                }
                
                string workingDirectory = Path.GetDirectoryName(dispwinPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                
                // Determine the command based on the batch file type
                string arguments;
                if (fileName.Contains("srgb-to-gamma"))
                {
                    // Apply gamma profile to specific monitor
                    string lutPath = Path.Combine(workingDirectory, "lut.cal");
                    arguments = $"-d {monitorIndex} \"{lutPath}\"";
                }
                else if (fileName.Contains("revert"))
                {
                    // Clear profile from specific monitor  
                    arguments = $"-d {monitorIndex} -c";
                }
                else
                {
                    // Fallback to original batch file execution
                    return ExecuteBatchFileOriginal(fileName);
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = dispwinPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workingDirectory
                };

                using Process? process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(10000); // 10 second timeout
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing monitor-specific command: {ex.Message}", "Error", 
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                QueueBalloonTip("Error", $"Error executing command: {ex.Message}", ToolTipIcon.Error);
            }

            return false;
        }
        
        private bool ExecuteBatchFileOriginal(string fileName)
        {
            string? foundPath = null;
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
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workingDirectory
                };

                using (Process? process = Process.Start(psi))
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


        private void OnExit(object? sender, EventArgs e)
        {
            _profileRecoveryTimer?.Stop();
            _settingsWatchdogTimer?.Stop();
            UnregisterSystemEventHandlers();

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
                _profileRecoveryTimer?.Stop();
                _settingsWatchdogTimer?.Stop();
                _notificationTimer?.Dispose();
                _profileRecoveryTimer?.Dispose();
                _settingsWatchdogTimer?.Dispose();
                _defaultIcon?.Dispose();
                _gammaIcon?.Dispose();
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose();
                 UnregisterSystemEventHandlers();
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
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
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
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
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
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_SETTINGCHANGE = 0x001A;

        public event Action<int>? HotkeyPressed;
        public event Action? DisplayConfigurationChanged;
        public event Action<string?>? SystemSettingChanged;

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
            else if (m.Msg == WM_DISPLAYCHANGE)
            {
                DisplayConfigurationChanged?.Invoke();
            }
            else if (m.Msg == WM_SETTINGCHANGE)
            {
                SystemSettingChanged?.Invoke(Marshal.PtrToStringAuto(m.LParam));
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
