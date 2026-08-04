using NUnit.Framework;
using UnityEngine;
using Wildgrove.Game.World;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the node-strip layout and hit-test maths: sprites spread evenly
    /// inside the HUD's free gap, size to fit it, and a tap resolves to the
    /// nearest node within its radius or to a miss.
    /// </summary>
    public class WorldStripTests
    {
        private const float Tolerance = 1e-4f;

        private static readonly Rect Strip = new Rect(100f, 800f, 880f, 400f);

        /// <summary>
        /// A single row rides above the band's middle by exactly the lift that
        /// puts its hanging captions back inside the band.
        /// </summary>
        private static float RowY(Rect strip, int count)
        {
            return strip.center.y + WorldStrip.CaptionLift(WorldStrip.Diameter(strip, count));
        }

        [Test]
        public void LayoutCentres_SpreadsEvenly_AlongTheMiddle()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 3);

            Assert.That(centres.Length, Is.EqualTo(3));
            Assert.That(centres[0].x, Is.EqualTo(100f + 880f * 0.25f).Within(Tolerance));
            Assert.That(centres[1].x, Is.EqualTo(100f + 880f * 0.50f).Within(Tolerance));
            Assert.That(centres[2].x, Is.EqualTo(100f + 880f * 0.75f).Within(Tolerance));
            Assert.That(centres[0].y, Is.EqualTo(RowY(Strip, 3)).Within(Tolerance));
        }

        [Test]
        public void LayoutCentres_SingleNode_SitsInTheCentre()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 1);

            Assert.That(centres[0].x, Is.EqualTo(Strip.center.x).Within(Tolerance));
            Assert.That(centres[0].y, Is.EqualTo(RowY(Strip, 1)).Within(Tolerance));
        }

        [Test]
        public void SingleRow_PlateAndItsCaption_StayInsideTheBand()
        {
            // The regression this pins: the caption used to hang below the band
            // and, once the bar under the strip moved onto the page, printed
            // itself across the facing page's running head.
            foreach (var band in new[] { PhoneBand, SpreadBand, Strip })
            {
                var count = 3;
                var diameter = WorldStrip.Diameter(band, count);
                var centre = WorldStrip.LayoutCentres(band, count)[0];

                Assert.That(WorldStrip.ShowsCaptions(band, count), Is.True, "captioned: " + band);
                Assert.That(centre.y + diameter * 0.5f, Is.LessThanOrEqualTo(band.yMax + Tolerance),
                    "plate top inside the band: " + band);
                Assert.That(centre.y - diameter * (WorldStrip.CaptionedPlate - 0.5f),
                    Is.GreaterThanOrEqualTo(band.yMin - Tolerance),
                    "caption bottom inside the band: " + band);
            }
        }

        [Test]
        public void SingleRow_ShallowBand_SizesThePlateForItsCaption()
        {
            // Height-bound, so the whole captioned plate — not the disc alone —
            // is what has to fit the band.
            var band = new Rect(0f, 0f, 2000f, 100f);

            Assert.That(WorldStrip.Diameter(band, 3),
                Is.EqualTo(100f / WorldStrip.CaptionedPlate).Within(Tolerance));
        }

        [Test]
        public void LayoutCentres_NoNodes_IsEmpty()
        {
            Assert.That(WorldStrip.LayoutCentres(Strip, 0), Is.Empty);
        }

        [Test]
        public void Diameter_FitsBothHeightAndSpread()
        {
            // Height would allow 400/1.82 ≈ 220 for a captioned row; the spread
            // allows 880/4 * 0.7 = 154, so the spread wins.
            Assert.That(WorldStrip.Diameter(Strip, 3), Is.EqualTo(154f).Within(Tolerance));

            // A short wide strip is height-bound instead.
            var shortStrip = new Rect(0f, 0f, 2000f, 100f);
            Assert.That(WorldStrip.Diameter(shortStrip, 3),
                Is.EqualTo(100f / WorldStrip.CaptionedPlate).Within(Tolerance));
        }

        [Test]
        public void Diameter_DegenerateStripOrNoNodes_IsZeroNotNegative()
        {
            Assert.That(WorldStrip.Diameter(new Rect(0f, 0f, 0f, 0f), 3), Is.EqualTo(0f));
            Assert.That(WorldStrip.Diameter(Strip, 0), Is.EqualTo(0f));
        }

        [Test]
        public void LayoutCentres_Crowded_WrapsToTwoRows()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 15);

            Assert.That(centres.Length, Is.EqualTo(15));
            // Top band takes the larger half, bottom the rest.
            var topY = Strip.yMin + Strip.height * 0.68f;
            var bottomY = Strip.yMin + Strip.height * 0.32f;
            for (var i = 0; i < 8; i++)
            {
                Assert.That(centres[i].y, Is.EqualTo(topY).Within(Tolerance), "top row " + i);
            }

            for (var i = 8; i < 15; i++)
            {
                Assert.That(centres[i].y, Is.EqualTo(bottomY).Within(Tolerance), "bottom row " + i);
            }

            // Every centre stays inside the strip.
            foreach (var centre in centres)
            {
                Assert.That(Strip.Contains(centre), Is.True, centre.ToString());
            }
        }

        [Test]
        public void LayoutCentres_JustOverTheRowCap_SplitsDownTheMiddle()
        {
            // Four is the first count that wraps: 2/2, not the top row filled
            // to its cap with one plate stranded underneath.
            var centres = WorldStrip.LayoutCentres(Strip, 4);
            var topY = Strip.yMin + Strip.height * 0.68f;
            var bottomY = Strip.yMin + Strip.height * 0.32f;

            Assert.That(centres[0].y, Is.EqualTo(topY).Within(Tolerance), "top row 0");
            Assert.That(centres[1].y, Is.EqualTo(topY).Within(Tolerance), "top row 1");
            Assert.That(centres[2].y, Is.EqualTo(bottomY).Within(Tolerance), "bottom row 2");
            Assert.That(centres[3].y, Is.EqualTo(bottomY).Within(Tolerance), "bottom row 3");
        }

        [Test]
        public void LayoutCentres_OddCount_RidesTheExtraOnTop()
        {
            // Seven cannot go 3/3, so the seventh plate joins the top row.
            var centres = WorldStrip.LayoutCentres(Strip, 7);
            var topY = Strip.yMin + Strip.height * 0.68f;
            var bottomY = Strip.yMin + Strip.height * 0.32f;

            for (var i = 0; i < 4; i++)
            {
                Assert.That(centres[i].y, Is.EqualTo(topY).Within(Tolerance), "top row " + i);
            }

            for (var i = 4; i < 7; i++)
            {
                Assert.That(centres[i].y, Is.EqualTo(bottomY).Within(Tolerance), "bottom row " + i);
            }
        }

        [Test]
        public void LayoutCentres_AtTheRowCap_StaysSingleRow()
        {
            var centres = WorldStrip.LayoutCentres(Strip, WorldStrip.MaxPerRow);

            foreach (var centre in centres)
            {
                Assert.That(centre.y, Is.EqualTo(RowY(Strip, WorldStrip.MaxPerRow)).Within(Tolerance));
            }
        }

        // The band the HUD actually leaves open, in screen pixels: a portrait
        // phone's is broad and shallow, a spread's is twice as broad and half as
        // deep (landscape is short of the one thing the band spends — height).
        private static readonly Rect PhoneBand = new Rect(0f, 0f, 1048f, 282f);
        private static readonly Rect SpreadBand = new Rect(0f, 0f, 1900f, 176f);

        [Test]
        public void PerRowCap_PhoneBand_IsTheAuthoredThree()
        {
            // The cap the constant was tuned against: the shape agrees with it,
            // so a column's band wraps exactly where it always did.
            Assert.That(WorldStrip.PerRowCap(PhoneBand), Is.EqualTo(WorldStrip.MaxPerRow));
            Assert.That(WorldStrip.Rows(PhoneBand, 4), Is.EqualTo(2), "four still wraps in a column");
        }

        [Test]
        public void PerRowCap_SpreadBand_SeatsTheWholeCamp()
        {
            // A spread's band is broad enough for a full kith in one row, and
            // wrapping them was what made the plates specks: two rows of that
            // shallow band leave 52px plates where one row keeps 105.
            Assert.That(WorldStrip.PerRowCap(SpreadBand), Is.GreaterThanOrEqualTo(6));
            Assert.That(WorldStrip.Rows(SpreadBand, 6), Is.EqualTo(1));
            Assert.That(WorldStrip.Diameter(SpreadBand, 6),
                Is.EqualTo(SpreadBand.height / WorldStrip.CaptionedPlate).Within(Tolerance),
                "the band's height sizes the plates, not the crowd");
        }

        [Test]
        public void PerRowCap_NeverFallsBelowTheAuthoredCap()
        {
            // A squarish or degenerate band must not wrap earlier than the
            // constant — the shape only ever buys a row more room.
            Assert.That(WorldStrip.PerRowCap(new Rect(0f, 0f, 400f, 400f)), Is.EqualTo(WorldStrip.MaxPerRow));
            Assert.That(WorldStrip.PerRowCap(Strip), Is.EqualTo(WorldStrip.MaxPerRow));
            Assert.That(WorldStrip.PerRowCap(new Rect(0f, 0f, 0f, 0f)), Is.EqualTo(WorldStrip.MaxPerRow));
        }

        [Test]
        public void LayoutCentres_SpreadBand_KeepsTheKithOnOneLine()
        {
            var centres = WorldStrip.LayoutCentres(SpreadBand, 6);

            foreach (var centre in centres)
            {
                Assert.That(centre.y, Is.EqualTo(RowY(SpreadBand, 6)).Within(Tolerance), centre.ToString());
                Assert.That(SpreadBand.Contains(centre), Is.True, centre.ToString());
            }
        }

        [Test]
        public void LayoutCentres_SpreadBandOverItsCap_StillWraps()
        {
            // Two rows are the last resort, not a thing the spread lost: a band
            // carrying more than its width can seat still splits.
            var count = WorldStrip.PerRowCap(SpreadBand) + 1;
            var centres = WorldStrip.LayoutCentres(SpreadBand, count);

            Assert.That(WorldStrip.Rows(SpreadBand, count), Is.EqualTo(2));
            Assert.That(centres[0].y, Is.EqualTo(SpreadBand.yMin + SpreadBand.height * 0.68f).Within(Tolerance));
            Assert.That(centres[count - 1].y, Is.EqualTo(SpreadBand.yMin + SpreadBand.height * 0.32f).Within(Tolerance));
        }

        [Test]
        public void Diameter_TwoRows_UsesPerRowHeightAndSpread()
        {
            // 15 sprites wrap to two rows of 8: height allows 400/2 * 0.6 = 120,
            // the spread allows 880/9 * 0.7 ≈ 68.4 — spread wins.
            Assert.That(WorldStrip.Diameter(Strip, 15), Is.EqualTo(880f / 9f * 0.7f).Within(Tolerance));
        }

        [Test]
        public void BubbleDiameter_TakesItsSizeFromTheBand_NotTheNodePlates()
        {
            // Band-bound: 400 * 0.42 = 168 beats the width share (880 * 0.2 = 176)
            // only after the min — so the band's height governs here.
            Assert.That(WorldStrip.BubbleDiameter(Strip, 3), Is.EqualTo(168f).Within(Tolerance));

            // The plates shrink as the strip crowds; the windfall must not.
            Assert.That(WorldStrip.BubbleDiameter(Strip, 15),
                Is.EqualTo(WorldStrip.BubbleDiameter(Strip, 3)).Within(Tolerance));
        }

        [Test]
        public void BubbleDiameter_NeverNarrowerThanANodePlate()
        {
            // A tall, near-empty band: one plate is bigger than the band share,
            // so the plate is the floor.
            var tall = new Rect(0f, 0f, 400f, 1000f);

            Assert.That(WorldStrip.BubbleDiameter(tall, 1),
                Is.EqualTo(WorldStrip.Diameter(tall, 1)).Within(Tolerance));
        }

        [Test]
        public void BubbleDiameter_DegenerateStrip_IsZeroNotNegative()
        {
            Assert.That(WorldStrip.BubbleDiameter(new Rect(0f, 0f, 0f, 0f), 3), Is.EqualTo(0f));
        }

        [Test]
        public void HitIndex_InsideRadius_ReturnsThatNode()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 3);

            var hit = WorldStrip.HitIndex(centres, 80f, centres[1] + new Vector2(50f, -30f));

            Assert.That(hit, Is.EqualTo(1));
        }

        [Test]
        public void HitIndex_BetweenNodes_PicksTheNearest()
        {
            var centres = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) };

            var hit = WorldStrip.HitIndex(centres, 60f, new Vector2(60f, 0f));

            Assert.That(hit, Is.EqualTo(1));
        }

        [Test]
        public void HitIndex_OutsideEveryRadius_IsAMiss()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 3);

            var hit = WorldStrip.HitIndex(centres, 80f, new Vector2(Strip.xMin, Strip.yMin));

            Assert.That(hit, Is.EqualTo(-1));
        }

        [Test]
        public void HitIndex_NoCentres_IsAMiss()
        {
            Assert.That(WorldStrip.HitIndex(new Vector2[0], 80f, Vector2.zero), Is.EqualTo(-1));
        }

        [Test]
        public void ResolveHit_TapOnAPlateInsideAnotherRowsBadgeCircle_ThePlateWins()
        {
            // Two rows: the top-row badge (centre 272 − 72 = 200, radius 33)
            // hangs into the bottom row's plate band (centre 128, radius 57.5,
            // reaching up to 185.5). A tap at y = 170 is on the bottom plate
            // AND inside the top badge's circle — the old badge-first order
            // sent it to the TOP row's posting sheet. Normalised depth:
            // badge (30/33)² ≈ 0.83 vs plate (42/57.5)² ≈ 0.53 — plate wins.
            var strip = new Rect(0f, 0f, 400f, 400f);
            var centres = new[] { new Vector2(200f, 272f), new Vector2(200f, 128f) };

            var hit = WorldStrip.ResolveHit(strip, centres, 57.5f, 100f, new[] { true, true }, new Vector2(200f, 170f));

            Assert.That(hit, Is.EqualTo(1));
        }

        [Test]
        public void ResolveHit_TapAtTheBadgeCentre_TheBadgeStillWins()
        {
            // Dead-on the badge icon: outside the plate circles, proportionally
            // deepest in the badge — it must keep resolving to its own post.
            var strip = new Rect(0f, 0f, 400f, 400f);
            var centres = new[] { new Vector2(200f, 272f), new Vector2(200f, 128f) };

            var hit = WorldStrip.ResolveHit(strip, centres, 57.5f, 100f, new[] { true, true }, new Vector2(200f, 200f));

            Assert.That(hit, Is.EqualTo(0));
        }

        [Test]
        public void ResolveHit_VacantBadge_HitsNothing()
        {
            // A vacant badge draws nothing, so its (invisible) circle must not
            // swallow taps — the plate is the assign gesture there.
            var strip = new Rect(0f, 0f, 400f, 400f);
            var centre = new Vector2(200f, 272f);
            var badgePoint = centre + new Vector2(0f, WorldStrip.BadgeOffsetFactor * 100f);

            var occupied = WorldStrip.ResolveHit(strip, new[] { centre }, 40f, 100f, new[] { true }, badgePoint);
            var vacant = WorldStrip.ResolveHit(strip, new[] { centre }, 40f, 100f, new[] { false }, badgePoint);

            Assert.That(occupied, Is.EqualTo(0));
            Assert.That(vacant, Is.EqualTo(-1));
        }

        [Test]
        public void ResolveHit_BadgeCircleBelowTheBand_HitsNothing()
        {
            // Badges hang low enough to spill under the strip into the chrome
            // below (the trail-home button) — outside the band they must miss.
            var strip = new Rect(0f, 100f, 400f, 200f);
            var centre = new Vector2(200f, 130f);
            var below = centre + new Vector2(0f, WorldStrip.BadgeOffsetFactor * 100f); // y = 58, under the band

            var hit = WorldStrip.ResolveHit(strip, new[] { centre }, 40f, 100f, new[] { true }, below);

            Assert.That(hit, Is.EqualTo(-1));
        }

        [Test]
        public void ResolveHit_PlateAlone_StillResolves()
        {
            var strip = new Rect(0f, 0f, 400f, 400f);
            var centres = WorldStrip.LayoutCentres(strip, 3);

            var hit = WorldStrip.ResolveHit(strip, centres, 60f, 80f, new[] { false, false, false }, centres[2] + new Vector2(20f, 10f));

            Assert.That(hit, Is.EqualTo(2));
        }
    }
}
