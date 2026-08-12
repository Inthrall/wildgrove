using System.Collections.Generic;
using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the Record page's folding rule. The page is the longest in the book
    /// and the rule is what keeps it short without maintenance, so the cases
    /// that matter are the two nobody presses: a record card is shut on arrival,
    /// and the one card with a button on it is not.
    /// </summary>
    public class JournalRecordFoldsTests
    {
        private Dictionary<string, bool> _choices;

        [SetUp]
        public void SetUp()
        {
            _choices = new Dictionary<string, bool>();
        }

        [Test]
        public void IsOpen_ACardThatOnlyRecords_ArrivesFoldedShut()
        {
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Compendium), Is.False,
                "the compendium is a drawer of every gatherable — unfolded it is the whole page");
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.DeepPages), Is.False);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Wheel), Is.False);
        }

        [Test]
        public void IsOpen_TheFolio_ArrivesOpenBecauseItIsTheOneWithAButtonOnIt()
        {
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Folio), Is.True);
        }

        [Test]
        public void IsOpen_ACardWithNoRuleOfItsOwn_StandsOpen()
        {
            // A card that defaulted shut by accident would have no way in: the
            // fold in its own head is the only door.
            Assert.That(JournalRecordFolds.IsOpen(_choices, "a-card-nobody-wrote-a-rule-for"), Is.True);
        }

        [Test]
        public void Toggle_OpensARecordCard_AndItStaysOpen()
        {
            JournalRecordFolds.Toggle(_choices, JournalRecordFolds.Compendium);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Compendium), Is.True,
                "the press must land");

            // Nothing else the player does moves it back — unlike a Trail
            // ground, which folds itself away when a newer one opens.
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Compendium), Is.True);
        }

        [Test]
        public void Toggle_ShutsTheFolio_WhichWasOpenByDefault()
        {
            JournalRecordFolds.Toggle(_choices, JournalRecordFolds.Folio);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Folio), Is.False,
                "a default-open card has to be shuttable, or the fold is decoration");
        }

        [Test]
        public void Toggle_Twice_ReturnsTheCardToWhereItStarted()
        {
            JournalRecordFolds.Toggle(_choices, JournalRecordFolds.DeepPages);
            JournalRecordFolds.Toggle(_choices, JournalRecordFolds.DeepPages);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.DeepPages), Is.False);
        }

        [Test]
        public void OneCardsFold_LeavesTheOthersAlone()
        {
            JournalRecordFolds.Toggle(_choices, JournalRecordFolds.Compendium);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.DeepPages), Is.False);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Wheel), Is.False);
            Assert.That(JournalRecordFolds.IsOpen(_choices, JournalRecordFolds.Folio), Is.True);
        }

        [Test]
        public void NoStore_ReadsTheDefaultsAndSwallowsTheToggle()
        {
            // The HUD holds the store, and a page built before it exists must
            // not throw — the rule is the fallback, not the dictionary.
            Assert.That(JournalRecordFolds.IsOpen(null, JournalRecordFolds.Folio), Is.True);
            Assert.That(JournalRecordFolds.IsOpen(null, JournalRecordFolds.Compendium), Is.False);
            Assert.DoesNotThrow(() => JournalRecordFolds.Toggle(null, JournalRecordFolds.Folio));
        }

        [Test]
        public void NoCardId_IsNeverOpen()
        {
            Assert.That(JournalRecordFolds.IsOpen(_choices, null), Is.False);
            Assert.DoesNotThrow(() => JournalRecordFolds.Toggle(_choices, null));
        }
    }
}
