using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the store's kith slots (design §4 ladder): entitlements fold into
    /// the sim idempotently — the starter bundle's Amber arrives exactly once,
    /// and a billing hiccup never shrinks a ladder the save remembers.
    /// </summary>
    public class KithPurchasesTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                store = new EconomyData.StoreData { starterBundleAmber = 30 },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Apply_NothingOwned_ChangesNothing()
        {
            var state = new GameState();

            Assert.That(KithPurchases.Apply(state, _data, false, false), Is.False);
            Assert.That(state.purchasedKithSlots, Is.EqualTo(0));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Apply_TheStarterBundle_OpensASlotAndPaysTheAmberOnce()
        {
            var state = new GameState();

            Assert.That(KithPurchases.Apply(state, _data, true, false), Is.True);
            Assert.That(state.purchasedKithSlots, Is.EqualTo(1));
            Assert.That(state.amber, Is.EqualTo(30.0).Within(Tolerance));
            Assert.That(state.starterBundleAmberGranted, Is.True);

            // Entitlements re-resolve on every launch and device — the pile
            // must not.
            Assert.That(KithPurchases.Apply(state, _data, true, false), Is.False);
            Assert.That(state.amber, Is.EqualTo(30.0).Within(Tolerance), "one bundle, one pile");
        }

        [Test]
        public void Apply_BothProducts_OpenBothSlots()
        {
            var state = new GameState();

            KithPurchases.Apply(state, _data, true, true);

            Assert.That(state.purchasedKithSlots, Is.EqualTo(2));
        }

        [Test]
        public void Apply_ABillingHiccup_NeverShrinksTheLadder()
        {
            var state = new GameState { purchasedKithSlots = 2, starterBundleAmberGranted = true };

            Assert.That(KithPurchases.Apply(state, _data, false, false), Is.False,
                "the store not knowing yet is not the store saying no");
            Assert.That(state.purchasedKithSlots, Is.EqualTo(2));
        }

        [Test]
        public void Apply_UnconfiguredStore_StillHonoursTheSlotAndFlagsTheGrant()
        {
            _data.economy.store = null;
            var state = new GameState();

            KithPurchases.Apply(state, _data, true, false);

            Assert.That(state.purchasedKithSlots, Is.EqualTo(1));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.starterBundleAmberGranted, Is.True,
                "a later config change must not mint a retroactive pile");
        }
    }
}
