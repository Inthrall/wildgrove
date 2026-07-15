using System.Globalization;
using BreakInfinity;

namespace Wildgrove.Game
{
    /// <summary>
    /// Compact currency/quantity formatting for the HUD: small numbers read
    /// plainly, thousands and up abbreviate (K, M, B, T), and anything past the
    /// suffix table falls back to scientific. Presentation-only — the sim always
    /// works in exact <see cref="BigDouble"/>.
    /// </summary>
    public static class NumberFormat
    {
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "aa", "ab", "ac", "ad", "ae" };

        /// <summary>Format a currency/quantity for display, e.g. 0, 42, 1.2K, 3.45M.</summary>
        public static string Short(BigDouble value)
        {
            if (value <= BigDouble.Zero)
            {
                return "0";
            }

            // Below a thousand: whole units, no suffix (idle readouts don't need
            // fractional berries on screen).
            if (value < new BigDouble(1000.0))
            {
                return System.Math.Floor(value.ToDouble()).ToString("0", CultureInfo.InvariantCulture);
            }

            // Group the base-10 exponent into thousands to pick a suffix.
            var exponent = (int)value.Exponent;
            var group = exponent / 3;
            if (group < Suffixes.Length)
            {
                var scaled = value.Mantissa * System.Math.Pow(10.0, exponent - group * 3);
                return scaled.ToString("0.##", CultureInfo.InvariantCulture) + Suffixes[group];
            }

            // Beyond the table, scientific keeps it honest rather than wrong.
            return value.Mantissa.ToString("0.##", CultureInfo.InvariantCulture)
                   + "e" + exponent.ToString(CultureInfo.InvariantCulture);
        }
    }
}
