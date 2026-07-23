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
    }
}
