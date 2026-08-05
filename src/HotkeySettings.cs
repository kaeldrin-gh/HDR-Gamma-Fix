using System;
using System.Collections.Generic;

namespace SystemTrayApp
{
    /// <summary>
    /// Shared helpers for the configurable global hotkeys. Modifier/key combinations
    /// are stored and displayed as readable strings such as "Alt+F1".
    /// </summary>
    public static class HotkeySettings
    {
        // Windows modifier flags (used by RegisterHotKey)
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        public static readonly (string Name, uint Flags)[] ModifierOptions = new[]
        {
            ("Alt", MOD_ALT),
            ("Ctrl", MOD_CONTROL),
            ("Shift", MOD_SHIFT),
            ("Alt+Ctrl", MOD_ALT | MOD_CONTROL),
            ("Alt+Shift", MOD_ALT | MOD_SHIFT),
            ("Ctrl+Shift", MOD_CONTROL | MOD_SHIFT),
            ("Alt+Ctrl+Shift", MOD_ALT | MOD_CONTROL | MOD_SHIFT),
            ("Win", MOD_WIN),
            ("Alt+Win", MOD_ALT | MOD_WIN),
            ("Ctrl+Win", MOD_CONTROL | MOD_WIN),
            ("Shift+Win", MOD_SHIFT | MOD_WIN),
        };

        public static (string Name, uint Vk)[] KeyOptions { get; } = BuildKeyOptions();

        private static (string Name, uint Vk)[] BuildKeyOptions()
        {
            var list = new List<(string, uint)>();
            for (uint i = 0; i < 12; i++)
            {
                list.Add(($"F{i + 1}", 0x70u + i));
            }
            for (char c = '0'; c <= '9'; c++)
            {
                list.Add((c.ToString(), (uint)c));
            }
            for (char c = 'A'; c <= 'Z'; c++)
            {
                list.Add((c.ToString(), (uint)c));
            }
            return list.ToArray();
        }

        /// <summary>Formats a hotkey as a readable string, e.g. "Alt+F1".</summary>
        public static string Format(uint modifiers, uint vk)
        {
            var parts = new List<string>();
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(VkToName(vk));
            return string.Join("+", parts);
        }

        /// <summary>Parses a readable hotkey string such as "Alt+F1" back into modifier flags and a virtual key code.</summary>
        public static bool TryParse(string? text, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] parts = text.Split('+');
            if (parts.Length < 2)
            {
                return false; // Require at least one modifier and a key
            }

            uint? parsedVk = NameToVk(parts[^1].Trim());
            if (parsedVk == null)
            {
                return false;
            }

            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToLowerInvariant())
                {
                    case "alt": modifiers |= MOD_ALT; break;
                    case "ctrl":
                    case "control": modifiers |= MOD_CONTROL; break;
                    case "shift": modifiers |= MOD_SHIFT; break;
                    case "win": modifiers |= MOD_WIN; break;
                    default: return false;
                }
            }

            vk = parsedVk.Value;
            return true;
        }

        public static string VkToName(uint vk)
        {
            if (vk >= 0x70 && vk <= 0x7B)
            {
                return $"F{vk - 0x70 + 1}";
            }
            char ch = (char)vk;
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
            {
                return ch.ToString();
            }
            return $"0x{vk:X}";
        }

        public static uint? NameToVk(string name)
        {
            string n = name.Trim().ToUpperInvariant();
            if (n.Length == 0)
            {
                return null;
            }
            if (n.Length >= 2 && n[0] == 'F' && int.TryParse(n.Substring(1), out int f) && f >= 1 && f <= 12)
            {
                return (uint)(0x70 + f - 1);
            }
            if (n.Length == 1)
            {
                char c = n[0];
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    return (uint)c;
                }
            }
            return null;
        }
    }
}
