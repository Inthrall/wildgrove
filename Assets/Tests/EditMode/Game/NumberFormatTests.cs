using BreakInfinity;
using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the HUD's compact number formatting across the suffix boundaries so
    /// the readouts stay readable as the run scales into big numbers.
    /// </summary>
    public class NumberFormatTests
    {
        [Test]
        public void Short_Zero_ReturnsZero()
        {
            Assert.That(NumberFormat.Short(BigDouble.Zero), Is.EqualTo("0"));
        }

        [Test]
        public void Short_JustUnderASuffixBoundary_CarriesIntoTheNextGroup()
        {
            // "0.##" rounds 999.996 up — must read 1M, never 1000K.
            Assert.That(NumberFormat.Short(new BigDouble(999996.0)), Is.EqualTo("1M"));
            Assert.That(NumberFormat.Short(new BigDouble(999994.0)), Is.EqualTo("999.99K"));
        }

        [Test]
        public void Short_Negative_ReturnsZero()
        {
            Assert.That(NumberFormat.Short(new BigDouble(-5.0)), Is.EqualTo("0"));
        }

        [Test]
        public void Short_BelowThousand_ShowsWholeUnits()
        {
            Assert.That(NumberFormat.Short(new BigDouble(42.9)), Is.EqualTo("42"));
        }

        [Test]
        public void Short_Thousands_UsesKSuffix()
        {
            Assert.That(NumberFormat.Short(new BigDouble(1200.0)), Is.EqualTo("1.2K"));
        }

        [Test]
        public void Short_Millions_UsesMSuffix()
        {
            Assert.That(NumberFormat.Short(new BigDouble(3_450_000.0)), Is.EqualTo("3.45M"));
        }

        [Test]
        public void Short_ExactThousand_IsOneK()
        {
            Assert.That(NumberFormat.Short(new BigDouble(1000.0)), Is.EqualTo("1K"));
        }

        [Test]
        public void ShortFloor_JustShortOfTheAsk_NeverRoundsUpIntoIt()
        {
            // The verse card abbreviates have-against-asked. Short rounds
            // 19,995 to "20K", which beside a "20K" ask and a dead Set down
            // button reads as a bug rather than as five units short.
            Assert.That(NumberFormat.Short(new BigDouble(19995.0)), Is.EqualTo("20K"));
            Assert.That(NumberFormat.ShortFloor(new BigDouble(19995.0)), Is.EqualTo("19.99K"));
            Assert.That(NumberFormat.ShortFloor(new BigDouble(1_249_999.0)), Is.EqualTo("1.24M"));
        }

        [Test]
        public void ShortFloor_OnTheAsk_ReadsExactlyLikeShort()
        {
            // Nothing to truncate: a full store must not read short either.
            // 19,990 is the one that catches a missing epsilon — it lands a
            // hair below 1999 hundredths and floors to "19.98K" without one.
            Assert.That(NumberFormat.ShortFloor(new BigDouble(20000.0)), Is.EqualTo("20K"));
            Assert.That(NumberFormat.ShortFloor(new BigDouble(19990.0)), Is.EqualTo("19.99K"));
            Assert.That(NumberFormat.ShortFloor(new BigDouble(3_450_000.0)), Is.EqualTo("3.45M"));
            Assert.That(NumberFormat.ShortFloor(new BigDouble(1000.0)), Is.EqualTo("1K"));
        }

        [Test]
        public void ShortFloor_JustUnderASuffixBoundary_StaysInTheLowerGroup()
        {
            // Short carries 999,996 up to "1M" so it doesn't print "1000K".
            // ShortFloor must not: a store six short of a million asked for is
            // exactly the case this method exists to keep honest.
            Assert.That(NumberFormat.ShortFloor(new BigDouble(999996.0)), Is.EqualTo("999.99K"));
        }

        [Test]
        public void ShortFloor_BelowThousand_MatchesShort()
        {
            // Under a thousand there is no suffix to round into — whole units,
            // same as Short, and zero still reads "0" rather than throwing.
            Assert.That(NumberFormat.ShortFloor(new BigDouble(999.0)), Is.EqualTo("999"));
            Assert.That(NumberFormat.ShortFloor(BigDouble.Zero), Is.EqualTo("0"));
        }

        [Test]
        public void Rate_FractionalTrickle_KeepsTheFraction()
        {
            Assert.That(NumberFormat.Rate(new BigDouble(0.5)), Is.EqualTo("0.5"));
        }

        [Test]
        public void Rate_ZeroOrNegative_ReturnsZero()
        {
            Assert.That(NumberFormat.Rate(BigDouble.Zero), Is.EqualTo("0"));
            Assert.That(NumberFormat.Rate(new BigDouble(-1.0)), Is.EqualTo("0"));
        }

        [Test]
        public void Rate_SmallRate_ShowsAtMostTwoDecimals()
        {
            Assert.That(NumberFormat.Rate(new BigDouble(3.14159)), Is.EqualTo("3.14"));
        }

        [Test]
        public void Rate_WholeRate_DropsTheFraction()
        {
            Assert.That(NumberFormat.Rate(new BigDouble(12.0)), Is.EqualTo("12"));
        }

        [Test]
        public void Rate_ThousandAndUp_AbbreviatesLikeShort()
        {
            Assert.That(NumberFormat.Rate(new BigDouble(1200.0)), Is.EqualTo("1.2K"));
        }

        [Test]
        public void Duration_UnderAMinute_ShowsSeconds()
        {
            Assert.That(NumberFormat.Duration(42.7), Is.EqualTo("42s"));
        }

        [Test]
        public void Duration_MinutesAndSeconds_ShowsBoth()
        {
            Assert.That(NumberFormat.Duration(312.0), Is.EqualTo("5m 12s"));
        }

        [Test]
        public void Duration_WholeMinutes_DropsZeroSeconds()
        {
            Assert.That(NumberFormat.Duration(300.0), Is.EqualTo("5m"));
        }

        [Test]
        public void Duration_HoursAndMinutes_DropsSeconds()
        {
            Assert.That(NumberFormat.Duration(14 * 3600.0 + 2 * 60.0 + 33.0), Is.EqualTo("14h 2m"));
        }

        [Test]
        public void Duration_MultiDay_StaysInHours()
        {
            Assert.That(NumberFormat.Duration(72 * 3600.0), Is.EqualTo("72h"));
        }

        [Test]
        public void Duration_NegativeOrZero_IsZeroSeconds()
        {
            Assert.That(NumberFormat.Duration(0.0), Is.EqualTo("0s"));
            Assert.That(NumberFormat.Duration(-5.0), Is.EqualTo("0s"));
        }

        [Test]
        public void Countdown_MultiDay_ShowsDaysAndHours()
        {
            Assert.That(NumberFormat.Countdown(6 * 86400.0 + 4 * 3600.0 + 90.0), Is.EqualTo("6d 4h"));
        }

        [Test]
        public void Countdown_WholeDays_DropsZeroHours()
        {
            Assert.That(NumberFormat.Countdown(7 * 86400.0), Is.EqualTo("7d"));
        }

        [Test]
        public void Countdown_UnderADay_ReadsLikeDuration()
        {
            Assert.That(NumberFormat.Countdown(3 * 3600.0 + 12 * 60.0), Is.EqualTo("3h 12m"));
            Assert.That(NumberFormat.Countdown(42.0), Is.EqualTo("42s"));
            Assert.That(NumberFormat.Countdown(-5.0), Is.EqualTo("0s"));
        }

        [Test]
        public void CountdownCoarse_KeepsTheLargestUnitAlone()
        {
            // What the events rail's cells wear: one unit, because two do not
            // fit a cell a fingertip wide.
            Assert.That(NumberFormat.CountdownCoarse(11 * 86400.0 + 16 * 3600.0), Is.EqualTo("11d"));
            Assert.That(NumberFormat.CountdownCoarse(16 * 3600.0 + 40 * 60.0), Is.EqualTo("16h"));
            Assert.That(NumberFormat.CountdownCoarse(40 * 60.0 + 30.0), Is.EqualTo("40m"));
            Assert.That(NumberFormat.CountdownCoarse(30.0), Is.EqualTo("30s"));
        }

        [Test]
        public void CountdownCoarse_TruncatesRatherThanRounding()
        {
            // A cell reading "1d" with an hour to go would be a lie the player
            // acts on; reading "23h" with 23h59m to go is only ever cautious.
            Assert.That(NumberFormat.CountdownCoarse(86400.0 - 1.0), Is.EqualTo("23h"));
            Assert.That(NumberFormat.CountdownCoarse(3600.0 - 1.0), Is.EqualTo("59m"));
            Assert.That(NumberFormat.CountdownCoarse(-5.0), Is.EqualTo("0s"), "a lapsed clock never goes negative");
        }
    }
}
