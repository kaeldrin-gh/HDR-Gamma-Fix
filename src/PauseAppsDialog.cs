using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SystemTrayApp
{
    /// <summary>
    /// Lets the user manage the apps (typically HDR games) that pause the gamma fix while they're
    /// in the foreground. Apps are identified by exe file name, e.g. "Cyberpunk2077.exe".
    /// </summary>
    public class PauseAppsDialog : Form
    {
        private readonly ListBox _appList;
        private readonly Button _removeButton;
        private readonly ContextMenuStrip _runningAppsMenu;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public List<string> Apps { get; private set; }

        public PauseAppsDialog(IEnumerable<string> apps)
        {
            Apps = apps.ToList();

            Text = "Pause for Apps";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var intro = new Label
            {
                Text = "The gamma fix pauses while one of these apps is in the foreground, and resumes " +
                       "when you switch away. Add HDR games here: the fix is meant for SDR content " +
                       "and darkens shadows in HDR games.",
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Margin = new Padding(0, 0, 0, 8)
            };
            layout.Controls.Add(intro, 0, 0);
            layout.SetColumnSpan(intro, 2);

            _appList = new ListBox
            {
                Width = 300,
                Height = 170,
                Sorted = true,
                IntegralHeight = false,
                Margin = new Padding(0, 0, 8, 0)
            };
            _appList.Items.AddRange(Apps.Cast<object>().ToArray());
            _appList.SelectedIndexChanged += (s, e) => _removeButton!.Enabled = _appList.SelectedIndex >= 0;
            layout.Controls.Add(_appList, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };
            var addRunningButton = new Button { Text = "Add Running App ▾", AutoSize = true, MinimumSize = new Size(140, 0) };
            var browseButton = new Button { Text = "Browse for .exe...", AutoSize = true, MinimumSize = new Size(140, 0) };
            _removeButton = new Button { Text = "Remove", AutoSize = true, MinimumSize = new Size(140, 0), Enabled = false };
            buttons.Controls.AddRange(new Control[] { addRunningButton, browseButton, _removeButton });
            layout.Controls.Add(buttons, 1, 1);

            _runningAppsMenu = new ContextMenuStrip();
            addRunningButton.Click += (s, e) =>
            {
                BuildRunningAppsMenu();
                _runningAppsMenu.Show(addRunningButton, new Point(0, addRunningButton.Height));
            };
            browseButton.Click += OnBrowse;
            _removeButton.Click += (s, e) =>
            {
                if (_appList.SelectedItem != null)
                {
                    _appList.Items.Remove(_appList.SelectedItem);
                }
            };

            var dialogButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(82, 0) };
            var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(82, 0) };
            saveButton.Click += (s, e) => Apps = _appList.Items.Cast<string>().ToList();
            dialogButtons.Controls.AddRange(new Control[] { cancelButton, saveButton });
            layout.Controls.Add(dialogButtons, 0, 2);
            layout.SetColumnSpan(dialogButtons, 2);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        /// <summary>Lists apps that currently have a window, so a running game can be picked directly.</summary>
        private void BuildRunningAppsMenu()
        {
            _runningAppsMenu.Items.Clear();
            int ownProcessId = Environment.ProcessId;
            var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase); // exe -> window title

            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == ownProcessId || process.MainWindowHandle == IntPtr.Zero
                            || string.IsNullOrWhiteSpace(process.MainWindowTitle))
                        {
                            continue;
                        }

                        string? exe = ForegroundAppWatcher.GetExeNameForProcess((uint)process.Id);
                        if (exe != null && !entries.ContainsKey(exe))
                        {
                            entries[exe] = process.MainWindowTitle;
                        }
                    }
                    catch
                    {
                        // Process exited or is inaccessible; skip it
                    }
                }
            }

            foreach (var entry in entries)
            {
                string title = entry.Value.Length > 40 ? entry.Value.Substring(0, 37) + "..." : entry.Value;
                var item = new ToolStripMenuItem($"{entry.Key}  —  {title}") { Tag = entry.Key };
                item.Click += (s, e) => AddApp((string)((ToolStripMenuItem)s!).Tag!);
                _runningAppsMenu.Items.Add(item);
            }

            if (_runningAppsMenu.Items.Count == 0)
            {
                _runningAppsMenu.Items.Add(new ToolStripMenuItem("No apps with open windows found") { Enabled = false });
            }
        }

        private void OnBrowse(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choose the game's .exe",
                Filter = "Programs (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                AddApp(Path.GetFileName(dialog.FileName));
            }
        }

        private void AddApp(string exeName)
        {
            bool exists = _appList.Items.Cast<string>().Any(a => string.Equals(a, exeName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                _appList.Items.Add(exeName);
            }
            _appList.SelectedItem = _appList.Items.Cast<string>()
                .First(a => string.Equals(a, exeName, StringComparison.OrdinalIgnoreCase));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _runningAppsMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
