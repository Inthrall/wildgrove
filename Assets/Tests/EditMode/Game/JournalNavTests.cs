using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the keyboard/controller navigation rules (design §13 Phase 2): the
    /// shoulders step tabs and skip the Trail's missing tab on a spread, focus
    /// returns to its place in a rebuilt page, and revealing a marked control
    /// scrolls the shortest distance that works — never past a control already
    /// in view.
    /// </summary>
    public class JournalNavTests
    {
        [Test]
        public void StepTab_WalksForwardAndWraps()
        {
            Assert.That(JournalNav.StepTab("trail", 1, false), Is.EqualTo("camp"));
            Assert.That(JournalNav.StepTab("camp", 1, false), Is.EqualTo("stores"),
                "the drawer sits beside the camp that holds it");
            Assert.That(JournalNav.StepTab("stores", 1, false), Is.EqualTo("warden"));
            Assert.That(JournalNav.StepTab("record", 1, false), Is.EqualTo("trail"), "the last tab wraps to the first");
        }

        [Test]
        public void StepTab_WalksBackwardAndWraps()
        {
            Assert.That(JournalNav.StepTab("camp", -1, false), Is.EqualTo("trail"));
            Assert.That(JournalNav.StepTab("trail", -1, false), Is.EqualTo("record"), "the first tab wraps to the last");
        }

        [Test]
        public void StepTab_OnASpread_SkipsTheTrail()
        {
            // Wide, the Trail is permanently open on the right page and has no
            // tab of its own — stopping on it would light a tab that isn't there.
            Assert.That(JournalNav.StepTab("record", 1, true), Is.EqualTo("camp"));
            Assert.That(JournalNav.StepTab("camp", -1, true), Is.EqualTo("record"));
        }

        [Test]
        public void StepTab_NoStepOrUnknownTab_IsSafe()
        {
            Assert.That(JournalNav.StepTab("camp", 0, false), Is.EqualTo("camp"), "no press, no turn");
            Assert.That(JournalNav.StepTab("nonsense", 1, false), Is.EqualTo("camp"),
                "an unknown tab steps from the first rather than throwing");
        }

        [Test]
        public void RestoreIndex_KeepsThePlaceAndClampsToAShorterPage()
        {
            Assert.That(JournalNav.RestoreIndex(3, 8), Is.EqualTo(3), "the same place in the rebuilt page");
            Assert.That(JournalNav.RestoreIndex(7, 4), Is.EqualTo(3),
                "a page that lost rows keeps focus on its last one");
        }

        [Test]
        public void RestoreIndex_NothingToRestoreOrNothingToRestoreInto()
        {
            Assert.That(JournalNav.RestoreIndex(-1, 5), Is.EqualTo(-1), "nothing was focused");
            Assert.That(JournalNav.RestoreIndex(2, 0), Is.EqualTo(-1), "the page has no controls left");
        }

        [Test]
        public void KeptPosition_HoldsTheSamePlaceWhenThePageGrows()
        {
            // Reading 300 down a page, and the rebuilt page is 200 taller. The
            // fraction that was 300 down the old page is 375 down the new one —
            // keeping the place means keeping the 300.
            Assert.That(JournalNav.KeptPosition(300f, 800f), Is.EqualTo(1f - (300f / 800f)).Within(1e-4f),
                "the same row of the page, not the same fraction of it");
        }

        [Test]
        public void KeptPosition_ClampsAtBothEnds()
        {
            Assert.That(JournalNav.KeptPosition(0f, 600f), Is.EqualTo(1f), "the top of the page stays the top");
            Assert.That(JournalNav.KeptPosition(900f, 600f), Is.EqualTo(0f),
                "a page that lost more than was below the reader lands on its last line, not past it");
        }

        [Test]
        public void KeptPosition_AShortPageCannotScroll()
        {
            // The rebuilt page fits the window — there is nowhere to be but the
            // top, and asking must not divide by a zero range.
            Assert.That(JournalNav.KeptPosition(300f, 0f), Is.EqualTo(1f));
            Assert.That(JournalNav.KeptPosition(300f, -50f), Is.EqualTo(1f));
        }

        [Test]
        public void RevealPosition_LeavesAVisibleControlAlone()
        {
            // A 1000-tall page in a 400-tall window, scrolled to the top: a
            // control at 100..160 is comfortably in view.
            var position = JournalNav.RevealPosition(1f, 1000f, 400f, 100f, 60f, 12f);

            Assert.That(position, Is.EqualTo(1f), "no scrolling for a control already on screen");
        }

        [Test]
        public void RevealPosition_ScrollsDownJustEnough()
        {
            // Window shows 0..400 of a 1000 page; the control sits at 500..560,
            // so its bottom plus padding (572) must reach the window's bottom —
            // the window top lands at 172, which is 1 - 172/600 up the page.
            var position = JournalNav.RevealPosition(1f, 1000f, 400f, 500f, 60f, 12f);

            Assert.That(position, Is.EqualTo(1f - (172f / 600f)).Within(1e-4f),
                "the page comes up by the shortfall, not to the control's row");
        }

        [Test]
        public void RevealPosition_ScrollsUpJustEnough()
        {
            // Scrolled to the bottom (window shows 600..1000); the control at
            // 500..560 is above it, so the window top lands at 488 (500 - pad).
            var position = JournalNav.RevealPosition(0f, 1000f, 400f, 500f, 60f, 12f);

            Assert.That(position, Is.EqualTo(1f - (488f / 600f)).Within(1e-4f));
        }

        [Test]
        public void RevealPosition_AShortPageCannotScroll()
        {
            // Content fits the window — there is nowhere to go, and asking must
            // not divide by a zero range.
            Assert.That(JournalNav.RevealPosition(1f, 300f, 400f, 100f, 60f, 12f), Is.EqualTo(1f));
        }

        [Test]
        public void RevealPosition_ClampsAtBothEnds()
        {
            // The very first control, padded above the top of the page, would
            // ask for a negative offset; the very last for one past the end.
            Assert.That(JournalNav.RevealPosition(0.5f, 1000f, 400f, 0f, 60f, 12f), Is.EqualTo(1f),
                "the top of the page is as far up as it goes");
            Assert.That(JournalNav.RevealPosition(0.5f, 1000f, 400f, 940f, 60f, 12f), Is.EqualTo(0f),
                "and the bottom as far down");
        }
    }
}
