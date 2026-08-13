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
        public void RenownForNextVerdure_NamesTheThresholdTheCurveWillCross()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(20000.0); // banks 2

            // The third point sits at 3^2 * 5000 — the same 45000 the curve test pins.
            Assert.That(Migration.RenownForNextVerdure(state, _data), Is.EqualTo(45000.0).Within(Tolerance));
        }

        [Test]
        public void ProgressToNextVerdure_MeasuresTheClimbBetweenTwoPoints()
        {
            var state = GameStateFactory.NewGame(_data);

            // The 2nd point sits at 20000 and the 3rd at 45000, so halfway
            // between them is 32500 — the fold banner's "50% to the next".
            state.renown = new BigDouble(20000.0);
            Assert.That(Migration.ProgressToNextVerdure(state, _data), Is.EqualTo(0.0).Within(Tolerance),
                "a point just banked starts the next climb at zero");

            state.renown = new BigDouble(32500.0);
            Assert.That(Migration.ProgressToNextVerdure(state, _data), Is.EqualTo(0.5).Within(Tolerance));

            state.renown = new BigDouble(45000.0);
            Assert.That(Migration.ProgressToNextVerdure(state, _data), Is.EqualTo(0.0).Within(Tolerance),
                "crossing the threshold banks the point and restarts the climb");
        }

        [Test]
        public void ProgressToNextVerdure_IsZeroWhenTheFoldEconomyIsInert()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(32500.0);
            _data.economy.verdure = null;

            Assert.That(Migration.ProgressToNextVerdure(state, _data), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void RenownForVerdure_IsTheInverseOfTheCurve()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(Migration.RenownForVerdure(_data, 7.0));

            Assert.That(Migration.VerdureAfterMigration(state, _data), Is.EqualTo(7.0).Within(Tolerance));
        }

        [Test]
        public void RenownForVerdure_WithNoFoldEconomy_IsZero()
        {
            Assert.That(Migration.RenownForVerdure(null, 3.0), Is.EqualTo(0.0));
            Assert.That(Migration.RenownForVerdure(_data, 0.0), Is.EqualTo(0.0));
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
            state.AddDecent("berries", 5);
            state.AddChoice("berries", 2);
            state.nodes[0].masteryXp = 500.0;
            state.purchasedUpgradeIds.Add("flint-sickle");
            state.buildingLevels["fire"] = 3;
            state.stations.Add(new StationState { stationId = "fire", recipeId = "berry-preserve" });
            state.skillXp["foraging"] = 1000.0;
            state.digSites.Add(new DigSiteState { zoneId = GameStateFactory.StartingZoneId });
            state.deedCounts["tend"] = 9;
            state.gearBySlot["camp"] = "oilskin-tarp";
            state.gearCrafted.Add("oilskin-tarp");

            var next = Migration.Migrate(state, _data);

            // The run's own state resets to the fresh-run baseline (the kith
            // itself crosses — see Migrate_CarriesTheKithFolded).
            Assert.That(next.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetDecent("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.GetChoice("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.nodes[0].masteryXp, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(next.purchasedUpgradeIds, Is.Empty);
            Assert.That(next.buildingLevels, Is.Empty);
            Assert.That(next.stations, Is.Empty);
            Assert.That(next.skillXp, Is.Empty);
            Assert.That(next.digSites, Is.Empty);
            Assert.That(next.deedCounts, Is.Empty);
            Assert.That(next.verseProgress, Is.Empty);
            Assert.That(next.gearBySlot, Is.Empty, "the kit is rebuilt cheaply each run, not carried");
            Assert.That(next.gearCrafted, Is.Empty, "the kit bag folds with the kit — the rebuild is the point");
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
        public void Migrate_KeepsTheVersesThatOpenedThePlaces_AndTheirFloor()
        {
            var state = StateWithTheRiteSung();
            state.sungVerseZones.Add("a-verse-from-an-older-run");
            state.grandfatheredKithSlots = 2;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.sungVerseZones, Contains.Item("a-verse-from-an-older-run"),
                "a verse sung is never unsung - the fold must not take an earned place back");
            Assert.That(next.grandfatheredKithSlots, Is.EqualTo(2),
                "and neither must it drop the floor under a save carried over from the old tally ladder");
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
            state.AddChoice("berries", 1);
            Folio.TryFix(state, _data, "berries");

            var next = Migration.Migrate(state, _data);

            Assert.That(next.fixedResources, Is.EqualTo(new[] { "berries" }));
            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                "the Folio's permanence survives the fold");
        }

        [Test]
        public void Migrate_CarriesTheKithFolded()
        {
            var state = StateWithTheRiteSung();
            Roster.Recruit(state, _data, "meadow-vole", Familiar.WatchStation("old-growth-wood"));
            state.roster[0].xp = 5000.0;
            var count = state.roster.Count;

            var next = Migration.Migrate(state, _data);

            // The roster crosses the fold; each familiar returns to a clean run
            // build — station cleared — with run XP banked into permanent
            // Kinship (design §4).
            Assert.That(next.roster.Count, Is.EqualTo(count), "the kith crosses whole");
            Assert.That(next.roster[0].kinshipXp, Is.GreaterThan(0.0), "run XP banks into Kinship");
            Assert.That(next.roster.TrueForAll(f => f.IsResting), Is.True, "stations reset");
        }

        [Test]
        public void Migrate_LeavesTheRetiringRunAlone()
        {
            // Folding read the old run's worth and wrecked it in the process:
            // the fold ran over the roster in place, so the state the caller
            // still holds had every station emptied and every run level reset,
            // and the two states then shared one roster list. Nothing depended
            // on it only because the caller swaps states immediately.
            var state = StateWithTheRiteSung();
            Roster.Recruit(state, _data, "meadow-vole", Familiar.WatchStation("old-growth-wood"));
            state.roster[0].xp = 5000.0;

            var next = Migration.Migrate(state, _data);

            Assert.That(state.roster[0].xp, Is.EqualTo(5000.0).Within(Tolerance),
                "the retiring run keeps the run XP it was read for");
            Assert.That(state.roster[0].stationId, Is.EqualTo(Familiar.WatchStation("old-growth-wood")),
                "and keeps its posts");
            Assert.That(next.roster[0], Is.Not.SameAs(state.roster[0]),
                "the fold carries copies, so an edit to either run can't reach the other");
            Assert.That(next.roster[0].kinshipXp, Is.GreaterThan(state.roster[0].kinshipXp),
                "and the copy is the one that was folded");
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
            Assert.That(next.wardenPostNodeId, Is.EqualTo(next.nodes[0].id),
                "the fold reseeds the fresh run's opening post");
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
        public void Migrate_CarriesTheFinalWaystonesAndTheFoldTheyWereReadOn()
        {
            var state = StateWithTheRiteSung();
            state.finalWaystonesRead = 2;
            state.finalWaystoneLastFold = state.migrationCount;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.finalWaystonesRead, Is.EqualTo(2), "the §7 reveal is lore — it stays read");
            Assert.That(next.finalWaystoneLastFold, Is.EqualTo(state.migrationCount),
                "and the stamp crosses with the count, so the pair never disagree about whether one was ever taken");
            Assert.That(next.migrationCount, Is.GreaterThan(next.finalWaystoneLastFold),
                "folding is what earns the next stone: the new run opens with one waiting");
        }

        [Test]
        public void Migrate_BanksTheRunsVersesAndKeepsThePurchasedSlots()
        {
            var state = StateWithTheRiteSung();
            state.foldedVersesSung = 3;
            state.purchasedKithSlots = 1;
            state.starterBundleAmberGranted = true;
            var sungThisRun = Rite.CompletedVerseCount(state, _data);
            Assert.That(sungThisRun, Is.GreaterThan(0), "the rite is sung — its verses must count");

            var next = Migration.Migrate(state, _data);

            Assert.That(next.foldedVersesSung, Is.EqualTo(3 + sungThisRun),
                "the ladder's currency survives the fold");
            Assert.That(next.purchasedKithSlots, Is.EqualTo(1), "a purchase is a purchase");
            Assert.That(next.starterBundleAmberGranted, Is.True, "one bundle, one pile — across folds too");
        }

        [Test]
        public void Migrate_CarriesTheDroversHalterAndItsPony()
        {
            var state = StateWithTheRiteSung();
            state.droversHalterOwned = true;

            var next = Migration.Migrate(state, _data);

            // A redemption is a redemption (§11): the entitlement crosses the
            // fold and the pony is already standing in her lane in the new run.
            Assert.That(next.droversHalterOwned, Is.True, "the reward crosses the fold");
            var pony = Roster.OfSpecies(next, Familiar.PonySpecies);
            Assert.That(pony, Is.Not.Null, "and she comes with it");
            Assert.That(pony.stationId, Is.EqualTo(Familiar.PonyStation), "already at her lane, unasked");
        }

        [Test]
        public void Migrate_CarriesTheWayfarersPlateOnTheFoliosOwnRecord()
        {
            var state = StateWithTheRiteSung();
            state.wayfarersPlateOwned = true;
            state.insectSketches[PlayRewards.WayfarersPlateId] = 1;

            var next = Migration.Migrate(state, _data);

            // The page needs no handling of its own: recorded plates already
            // cross the fold, which is why the grant writes it into the Folio
            // instead of deriving it from the flag each run.
            Assert.That(next.wayfarersPlateOwned, Is.True, "the redemption crosses the fold");
            Assert.That(next.insectSketches.ContainsKey(PlayRewards.WayfarersPlateId), Is.True,
                "and the page crosses with the rest of the book");
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
        public void Migrate_CarriesTheBoughtNames()
        {
            var state = StateWithTheRiteSung();
            state.wardenName = "Rowan";
            state.campName = "Thistledown";

            var next = Migration.Migrate(state, _data);

            // Both names were paid for in Amber, and a fold that dropped either
            // would charge for a name already given (design §9, amended
            // 2026-08-13 — the camp's name used to fold with the run).
            Assert.That(next.wardenName, Is.EqualTo("Rowan"), "a warden does not forget their name by migrating");
            Assert.That(next.campName, Is.EqualTo("Thistledown"), "the camp is re-pitched a region north, not replaced");
            Assert.That(Camp.DisplayName(next), Is.EqualTo("Thistledown"));
        }

        [Test]
        public void Migrate_CarriesTheDeepAmberButNotItsPityClock()
        {
            var state = StateWithTheRiteSung();
            state.deepAmberFound = 2;
            state.deepAmberPityHours = 3.5;

            var next = Migration.Migrate(state, _data);

            Assert.That(next.deepAmberFound, Is.EqualTo(2), "the deep amber is journal content — it crosses the fold");
            Assert.That(next.deepAmberPityHours, Is.EqualTo(0.0).Within(Tolerance), "a fresh run starts a fresh watch");
        }

        [Test]
        public void Migrate_CarriesAmberEarnCooldowns()
        {
            var state = StateWithTheRiteSung();
            state.weeklyCacheClaimedUnixMs = 1_700_000_000_000L;
            state.adDripClaimedUnixMs = 1_700_000_001_000L;
            state.timeSkipClaimedUnixMs = 1_700_000_002_000L;
            state.clockHighWaterUnixMs = 1_700_000_003_000L;
            state.playedMs = 9_000_000L;

            var next = Migration.Migrate(state, _data);

            // A fold must not re-arm the premium-currency cooldowns — otherwise a
            // player could claim, migrate, and claim again immediately.
            Assert.That(next.weeklyCacheClaimedUnixMs, Is.EqualTo(1_700_000_000_000L), "the weekly cache does not re-arm on a fold");
            Assert.That(next.adDripClaimedUnixMs, Is.EqualTo(1_700_000_001_000L), "the drip cooldown crosses the fold");
            Assert.That(next.timeSkipClaimedUnixMs, Is.EqualTo(1_700_000_002_000L), "the time-skip cooldown crosses the fold");
            Assert.That(next.clockHighWaterUnixMs, Is.EqualTo(1_700_000_003_000L),
                "and so does the clock ratchet, or a fold would hand back every wind-forward it had just charged for");
            Assert.That(next.playedMs, Is.EqualTo(9_000_000L), "lifetime play time crosses the fold");
        }

        [Test]
        public void Migrate_TheCompendiumCrossesWhole()
        {
            var state = StateWithTheRiteSung();
            state.lifetimeGathered["berries"] = new BigDouble(123456.0);
            state.lifetimeCrafted["berry-preserve"] = 42.0;
            state.lifetimeChoice["berries"] = new BigDouble(3.0);

            var next = Migration.Migrate(state, _data);

            Assert.That(Compendium.LifetimeGathered(next, "berries").ToDouble(), Is.EqualTo(123456.0).Within(Tolerance));
            Assert.That(Compendium.LifetimeCrafted(next, "berry-preserve"), Is.EqualTo(42.0).Within(Tolerance));
            Assert.That(Compendium.LifetimeChoice(next, "berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance),
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
        public void Migrate_KeepsTheRepeatableAlmanacLine_AtItsLevel()
        {
            _data.economy.costGrowth = new EconomyData.CostGrowthData { building = 1.25, almanac = 1.25 };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "the-long-song", displayName = "The Long Song", costVerdure = 8, repeatable = true,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.05 } },
                },
            };
            var state = StateWithTheRiteSung();
            state.verdurePoints = 100.0;
            Almanac.TryBuy(state, _data, _data.almanac[0]);
            Almanac.TryBuy(state, _data, _data.almanac[0]);

            var next = Migration.Migrate(state, _data);

            // The endless sink is the reason Verdure still buys something after
            // the 99-point tree is finished — its levels are as permanent as
            // the one-off nodes, and its allocation crosses with them.
            Assert.That(Almanac.Levels(next, "the-long-song"), Is.EqualTo(2));
            Assert.That(Almanac.SpentVerdure(next, _data), Is.EqualTo(18.0).Within(Tolerance));
            Assert.That(next.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance));
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
