using System;
using System.Drawing;
using System.Windows.Forms;

namespace SystemTrayApp
{
    /// <summary>
    /// Simple dialog to configure the two global hotkeys (apply gamma / revert to default).
    /// </summary>
    public class HotkeyConfigDialog : Form
    {
        private readonly ComboBox _gammaModifiers;
        private readonly ComboBox _gammaKeys;
        private readonly ComboBox _defaultModifiers;
        private readonly ComboBox _defaultKeys;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public uint GammaModifiers { get; private set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public uint GammaVk { get; private set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public uint DefaultModifiers { get; private set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public uint DefaultVk { get; private set; }

        public HotkeyConfigDialog(uint gammaModifiers, uint gammaVk, uint defaultModifiers, uint defaultVk)
        {
            Text = "Configure Hotkeys";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(360, 168);
            Font = SystemFonts.MessageBoxFont;

            _gammaModifiers = new ComboBox();
            _gammaKeys = new ComboBox();
            _defaultModifiers = new ComboBox();
            _defaultKeys = new ComboBox();

            foreach (var opt in HotkeySettings.ModifierOptions)
            {
                _gammaModifiers.Items.Add(opt.Name);
                _defaultModifiers.Items.Add(opt.Name);
            }
            foreach (var opt in HotkeySettings.KeyOptions)
            {
                _gammaKeys.Items.Add(opt.Name);
                _defaultKeys.Items.Add(opt.Name);
            }

            _gammaModifiers.DropDownStyle = ComboBoxStyle.DropDownList;
            _defaultModifiers.DropDownStyle = ComboBoxStyle.DropDownList;
            _gammaKeys.DropDownStyle = ComboBoxStyle.DropDownList;
            _defaultKeys.DropDownStyle = ComboBoxStyle.DropDownList;

            _gammaModifiers.SelectedIndex = IndexOfModifiers(gammaModifiers);
            _gammaKeys.SelectedIndex = IndexOfKey(gammaVk);
            _defaultModifiers.SelectedIndex = IndexOfModifiers(defaultModifiers);
            _defaultKeys.SelectedIndex = IndexOfKey(defaultVk);

            // Layout (two rows of label + modifier + key, then Save/Cancel)
            int labelX = 12, comboX = 150, keyX = 278;
            int comboWidth = 118, keyWidth = 70;
            int row1Y = 14, row2Y = 46;
            int comboHeight = _gammaModifiers.Height;

            AddLabel("Apply sRGB to Gamma:", labelX, row1Y);
            AddLabel("Revert to Default:", labelX, row2Y);

            PlaceCombo(_gammaModifiers, comboX, row1Y, comboWidth, comboHeight);
            PlaceCombo(_gammaKeys, keyX, row1Y, keyWidth, comboHeight);
            PlaceCombo(_defaultModifiers, comboX, row2Y, comboWidth, comboHeight);
            PlaceCombo(_defaultKeys, keyX, row2Y, keyWidth, comboHeight);

            var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            saveButton.Click += OnSave;
            saveButton.Left = 168;
            cancelButton.Left = 260;
            int buttonTop = 84;
            saveButton.Top = buttonTop;
            cancelButton.Top = buttonTop;
            saveButton.Width = 82;
            cancelButton.Width = 82;
            saveButton.Height = 27;
            cancelButton.Height = 27;
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(x, y + 3)
            });
        }

        private void PlaceCombo(ComboBox combo, int x, int y, int width, int height)
        {
            combo.Location = new Point(x, y);
            combo.Size = new Size(width, height);
            Controls.Add(combo);
        }

        private void OnSave(object? sender, EventArgs e)
        {
            GammaModifiers = HotkeySettings.ModifierOptions[_gammaModifiers.SelectedIndex].Flags;
            GammaVk = HotkeySettings.KeyOptions[_gammaKeys.SelectedIndex].Vk;
            DefaultModifiers = HotkeySettings.ModifierOptions[_defaultModifiers.SelectedIndex].Flags;
            DefaultVk = HotkeySettings.KeyOptions[_defaultKeys.SelectedIndex].Vk;
        }

        private static int IndexOfModifiers(uint flags)
        {
            for (int i = 0; i < HotkeySettings.ModifierOptions.Length; i++)
            {
                if (HotkeySettings.ModifierOptions[i].Flags == flags)
                {
                    return i;
                }
            }
            return 0;
        }

        private static int IndexOfKey(uint vk)
        {
            for (int i = 0; i < HotkeySettings.KeyOptions.Length; i++)
            {
                if (HotkeySettings.KeyOptions[i].Vk == vk)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
