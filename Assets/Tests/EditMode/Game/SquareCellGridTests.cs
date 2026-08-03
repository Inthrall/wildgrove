using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the Stores drawer's grid fit: a cell that divides the width it is
    /// given exactly (gutters and padding taken out), and a column count that
    /// answers the width rather than a guess made on one device. The two page
    /// widths that matter are a phone column and a page of a spread, which are
    /// better than two to one apart — the whole reason the count is chosen at
    /// all rather than authored.
    /// </summary>
    public class SquareCellGridTests
    {
        private const float Tolerance = 1e-3f;

        // A 1080-wide canvas less the page gutters and a card's own padding.
        private const float PhoneCard = 1002f;

        // One page of a spread on a landscape phone (~1920 canvas units).
        private const float SpreadPage = 470f;

        [Test]
        public void CellFor_DividesTheWidthExactly()
        {
            var cell = SquareCellGrid.CellFor(1000f, 5, 0f, 10f);

            // Five cells and the four gutters between them fill the width and
            // leave nothing: 5 · 192 + 4 · 10 = 1000.
            Assert.That(cell, Is.EqualTo(192f).Within(Tolerance));
            Assert.That(cell * 5 + 10f * 4, Is.EqualTo(1000f).Within(Tolerance));
        }

        [Test]
        public void CellFor_TakesOutThePadding()
        {
            Assert.That(SquareCellGrid.CellFor(1000f, 4, 40f, 0f), Is.EqualTo(240f).Within(Tolerance));
        }

        [Test]
        public void CellFor_WidthNarrowerThanItsGutters_IsZeroNotNegative()
        {
            // A card measured before its first horizontal pass can report a
            // width smaller than its own gutters. A negative cell size makes
            // uGUI lay the grid out inside-out; zero just draws nothing until
            // the real width arrives.
            Assert.That(SquareCellGrid.CellFor(20f, 5, 0f, 30f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(SquareCellGrid.CellFor(1000f, 0, 0f, 10f), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ColumnsFor_APhoneColumn_TakesTheWidestFit()
        {
            // 1002 wide at an ideal of 190: five columns give ~194 and four
            // give ~244, so five is the nearer.
            var columns = SquareCellGrid.ColumnsFor(PhoneCard, 190f, 0f, 8f, 3, 5);

            Assert.That(columns, Is.EqualTo(5));
            Assert.That(SquareCellGrid.CellFor(PhoneCard, columns, 0f, 8f),
                Is.EqualTo(194f).Within(Tolerance));
        }

        [Test]
        public void ColumnsFor_APageOfASpread_FallsBackToFewerColumns()
        {
            // Half the width must not mean half-size plates. At 470 the
            // three-column floor gives ~151 — the nearest reachable to 190,
            // and a plate still worth drawing.
            var columns = SquareCellGrid.ColumnsFor(SpreadPage, 190f, 0f, 8f, 3, 5);

            Assert.That(columns, Is.EqualTo(3));
            Assert.That(SquareCellGrid.CellFor(SpreadPage, columns, 0f, 8f),
                Is.GreaterThan(140f), "a plate on a spread stays legible");
        }

        [Test]
        public void ColumnsFor_StaysWithinItsBounds()
        {
            // A very wide page must not run to twenty tiny columns, and a very
            // narrow one must not drop below the floor and go to one huge tile.
            Assert.That(SquareCellGrid.ColumnsFor(4000f, 190f, 0f, 8f, 3, 5), Is.EqualTo(5));
            Assert.That(SquareCellGrid.ColumnsFor(120f, 190f, 0f, 8f, 3, 5), Is.EqualTo(3));
        }

        [Test]
        public void ColumnsFor_ATie_TakesTheBiggerCells()
        {
            // 1200 wide, no gutters: 3 columns → 400, 4 columns → 300. Both
            // miss an ideal of 350 by exactly 50. The smaller count wins,
            // because an extra column costs a plate more than a wide margin
            // does.
            Assert.That(SquareCellGrid.ColumnsFor(1200f, 350f, 0f, 0f, 3, 4), Is.EqualTo(3));
        }

        [Test]
        public void ColumnsFor_NonsenseBounds_DoNotThrow()
        {
            Assert.That(SquareCellGrid.ColumnsFor(1000f, 190f, 0f, 8f, 0, 0), Is.EqualTo(1));
            Assert.That(SquareCellGrid.ColumnsFor(1000f, 190f, 0f, 8f, 6, 2), Is.EqualTo(6),
                "a max under the min yields the min rather than an empty range");
        }
    }
}
