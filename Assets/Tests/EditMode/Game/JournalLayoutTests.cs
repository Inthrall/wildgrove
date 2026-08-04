using NUnit.Framework;
using UnityEngine;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the journal's spread breakpoint (design §13 Phase 2 — and Level
    /// Up's no-letterboxing requirement across 4:3 / 16:10 / 21:9), the height
    /// the three stacked rows divide between them, and the width past which the
    /// book stops growing. The shapes below are the canvas sizes those devices
    /// actually produce under ScaleWithScreenSize at the 1080x1920 reference,
    /// which is why a 4:3 tablet held in portrait must stay a single column
    /// while a landscape phone must not.
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

        // ── The vertical budget ──────────────────────────────────────────────
        // Canvas heights are the shapes ScaleWithScreenSize produces at the
        // 1080x1920 reference; Chrome is what the pinned rows (header, ledger,
        // note, tracker, tabs, padding, spacing) actually measure — the same
        // absolute cost on every shape, which is the whole problem landscape has.
        private const float PortraitHeight = 1920f;
        private const float LandscapeHeight = 1080f;
        private const float Chrome = 430f;

        private static float PageHeight(float available, bool wide)
        {
            return available - Chrome - JournalLayout.StripHeight(available, Chrome, wide);
        }

        [Test]
        public void StripHeight_OnASpread_LeavesThePageMoreThanAColumnWould()
        {
            // The bug this pins: held at the portrait shares, a landscape canvas
            // gave a quarter of its short height to a near-empty band and left
            // the page under its own floor — clipped by the tabs bar.
            var spread = PageHeight(LandscapeHeight, true);
            var column = PageHeight(LandscapeHeight, false);

            Assert.That(spread, Is.GreaterThan(column),
                "a spread must not pay the portrait band's rent");
            Assert.That(spread / LandscapeHeight, Is.GreaterThanOrEqualTo(JournalLayout.WideMinPageShare),
                "the page keeps its floor on a spread");
        }

        [Test]
        public void StripHeight_OnASpread_IsShallowerThanOnATallColumn()
        {
            var spread = JournalLayout.StripHeight(LandscapeHeight, Chrome, true);
            var portrait = JournalLayout.StripHeight(PortraitHeight, Chrome, false);

            Assert.That(spread, Is.LessThan(portrait));
            Assert.That(spread, Is.LessThanOrEqualTo(LandscapeHeight * JournalLayout.WideStripShareMax));
        }

        [Test]
        public void StripHeight_TallCanvasWithThinChrome_StopsAtTheCeiling()
        {
            // Slack goes to the page, not to empty paper around the band.
            Assert.That(JournalLayout.StripHeight(PortraitHeight, 100f, false),
                Is.EqualTo(PortraitHeight * JournalLayout.StripShareMax).Within(0.01f));
        }

        [Test]
        public void StripHeight_ChromeHeavyCanvas_KeepsTheBandReadable()
        {
            // Even with the chrome eating the canvas the band cannot collapse to
            // nothing: the plates are the posting and catching surface.
            Assert.That(JournalLayout.StripHeight(LandscapeHeight, 900f, true),
                Is.EqualTo(LandscapeHeight * JournalLayout.WideStripShareMin).Within(0.01f));
        }

        [Test]
        public void StripHeight_UnmeasuredCanvas_IsZero()
        {
            Assert.That(JournalLayout.StripHeight(0f, Chrome, true), Is.EqualTo(0f));
            Assert.That(JournalLayout.StripHeight(-1080f, Chrome, false), Is.EqualTo(0f));
        }

        // ── The width cap ────────────────────────────────────────────────────

        [Test]
        public void SideMargin_PhoneAndLandscapePhone_KeepTheBaseMargin()
        {
            Assert.That(JournalLayout.SideMargin(1080f, 16), Is.EqualTo(16), "portrait phone");
            Assert.That(JournalLayout.SideMargin(1920f, 16), Is.EqualTo(16), "landscape phone");
            Assert.That(JournalLayout.SideMargin(2222f, 16), Is.EqualTo(16), "21:9 fits the book");
        }

        [Test]
        public void SideMargin_Ultrawide_CentresTheBook()
        {
            // 32:9 measures ~2716 canvas units across — the surplus is split
            // into paper margins rather than poured into the columns.
            var margin = JournalLayout.SideMargin(2716f, 16);

            Assert.That(margin, Is.EqualTo(16 + Mathf.RoundToInt((2716f - JournalLayout.MaxWidth) * 0.5f)));
            Assert.That(2716f - 2f * margin, Is.LessThanOrEqualTo(JournalLayout.MaxWidth + 1f));
        }

        [Test]
        public void SideMargin_AtTheCap_TakesTheThreshold()
        {
            Assert.That(JournalLayout.SideMargin(JournalLayout.MaxWidth, 16), Is.EqualTo(16));
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
