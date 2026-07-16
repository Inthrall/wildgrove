using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the station auto-craft loop: recipe availability gating, inputs
    /// spent at batch start, output at completion, stalling and resuming with
    /// camp stock, refunds on stop/displacement, craft-speed upgrades, and
    /// batch-by-batch behaviour through the sub-stepped offline tick.
    /// </summary>
    public class CraftingTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                crafting = new EconomyData.CraftingData { baseCraftSeconds = 5.0 },
                // Effectively unbounded so gathered goods reach camp the same
                // tick — the crafting timeline stays exact.
                hauling = new EconomyData.HaulingData { baseCarryCapacity = 1e9, tripSeconds = 1.0, basketCapacity = 1e18 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "wildflowers", "fibres" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    id = "berry-jam", station = "fire", skill = "foraging",
                    inputs = { new ItemAmount { id = "berries", amount = 5 } },
                    output = "berry-jam", valueMult = 4, kind = "trade", defaultKnown = true,
                },
                new RecipeData
                {
                    id = "dried-berries", station = "fire", skill = "foraging",
                    inputs = { new ItemAmount { id = "berries", amount = 3 } },
                    output = "dried-berries", valueMult = 2, kind = "trade", defaultKnown = true,
                },
                new RecipeData
                {
                    id = "charcoal", station = "fire", skill = "firecraft",
                    inputs = { new ItemAmount { id = "timber", amount = 2 } },
                    output = "charcoal", valueMult = 1, kind = "material", defaultKnown = true,
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 7, id = "camp-fire-ring", costCoin = 100,
                    effects = { new EffectData { type = EffectType.UnlockSkill, skill = "firecraft" } },
                },
                new UpgradeData
                {
                    order = 18, id = "quick-hands", costCoin = 100,
                    effects = { new EffectData { type = EffectType.CraftSpeedMult, skill = "foraging", value = 2 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private RecipeData Recipe(string id)
        {
            return _data.RecipesById[id];
        }

        [Test]
        public void AvailableRecipes_GatedByUnlockedSkill()
        {
            var state = new GameState();

            var fresh = Crafting.AvailableRecipes(state, _data);

            // Foraging recipes are workable from the start; firecraft waits
            // for the Camp Fire Ring's unlockSkill effect.
            Assert.That(fresh.ConvertAll(r => r.id), Is.EqualTo(new[] { "berry-jam", "dried-berries" }));

            state.purchasedUpgradeIds.Add("camp-fire-ring");

            Assert.That(Crafting.AvailableRecipes(state, _data).ConvertAll(r => r.id),
                Is.EqualTo(new[] { "berry-jam", "dried-berries", "charcoal" }));
        }

        [Test]
        public void Advance_BatchStart_SpendsInputsBeforeOutputExists()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(12.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Crafting.Advance(state, _data, 1.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(7.0).Within(Tolerance));
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Crafting.Progress(state, _data, Recipe("berry-jam")), Is.EqualTo(0.2).Within(Tolerance));
        }

        [Test]
        public void Advance_CompletedBatch_AddsOutputAndStartsTheNext()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(12.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Crafting.Advance(state, _data, 5.0);
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));

            Crafting.Advance(state, _data, 5.0);
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void Advance_StockShort_StallsThenResumesWhenGoodsArrive()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(2.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Crafting.Advance(state, _data, 10.0);
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));

            state.AddResource("berries", new BigDouble(3.0));
            Crafting.Advance(state, _data, 5.0);

            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Stop_MidFlight_RefundsTheBatchInputs()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(5.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));
            Crafting.Advance(state, _data, 2.0);

            Crafting.Stop(state, _data, Recipe("berry-jam"));

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(Crafting.ActiveStationFor(state, Recipe("berry-jam")), Is.Null);
        }

        [Test]
        public void Assign_SameStation_DisplacesAndRefundsTheOldBatch()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(5.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));
            Crafting.Advance(state, _data, 2.0);

            // Both recipes use the fire — switching mid-batch is never a punishment.
            Crafting.Assign(state, _data, Recipe("dried-berries"));

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(Crafting.ActiveStationFor(state, Recipe("berry-jam")), Is.Null);
            Assert.That(Crafting.ActiveStationFor(state, Recipe("dried-berries")), Is.Not.Null);
        }

        [Test]
        public void Advance_CraftSpeedUpgrade_DividesTheBatchTime()
        {
            var state = new GameState();
            state.purchasedUpgradeIds.Add("quick-hands");
            state.AddResource("berries", new BigDouble(5.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            // ×2 foraging craft speed: the 5 s batch takes 2.5 s.
            Crafting.Advance(state, _data, 2.5);

            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void AvailableRecipes_StationLine_GatesUntilBuilt()
        {
            _data.buildings = new List<BuildingData>
            {
                new BuildingData
                {
                    id = "fire", displayName = "The Fire", baseCostCoin = 1300,
                    milestoneUpgradeIds = new List<string> { "camp-fire-ring" },
                    perLevel = new BuildingPerLevelData { type = "stationSpeedBonus", station = "fire", value = 0.05 },
                },
            };
            var state = new GameState();

            // All three recipes use the fire, and the fire isn't built yet.
            Assert.That(Crafting.AvailableRecipes(state, _data), Is.Empty);

            // The milestone builds the fire (and unlocks firecraft with it).
            state.purchasedUpgradeIds.Add("camp-fire-ring");

            Assert.That(Crafting.AvailableRecipes(state, _data).ConvertAll(r => r.id),
                Is.EqualTo(new[] { "berry-jam", "dried-berries", "charcoal" }));
        }

        [Test]
        public void Advance_StationSpeedLevels_DivideTheBatchTime()
        {
            _data.buildings = new List<BuildingData>
            {
                new BuildingData
                {
                    id = "fire", displayName = "The Fire", baseCostCoin = 1300,
                    perLevel = new BuildingPerLevelData { type = "stationSpeedBonus", station = "fire", value = 1.0 },
                },
            };
            var state = new GameState();
            state.buildingLevels["fire"] = 1; // ×2 fire speed: the 5 s batch takes 2.5 s.
            state.AddResource("berries", new BigDouble(5.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Crafting.Advance(state, _data, 2.5);

            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_UnknownRecipeId_IsSkippedHarmlessly()
        {
            var state = new GameState();
            state.stations.Add(new StationState { stationId = "fire", recipeId = "renamed-away" });

            Assert.DoesNotThrow(() => Crafting.Advance(state, _data, 10.0));
        }

        [Test]
        public void SimulationAdvance_OfflineCatchup_CraftsBatchByBatchAsGoodsArrive()
        {
            var state = new GameState { carrierCount = 1 };
            state.nodes.Add(new NodeState
            {
                id = "sunfield-meadow:berries", zoneId = GameStateFactory.StartingZoneId,
                resourceId = "berries", skill = "foraging", familiarCount = 1,
            });
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Simulation.Advance(state, _data, 30.0);

            // Gathering 1/s: batches can only start as stock reaches 5, so a
            // 30 s absence yields five finished jams (t=9,14,19,24,29) with a
            // sixth batch's inputs consumed at t=30 — not one giant batch.
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }
    }
}
