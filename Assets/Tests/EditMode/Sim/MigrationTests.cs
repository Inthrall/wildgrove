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
    /// keeping what the land remembers (Verdure, Renown, every insect, the
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
            _data.insects = new List<InsectData>
            {
                new InsectData
                {
                    id = "stags-herald", sketches = 3,
                    habitats = new List<string> { GameStateFactory.StartingZoneId }, rarity = 1.0,
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
            state.AddResource("berries", 500);
            state.AddFine("berries", 5);
            state.AddPristine("berries", 2);
            state.nodes[0].masteryXp = 500.0;
            state.purchasedUpgradeIds.Add("flint-sickle");
            state.buildingLevels["fire"] = 3;
            state.stations.Add(new StationState { stationId = "fire", recipeId = "berry-preserve" });
            state.skillXp["foraging"] = 1000.0;
            state.digSites.Add(new DigSiteState { zoneId = GameStateFactory.StartingZoneId });
            state.deedCounts["tend"] = 9;
            state.gearBySlot["camp"] = "oilskin-tarp";

            var next = Migration.Migrate(state, _data);

            // The run's own state resets to the fresh-run baseline (the crew
            // itself crosses — see Migrate_CarriesTheCrewFolded).
            Assert.That(next.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetFine("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetPristine("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.nodes[0].masteryXp, Is.EqualTo(0.0).Within(Tolerance));
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
            state.insectSketches["stags-herald"] = 2;
            state.rngState = 123456789UL;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.verdurePoints, Is.EqualTo(3.0).Within(Tolerance), "verdure banks from lifetime renown");
            Assert.That(next.renown.ToDouble(), Is.EqualTo(45000.0).Within(Tolerance), "renown is lifetime, never spent");
            Assert.That(Insects.SketchCount(next, "stags-herald"), Is.EqualTo(2), "the record spans migrations");
            Assert.That(next.migrationCount, Is.EqualTo(1));
            Assert.That(next.rngState, Is.EqualTo(123456789UL));
        }

        [Test]
        public void Migrate_KeepsFolioFixings_AndTheirSpreadBonuses()
        {
            _data.folioSpreads = new List<FolioSpreadData>
            {
                new FolioSpreadData
                {
                    id = "meadow-blooms", displayName = "Meadow Blooms",
                    entries = new List<string> { "berries" },
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.10 } },
                },
            };
            var state = StateWithTheRiteSung();
            state.AddPristine("berries", 1);
            Folio.TryFix(state, _data, "berries");

            var next = Migration.Migrate(state, _data);

            Assert.That(next.fixedResources, Is.EqualTo(new[] { "berries" }));
            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                "the Folio's permanence survives the fold");
        }

        [Test]
        public void Migrate_CarriesTheCrewFolded()
        {
            var state = StateWithTheRiteSung();
            state.roster[0].xp = 5000.0;
            state.roster[0].stationId = state.nodes[0].id;
            state.roster[0].powerupIds.Add("berry-wise");
            var count = state.roster.Count;

            var next = Migration.Migrate(state, _data);

            // The roster crosses the fold; each familiar returns to a clean run
            // build — station cleared, powerups dropped — with run XP banked into
            // permanent Kinship (design §4).
            Assert.That(next.roster.Count, Is.EqualTo(count), "the crew crosses whole");
            Assert.That(next.roster[0].kinshipXp, Is.GreaterThan(0.0), "run XP banks into Kinship");
            Assert.That(next.roster[0].powerupIds, Is.Empty, "run build resets");
            Assert.That(next.roster.TrueForAll(f => f.IsWandering), Is.True, "stations reset");
        }

        [Test]
        public void Migrate_BondedFamiliarsCross_ButThePostResets()
        {
            _data.folioSpreads = new List<FolioSpreadData>
            {
                new FolioSpreadData
                {
                    id = "meadow-blooms", displayName = "Meadow Blooms",
                    entries = new List<string> { "berries" },
                },
            };
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "sootwing", displayName = "Sootwing", species = "pack-raven", role = "carrier",
                    source = new BondSourceData { type = "folioSpread", id = "meadow-blooms" },
                },
            };
            var state = StateWithTheRiteSung();
            state.fixedResources.Add("berries");
            state.wardenPostNodeId = state.nodes[0].id;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.roster.Exists(f => f.bonded && f.bondId == "sootwing"), Is.True,
                "the bond is derived from the donations the fold carries — Sootwing crosses too");
            Assert.That(next.wardenPostNodeId, Is.Null, "the post resets with the camp");
        }

        [Test]
        public void Migrate_LoreStaysRead()
        {
            var state = StateWithTheRiteSung();
            state.seenWaystoneZoneIds.Add(GameStateFactory.StartingZoneId);

            var next = Migration.Migrate(state, _data);

            Assert.That(next.seenWaystoneZoneIds, Is.EqualTo(new[] { GameStateFactory.StartingZoneId }),
                "run 2 re-unlocks the zones without re-showing read stones");
        }

        [Test]
        public void Migrate_KeepsAmber()
        {
            var state = StateWithTheRiteSung();
            state.amber = 37.0;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.amber, Is.EqualTo(37.0).Within(Tolerance), "'you keep … Amber'");
        }

        [Test]
        public void Migrate_TheCompendiumCrossesWhole()
        {
            var state = StateWithTheRiteSung();
            state.lifetimeGathered["berries"] = new BigDouble(123456.0);
            state.lifetimeCrafted["berry-preserve"] = 42.0;
            state.lifetimePristine["berries"] = new BigDouble(3.0);

            var next = Migration.Migrate(state, _data);

            Assert.That(Compendium.LifetimeGathered(next, "berries").ToDouble(), Is.EqualTo(123456.0).Within(Tolerance));
            Assert.That(Compendium.LifetimeCrafted(next, "berry-preserve"), Is.EqualTo(42.0).Within(Tolerance));
            Assert.That(Compendium.LifetimePristine(next, "berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance),
                "the record is one of the axes that never reset");
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
        public void Migrate_RecordedPlateEffects_CarryIntoTheFreshRun()
        {
            var state = StateWithTheRiteSung();
            state.insectSketches["stags-herald"] = 3; // assembled

            var next = Migration.Migrate(state, _data);

            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                "the Antler Crown's +10% survives the fold");
        }
    }
}
