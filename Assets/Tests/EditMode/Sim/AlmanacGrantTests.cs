using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Almanac's exotic lines (design §8): grantUpgrade nodes put a
    /// ladder rung on every run free — starting tool tiers and zone skips,
    /// the lever that scales the ladder re-climb DOWNWARD with the fold —
    /// and keepCraftOrders carries the stations' standing orders across the
    /// fold. Grants respect the fold gate and the tool requirement (never
    /// materials or skill gates — the grant IS the head start), and are
    /// re-derived at every fold, on buy, and on restore.
    /// </summary>
    public class AlmanacGrantTests
    {
        private GameDataAsset _data;
        private RiteVerseData _verse;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { renownDivisor = 5000, exponent = 0.5, yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                crafting = new EconomyData.CraftingData { baseCraftSeconds = 5.0 },
                tools = new EconomyData.ToolsData { tiers = new List<string> { "flint", "copper" } },
                costGrowth = new EconomyData.CostGrowthData { building = 1.25, almanac = 1.25 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
                new ResourceData { id = "nuts", sellValue = 3, skill = "foraging" },
                new ResourceData { id = "peat", sellValue = 9, skill = "foraging" },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
                // The skippable trail: demands flint tools, like Bramble.
                new ZoneData
                {
                    id = "hedgerows",
                    order = 2,
                    resources = new List<string> { "nuts" },
                    unlocks = new List<string> { "foraging" },
                    requiredTool = "flint",
                },
                // The far trail: fold-gated, so its granted map must wait for
                // the run that earns it.
                new ZoneData
                {
                    id = "far-marsh",
                    order = 3,
                    resources = new List<string> { "peat" },
                    unlocks = new List<string> { "foraging" },
                    minMigration = 2,
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                // Materials and a skill gate a fresh run can't meet — exactly
                // what the grant must walk past.
                new UpgradeData
                {
                    order = 1, id = "flint-sickle", displayName = "Flint Sickle", toolTier = "flint",
                    gateSkill = "foraging", gateLevel = 5,
                    materials = { new ItemAmount { id = "berries", amount = 100 } },
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 2, id = "map-hedgerows", displayName = "Trail Map: Hedgerows",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "hedgerows" } },
                },
                new UpgradeData
                {
                    order = 3, id = "map-far",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "far-marsh" } },
                },
            };
            _data.recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    id = "berry-preserve", station = "fire", skill = "foraging", defaultKnown = true,
                    inputs = { new ItemAmount { id = "berries", amount = 4 } },
                    output = "berry-preserve", valueMult = 1.5, kind = "trade",
                },
            };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "remembered-edge", displayName = "The Remembered Edge", costVerdure = 6,
                    effects = { new EffectData { type = EffectType.GrantUpgrade, upgrade = "flint-sickle" } },
                },
                new AlmanacNodeData
                {
                    id = "known-way", displayName = "The Known Way", costVerdure = 10, requires = "remembered-edge",
                    effects = { new EffectData { type = EffectType.GrantUpgrade, upgrade = "map-hedgerows" } },
                },
                new AlmanacNodeData
                {
                    id = "far-way", displayName = "The Far Way", costVerdure = 20,
                    effects = { new EffectData { type = EffectType.GrantUpgrade, upgrade = "map-far" } },
                },
                new AlmanacNodeData
                {
                    id = "fire-remembers", displayName = "The Fire Remembers", costVerdure = 8,
                    effects = { new EffectData { type = EffectType.KeepCraftOrders } },
                },
            };
            _verse = new RiteVerseData
            {
                id = "verse-sunfield",
                zone = GameStateFactory.StartingZoneId,
                slots = { new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 10 } },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                rites = new List<RiteData>
                {
                    new RiteData { id = "first-rite", migration = 0, verses = { _verse } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState StateWithTheRiteSung()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 10);
            Rite.DeliverResource(state, _data, _verse, 0);
            return state;
        }

        [Test]
        public void TryBuy_AGrantNode_HandsTheRungOverAtOnce_Free()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 6.0;

            // No berries in camp and foraging level 0 — a purchase would be
            // refused twice over. The grant is the head start; it owes neither.
            Assert.That(Almanac.TryBuy(state, _data, _data.almanac[0]), Is.True);

            Assert.That(state.HasUpgrade("flint-sickle"), Is.True, "yours from this very run, not the next fold");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0), "granted, not bought");
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(2.0).Within(1e-9), "and its effects are live");
        }

        [Test]
        public void SyncGrantedUpgrades_TheGrantedToolCoversTheGrantedMap()
        {
            // Both nodes owned (a restored save) — the map rung's tool
            // requirement must be satisfied by the tool rung granted beside
            // it, whatever order the sweep visits them.
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("known-way");
            state.almanacNodeIds.Add("remembered-edge");

            Almanac.SyncGrantedUpgrades(state, _data);

            Assert.That(state.HasUpgrade("flint-sickle"), Is.True);
            Assert.That(state.HasUpgrade("map-hedgerows"), Is.True);
            Assert.That(state.nodes.Any(n => n.zoneId == "hedgerows"), Is.True, "the skipped trail is open and worked");
        }

        [Test]
        public void SyncGrantedUpgrades_WithoutTheCoveringTool_LeavesTheMapWaiting()
        {
            // A retuned tree could strand a map grant without its tool. The
            // sync holds the same line a purchase would rather than open a
            // trail the run's kit can't walk.
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("known-way");

            Almanac.SyncGrantedUpgrades(state, _data);

            Assert.That(state.HasUpgrade("map-hedgerows"), Is.False);

            state.almanacNodeIds.Add("remembered-edge");
            Almanac.SyncGrantedUpgrades(state, _data);

            Assert.That(state.HasUpgrade("map-hedgerows"), Is.True, "the tool's arrival frees the map");
        }

        [Test]
        public void SyncGrantedUpgrades_IsIdempotent()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("remembered-edge");

            Almanac.SyncGrantedUpgrades(state, _data);
            Almanac.SyncGrantedUpgrades(state, _data);

            Assert.That(state.purchasedUpgradeIds.Count(id => id == "flint-sickle"), Is.EqualTo(1));
        }

        [Test]
        public void Migrate_GrantedRungs_AreOnTheNewRunFromItsFirstMorning()
        {
            var state = StateWithTheRiteSung();
            state.almanacNodeIds.Add("remembered-edge");
            state.almanacNodeIds.Add("known-way");

            var next = Migration.Migrate(state, _data);

            Assert.That(next, Is.Not.Null);
            Assert.That(next.HasUpgrade("flint-sickle"), Is.True);
            Assert.That(next.HasUpgrade("map-hedgerows"), Is.True);
            Assert.That(next.nodes.Any(n => n.zoneId == "hedgerows"), Is.True);
        }

        [Test]
        public void Migrate_AGrantBehindTheFoldGate_WaitsForTheRunThatEarnsIt()
        {
            var state = StateWithTheRiteSung();
            state.almanacNodeIds.Add("far-way");
            state.migrationCount = 0;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.migrationCount, Is.EqualTo(1));
            Assert.That(next.HasUpgrade("map-far"), Is.False, "the marsh does not exist until fold 2");

            state.migrationCount = 1;
            var later = Migration.Migrate(state, _data);

            Assert.That(later.migrationCount, Is.EqualTo(2));
            Assert.That(later.HasUpgrade("map-far"), Is.True, "and on its fold the grant is simply there");
        }

        [Test]
        public void Migrate_TheFireRemembers_CarriesTheStandingOrders()
        {
            var state = StateWithTheRiteSung();
            state.almanacNodeIds.Add("fire-remembers");
            Crafting.Assign(state, _data, _data.recipes[0]);
            var station = state.stations[0];
            station.inFlight = true;
            station.progressSeconds = 3.0;
            // An idle station has no order to remember.
            state.stations.Add(new StationState { stationId = "bench" });

            var next = Migration.Migrate(state, _data);

            Assert.That(next.stations, Has.Count.EqualTo(1));
            Assert.That(next.stations[0].stationId, Is.EqualTo("fire"));
            Assert.That(next.stations[0].recipeId, Is.EqualTo("berry-preserve"));
            Assert.That(next.stations[0].inFlight, Is.False, "the assignment crosses, never the batch");
            Assert.That(next.stations[0].progressSeconds, Is.EqualTo(0.0));
        }

        [Test]
        public void Migrate_WithoutTheFireRemembers_TheStationsStandDown()
        {
            var state = StateWithTheRiteSung();
            Crafting.Assign(state, _data, _data.recipes[0]);

            var next = Migration.Migrate(state, _data);

            Assert.That(next.stations, Is.Empty);
        }
    }
}
