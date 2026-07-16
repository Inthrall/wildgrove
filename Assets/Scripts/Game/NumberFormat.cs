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

                // "0.##" rounds, so 999.996 would print as "1000K" — carry it
                // into the next group ("1M") instead.
                if (scaled >= 999.995 && group + 1 < Suffixes.Length)
                {
                    group++;
                    scaled /= 1000.0;
                }

                return scaled.ToString("0.##", CultureInfo.InvariantCulture) + Suffixes[group];
            }

            // Beyond the table, scientific keeps it honest rather than wrong.
            return value.Mantissa.ToString("0.##", CultureInfo.InvariantCulture)
                   + "e" + exponent.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A per-second rate for display: small rates keep their fraction
        /// ("0.5" — the warden's hand-gather trickle must not read as 0/s),
        /// a thousand and up abbreviate like <see cref="Short"/>.
        /// </summary>
        public static string Rate(BigDouble value)
        {
            if (value <= BigDouble.Zero)
            {
                return "0";
            }

            if (value < new BigDouble(1000.0))
            {
                return value.ToDouble().ToString("0.##", CultureInfo.InvariantCulture);
            }

            return Short(value);
        }

        /// <summary>
        /// A wall-clock duration for display, at most two units and no zero
        /// tail: "42s", "5m 12s", "5m", "14h 2m". Hours are the largest unit —
        /// a multi-day absence reads as "72h", which is honest about the
        /// offline cap maths without a calendar's worth of units.
        /// </summary>
        public static string Duration(double seconds)
        {
            var total = (long)System.Math.Floor(System.Math.Max(0.0, seconds));
            var hours = total / 3600;
            var minutes = total % 3600 / 60;
            var secs = total % 60;

            if (hours > 0)
            {
                return minutes > 0 ? hours + "h " + minutes + "m" : hours + "h";
            }

            if (minutes > 0)
            {
                return secs > 0 ? minutes + "m " + secs + "s" : minutes + "m";
            }

            return secs + "s";
        }
    }
}
