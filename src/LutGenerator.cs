using System;
using System.Globalization;
using System.Text;

namespace SystemTrayApp
{
    /// <summary>
    /// How the GPU applies the vcgt LUT in HDR mode, which decides how the LUT must be built.
    /// </summary>
    public enum LutMethod
    {
        Nvidia,
        Amd
    }

    /// <summary>
    /// Generates the sRGB-to-gamma LUT (.cal) in-process. A direct port of dylanraga's generator
    /// (https://dylanraga.github.io/gen-srgb-to-gamma-lut/, script.js), so that for the same inputs
    /// it produces the same file the website does.
    /// </summary>
    public static class LutGenerator
    {
        public const int Entries = 1024;

        /// <summary>The parameters the bundled scripts\lut.cal was generated with.</summary>
        public const double BundledWhiteLevel = 300;
        public const double BundledBlackLevel = 0;
        public const double BundledGamma = 2.2;
        public const LutMethod BundledMethod = LutMethod.Nvidia;

        /// <summary>
        /// Windows' "SDR content brightness" slider (0-100) maps linearly to the SDR white level.
        /// </summary>
        public static double SliderToNits(double slider) => 80 + slider * 4;
        public static double NitsToSlider(double nits) => (nits - 80) / 4;

        /// <summary>Builds the .cal file contents for the given SDR white level (nits).</summary>
        public static string GenerateCal(double whiteLevel, double blackLevel, double gamma, LutMethod method)
        {
            var contents = new StringBuilder();
            contents.Append("CAL\n\nORIGINATOR \"vcgt\"\nDEVICE_CLASS \"DISPLAY\"\nCOLOR_REP \"RGB\"\n\n")
                .Append("NUMBER_OF_FIELDS 4\nBEGIN_DATA_FORMAT\nRGB_I RGB_R RGB_G RGB_B\nEND_DATA_FORMAT\n\n")
                .Append("NUMBER_OF_SETS ").Append(Entries).Append("\nBEGIN_DATA");

            for (int i = 0; i < Entries; i++)
            {
                double input = i / (double)(Entries - 1);
                string output = ToFixed14(TransformToGamma(input, whiteLevel, blackLevel, gamma, method));
                contents.Append('\n').Append(ToFixed14(input))
                    .Append('\t').Append(output)
                    .Append('\t').Append(output)
                    .Append('\t').Append(output);
            }

            contents.Append("\nEND_DATA\n");
            return contents.ToString();
        }

        /// <summary>
        /// True if two .cal files hold the same LUT data. Compared numerically rather than as
        /// text: .NET's and JavaScript's pow() can differ in the last bit, which shows up in the
        /// 14th decimal - far below the GPU LUT's 16-bit precision - and line endings may differ.
        /// </summary>
        public static bool CalDataEquivalent(string a, string b)
        {
            double[]? rowsA = ParseCalData(a);
            double[]? rowsB = ParseCalData(b);
            if (rowsA == null || rowsB == null || rowsA.Length != rowsB.Length)
            {
                return false;
            }

            for (int i = 0; i < rowsA.Length; i++)
            {
                if (!(Math.Abs(rowsA[i] - rowsB[i]) <= 1e-9))
                {
                    return false;
                }
            }
            return true;
        }

        // All numbers between BEGIN_DATA and END_DATA, in order; null if the file isn't a .cal.
        private static double[]? ParseCalData(string cal)
        {
            int begin = cal.IndexOf("BEGIN_DATA\n", StringComparison.Ordinal);
            int beginCrLf = cal.IndexOf("BEGIN_DATA\r\n", StringComparison.Ordinal);
            int start = begin >= 0 ? begin : beginCrLf;
            int end = cal.IndexOf("END_DATA", Math.Max(start, 0) + "BEGIN_DATA".Length, StringComparison.Ordinal);
            if (start < 0 || end < 0)
            {
                return null;
            }

            string data = cal.Substring(start + "BEGIN_DATA".Length, end - start - "BEGIN_DATA".Length);
            var values = new List<double>();
            foreach (string token in data.Split(new[] { '\t', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    return null;
                }
                values.Add(value);
            }
            return values.ToArray();
        }

        // Matches JavaScript's Number.prototype.toFixed(14) for the values produced here.
        private static string ToFixed14(double value) =>
            double.IsNaN(value) ? "NaN" : value.ToString("F14", CultureInfo.InvariantCulture);

        private static double TransformToGamma(double input, double whiteLevel, double blackLevel, double gamma, LutMethod method)
        {
            if (input == 0)
            {
                return 0;
            }

            double output;
            switch (method)
            {
                case LutMethod.Nvidia:
                {
                    double luminanceOriginal = PqEotf(input);

                    // Above SDR white is HDR content: leave it untouched.
                    if (luminanceOriginal > whiteLevel)
                    {
                        return input;
                    }

                    double inputSrgb = SrgbInvEotf(luminanceOriginal / whiteLevel);
                    double gammaLuminance = (whiteLevel - blackLevel) * Math.Pow(inputSrgb, gamma) + blackLevel;

                    output = PqInvEotf(gammaLuminance);
                    if (blackLevel > 0)
                    {
                        output = Eetf(output, 0, 10000, blackLevel, 10000);
                    }
                    break;
                }

                case LutMethod.Amd:
                {
                    double gammaLuminance = (whiteLevel - blackLevel) * Math.Pow(input, gamma) + blackLevel;
                    output = SrgbInvEotf(gammaLuminance / whiteLevel);

                    if (blackLevel > 0)
                    {
                        output = Eetf(output, 0, 10000, blackLevel, 10000);
                    }
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(method));
            }

            return output;
        }

        // BT.2390 EETF, used here only to lift the black floor.
        private static double Eetf(double v, double lb, double lw, double lmin, double lmax)
        {
            double pqLb = PqInvEotf(lb);
            double pqLw = PqInvEotf(lw);

            double e1 = (v - pqLb) / (pqLw - pqLb);
            double minLum = (PqInvEotf(lmin) - pqLb) / (pqLw - pqLb);
            double maxLum = (PqInvEotf(lmax) - pqLb) / (pqLw - pqLb);

            double ks = 1.5 * maxLum - 0.5;
            double b = minLum;

            double T(double a) => (a - ks) / (1 - ks);
            double P(double x)
            {
                double t = T(x);
                return (2 * t * t * t - 3 * t * t + 1) * ks
                    + (t * t * t - 2 * t * t + t) * (1 - ks)
                    + (-2 * t * t * t + 3 * t * t) * maxLum;
            }

            // NaN mirrors the JavaScript original, where these stay undefined outside the ranges.
            double e2 = double.NaN, e3 = double.NaN;
            if (e1 < ks)
            {
                e2 = e1;
            }
            if (ks <= e1 && e1 <= 1)
            {
                e2 = P(e1);
            }
            if (0 <= e2 && e2 <= 1)
            {
                e3 = e2 + b * Math.Pow(1 - e2, 4);
            }

            return e3 * (pqLw - pqLb) + pqLb;
        }

        // SMPTE ST 2084 (PQ) constants
        private const double M1 = 0.1593017578125;
        private const double M2 = 78.84375;
        private const double C1 = 0.8359375;
        private const double C2 = 18.8515625;
        private const double C3 = 18.6875;

        private static double PqEotf(double v)
        {
            double vp = Math.Pow(v, 1 / M2);
            return 10000 * Math.Pow(Math.Max(vp - C1, 0) / (C2 - C3 * vp), 1 / M1);
        }

        private static double PqInvEotf(double l)
        {
            double lp = Math.Pow(l / 10000, M1);
            return Math.Pow((C1 + C2 * lp) / (1 + C3 * lp), M2);
        }

        private const double SrgbLinearThreshold = 0.00313066844250063;

        private static double SrgbInvEotf(double l) =>
            l <= SrgbLinearThreshold ? l * 12.92 : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;
    }
}
