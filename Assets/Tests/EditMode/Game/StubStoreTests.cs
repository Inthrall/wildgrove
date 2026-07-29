using NUnit.Framework;
using Wildgrove.Game.Services;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the store's entitlement-vs-consumable split (design §10): one-off
    /// products are owned once and re-report AlreadyOwned; the amber packs are
    /// consumable, so they never register ownership and re-buy every time. The
    /// stub stands in for the real Unity IAP store in the editor and tests.
    /// </summary>
    public class StubStoreTests
    {
        private StubStore _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = new StubStore();
        }

        [Test]
        public void Purchase_OneOffEntitlement_OwnsThenReportsAlreadyOwned()
        {
            StoreResult first = StoreResult.Failed;
            StoreResult second = StoreResult.Failed;
            _sut.Purchase(StoreProductIds.RemoveAds, r => first = r);
            _sut.Purchase(StoreProductIds.RemoveAds, r => second = r);

            Assert.That(first, Is.EqualTo(StoreResult.Purchased));
            Assert.That(second, Is.EqualTo(StoreResult.AlreadyOwned), "a one-off is owned after the first buy");
            Assert.That(_sut.IsOwned(StoreProductIds.RemoveAds), Is.True);
        }

        [Test]
        public void Purchase_AmberPack_ReBuysAndIsNeverOwned()
        {
            StoreResult first = StoreResult.Failed;
            StoreResult second = StoreResult.Failed;
            _sut.Purchase(StoreProductIds.AmberPackSmall, r => first = r);
            _sut.Purchase(StoreProductIds.AmberPackSmall, r => second = r);

            Assert.That(first, Is.EqualTo(StoreResult.Purchased));
            Assert.That(second, Is.EqualTo(StoreResult.Purchased), "a consumable re-buys every time");
            Assert.That(_sut.IsOwned(StoreProductIds.AmberPackSmall), Is.False, "packs are consumed, never owned");
        }

        [Test]
        public void Purchase_UnknownProduct_Fails()
        {
            StoreResult result = StoreResult.Purchased;
            _sut.Purchase("not_a_product", r => result = r);

            Assert.That(result, Is.EqualTo(StoreResult.Failed));
        }

        [Test]
        public void IsConsumable_MarksPacksOnly()
        {
            Assert.That(StoreProductIds.IsConsumable(StoreProductIds.AmberPackSmall), Is.True);
            Assert.That(StoreProductIds.IsConsumable(StoreProductIds.AmberPackLarge), Is.True);
            Assert.That(StoreProductIds.IsConsumable(StoreProductIds.RemoveAds), Is.False);
            Assert.That(StoreProductIds.IsConsumable(StoreProductIds.KithSlot), Is.False);
        }

        [Test]
        public void Purchase_ARewardProduct_Fails()
        {
            // Rewards are awarded by Play, never bought — a buy button that
            // could reach one would be selling a Quest prize.
            StoreResult result = StoreResult.Purchased;
            _sut.Purchase(RewardProductIds.DroversHalter, r => result = r);

            Assert.That(result, Is.EqualTo(StoreResult.Failed));
        }

        [Test]
        public void DeliverReward_GrantsBeforeItAcknowledges()
        {
            var granted = false;
            _sut.RewardRedeemed = id =>
            {
                Assert.That(_sut.IsOwned(RewardProductIds.DroversHalter), Is.False,
                    "the order must not be acknowledged until the grant has landed");
                granted = id == RewardProductIds.DroversHalter;
                return granted;
            };

            Assert.That(_sut.DeliverReward(RewardProductIds.DroversHalter), Is.True);
            Assert.That(granted, Is.True);
            Assert.That(_sut.IsOwned(RewardProductIds.DroversHalter), Is.True, "a single-use reward is owned after");
        }

        [Test]
        public void DeliverReward_WhenTheGrantRefuses_LeavesItUnacknowledged()
        {
            // An unacknowledged reward is refunded by Play in three days and can
            // be offered again; an acknowledged one that never landed is gone.
            _sut.RewardRedeemed = id => false;

            Assert.That(_sut.DeliverReward(RewardProductIds.DroversHalter), Is.False);
            Assert.That(_sut.IsOwned(RewardProductIds.DroversHalter), Is.False);
        }

        [Test]
        public void DeliverReward_WithNoHandlerSet_LeavesItUnacknowledged()
        {
            Assert.That(_sut.DeliverReward(RewardProductIds.WeeklyAmberCache), Is.False);
        }

        [Test]
        public void DeliverReward_ARepeatableReward_IsNeverOwnedSoItCanComeAgain()
        {
            var deliveries = 0;
            _sut.RewardRedeemed = id =>
            {
                deliveries++;
                return true;
            };

            Assert.That(_sut.DeliverReward(RewardProductIds.WeeklyAmberCache), Is.True);
            Assert.That(_sut.DeliverReward(RewardProductIds.WeeklyAmberCache), Is.True);
            Assert.That(deliveries, Is.EqualTo(2), "next week's cache must not read as already owned");
            Assert.That(_sut.IsOwned(RewardProductIds.WeeklyAmberCache), Is.False);
        }
    }
}
