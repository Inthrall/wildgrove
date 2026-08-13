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
    /// <para>
    /// The station cards come in with a default of their own instead, and the
    /// case worth pinning there is the press on a station the rule would have
    /// called open: computed one way and toggled the other, the head does nothing.
    /// </para>
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

        // ── The stations, whose default is positional ──────────────────────

        [Test]
        public void IsOpen_AStationNobodyPressed_FollowsTheDefaultItWasHanded()
        {
            var fire = JournalCardFolds.Station("fire");
            var forge = JournalCardFolds.Station("forge");

            Assert.That(JournalCardFolds.IsOpen(_choices, fire, true), Is.True, "the work stands here");
            Assert.That(JournalCardFolds.IsOpen(_choices, forge, false), Is.False,
                "and every other station is a head and a tally");
        }

        [Test]
        public void Toggle_ShutsAStationThatWasOpenByItsPosition()
        {
            var fire = JournalCardFolds.Station("fire");

            JournalCardFolds.Toggle(_choices, fire, true);

            Assert.That(JournalCardFolds.IsOpen(_choices, fire, true), Is.False);
        }

        [Test]
        public void Toggle_OpensAStationThatWasShutByItsPosition()
        {
            // The case a default of this class's own would get wrong: a station
            // id is in nobody's shut-unasked set, so the rule-based Toggle would
            // read it as open, write shut, and the head would do nothing at all.
            var forge = JournalCardFolds.Station("forge");

            JournalCardFolds.Toggle(_choices, forge, false);

            Assert.That(JournalCardFolds.IsOpen(_choices, forge, false), Is.True,
                "one press on a folded station's head must open it");
        }

        [Test]
        public void IsOpen_APressedStation_OutlastsTheWorkMovingOn()
        {
            var forge = JournalCardFolds.Station("forge");
            JournalCardFolds.Toggle(_choices, forge, false);

            // The work has moved to another station, so the positional default
            // for this one is shut again — and the press still wins, which is
            // what keeps a card from folding away under the finger that was
            // using it.
            Assert.That(JournalCardFolds.IsOpen(_choices, forge, false), Is.True);
        }

        [Test]
        public void Station_IsNullForNoStation()
        {
            Assert.That(JournalCardFolds.Station(null), Is.Null);
            Assert.That(JournalCardFolds.Station("fire"), Is.EqualTo("station-fire"));
        }
    }
}
