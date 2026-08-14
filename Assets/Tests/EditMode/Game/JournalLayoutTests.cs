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
            Assert.That(JournalLayout.SideMargin(1080f, 16, false), Is.EqualTo(16), "portrait phone");
            Assert.That(JournalLayout.SideMargin(1920f, 16, true), Is.EqualTo(16), "landscape phone");
            Assert.That(JournalLayout.SideMargin(2222f, 16, true), Is.EqualTo(16), "21:9 fits the book");
        }

        [Test]
        public void SideMargin_Ultrawide_CentresTheBook()
        {
            // 32:9 measures ~2716 canvas units across — the surplus is split
            // into paper margins rather than poured into the columns.
            var margin = JournalLayout.SideMargin(2716f, 16, true);

            Assert.That(margin, Is.EqualTo(16 + Mathf.RoundToInt((2716f - JournalLayout.MaxWidth) * 0.5f)));
            Assert.That(2716f - 2f * margin, Is.LessThanOrEqualTo(JournalLayout.MaxWidth + 1f));
        }

        [Test]
        public void SideMargin_AtTheCap_TakesTheThreshold()
        {
            Assert.That(JournalLayout.SideMargin(JournalLayout.MaxWidth, 16, true), Is.EqualTo(16));
            Assert.That(JournalLayout.SideMargin(JournalLayout.PageMaxWidth, 16, false), Is.EqualTo(16));
        }

        /// <summary>One page's drawn width, given the canvas it is fitted into.</summary>
        private static float PageWidth(float canvas, bool wide)
        {
            var inner = canvas - 2f * JournalLayout.SideMargin(canvas, 16, wide);
            return wide ? (inner - JournalLayout.SpreadGap) / 2f : inner;
        }

        [Test]
        public void SideMargin_NoShape_RunsAPagePastItsMeasure()
        {
            // What the cap is for, stated as the property rather than as a
            // threshold: whatever the screen, a page is never drawn wider than
            // the one the type was cut against, so the line length a reader
            // gets is the same everywhere and the surplus goes to margins.
            Assert.That(PageWidth(1080f, false), Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth), "portrait phone");
            Assert.That(PageWidth(1248f, false), Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth), "portrait tablet");
            Assert.That(PageWidth(1967f, false), Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth),
                "portrait tablet, with the units RoomFactor hands it");
            Assert.That(PageWidth(1920f, true), Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth), "landscape phone");
            Assert.That(PageWidth(2498f, true), Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth), "landscape tablet");
            Assert.That(PageWidth(2716f, true), Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth), "32:9");
        }

        [Test]
        public void SideMargin_ABigScreen_StillFillsItsPages()
        {
            // The other edge of the same cap: margins take the surplus, not a
            // page's worth of it. A tablet must read as a book on a desk, not
            // as a phone marooned in paper.
            Assert.That(PageWidth(1967f, false), Is.GreaterThan(1000f), "portrait tablet");
            Assert.That(PageWidth(2498f, true), Is.GreaterThan(1000f), "landscape tablet");
        }

        // ── The room a screen is given ───────────────────────────────────────
        // Devices as (pixels, dpi). The dp a plate lands at is
        // units · scaleFactor / (dpi / 160), and scaleFactor is
        // sqrt(w · h) / (1440 · room) — so the whole thing collapses to
        // (units / 9) · inches / room, which is what TouchDp works out.

        private const float TouchPlate = 120f;
        private const float TouchFloorDp = 48f;

        private static float TouchDp(float width, float height, float dpi)
        {
            var room = JournalLayout.RoomFactor(width, height, dpi);
            return TouchPlate / 9f * (Mathf.Sqrt(width * height) / dpi) / room;
        }

        /// <summary>Phones, as (width, height, dpi) — from the smallest still sold to the largest that is not a tablet.</summary>
        private static readonly Vector3[] Phones =
        {
            new Vector3(720f, 1280f, 294f),   // 5.0" 16:9
            new Vector3(1080f, 1920f, 400f),  // 5.5" 16:9 — the reference
            new Vector3(1080f, 2340f, 409f),  // 6.1" 19.5:9
            new Vector3(1080f, 2400f, 395f),  // 6.7" 20:9
            new Vector3(1440f, 3200f, 525f),  // 6.8" QHD
        };

        /// <summary>Tablets and the opened foldable, same shape.</summary>
        private static readonly Vector3[] Tablets =
        {
            new Vector3(1280f, 800f, 189f),   // 8" — fewer pixels than a flagship phone, twice the screen
            new Vector3(2000f, 1200f, 274f),  // 8.4" foldable, opened
            new Vector3(2560f, 1600f, 299f),  // 10.1"
            new Vector3(2732f, 2048f, 264f),  // 13" 4:3
        };

        [Test]
        public void RoomFactor_EveryPhone_IsLeftExactlyAsItIs()
        {
            // The floor is the biggest PHONE, not the reference one, so no
            // handheld moves at all — the whole argument for extra room is that
            // the screen is held further away, and a 6.7" phone is a 5.5" phone
            // held the same way. This is the assertion the Editor's own dpi
            // broke: every game view read as a twenty-inch screen, so a phone
            // preset was laid out for a tablet.
            foreach (var phone in Phones)
            {
                Assert.That(JournalLayout.RoomFactor(phone.x, phone.y, phone.z), Is.EqualTo(1f),
                    "no extra room on " + phone.x + "x" + phone.y + " at " + phone.z + "dpi");
            }

            Assert.That(TouchDp(1080f, 1920f, 400f), Is.EqualTo(TouchFloorDp).Within(1f),
                "and a plate on the reference phone is Android's touch floor exactly");
        }

        [Test]
        public void RoomFactor_Tablets_AreGivenMoreOfTheBook()
        {
            Assert.That(JournalLayout.RoomFactor(1280f, 800f, 189f), Is.GreaterThan(1.1f),
                "a low-resolution 8-inch tablet is still a tablet");
            Assert.That(JournalLayout.RoomFactor(2560f, 1600f, 299f), Is.GreaterThan(1.25f),
                "a 10-inch tablet is given a quarter more units again");
            Assert.That(JournalLayout.RoomFactor(2732f, 2048f, 264f), Is.GreaterThan(1.4f),
                "and a 13-inch tablet nearly half");
        }

        [Test]
        public void RoomFactor_NoScreen_TakesAPlateUnderTheTouchFloor()
        {
            // The proof in RoomFactor's summary, asserted rather than argued:
            // room is the square root of how much bigger the screen is, so the
            // dp a plate lands at is the geometric mean of the phone floor's and
            // this screen's — and cannot come out under the smaller of them.
            var handheldDp = TouchPlate / 9f * JournalLayout.PhoneInches;
            foreach (var shape in Tablets)
            {
                Assert.That(TouchDp(shape.x, shape.y, shape.z), Is.GreaterThanOrEqualTo(handheldDp),
                    "a plate stays a fingertip on " + shape.x + "x" + shape.y + " at " + shape.z + "dpi");
            }

            Assert.That(handheldDp, Is.GreaterThan(TouchFloorDp),
                "and that bound is itself clear of Android's floor");
        }

        [Test]
        public void RoomFactor_UnknownOrAbsurdDpi_TakesThePhonesAnswer()
        {
            // Getting this wrong has to fall toward the SMALLEST device: a
            // tablet's page at a phone's size is unreadable, while a phone's
            // page on a tablet is merely generous. Zero is what Android reports
            // when it doesn't know; 96 is a desktop monitor, which is what the
            // Editor's plain game view hands back (see DeviceForm.ScreenDpi).
            Assert.That(JournalLayout.RoomFactor(2560f, 1600f, 0f), Is.EqualTo(1f),
                "an unknown dpi is not a device");
            Assert.That(JournalLayout.RoomFactor(2560f, 1600f, 96f), Is.EqualTo(1f),
                "nor is a monitor");
            Assert.That(JournalLayout.RoomFactor(2560f, 1600f, 4000f), Is.EqualTo(1f),
                "nor a panel claiming an impossible density");
            Assert.That(JournalLayout.RoomFactor(0f, 0f, 400f), Is.EqualTo(1f),
                "nor a screen measured before its first frame");
        }

        [Test]
        public void RoomFactor_AVastScreen_StopsAtTheCap()
        {
            // Believable density, unbelievable size — a ~17" panel. Past the cap
            // the reading distance grows with the screen, so the marks should
            // start growing again rather than the page going on subdividing.
            Assert.That(JournalLayout.RoomFactor(2560f, 1600f, 120f), Is.EqualTo(JournalLayout.MaxRoom));
        }

        [Test]
        public void ReferenceDpi_IsTheDensityTheReferenceResolutionIsCutAt()
        {
            // DeviceForm hands this back for the Editor's plain game view, so a
            // 1080x1920 view must come out as the reference phone exactly — the
            // preview is "this many pixels at a handheld's density".
            Assert.That(Mathf.Sqrt(1080f * 1920f) / JournalLayout.ReferenceDpi,
                Is.EqualTo(JournalLayout.ReferenceInches).Within(0.001f));
            Assert.That(JournalLayout.RoomFactor(1080f, 1920f, JournalLayout.ReferenceDpi), Is.EqualTo(1f));
        }

        [Test]
        public void ReferenceResolution_KeepsTheAuthoredShape()
        {
            // The reference only ever grows; its aspect is what every authored
            // unit in the journal is measured in, and must not move.
            var reference = JournalLayout.ReferenceResolution(2560f, 1600f, 299f);

            Assert.That(reference.y / reference.x, Is.EqualTo(1920f / 1080f).Within(0.001f),
                "the page keeps its proportions whatever room it is given");
            Assert.That(reference.x, Is.GreaterThanOrEqualTo(1080f), "never narrower than the phone's");
            Assert.That(reference.x, Is.LessThanOrEqualTo(1080f * JournalLayout.MaxRoom + 0.01f), "and never past the cap");
        }

        // ── The tabs, once they leave the bottom edge ────────────────────────
        // On a spread the tabs stand in a rail down the page's fore-edge rather
        // than in a bar along the bottom (GameHud.ApplyTabFold), because the
        // scarce axis in landscape is height. FOUR of them there, not five: the
        // Trail has no tab on a spread, being the facing page.
        private const int PageMargin = 16;
        private const int SpreadTabs = 4;

        [Test]
        public void RailSeatsTabs_TheShapesThatCanCarryARail_Do()
        {
            // The rail cannot scroll and is not allowed to clip: a tab a player
            // cannot reach is a page they cannot open.
            Assert.That(JournalLayout.RailSeatsTabs(LandscapeHeight, SpreadTabs), Is.True, "landscape phone");
            Assert.That(JournalLayout.RailSeatsTabs(1248f, SpreadTabs), Is.True, "landscape tablet");
            Assert.That(JournalLayout.RailSeatsTabs(939f, SpreadTabs), Is.True, "21:9");
        }

        [Test]
        public void RailSeatsTabs_AShapeTooShortForFingertips_KeepsTheBottomBar()
        {
            // 32:9 measures ~760 units top to bottom: a spread by aspect, with
            // nowhere to stand four fingertips beside the page. The bar always
            // fits, however short the screen — this is what sends the tabs back
            // to it rather than clipping the last one off the rail.
            Assert.That(JournalLayout.RailSeatsTabs(764f, SpreadTabs), Is.False, "32:9 is too short to rail");
            Assert.That(JournalLayout.RailSeatsTabs(0f, SpreadTabs), Is.False, "and an unmeasured canvas is not a spread");
        }

        [Test]
        public void RailTabWidth_BuysMoreHeightThanItCostsMeasure()
        {
            // The trade the landscape pass turns on, asserted rather than
            // argued: the two axes are not worth the same. The rail gives the
            // page back a whole tab's DEPTH of the scarce axis, and takes half
            // its width from each of two pages of the abundant one.
            var landscape = new Vector2(1920f, 1080f);
            var heightWon = JournalLayout.TabDepth / landscape.y;
            var widthLost = JournalLayout.RailTabWidth / landscape.x;

            Assert.That(heightWon, Is.GreaterThan(widthLost),
                "the rail must cost less of the width than the bar cost of the height");
        }

        [Test]
        public void RailTabWidth_LeavesBothPagesAReadableMeasure()
        {
            // What the rail takes comes off the columns, so the pages must
            // still carry a measure the type was cut for rather than two
            // gutters. Half the phone's measure is the floor here: under that
            // a card row is more wrap than words.
            var canvas = 1920f;
            var inner = canvas - 2f * JournalLayout.SideMargin(canvas, PageMargin, true)
                        - JournalLayout.RailTabWidth;
            var column = (inner - JournalLayout.SpreadGap) / 2f;

            Assert.That(column, Is.GreaterThan(0.5f * JournalLayout.PageMaxWidth),
                "a landscape phone's pages keep a measure worth reading");
            Assert.That(column, Is.LessThanOrEqualTo(JournalLayout.PageMaxWidth),
                "and never run past the one the type was cut for");
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
