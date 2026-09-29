using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SystemTrayApp
{
    internal enum TrayIconState
    {
        Off,     // Fix turned off: outline in the taskbar's text color
        Applied, // LUT loaded: filled blue monitor
        Paused   // Fix on, but nothing loaded (HDR off / monitor disconnected): blue outline with pause bars
    }

    /// <summary>
    /// Draws the tray icons at the exact pixel size the tray uses, so they stay sharp at any display
    /// scaling, with the "off" icon matched to the taskbar theme so it's visible on dark and light
    /// taskbars alike. Geometry is snapped to whole pixels to avoid blurry edges at 16-24 px.
    /// </summary>
    internal static class TrayIconRenderer
    {
        private static readonly Color Blue = Color.FromArgb(59, 143, 240);
        private static readonly Color BlueDark = Color.FromArgb(49, 102, 214);
        private static readonly Color Amber = Color.FromArgb(245, 180, 0);
        private static readonly Color OnDarkTaskbar = Color.FromArgb(235, 235, 235);
        private static readonly Color OnLightTaskbar = Color.FromArgb(32, 32, 32);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>True if the taskbar uses the light theme (Windows' "default app mode" for system UI).</summary>
        public static bool IsTaskbarLight()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
            }
            catch
            {
                return false; // Windows 11's default is a dark taskbar
            }
        }

        public static Icon Create(TrayIconState state, int size, bool lightTaskbar)
        {
            using var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                // Pixel i covers [i, i+1), so whole-number edges fall exactly on pixel boundaries
                // (GDI+'s default centers pixels on whole numbers, smearing 1 px lines over two).
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                Draw(graphics, state, size, lightTaskbar ? OnLightTaskbar : OnDarkTaskbar);
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                using var temporary = Icon.FromHandle(handle);
                return (Icon)temporary.Clone(); // Owns its own copy, so the handle can be freed
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        private static void Draw(Graphics graphics, TrayIconState state, int s, Color foreground)
        {
            int stroke = Math.Max(1, (int)Math.Round(s / 16.0));

            // Screen: full-pixel bounds, from ~6% in at the sides to ~70% down
            int left = Math.Max(1, (int)Math.Round(s * 0.06));
            int top = (int)Math.Round(s * 0.12);
            int right = s - left;
            int bottom = (int)Math.Round(s * 0.70);
            float radius = Math.Max(1.5f, s * 0.1f);

            // Stand: a neck and a base, centered, with widths matching the icon's parity so they
            // land on whole pixels
            int neckWidth = EvenIfEven(Math.Max(2, (int)Math.Round(s * 0.12)), s);
            int neckHeight = Math.Max(1, (int)Math.Round(s * 0.12));
            int baseWidth = EvenIfEven((int)Math.Round(s * 0.5), s);
            int baseHeight = Math.Max(1, (int)Math.Round(s * 0.08));
            var neck = new Rectangle((s - neckWidth) / 2, bottom, neckWidth, neckHeight);
            var standBase = new Rectangle((s - baseWidth) / 2, bottom + neckHeight, baseWidth, baseHeight);

            Color standColor;
            switch (state)
            {
                case TrayIconState.Applied:
                {
                    using var screen = RoundedRect(left, top, right - left, bottom - top, radius);
                    using var blue = new SolidBrush(Blue);
                    using var blueDark = new SolidBrush(BlueDark);
                    graphics.FillPath(blue, screen);
                    graphics.SetClip(screen);
                    graphics.FillRectangle(blueDark, left, top + (int)Math.Round((bottom - top) * 0.58), right - left, bottom - top);
                    graphics.ResetClip();
                    standColor = Amber;
                    break;
                }

                case TrayIconState.Paused:
                {
                    DrawScreenOutline(graphics, left, top, right, bottom, radius, stroke, Blue);

                    // Two pause bars centered in the screen
                    int barWidth = Math.Max(2, (int)Math.Round(s * 0.1));
                    int gap = barWidth;
                    int innerHeight = bottom - top - 2 * stroke;
                    int barHeight = Math.Max(2, (int)Math.Round(innerHeight * 0.55));
                    int barsLeft = (s - (2 * barWidth + gap)) / 2;
                    int barsTop = top + stroke + (innerHeight - barHeight) / 2;
                    using var blue = new SolidBrush(Blue);
                    graphics.SmoothingMode = SmoothingMode.None; // Bars are pixel-aligned
                    graphics.FillRectangle(blue, barsLeft, barsTop, barWidth, barHeight);
                    graphics.FillRectangle(blue, barsLeft + barWidth + gap, barsTop, barWidth, barHeight);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    standColor = Blue;
                    break;
                }

                default:
                    DrawScreenOutline(graphics, left, top, right, bottom, radius, stroke, foreground);
                    standColor = foreground;
                    break;
            }

            using var standBrush = new SolidBrush(standColor);
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.FillRectangle(standBrush, neck);
            graphics.FillRectangle(standBrush, standBase);
        }

        private static void DrawScreenOutline(Graphics graphics, int left, int top, int right, int bottom,
            float radius, int stroke, Color color)
        {
            // A pen is centered on its path, so inset by half the stroke to keep the outer edge on
            // the pixel boundary.
            float inset = stroke / 2f;
            using var path = RoundedRect(left + inset, top + inset, right - left - stroke, bottom - top - stroke,
                Math.Max(1f, radius - inset));
            using var pen = new Pen(color, stroke);
            graphics.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
        {
            float diameter = Math.Min(2 * radius, Math.Min(width, height));
            var path = new GraphicsPath();
            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Centering a width on an s-pixel canvas lands on whole pixels only if both have the same parity.
        private static int EvenIfEven(int width, int size) => (width % 2 == size % 2) ? width : width + 1;
    }
}
