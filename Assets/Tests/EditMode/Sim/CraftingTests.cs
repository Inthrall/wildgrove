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
                // Two slots so the factory stations both seeds (vole + raven) —
                // these fixtures exercise the gather→haul pipeline, not the ladder.
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
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
                    order = 7, id = "camp-fire-ring",
                    effects = { new EffectData { type = EffectType.UnlockSkill, skill = "firecraft" } },
                },
                new UpgradeData
                {
                    order = 18, id = "quick-hands",
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
        public void Advance_SpeedUpgradeMidBatch_CompletesWithoutMintingCraftTime()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(100.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            // 4 s banked into the 5 s batch, THEN the ×2 speed upgrade lands:
            // the new 2.5 s duration is below the banked progress.
            Crafting.Advance(state, _data, 4.0);
            state.purchasedUpgradeIds.Add("quick-hands");

            Crafting.Advance(state, _data, 0.1);

            // The over-banked batch completes, but the 1.5 s of surplus must
            // not refund into the tick — the next batch has seen exactly the
            // 0.1 s this Advance carried, nothing minted.
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(Crafting.Progress(state, _data, Recipe("berry-jam")),
                Is.EqualTo(0.1 / 2.5).Within(Tolerance));
        }

        [Test]
        public void AvailableRecipes_StationLine_GatesUntilBuilt()
        {
            _data.buildings = new List<BuildingData>
            {
                new BuildingData
                {
                    id = "fire", displayName = "The Fire",
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
                    id = "fire", displayName = "The Fire",
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
        public void Advance_CompletedBatch_GrantsCraftXpToTheRecipeSkill()
        {
            _data.economy.xp = new EconomyData.XpData
            {
                baseXp = 100, growth = 1.1, maxLevel = 99, craftPerBatch = 25,
            };
            var state = new GameState();
            state.AddResource("berries", new BigDouble(12.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Crafting.Advance(state, _data, 10.0);

            // Two finished batches, 25 XP each; the in-flight third pays nothing yet.
            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(50.0).Within(Tolerance));
        }

        [Test]
        public void Assign_SkillLevelTooLow_Refuses_UntilTheLevelIsEarned()
        {
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99 };
            Recipe("berry-jam").skillLevel = 3;
            var state = new GameState();

            Crafting.Assign(state, _data, Recipe("berry-jam"));
            Assert.That(Crafting.ActiveStationFor(state, Recipe("berry-jam")), Is.Null);

            // Levels 2 and 3 cost 100 + 110.
            state.skillXp["foraging"] = 210.0;
            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Assert.That(Crafting.ActiveStationFor(state, Recipe("berry-jam")), Is.Not.Null);
        }

        [Test]
        public void AvailableRecipes_LevelLockedRecipe_StaysListedAsAGoal()
        {
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99 };
            Recipe("berry-jam").skillLevel = 3;
            var state = new GameState();

            Assert.That(Crafting.AvailableRecipes(state, _data).ConvertAll(r => r.id),
                Does.Contain("berry-jam"));
            Assert.That(Crafting.SkillLevelMet(state, _data, Recipe("berry-jam")), Is.False);
        }

        [Test]
        public void Advance_GateLostMidBatch_StallsFrozen_AndStopStillRefunds()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(5.0));
            Crafting.Assign(state, _data, Recipe("berry-jam"));
            Crafting.Advance(state, _data, 2.0); // batch in flight, inputs spent

            // A retune raises the gate above the run's level after assignment.
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99 };
            Recipe("berry-jam").skillLevel = 3;

            Crafting.Advance(state, _data, 30.0);

            // Frozen, not completed — it must never craft through a gate
            // Assign would refuse — and Stop still refunds the batch.
            Assert.That(state.GetResource("berry-jam").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Crafting.Stop(state, _data, Recipe("berry-jam"));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Advance_SkillNoLongerUnlocked_Stalls()
        {
            // The restored-save shape: a station holds a recipe whose skill
            // grant no longer exists in purchasedUpgradeIds.
            var state = new GameState();
            state.AddResource("timber", new BigDouble(10.0));
            state.stations.Add(new StationState { stationId = "fire", recipeId = "charcoal" });

            Crafting.Advance(state, _data, 30.0);

            Assert.That(state.GetResource("charcoal").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void Assign_StationLineNotBuilt_Refuses()
        {
            _data.buildings = new List<BuildingData>
            {
                new BuildingData
                {
                    id = "fire", displayName = "The Fire",
                    perLevel = new BuildingPerLevelData { type = "stationSpeedBonus", station = "fire", value = 0.05 },
                },
            };
            var state = new GameState();
            state.AddResource("berries", new BigDouble(5.0));

            Crafting.Assign(state, _data, Recipe("berry-jam"));

            Assert.That(Crafting.ActiveStationFor(state, Recipe("berry-jam")), Is.Null);
        }

        [Test]
        public void SkillLevelMet_XpUnconfigured_IsOpen()
        {
            Recipe("berry-jam").skillLevel = 5;

            Assert.That(Crafting.SkillLevelMet(new GameState(), _data, Recipe("berry-jam")), Is.True);
        }

        [Test]
        public void SimulationAdvance_OfflineCatchup_CraftsBatchByBatchAsGoodsArrive()
        {
            var state = new GameState();
            state.nodes.Add(new NodeState
            {
                id = "sunfield-meadow:berries", zoneId = GameStateFactory.StartingZoneId,
                resourceId = "berries", skill = "foraging",
            });
            TestKith.Station(state, "sunfield-meadow:berries", 1); // a gatherer
            TestKith.Station(state, Familiar.TrailStation, 1); // a carrier
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
