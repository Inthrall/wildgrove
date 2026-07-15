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
    }
}
