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
        /// <see cref="Short"/> that never rounds UP — 19,995 reads "19.99K",
        /// not "20K". For a have-against-asked pair, where the two sides are
        /// abbreviated side by side: <see cref="Short"/> would print
        /// "20K / 20K" for a store five units short, and a full-looking bar
        /// beside a dead button reads as a bug rather than as "not yet".
        /// </summary>
        public static string ShortFloor(BigDouble value)
        {
            var exponent = (int)value.Exponent;
            var group = exponent / 3;

            // Under a thousand Short already truncates, and past the suffix
            // table it goes scientific — neither can round a shortfall up into
            // the ask, so both are Short's to answer.
            if (value < new BigDouble(1000.0) || group >= Suffixes.Length)
            {
                return Short(value);
            }

            var scaled = value.Mantissa * System.Math.Pow(10.0, exponent - group * 3);

            // Truncate at the two decimals the suffix shows. The nudge absorbs
            // the representation error in the line above — 19,990 lands a
            // hair under 1999 in hundredths and would otherwise read "19.98K".
            // It is a billionth of a displayed digit; a real shortfall is never
            // smaller than a whole one.
            var floored = System.Math.Floor((scaled * 100.0) + 1e-9) / 100.0;
            return floored.ToString("0.##", CultureInfo.InvariantCulture) + Suffixes[group];
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

        /// <summary>
        /// A time-until for a cooldown: same two-unit shape as
        /// <see cref="Duration"/> but with days as the largest unit — "6d 4h",
        /// "3h 12m", "42s". Duration stops at hours on purpose (offline-cap
        /// honesty), and a week-long cache reading "167h" is no use as a
        /// countdown.
        /// </summary>
        public static string Countdown(double seconds)
        {
            var total = (long)System.Math.Floor(System.Math.Max(0.0, seconds));
            var days = total / 86400;
            if (days > 0)
            {
                var hours = total % 86400 / 3600;
                return hours > 0 ? days + "d " + hours + "h" : days + "d";
            }

            return Duration(total);
        }
    }
}
