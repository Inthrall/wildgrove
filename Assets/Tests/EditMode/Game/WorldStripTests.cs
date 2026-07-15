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

        [Test]
        public void LayoutCentres_SpreadsEvenly_AlongTheMiddle()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 3);

            Assert.That(centres.Length, Is.EqualTo(3));
            Assert.That(centres[0].x, Is.EqualTo(100f + 880f * 0.25f).Within(Tolerance));
            Assert.That(centres[1].x, Is.EqualTo(100f + 880f * 0.50f).Within(Tolerance));
            Assert.That(centres[2].x, Is.EqualTo(100f + 880f * 0.75f).Within(Tolerance));
            Assert.That(centres[0].y, Is.EqualTo(1000f).Within(Tolerance));
        }

        [Test]
        public void LayoutCentres_SingleNode_SitsInTheCentre()
        {
            var centres = WorldStrip.LayoutCentres(Strip, 1);

            Assert.That(centres[0].x, Is.EqualTo(Strip.center.x).Within(Tolerance));
            Assert.That(centres[0].y, Is.EqualTo(Strip.center.y).Within(Tolerance));
        }

        [Test]
        public void LayoutCentres_NoNodes_IsEmpty()
        {
            Assert.That(WorldStrip.LayoutCentres(Strip, 0), Is.Empty);
        }

        [Test]
        public void Diameter_FitsBothHeightAndSpread()
        {
            // Height would allow 240 (400 * 0.6); the spread allows 880/4 * 0.7 = 154.
            Assert.That(WorldStrip.Diameter(Strip, 3), Is.EqualTo(154f).Within(Tolerance));

            // A short wide strip is height-bound instead.
            var shortStrip = new Rect(0f, 0f, 2000f, 100f);
            Assert.That(WorldStrip.Diameter(shortStrip, 3), Is.EqualTo(60f).Within(Tolerance));
        }

        [Test]
        public void Diameter_DegenerateStripOrNoNodes_IsZeroNotNegative()
        {
            Assert.That(WorldStrip.Diameter(new Rect(0f, 0f, 0f, 0f), 3), Is.EqualTo(0f));
            Assert.That(WorldStrip.Diameter(Strip, 0), Is.EqualTo(0f));
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
    }
}
