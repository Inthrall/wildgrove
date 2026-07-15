using System.Collections.Generic;
using BreakInfinity;
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
                costGrowth = new EconomyData.CostGrowthData { gathererGift = 1.09, carrierGift = 1.10 },
                gifts = new EconomyData.GiftsData { gathererBaseGoods = 10, carrierBaseGoods = 8 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                // Effectively unbounded, so offline/sale tests see goods at camp
                // the same tick they're gathered; HaulTests pins the tight case.
                hauling = new EconomyData.HaulingData { baseCarryCapacity = 1e9, tripSeconds = 1.0, basketCapacity = 1e18 },
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
        public void GathererGiftCost_FirstGift_IsBaseCost()
        {
            var node = new NodeState();

            var cost = Economy.GathererGiftCost(node, _data.economy);

            // n = 0 familiars befriended at the node → base · 1.09^0 = base.
            Assert.That(cost.ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void GathererGiftCost_ScalesWithTheNodesOwnFamiliars()
        {
            var node = new NodeState { familiarCount = 2 };

            var cost = Economy.GathererGiftCost(node, _data.economy);

            // 10 · 1.09^2 = 11.881
            Assert.That(cost.ToDouble(), Is.EqualTo(11.881).Within(Tolerance));
        }

        [Test]
        public void GathererGiftCost_BareNode_IgnoresTheFlockElsewhere()
        {
            var state = new GameState();
            state.nodes.Add(new NodeState { resourceId = "berries", familiarCount = 30 });
            var bare = new NodeState { resourceId = "wildflowers" };
            state.nodes.Add(bare);

            // Depth pricing is per node: a virgin trail starts at the base cost
            // however large the flock is at other nodes.
            Assert.That(Economy.GathererGiftCost(bare, _data.economy).ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void CarrierGiftCostEach_ScalesWithCarriersOnItsOwnCurve()
        {
            var state = new GameState();

            // n = 0 carriers → base; n = 2 → 8 · 1.10² = 9.68 (of each worked resource).
            Assert.That(Economy.CarrierGiftCostEach(state, _data.economy).ToDouble(), Is.EqualTo(8.0).Within(Tolerance));

            state.carrierCount = 2;
            Assert.That(Economy.CarrierGiftCostEach(state, _data.economy).ToDouble(), Is.EqualTo(9.68).Within(Tolerance));
        }

        [Test]
        public void FeederResources_ListsOnlyWorkedNodes()
        {
            var state = new GameState();
            state.nodes.Add(new NodeState { resourceId = "berries", familiarCount = 2 });
            state.nodes.Add(new NodeState { resourceId = "wildflowers", familiarCount = 0 });
            state.nodes.Add(new NodeState { resourceId = "fibres", familiarCount = 1 });

            Assert.That(Economy.FeederResources(state), Is.EqualTo(new[] { "berries", "fibres" }));
        }

        [Test]
        public void TryGiftCarrier_FillsTheFeederFromEveryWorkedResource()
        {
            var state = new GameState { coin = new BigDouble(999.0), carrierCount = 1 };
            state.nodes.Add(new NodeState { resourceId = "berries", familiarCount = 1 });
            state.nodes.Add(new NodeState { resourceId = "wildflowers", familiarCount = 1 });
            state.nodes.Add(new NodeState { resourceId = "fibres", familiarCount = 0 });
            state.AddResource("berries", new BigDouble(20.0));
            state.AddResource("wildflowers", new BigDouble(20.0));

            var gifted = Economy.TryGiftCarrier(state, _data.economy);

            // costEach = 8 · 1.10¹ = 8.8 of berries AND wildflowers; the idle
            // fibres node isn't in the bundle, and Coin is untouched.
            Assert.That(gifted, Is.True);
            Assert.That(state.carrierCount, Is.EqualTo(2));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(11.2).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(11.2).Within(Tolerance));
            Assert.That(state.GetResource("fibres").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.coin.ToDouble(), Is.EqualTo(999.0).Within(Tolerance));
        }

        [Test]
        public void TryGiftCarrier_AnyShortResource_ChangesNothing()
        {
            var state = new GameState { carrierCount = 1 };
            state.nodes.Add(new NodeState { resourceId = "berries", familiarCount = 1 });
            state.nodes.Add(new NodeState { resourceId = "wildflowers", familiarCount = 1 });
            state.AddResource("berries", new BigDouble(20.0));
            state.AddResource("wildflowers", new BigDouble(5.0)); // short of 8.8

            var gifted = Economy.TryGiftCarrier(state, _data.economy);

            Assert.That(gifted, Is.False);
            Assert.That(state.carrierCount, Is.EqualTo(1));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void TryGiftCarrier_NothingWorked_IsRefusedNotFree()
        {
            var state = new GameState { carrierCount = 1 };
            state.nodes.Add(new NodeState { resourceId = "berries", familiarCount = 0 });
            state.AddResource("berries", new BigDouble(100.0));

            // An empty bundle must never read as "affordable".
            Assert.That(Economy.CanGiftCarrier(state, _data.economy), Is.False);
            Assert.That(Economy.TryGiftCarrier(state, _data.economy), Is.False);
            Assert.That(state.carrierCount, Is.EqualTo(1));
        }

        [Test]
        public void TryGiftGatherer_WhenStocked_SpendsTheNodesOwnResource()
        {
            var state = new GameState { coin = new BigDouble(999.0) };
            var node = new NodeState { resourceId = "berries", familiarCount = 0 };
            state.nodes.Add(node);
            state.AddResource("berries", new BigDouble(12.0));

            var gifted = Economy.TryGiftGatherer(state, _data.economy, node);

            Assert.That(gifted, Is.True);
            Assert.That(node.familiarCount, Is.EqualTo(1));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
            // Coin is untouched — gatherer gifts are a goods sink (design §13).
            Assert.That(state.coin.ToDouble(), Is.EqualTo(999.0).Within(Tolerance));
        }

        [Test]
        public void TryGiftGatherer_WhenStockShort_LeavesStateUnchanged()
        {
            var state = new GameState { coin = new BigDouble(999.0) };
            var node = new NodeState { resourceId = "berries", familiarCount = 0 };
            state.nodes.Add(node);
            state.AddResource("berries", new BigDouble(5.0));

            var gifted = Economy.TryGiftGatherer(state, _data.economy, node);

            // Coin can't stand in — only the node's own resource pays (design §13).
            Assert.That(gifted, Is.False);
            Assert.That(node.familiarCount, Is.EqualTo(0));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
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
