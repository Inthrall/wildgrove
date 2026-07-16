using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins Migration (design §7): gated by the completed Rite, banking
    /// Verdure from lifetime Renown on the sqrt curve, resetting the run
    /// (coin, familiars, upgrades, buildings, skills, sites, offerings) and
    /// keeping what the land remembers (Verdure, Renown, every fossil, the
    /// rng thread, the migration count).
    /// </summary>
    public class MigrationTests
    {
        private const double Tolerance = 1e-9;

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
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2 },
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
            };
            _data.fossils = new List<FossilData>
            {
                new FossilData
                {
                    id = "antler-crown", fragments = 3,
                    digSites = new List<string> { GameStateFactory.StartingZoneId }, strataRarity = 1.0,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all", value = 0.10 } },
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
        public void VerdureAfterMigration_FollowsTheSqrtCurve()
        {
            var state = GameStateFactory.NewGame(_data);

            state.renown = new BigDouble(20000.0);
            Assert.That(Migration.VerdureAfterMigration(state, _data), Is.EqualTo(2.0).Within(Tolerance));

            state.renown = new BigDouble(45000.0);
            Assert.That(Migration.VerdureAfterMigration(state, _data), Is.EqualTo(3.0).Within(Tolerance));
        }

        [Test]
        public void VerdureAfterMigration_NeverShrinksTheBankedTotal()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 5.0;
            state.renown = new BigDouble(20000.0); // curve says 2

            Assert.That(Migration.VerdureAfterMigration(state, _data), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Migrate_WithoutTheCompletedRite_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(20000.0);

            Assert.That(Migration.CanMigrate(state, _data), Is.False);
            Assert.That(Migration.Migrate(state, _data), Is.Null);
        }

        [Test]
        public void Migrate_ResetsTheRun()
        {
            var state = StateWithTheRiteSung();
            state.coin = new BigDouble(9999.0);
            state.AddResource("berries", 500);
            state.AddFine("berries", 5);
            state.AddPristine("berries", 2);
            state.nodes[0].familiarCount = 7;
            state.nodes[0].masteryXp = 500.0;
            state.carrierCount = 4;
            state.purchasedUpgradeIds.Add("flint-sickle");
            state.buildingLevels["fire"] = 3;
            state.stations.Add(new StationState { stationId = "fire", recipeId = "berry-preserve" });
            state.skillXp["foraging"] = 1000.0;
            state.digSites.Add(new DigSiteState { zoneId = GameStateFactory.StartingZoneId, familiarCount = 2 });
            state.deedCounts["tend"] = 9;
            state.gearBySlot["camp"] = "oilskin-tarp";

            var next = Migration.Migrate(state, _data);

            // Back to the fresh-run seeds — the §2 bootstrap, nothing else.
            Assert.That(next.coin.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetFine("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetPristine("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.nodes[0].familiarCount, Is.EqualTo(1));
            Assert.That(next.nodes[0].masteryXp, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.carrierCount, Is.EqualTo(1));
            Assert.That(next.purchasedUpgradeIds, Is.Empty);
            Assert.That(next.buildingLevels, Is.Empty);
            Assert.That(next.stations, Is.Empty);
            Assert.That(next.skillXp, Is.Empty);
            Assert.That(next.digSites, Is.Empty);
            Assert.That(next.deedCounts, Is.Empty);
            Assert.That(next.verseProgress, Is.Empty);
            Assert.That(next.gearBySlot, Is.Empty, "the kit is rebuilt cheaply each run, not carried");
        }

        [Test]
        public void Migrate_KeepsWhatTheLandRemembers()
        {
            var state = StateWithTheRiteSung();
            state.renown = new BigDouble(45000.0);
            state.fossilFragments["antler-crown"] = 2;
            state.rngState = 123456789UL;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.verdurePoints, Is.EqualTo(3.0).Within(Tolerance), "verdure banks from lifetime renown");
            Assert.That(next.renown.ToDouble(), Is.EqualTo(45000.0).Within(Tolerance), "renown is lifetime, never spent");
            Assert.That(Fossils.FragmentCount(next, "antler-crown"), Is.EqualTo(2), "the dig chase spans migrations");
            Assert.That(next.migrationCount, Is.EqualTo(1));
            Assert.That(next.rngState, Is.EqualTo(123456789UL));
        }

        [Test]
        public void Migrate_KeepsMuseumDonations_AndTheirSetBonuses()
        {
            _data.museumSets = new List<MuseumSetData>
            {
                new MuseumSetData
                {
                    id = "meadow-blooms", displayName = "Meadow Blooms",
                    entries = new List<string> { "berries" },
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.10 } },
                },
            };
            var state = StateWithTheRiteSung();
            state.AddPristine("berries", 1);
            Museum.TryDonate(state, _data, "berries");

            var next = Migration.Migrate(state, _data);

            Assert.That(next.donatedResources, Is.EqualTo(new[] { "berries" }));
            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                "the Museum's permanence survives the fold");
        }

        [Test]
        public void Migrate_KeepsTheAlmanac_AndItsEffects()
        {
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "old-songs-i", displayName = "Old Songs I", costVerdure = 2,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.10 } },
                },
            };
            var state = StateWithTheRiteSung();
            state.verdurePoints = 5.0;
            Almanac.TryBuy(state, _data, _data.almanac[0]);

            var next = Migration.Migrate(state, _data);

            Assert.That(next.almanacNodeIds, Is.EqualTo(new[] { "old-songs-i" }));
            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                "the permanent tree survives the fold");
        }

        [Test]
        public void Migrate_CompletedFossilEffects_CarryIntoTheFreshRun()
        {
            var state = StateWithTheRiteSung();
            state.fossilFragments["antler-crown"] = 3; // assembled

            var next = Migration.Migrate(state, _data);

            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                "the Antler Crown's +10% survives the fold");
        }
    }
}
