using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the journal's spread breakpoint (design §13 Phase 2 — and Level
    /// Up's no-letterboxing requirement across 4:3 / 16:10 / 21:9). The shapes
    /// below are the canvas sizes those devices actually produce under
    /// ScaleWithScreenSize at the 1080x1920 reference, which is why a 4:3
    /// tablet held in portrait must stay a single column while a landscape
    /// phone must not.
    /// </summary>
    public class JournalLayoutTests
    {
        [Test]
        public void IsWide_PortraitPhone_IsSingleColumn()
        {
            Assert.That(JournalLayout.IsWide(1080f, 1920f), Is.False);
        }

        [Test]
        public void IsWide_PortraitTablet_IsSingleColumn()
        {
            // 4:3 held upright is physically broad but still reads as a column;
            // ~1248x1665 canvas units at the reference resolution.
            Assert.That(JournalLayout.IsWide(1248f, 1665f), Is.False,
                "a portrait tablet is a page, not a spread");
        }

        [Test]
        public void IsWide_LandscapePhone_IsSpread()
        {
            Assert.That(JournalLayout.IsWide(1920f, 1080f), Is.True);
        }

        [Test]
        public void IsWide_LandscapeTabletAndUltrawide_AreSpreads()
        {
            Assert.That(JournalLayout.IsWide(1665f, 1248f), Is.True, "4:3 landscape");
            Assert.That(JournalLayout.IsWide(1728f, 1080f), Is.True, "16:10 landscape");
            Assert.That(JournalLayout.IsWide(2222f, 939f), Is.True, "21:9 landscape");
        }

        [Test]
        public void IsWide_WideButSmall_StaysSingleColumn()
        {
            // Landscape by aspect, but too few units across to split without
            // cramping both halves — the width floor catches it.
            Assert.That(JournalLayout.IsWide(900f, 600f), Is.False);
        }

        [Test]
        public void IsWide_BroadButUpright_StaysSingleColumn()
        {
            // Over the width floor, under the aspect: a big screen in portrait.
            Assert.That(JournalLayout.IsWide(1400f, 1900f), Is.False);
        }

        [Test]
        public void IsWide_DegenerateSizes_AreNotWide()
        {
            // A canvas measured before its first layout pass reads zero; it
            // must not answer "spread" and trigger a rebuild into nothing.
            Assert.That(JournalLayout.IsWide(0f, 0f), Is.False);
            Assert.That(JournalLayout.IsWide(1920f, 0f), Is.False);
            Assert.That(JournalLayout.IsWide(-1920f, 1080f), Is.False);
        }

        [Test]
        public void IsWide_AtTheBoundary_TakesTheThreshold()
        {
            var height = JournalLayout.MinWideWidth / JournalLayout.WideAspect;

            Assert.That(JournalLayout.IsWide(JournalLayout.MinWideWidth, height), Is.True,
                "exactly at both thresholds counts as wide");
            Assert.That(JournalLayout.IsWide(JournalLayout.MinWideWidth - 1f, height), Is.False,
                "a unit under the width floor does not");
        }
    }
}
