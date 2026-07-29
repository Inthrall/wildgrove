using System.Collections.Generic;
using NUnit.Framework;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins what the run owes the player a moment for. Every case here is a
    /// celebration that must show exactly once: a newcomer queues for the naming
    /// sheet but a loaded kith doesn't, a widened slot ladder is announced but an
    /// inherited one isn't, and an unshown welcome-back summary is never
    /// clobbered by the smaller absence that follows it.
    /// </summary>
    public class AnnouncementsTests
    {
        private Announcements _announce;

        [SetUp]
        public void SetUp()
        {
            _announce = new Announcements();
        }

        private static Familiar Familiar(string id, bool bonded = false)
        {
            return new Familiar { id = id, speciesId = "meadow-vole", bonded = bonded };
        }

        [Test]
        public void NoticeArrivals_QueuesEachNewcomerOnce()
        {
            var roster = new List<Familiar> { Familiar("a"), Familiar("b") };

            _announce.NoticeArrivals(roster);
            _announce.NoticeArrivals(roster);

            Assert.That(_announce.TakeArrival().id, Is.EqualTo("a"));
            Assert.That(_announce.TakeArrival().id, Is.EqualTo("b"));
            Assert.That(_announce.TakeArrival(), Is.Null);
        }

        [Test]
        public void NoticeArrivals_LeavesABondedCompanionAlone()
        {
            // A bond has a canonical name and its own celebration — it must never
            // be sent to the naming sheet as if it were a stray.
            _announce.NoticeArrivals(new List<Familiar> { Familiar("burr", bonded: true) });

            Assert.That(_announce.PeekArrival(), Is.Null);
        }

        [Test]
        public void MarkArrivalsSeen_StopsALoadedKithFromQueuing()
        {
            var roster = new List<Familiar> { Familiar("a"), Familiar("b") };

            _announce.MarkArrivalsSeen(roster);
            _announce.NoticeArrivals(roster);

            Assert.That(_announce.PeekArrival(), Is.Null);
        }

        [Test]
        public void MarkArrivalsSeen_ThenARealNewcomer_StillQueues()
        {
            _announce.MarkArrivalsSeen(new List<Familiar> { Familiar("a") });

            _announce.NoticeArrivals(new List<Familiar> { Familiar("a"), Familiar("b") });

            Assert.That(_announce.TakeArrival().id, Is.EqualTo("b"));
            Assert.That(_announce.TakeArrival(), Is.Null);
        }

        [Test]
        public void NoticeKithSlots_OnTheFirstLook_SeedsTheMarkSilently()
        {
            // A loaded ladder was earned in an earlier session; announcing it on
            // launch would celebrate a rung the player already has.
            Assert.That(_announce.NoticeKithSlots(4), Is.Zero);
            Assert.That(_announce.PendingSlotCelebration, Is.Zero);
        }

        [Test]
        public void NoticeKithSlots_WhenTheLadderWidens_CelebratesTheNewCount()
        {
            _announce.NoticeKithSlots(3);

            Assert.That(_announce.NoticeKithSlots(4), Is.EqualTo(4));
            Assert.That(_announce.TakeSlotCelebration(), Is.EqualTo(4));
            Assert.That(_announce.PendingSlotCelebration, Is.Zero);
        }

        [Test]
        public void NoticeKithSlots_AtTheSameCount_SaysNothing()
        {
            _announce.NoticeKithSlots(3);

            Assert.That(_announce.NoticeKithSlots(3), Is.Zero);
        }

        [Test]
        public void MarkKithSlotsSeen_ThenAWiderLadder_IsNotAnnounced()
        {
            // The fold and an adopted cloud save both hand over a ladder rather
            // than earn one, so the mark is re-seeded instead of compared.
            _announce.NoticeKithSlots(3);
            _announce.MarkKithSlotsSeen();

            Assert.That(_announce.NoticeKithSlots(6), Is.Zero);
            Assert.That(_announce.PendingSlotCelebration, Is.Zero);
        }

        [Test]
        public void OfferOfflineSummary_WithOneAlreadyPending_KeepsTheFirst()
        {
            var first = new OfflineSummary { realSeconds = 7200.0, creditedSeconds = 3600.0 };
            var topUp = new OfflineSummary { realSeconds = 90.0, creditedSeconds = 90.0 };

            Assert.That(_announce.OfferOfflineSummary(first), Is.True);
            Assert.That(_announce.OfferOfflineSummary(topUp), Is.False);
            Assert.That(_announce.PendingOfflineSummary, Is.SameAs(first));
        }

        [Test]
        public void OfferOfflineSummary_WithNothingCredited_IsIgnored()
        {
            Assert.That(_announce.OfferOfflineSummary(new OfflineSummary()), Is.False);
            Assert.That(_announce.PendingOfflineSummary, Is.Null);
        }

        [Test]
        public void TakeOfflineSummary_ShowsItOnceThenGoesQuiet()
        {
            var summary = new OfflineSummary { creditedSeconds = 3600.0 };
            _announce.OfferOfflineSummary(summary);

            Assert.That(_announce.TakeOfflineSummary(), Is.SameAs(summary));
            Assert.That(_announce.TakeOfflineSummary(), Is.Null);
        }

        [Test]
        public void DropOfflineSummary_ThenANewAbsence_TakesItsPlace()
        {
            // The adopted-cloud-save path: the pending summary credited a run
            // that has since been thrown away, so it must not win priority.
            _announce.OfferOfflineSummary(new OfflineSummary { creditedSeconds = 3600.0 });
            _announce.DropOfflineSummary();

            var adopted = new OfflineSummary { creditedSeconds = 120.0 };

            Assert.That(_announce.OfferOfflineSummary(adopted), Is.True);
            Assert.That(_announce.PendingOfflineSummary, Is.SameAs(adopted));
        }

        [Test]
        public void QueueReward_CountsAndDrainsInOrder()
        {
            _announce.QueueReward(new RewardGrant { rewardId = "drovers-halter" });
            _announce.QueueReward(new RewardGrant { rewardId = "cloak" });

            Assert.That(_announce.RewardsReceived, Is.EqualTo(2));
            Assert.That(_announce.TakeReward().rewardId, Is.EqualTo("drovers-halter"));
            Assert.That(_announce.TakeReward().rewardId, Is.EqualTo("cloak"));
            Assert.That(_announce.TakeReward(), Is.Null);
            // The count is a session tally, not a queue depth — the manual
            // "anything new?" check reads the difference across a re-fetch.
            Assert.That(_announce.RewardsReceived, Is.EqualTo(2));
        }

        [Test]
        public void CelebrateBond_ShowsTheBondOnce()
        {
            var bond = new BondData { id = "burr", displayName = "Burr" };

            _announce.CelebrateBond(bond);

            Assert.That(_announce.PendingBondCelebration, Is.SameAs(bond));
            Assert.That(_announce.TakeBondCelebration(), Is.SameAs(bond));
            Assert.That(_announce.TakeBondCelebration(), Is.Null);
        }
    }
}
