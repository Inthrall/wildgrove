using System.Collections.Generic;
using NUnit.Framework;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the journal's card-folding rule. It is what keeps the two longest
    /// surfaces in the book short without maintenance, so the cases that matter
    /// are the ones nobody presses: a card that only records is shut on arrival,
    /// the Record card with a button on it is not, and the keeping is shut
    /// despite having five.
    /// </summary>
    public class JournalCardFoldsTests
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
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Compendium), Is.False,
                "the compendium is a drawer of every gatherable — unfolded it is the whole page");
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.DeepPages), Is.False);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Wheel), Is.False);
        }

        [Test]
        public void IsOpen_TheFolio_ArrivesOpenBecauseItIsTheOneWithAButtonOnIt()
        {
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Folio), Is.True);
        }

        [Test]
        public void IsOpen_TheKeeping_ArrivesFoldedShutDespiteItsButtons()
        {
            // The exception to the rule above, and the reason this class is the
            // journal's rather than the Record's: the keeping carries a Set down
            // per slot and still folds, because unfolded it is a whole phone
            // viewport standing at the head of the Trail for two thirds of the
            // year. Both doors into it open the fold on the way through.
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Keeping), Is.False);
        }

        [Test]
        public void Toggle_OpensTheKeeping_AndItStaysOpen()
        {
            JournalCardFolds.Toggle(_choices, JournalCardFolds.Keeping);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Keeping), Is.True);
        }

        [Test]
        public void IsOpen_ACardWithNoRuleOfItsOwn_StandsOpen()
        {
            // A card that defaulted shut by accident would have no way in: the
            // fold in its own head is the only door.
            Assert.That(JournalCardFolds.IsOpen(_choices, "a-card-nobody-wrote-a-rule-for"), Is.True);
        }

        [Test]
        public void Toggle_OpensARecordCard_AndItStaysOpen()
        {
            JournalCardFolds.Toggle(_choices, JournalCardFolds.Compendium);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Compendium), Is.True,
                "the press must land");

            // Nothing else the player does moves it back — unlike a Trail
            // ground, which folds itself away when a newer one opens.
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Compendium), Is.True);
        }

        [Test]
        public void Toggle_ShutsTheFolio_WhichWasOpenByDefault()
        {
            JournalCardFolds.Toggle(_choices, JournalCardFolds.Folio);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Folio), Is.False,
                "a default-open card has to be shuttable, or the fold is decoration");
        }

        [Test]
        public void Toggle_Twice_ReturnsTheCardToWhereItStarted()
        {
            JournalCardFolds.Toggle(_choices, JournalCardFolds.DeepPages);
            JournalCardFolds.Toggle(_choices, JournalCardFolds.DeepPages);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.DeepPages), Is.False);
        }

        [Test]
        public void OneCardsFold_LeavesTheOthersAlone()
        {
            JournalCardFolds.Toggle(_choices, JournalCardFolds.Compendium);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.DeepPages), Is.False);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Wheel), Is.False);
            Assert.That(JournalCardFolds.IsOpen(_choices, JournalCardFolds.Folio), Is.True);
        }

        [Test]
        public void NoStore_ReadsTheDefaultsAndSwallowsTheToggle()
        {
            // The HUD holds the store, and a page built before it exists must
            // not throw — the rule is the fallback, not the dictionary.
            Assert.That(JournalCardFolds.IsOpen(null, JournalCardFolds.Folio), Is.True);
            Assert.That(JournalCardFolds.IsOpen(null, JournalCardFolds.Compendium), Is.False);
            Assert.DoesNotThrow(() => JournalCardFolds.Toggle(null, JournalCardFolds.Folio));
        }

        [Test]
        public void NoCardId_IsNeverOpen()
        {
            Assert.That(JournalCardFolds.IsOpen(_choices, null), Is.False);
            Assert.DoesNotThrow(() => JournalCardFolds.Toggle(_choices, null));
        }
    }
}
