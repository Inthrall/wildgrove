using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Coin economy: the climbing familiar-gift cost curve, Provisioner
    /// sales (raw resources only), and offline catch-up capping. Uses a
    /// hand-built content asset so no scene or Resources asset is loaded.
    /// </summary>
    public class EconomyTests
    {
        private const double Tolerance = 1e-6;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                costGrowth = new EconomyData.CostGrowthData { gathererGift = 1.09 },
                gifts = new EconomyData.GiftsData { familiarBaseCoin = 10 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2 },
                new ResourceData { id = "wildflowers", sellValue = 3 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries", "wildflowers", "fibres" },
                    unlocks = new List<string> { "foraging" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void FamiliarGiftCost_FirstGift_IsBaseCost()
        {
            var state = new GameState();

            var cost = Economy.FamiliarGiftCost(state, _data.economy);

            // n = 0 familiars befriended → base · 1.09^0 = base.
            Assert.That(cost.ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void FamiliarGiftCost_ScalesWithTotalFamiliars()
        {
            var state = new GameState();
            state.nodes.Add(new NodeState { familiarCount = 2 });

            var cost = Economy.FamiliarGiftCost(state, _data.economy);

            // 10 · 1.09^2 = 11.881
            Assert.That(cost.ToDouble(), Is.EqualTo(11.881).Within(Tolerance));
        }

        [Test]
        public void TryGiftFamiliar_WhenAffordable_SpendsCoinAndAddsFamiliar()
        {
            var state = new GameState { coin = 50 };
            var node = new NodeState { familiarCount = 0 };
            state.nodes.Add(node);

            var gifted = Economy.TryGiftFamiliar(state, _data.economy, node);

            Assert.That(gifted, Is.True);
            Assert.That(node.familiarCount, Is.EqualTo(1));
            Assert.That(state.coin.ToDouble(), Is.EqualTo(40.0).Within(Tolerance));
        }

        [Test]
        public void TryGiftFamiliar_WhenCoinShort_LeavesStateUnchanged()
        {
            var state = new GameState { coin = 5 };
            var node = new NodeState { familiarCount = 0 };
            state.nodes.Add(node);

            var gifted = Economy.TryGiftFamiliar(state, _data.economy, node);

            Assert.That(gifted, Is.False);
            Assert.That(node.familiarCount, Is.EqualTo(0));
            Assert.That(state.coin.ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void SellResource_RawResource_ConvertsToCoinAndClearsStock()
        {
            var state = new GameState();
            state.AddResource("berries", 5);

            var gained = Economy.SellResource(state, _data, "berries");

            Assert.That(gained.ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(state.coin.ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void SellResource_UnsellableResource_IsNoOp()
        {
            var state = new GameState();
            state.AddResource("iron-ingot", 5);

            var gained = Economy.SellResource(state, _data, "iron-ingot");

            Assert.That(gained.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.coin.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            // The unsellable stock is preserved, not destroyed.
            Assert.That(state.GetResource("iron-ingot").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void SellAll_SellsEverySellableResource()
        {
            var state = new GameState();
            state.AddResource("berries", 5);      // 5 · 2 = 10
            state.AddResource("wildflowers", 4);  // 4 · 3 = 12
            state.AddResource("iron-ingot", 9);   // unsellable

            var gained = Economy.SellAll(state, _data);

            Assert.That(gained.ToDouble(), Is.EqualTo(22.0).Within(Tolerance));
            Assert.That(state.coin.ToDouble(), Is.EqualTo(22.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("iron-ingot").ToDouble(), Is.EqualTo(9.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_WithinCap_CreditsFullElapsed()
        {
            var state = GameStateFactory.NewGame(_data); // 1 familiar on the berries node

            var credited = Simulation.AdvanceOffline(state, _data, 100.0);

            Assert.That(credited, Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_BeyondCap_CreditsCapOnly()
        {
            var state = GameStateFactory.NewGame(_data);

            // Away 10 h, cap 4 h → only 14400 s credited.
            var credited = Simulation.AdvanceOffline(state, _data, 10 * 3600.0);

            Assert.That(credited, Is.EqualTo(4 * 3600.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(4 * 3600.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_AppliesRateMultiplier()
        {
            _data.economy.offline.rateMultiplier = 0.5;
            var state = GameStateFactory.NewGame(_data);

            var credited = Simulation.AdvanceOffline(state, _data, 100.0);

            // Wall-clock credited is reported in full; yield is scaled by the rate.
            Assert.That(credited, Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_NonPositiveElapsed_IsNoOp()
        {
            var state = GameStateFactory.NewGame(_data);

            var credited = Simulation.AdvanceOffline(state, _data, 0.0);

            Assert.That(credited, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }
    }
}
