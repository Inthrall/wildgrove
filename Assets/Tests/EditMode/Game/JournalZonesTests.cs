using System.Collections.Generic;
using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the Trail page's folding rule. The whole point is that the page
    /// stays short <em>without maintenance</em> — so the case that matters most
    /// is the one nobody presses: a new trail map opens the next ground, and
    /// the ground before it has to fold shut on its own.
    /// </summary>
    public class JournalZonesTests
    {
        private const string Meadow = "sunfield-meadow";
        private const string Bramble = "bramble-hollow";
        private const string Peaks = "cloudreach-peaks";

        private Dictionary<string, bool> _choices;

        [SetUp]
        public void SetUp()
        {
            _choices = new Dictionary<string, bool>();
        }

        [Test]
        public void IsOpen_TheNewestGround_StandsOpen()
        {
            Assert.That(JournalZones.IsOpen(_choices, Peaks, Peaks), Is.True);
        }

        [Test]
        public void IsOpen_AnOlderGround_IsFoldedShut()
        {
            Assert.That(JournalZones.IsOpen(_choices, Meadow, Peaks), Is.False);
        }

        [Test]
        public void IsOpen_WhenTheNextGroundOpens_TheOneBeforeItFoldsOnItsOwn()
        {
            // Bramble was the newest and standing open, untouched.
            Assert.That(JournalZones.IsOpen(_choices, Bramble, Bramble), Is.True);

            // The trail reaches the peaks. Nothing was pressed.
            Assert.That(JournalZones.IsOpen(_choices, Bramble, Peaks), Is.False,
                "an unpressed ground is open only while it is the newest — this is what keeps the page short");
            Assert.That(JournalZones.IsOpen(_choices, Peaks, Peaks), Is.True);
        }

        [Test]
        public void IsOpen_AGroundThePlayerOpened_StaysOpenWhenItIsNoLongerNewest()
        {
            JournalZones.Toggle(_choices, Meadow, Bramble);
            Assert.That(JournalZones.IsOpen(_choices, Meadow, Bramble), Is.True, "the corruption must land");

            Assert.That(JournalZones.IsOpen(_choices, Meadow, Peaks), Is.True,
                "a ground opened by hand is not folded away by a later unlock");
        }

        [Test]
        public void Toggle_TheNewestGround_ShutsItAndItStaysShut()
        {
            JournalZones.Toggle(_choices, Peaks, Peaks);

            Assert.That(JournalZones.IsOpen(_choices, Peaks, Peaks), Is.False);
        }

        [Test]
        public void Toggle_Twice_LeavesTheGroundAsItWasFound()
        {
            JournalZones.Toggle(_choices, Meadow, Peaks);
            JournalZones.Toggle(_choices, Meadow, Peaks);

            Assert.That(JournalZones.IsOpen(_choices, Meadow, Peaks), Is.False);
        }

        [Test]
        public void Toggle_RemembersEachGroundSeparately()
        {
            JournalZones.Toggle(_choices, Meadow, Peaks);
            JournalZones.Toggle(_choices, Peaks, Peaks);

            Assert.That(JournalZones.IsOpen(_choices, Meadow, Peaks), Is.True);
            Assert.That(JournalZones.IsOpen(_choices, Bramble, Peaks), Is.False, "untouched, and not the newest");
            Assert.That(JournalZones.IsOpen(_choices, Peaks, Peaks), Is.False);
        }

        [Test]
        public void IsOpen_OnTheFirstRun_HasOneGroundAndItIsOpen()
        {
            // Run 1 opens on the meadow alone; the page draws no heading then,
            // so nothing could reopen a fold — it must never read as shut.
            Assert.That(JournalZones.IsOpen(_choices, Meadow, Meadow), Is.True);
        }

        [Test]
        public void IsOpen_AndToggle_SurviveNothingBeingThere()
        {
            Assert.That(JournalZones.IsOpen(null, Meadow, Meadow), Is.True);
            Assert.That(JournalZones.IsOpen(_choices, null, null), Is.False, "a nameless ground is not the newest one");
            Assert.DoesNotThrow(() => JournalZones.Toggle(null, Meadow, Meadow));
            Assert.DoesNotThrow(() => JournalZones.Toggle(_choices, null, Meadow));
        }
    }
}
