using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the trade-value table (design §9 money→XP): raw finds at their
    /// resources.json value, crafted trade goods derived from inputs × valueMult
    /// (recursively), mastery raising a raw find's value but never the goods
    /// crafted from it, and materials trading at zero. Plus offline catch-up
    /// capping. Uses a hand-built content asset — no scene or Resources asset.
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
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                // Effectively unbounded, so offline tests see goods at camp the
                // same tick they're gathered; HaulTests pins the tight case.
                // Two slots so the factory stations both seeds (vole + raven) —
                // these fixtures exercise the gather→haul pipeline, not the ladder.
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
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
            _data.recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    id = "berry-preserve", station = "fire", skill = "firecraft",
                    inputs =
                    {
                        new ItemAmount { id = "berries", amount = 4 },
                        new ItemAmount { id = "wildflowers", amount = 2 },
                    },
                    output = "berry-preserve", valueMult = 4, kind = "trade",
                },
                new RecipeData
                {
                    id = "gift-basket", station = "bench", skill = "bushcraft",
                    inputs = { new ItemAmount { id = "berry-preserve", amount = 2 } },
                    output = "gift-basket", valueMult = 2, kind = "trade",
                },
                new RecipeData
                {
                    id = "cordage", station = "bench", skill = "bushcraft",
                    inputs = { new ItemAmount { id = "fibres", amount = 8 } },
                    output = "cordage", valueMult = 2, kind = "material",
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void TradeValuePerUnit_RawResource_IsItsSellValue()
        {
            Assert.That(Economy.TradeValuePerUnit(new GameState(), _data, "berries").ToDouble(),
                Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void TradeValuePerUnit_TradeGood_DerivesFromInputsTimesValueMult()
        {
            var value = Economy.TradeValuePerUnit(new GameState(), _data, "berry-preserve");

            // (4 berries · 2 + 2 wildflowers · 3) · valueMult 4 = 56.
            Assert.That(value.ToDouble(), Is.EqualTo(56.0).Within(Tolerance));
        }

        [Test]
        public void TradeValuePerUnit_NestedTradeGood_DerivesRecursively()
        {
            var value = Economy.TradeValuePerUnit(new GameState(), _data, "gift-basket");

            // (2 preserves · 56) · valueMult 2 = 224.
            Assert.That(value.ToDouble(), Is.EqualTo(224.0).Within(Tolerance));
        }

        [Test]
        public void TradeValuePerUnit_MasteryRaisesTheRawResourceValue()
        {
            _data.economy.mastery = new EconomyData.MasteryData
            {
                yieldBonusPerLevel = 0.05, baseXp = 50, growth = 1.15, maxLevel = 99, xpPerUnit = 0.25,
            };
            var state = GameStateFactory.NewGame(_data);
            state.nodes[0].masteryXp = 107.5; // berries node, mastery level 2

            var value = Economy.TradeValuePerUnit(state, _data, "berries");

            // 2 base · (1 + 0.05 · 2) — §4's "+yield/value per level".
            Assert.That(value.ToDouble(), Is.EqualTo(2.2).Within(Tolerance));
        }

        [Test]
        public void TradeValuePerUnit_MasteryNeverInflatesCraftedGoods()
        {
            _data.economy.mastery = new EconomyData.MasteryData
            {
                yieldBonusPerLevel = 0.05, baseXp = 50, growth = 1.15, maxLevel = 99, xpPerUnit = 0.25,
            };
            var state = GameStateFactory.NewGame(_data);
            state.nodes[0].masteryXp = 107.5;

            var value = Economy.TradeValuePerUnit(state, _data, "berry-preserve");

            // Still input BASE values × valueMult — berry mastery notwithstanding.
            Assert.That(value.ToDouble(), Is.EqualTo(56.0).Within(Tolerance));
        }

        [Test]
        public void TradeValuePerUnit_CraftedMaterial_IsUnpriced()
        {
            Assert.That(Economy.TradeValuePerUnit(new GameState(), _data, "cordage").ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_WithinCap_CreditsFullElapsed()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.StageGathererAndCarrier(state); // a gatherer on the berries node, a carrier home

            var credited = Simulation.AdvanceOffline(state, _data, 100.0);

            Assert.That(credited, Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_BeyondCap_CreditsCapOnly()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.StageGathererAndCarrier(state);

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
            TestKith.StageGathererAndCarrier(state);

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
