using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the save wire format: capture → JSON → parse → migrate → restore
    /// round-trips the run exactly (BigDouble precision included), stale saves
    /// self-correct against the current content data, and unreadable or
    /// future-versioned saves are refused rather than guessed at.
    /// </summary>
    public class SaveCodecTests
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
                // A wide-open ladder: these tests stage stationed crowds to prove
                // the round trip, not the slot clamp (which sets its own ladder).
                kith = new EconomyData.KithData { slotsBase = 6, slotsMax = 6 },
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
                new ZoneData
                {
                    id = "bramble-hedgerows",
                    order = 2,
                    resources = new List<string> { "nuts", "copper-scree" },
                    unlocks = new List<string> { "firecraft", "mining" },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 1, id = "flint-sickle",
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 4, id = "map-bramble",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "bramble-hedgerows" } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState RoundTrip(GameState state, long savedAtUnixMs = 0)
        {
            var json = SaveCodec.ToJson(SaveCodec.Capture(state, savedAtUnixMs));
            var parsed = SaveCodec.FromJson(json);
            Assert.That(parsed, Is.Not.Null);
            Assert.That(SaveCodec.TryMigrate(parsed), Is.True);
            return SaveCodec.Restore(parsed, _data);
        }

        [Test]
        public void RoundTrip_RestoresCurrenciesResourcesAndNodeProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 7.5;
            state.AddResource("berries", new BigDouble(42.25));
            state.nodes[1].masteryXp = 107.5;
            state.nodes[1].richnessLevel = 4;
            state.nodes[1].basket = new BigDouble(7.5);
            state.nodes[2].tendBurstRemaining = 1.5;
            state.amber = 33.0;
            state.weeklyCacheClaimedUnixMs = 1_700_000_000_000L;
            state.adDripClaimedUnixMs = 1_700_000_001_000L;
            state.timeSkipClaimedUnixMs = 1_700_000_002_000L;
            state.playedMs = 5_400_000L;
            TestKith.Station(state, state.nodes[1].id, 1);

            var restored = RoundTrip(state);

            Assert.That(restored.amber, Is.EqualTo(33.0).Within(Tolerance));
            Assert.That(restored.weeklyCacheClaimedUnixMs, Is.EqualTo(1_700_000_000_000L), "the weekly-cache claim time survives a save");
            Assert.That(restored.adDripClaimedUnixMs, Is.EqualTo(1_700_000_001_000L), "the drip cooldown survives a save (no relaunch bypass)");
            Assert.That(restored.timeSkipClaimedUnixMs, Is.EqualTo(1_700_000_002_000L), "the time-skip cooldown survives a save");
            Assert.That(restored.playedMs, Is.EqualTo(5_400_000L), "accumulated play time survives a save");
            Assert.That(restored.verdurePoints, Is.EqualTo(7.5).Within(Tolerance));
            Assert.That(restored.GetResource("berries").ToDouble(), Is.EqualTo(42.25).Within(Tolerance));
            Assert.That(restored.nodes[1].masteryXp, Is.EqualTo(107.5).Within(Tolerance));
            Assert.That(restored.nodes[1].richnessLevel, Is.EqualTo(4));
            Assert.That(restored.nodes[1].basket.ToDouble(), Is.EqualTo(7.5).Within(Tolerance));
            Assert.That(restored.nodes[2].tendBurstRemaining, Is.EqualTo(1.5).Within(Tolerance));
            // The kith round-trips: the gatherer keeps its post (one body per
            // post — the warden's seed node stays the warden's).
            Assert.That(Stationing.CountAssignedTo(restored, restored.nodes[1].id), Is.EqualTo(1));
            Assert.That(Stationing.CountAssignedTo(restored, restored.nodes[0].id), Is.EqualTo(0));
            Assert.That(restored.wardenPostNodeId, Is.EqualTo(restored.nodes[0].id));
        }

        [Test]
        public void RoundTrip_RestoresAWanderingWarden()
        {
            var state = GameStateFactory.NewGame(_data);
            Warden.Wander(state);

            var restored = RoundTrip(state);

            // The wander post is a valid warden post — the sentinel survives the
            // save's node-existence scrub rather than dropping the warden to camp.
            Assert.That(Warden.IsWandering(restored), Is.True);
        }

        [Test]
        public void RoundTrip_PreservesBigDoublesBeyondDoubleRange_Exactly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(1.2345678901234567, 3000);

            var restored = RoundTrip(state);

            Assert.That(restored.renown.Mantissa, Is.EqualTo(1.2345678901234567));
            Assert.That(restored.renown.Exponent, Is.EqualTo(3000L));
        }

        [Test]
        public void Capture_StampsVersionAndTimestamp()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 1234567890123L);

            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.savedAtUnixMs, Is.EqualTo(1234567890123L));
        }

        [Test]
        public void Restore_RecomputesYieldMultipliers_FromOwnedUpgrades()
        {
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("flint-sickle");

            var restored = RoundTrip(state);

            // flint-sickle doubles every foraging node; the multiplier is derived
            // on restore, never trusted from the file.
            Assert.That(restored.nodes.All(n => System.Math.Abs(n.yieldMultiplier - 2.0) < Tolerance), Is.True);
        }

        [Test]
        public void Restore_DropsNodesTheDataNoLongerHas()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.nodes.Add(new SavedNode { id = "gone-zone:gone-resource", familiarCount = 9 });

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.nodes.Count, Is.EqualTo(3));
            Assert.That(restored.nodes.Any(n => n.id == "gone-zone:gone-resource"), Is.False);
        }

        [Test]
        public void Restore_NodeTheSavePredates_GetsFreshDefaults()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.nodes.RemoveAll(n => n.id == GameStateFactory.NodeId(GameStateFactory.StartingZoneId, "fibres"));

            var restored = SaveCodec.Restore(save, _data);

            var fibres = restored.nodes.Single(n => n.resourceId == "fibres");
            Assert.That(Stationing.CountAssignedTo(restored, fibres.id), Is.EqualTo(0));
        }

        [Test]
        public void Restore_RebuildsUnlockedZoneNodes_AndOverlaysTheirProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, _data.UpgradesById["map-bramble"]);
            var nuts = state.nodes.Single(n => n.resourceId == "nuts");
            TestKith.Station(state, nuts.id, 1);

            var restored = RoundTrip(state);

            // The unlocked zone's nodes exist again, and the kith stationed
            // there round-trips through the roster.
            Assert.That(restored.nodes.Count, Is.EqualTo(5));
            Assert.That(Stationing.CountAssignedTo(restored, restored.nodes.Single(n => n.resourceId == "nuts").id),
                Is.EqualTo(1));
        }

        [Test]
        public void Restore_ZoneNeverUnlocked_StaysAbsent()
        {
            var restored = RoundTrip(GameStateFactory.NewGame(_data));

            Assert.That(restored.nodes.Count, Is.EqualTo(3));
            Assert.That(restored.nodes.Any(n => n.zoneId == "bramble-hedgerows"), Is.False);
        }

        [Test]
        public void Restore_KeepsResourceAmounts_TheDataDoesNotKnow()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("renamed-away-good", new BigDouble(5.0));

            var restored = RoundTrip(state);

            Assert.That(restored.GetResource("renamed-away-good").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresStationWorkInProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.stations.Add(new StationState
            {
                stationId = "fire", recipeId = "berry-preserve", inFlight = true, progressSeconds = 2.5,
            });

            var restored = RoundTrip(state);

            var station = restored.stations.Single();
            Assert.That(station.stationId, Is.EqualTo("fire"));
            Assert.That(station.recipeId, Is.EqualTo("berry-preserve"));
            Assert.That(station.inFlight, Is.True);
            Assert.That(station.progressSeconds, Is.EqualTo(2.5).Within(Tolerance));
        }

        [Test]
        public void TryMigrate_V2Save_GetsEmptyStations()
        {
            // v2 predates crafting — the run simply has no stations yet.
            var save = new SaveData { version = 2, stations = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.stations, Is.Empty);
        }

        [Test]
        public void RoundTrip_RestoresBoughtBuildingLevels()
        {
            var state = GameStateFactory.NewGame(_data);
            state.buildingLevels["roosts"] = 3;

            var restored = RoundTrip(state);

            Assert.That(restored.buildingLevels["roosts"], Is.EqualTo(3));
        }

        [Test]
        public void RoundTrip_RestoresSkillXp()
        {
            var state = GameStateFactory.NewGame(_data);
            state.skillXp["foraging"] = 123.5;

            var restored = RoundTrip(state);

            Assert.That(restored.skillXp["foraging"], Is.EqualTo(123.5).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresHaulTripProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.haulTripProgress = 1.25;

            var restored = RoundTrip(state);

            // A save mid-trip resumes mid-trip — the next delivery isn't
            // pushed back (or brought forward) by quitting and reloading.
            Assert.That(restored.haulTripProgress, Is.EqualTo(1.25).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresQualityPoolsRngAndPristineWindow()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddFine("berries", new BigDouble(12.5));
            state.AddPristine("berries", new BigDouble(3.0));
            state.rngState = 987654321UL;
            state.nodes[0].pristineBonusRemaining = 17.5;

            var restored = RoundTrip(state);

            Assert.That(restored.GetFine("berries").ToDouble(), Is.EqualTo(12.5).Within(Tolerance));
            Assert.That(restored.GetPristine("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            // The rng must resume exactly — a reload can't reroll fate.
            Assert.That(restored.rngState, Is.EqualTo(987654321UL));
            Assert.That(restored.nodes[0].pristineBonusRemaining, Is.EqualTo(17.5).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresSitesSketchesAndPlateEffects()
        {
            _data.zones[1].digSite = true;
            _data.upgrades[1].effects.Add(new EffectData { type = EffectType.UnlockDigSite, zone = "bramble-hedgerows" });
            _data.insects = new List<InsectData>
            {
                new InsectData
                {
                    id = "stags-herald", sketches = 3,
                    habitats = new List<string> { "bramble-hedgerows" }, rarity = 1.0,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all", value = 0.10 } },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, _data.upgrades[1]); // opens the zone and its dig site
            TestKith.Station(state, Familiar.WanderStation, 1);
            state.digSites[0].pityHours = 1.5;
            state.insectSketches["stags-herald"] = 3; // assembled

            var restored = RoundTrip(state);

            Assert.That(restored.digSites, Has.Count.EqualTo(1));
            Assert.That(restored.digSites[0].zoneId, Is.EqualTo("bramble-hedgerows"));
            Assert.That(Stationing.Wandering(restored), Is.EqualTo(1));
            Assert.That(restored.digSites[0].pityHours, Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(Insects.SketchCount(restored, "stags-herald"), Is.EqualTo(3));
            // The completed insect's +10% all yields folds into the restored
            // multipliers alongside owned upgrades.
            Assert.That(restored.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresBuiltPlanters()
        {
            var state = GameStateFactory.NewGame(_data);
            var nodeId = state.nodes[0].id;
            state.builtPlanters.Add(new BuiltPlanter { planterId = "timber-frame", targetId = nodeId });
            state.builtPlanters.Add(new BuiltPlanter { planterId = "reed-screen", targetId = "old-growth-wood" });

            var restored = RoundTrip(state);

            Assert.That(restored.builtPlanters, Has.Count.EqualTo(2));
            Assert.That(restored.HasPlanter(nodeId, "timber-frame"), Is.True);
            Assert.That(restored.HasPlanter("old-growth-wood", "reed-screen"), Is.True);
        }

        [Test]
        public void RoundTrip_RestoresRiteProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(1234.5);
            state.deedCounts["tend"] = 7;
            state.verseProgress.Add(new VerseProgressState
            {
                verseId = "verse-sunfield",
                slots =
                {
                    new SlotProgressState { delivered = 40.0, granted = false },
                    new SlotProgressState { delivered = 3.0, granted = true },
                },
            });

            var restored = RoundTrip(state);

            Assert.That(restored.renown.ToDouble(), Is.EqualTo(1234.5).Within(Tolerance));
            Assert.That(restored.deedCounts["tend"], Is.EqualTo(7));
            var verse = restored.verseProgress.Single(v => v.verseId == "verse-sunfield");
            Assert.That(verse.slots[0].delivered, Is.EqualTo(40.0).Within(Tolerance));
            Assert.That(verse.slots[0].granted, Is.False);
            // The one-shot deed grant must never re-pay after a reload.
            Assert.That(verse.slots[1].granted, Is.True);
        }

        [Test]
        public void RoundTrip_RestoresTheMigrationCount()
        {
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 3;

            Assert.That(RoundTrip(state).migrationCount, Is.EqualTo(3));
        }

        [Test]
        public void TryMigrate_V10Save_ClimbsToCurrent()
        {
            // v10 predates Migration — no camp has folded yet.
            var save = new SaveData { version = 10 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.migrationCount, Is.EqualTo(0));
        }

        [Test]
        public void RoundTrip_RestoresAlmanacOwnership()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-songs-i");

            Assert.That(RoundTrip(state).almanacNodeIds, Is.EqualTo(new[] { "old-songs-i" }));
        }

        [Test]
        public void TryMigrate_V11Save_GetsAnEmptyAlmanac()
        {
            // v11 predates the Almanac — nothing bought yet.
            var save = new SaveData { version = 11, almanacNodeIds = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.almanacNodeIds, Is.Empty);
        }

        [Test]
        public void RoundTrip_RestoresTheWornKit()
        {
            var state = GameStateFactory.NewGame(_data);
            state.gearBySlot["hands"] = "cordage-wraps";
            state.gearBySlot["camp"] = "oilskin-tarp";

            var restored = RoundTrip(state);

            Assert.That(restored.gearBySlot["hands"], Is.EqualTo("cordage-wraps"));
            Assert.That(restored.gearBySlot["camp"], Is.EqualTo("oilskin-tarp"));
        }

        [Test]
        public void TryMigrate_V12Save_GetsBareHands()
        {
            // v12 predates the warden's kit.
            var save = new SaveData { version = 12, gear = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.gear, Is.Empty);
        }

        [Test]
        public void RoundTrip_RestoresFolioFixings()
        {
            var state = GameStateFactory.NewGame(_data);
            state.fixedResources.Add("berries");

            Assert.That(RoundTrip(state).fixedResources, Is.EqualTo(new[] { "berries" }));
        }

        [Test]
        public void RoundTrip_RestoresTheWardenPost()
        {
            var state = GameStateFactory.NewGame(_data);
            state.wardenPostNodeId = state.nodes[0].id;

            Assert.That(RoundTrip(state).wardenPostNodeId, Is.EqualTo(state.nodes[0].id));
        }

        [Test]
        public void Restore_DanglingWardenPost_ClearsToCamp()
        {
            var state = GameStateFactory.NewGame(_data);
            // A content update renamed/removed the posted node — the saved id
            // matches nothing in the rebuilt list.
            state.wardenPostNodeId = "retired-zone:retired-resource";

            var restored = RoundTrip(state);

            // Cleared, not kept: a dangling post would strand the warden
            // matching no node at all — they stand at camp until re-posted.
            Assert.That(restored.wardenPostNodeId, Is.Null);
            Assert.That(Warden.IsPosted(restored, restored.nodes[0]), Is.False);
        }

        [Test]
        public void TryMigrate_V26Save_RetiresTheWatch_TheFirstWatcherWanders()
        {
            // v26 stationed watchers at "dig:{zone}" posts; the watch stopped
            // being a post — the first watcher takes the wander post (it was
            // already out watching), the rest go home to camp.
            var save = new SaveData
            {
                version = 26,
                roster = new List<SavedFamiliar>
                {
                    new SavedFamiliar { id = "fam-1", speciesId = "a", stationId = "dig:bramble-hedgerows" },
                    new SavedFamiliar { id = "fam-2", speciesId = "b", stationId = "dig:old-growth-wood" },
                    new SavedFamiliar { id = "fam-3", speciesId = "c", stationId = "trail" },
                },
            };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.roster[0].stationId, Is.EqualTo(Familiar.WanderStation));
            Assert.That(save.roster[1].stationId, Is.Null, "one body per post — the second watcher rests");
            Assert.That(save.roster[2].stationId, Is.EqualTo("trail"), "the carrier is untouched");
        }

        [Test]
        public void TryMigrate_V26Save_BareWardenPost_BecomesTheFirstNode()
        {
            // Pre-v27, a null post MEANT "the first node"; v27 makes null mean
            // "at camp" — the migration writes the old meaning in explicitly.
            var save = new SaveData
            {
                version = 26,
                nodes = new List<SavedNode> { new SavedNode { id = "sunfield-meadow:berries" } },
            };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.wardenPostNodeId, Is.EqualTo("sunfield-meadow:berries"));
        }

        [Test]
        public void Restore_TwoOnOnePost_KeepsTheDeeperKinship_AndRestsTheOther()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.roster.Add(new SavedFamiliar { id = "fam-1", speciesId = "a", stationId = "sunfield-meadow:wildflowers", kinshipXp = 10.0 });
            save.roster.Add(new SavedFamiliar { id = "fam-2", speciesId = "b", stationId = "sunfield-meadow:wildflowers", kinshipXp = 500.0 });
            save.nextFamiliarSeq = 3;

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.roster.Single(f => f.id == "fam-2").stationId, Is.EqualTo("sunfield-meadow:wildflowers"));
            Assert.That(restored.roster.Single(f => f.id == "fam-1").IsResting, Is.True,
                "one body per post — the shallower bond steps back");
        }

        [Test]
        public void Restore_AFamiliarOnTheWardensNode_SendsTheWardenToCamp()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.wardenPostNodeId = "sunfield-meadow:berries";
            save.roster.Add(new SavedFamiliar { id = "fam-1", speciesId = "a", stationId = "sunfield-meadow:berries" });
            save.nextFamiliarSeq = 2;

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.wardenPostNodeId, Is.Null,
                "the familiar's post was the explicit choice — the warden yields");
        }

        [Test]
        public void RoundTrip_NaNPoisonedValue_SavesAsZeroInsteadOfCorruptingTheFile()
        {
            var state = GameStateFactory.NewGame(_data);
            state.renown = new BigDouble(double.NaN, 0);
            state.AddResource("berries", new BigDouble(42.0));

            // Must not throw on reload — a non-finite write would fail the
            // reader's TryParse and condemn the whole save as corrupt.
            var restored = RoundTrip(state);

            Assert.That(restored.renown.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(restored.GetResource("berries").ToDouble(), Is.EqualTo(42.0).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresReadWaystones()
        {
            // The starting stone is pre-read at run birth; a later zone's
            // read stone must round-trip alongside it.
            var state = GameStateFactory.NewGame(_data);
            state.seenWaystoneZoneIds.Add("bramble-hedgerows");

            Assert.That(RoundTrip(state).seenWaystoneZoneIds,
                Is.EqualTo(new[] { GameStateFactory.StartingZoneId, "bramble-hedgerows" }));
        }

        [Test]
        public void TryMigrate_V17Save_HasReadNoWaystones()
        {
            // v17 predates waystone reveals — already-unlocked zones will show
            // their stones once, which reads as a feature.
            var save = new SaveData { version = 17, seenWaystoneZoneIds = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.seenWaystoneZoneIds, Is.Empty);
        }

        [Test]
        public void RoundTrip_RestoresAmber()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 42.0;

            Assert.That(RoundTrip(state).amber, Is.EqualTo(42.0));
        }

        [Test]
        public void TryMigrate_V16Save_HoldsNoAmber()
        {
            // v16 predates the amber system — none held.
            var save = new SaveData { version = 16 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.amber, Is.EqualTo(0.0));
        }

        [Test]
        public void RoundTrip_RestoresTheCompendium()
        {
            var state = GameStateFactory.NewGame(_data);
            state.lifetimeGathered["berries"] = new BigDouble(1e42);
            state.lifetimeCrafted["berry-preserve"] = 7.0;
            state.lifetimePristine["berries"] = new BigDouble(2.0);

            var restored = RoundTrip(state);

            Assert.That(restored.lifetimeGathered["berries"].ToDouble(), Is.EqualTo(1e42).Within(1e33));
            Assert.That(restored.lifetimeCrafted["berry-preserve"], Is.EqualTo(7.0));
            Assert.That(restored.lifetimePristine["berries"].ToDouble(), Is.EqualTo(2.0));
        }

        [Test]
        public void TryMigrate_V15Save_StartsTheRecordEmpty()
        {
            // v15 predates the Compendium — the lifetime record starts here.
            var save = new SaveData { version = 15, lifetimeGathered = null, lifetimeCrafted = null, lifetimePristine = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.lifetimeGathered, Is.Empty);
            Assert.That(save.lifetimeCrafted, Is.Empty);
            Assert.That(save.lifetimePristine, Is.Empty);
        }

        [Test]
        public void TryMigrate_V14Save_HasNoWardenPost()
        {
            // v14 predates bonded familiars; earned bonds are derived, never
            // stored, so only the post needs a default.
            var save = new SaveData { version = 14, bondedPostNodeId = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.wardenPostNodeId, Is.Null);
        }

        [Test]
        public void TryMigrate_V18Save_CarriesTheBondedPostToTheWarden()
        {
            // v18's "bonded post" became the warden's post — same meaning
            // (the last-tended node), wider role.
            var save = new SaveData { version = 18, bondedPostNodeId = "sunfield-meadow:berries" };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.wardenPostNodeId, Is.EqualTo("sunfield-meadow:berries"));
        }

        [Test]
        public void TryMigrate_V13Save_GetsAnEmptyFolio()
        {
            // v13 predates the Museum/Folio — nothing fixed yet.
            var save = new SaveData { version = 13, donatedResources = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.fixedResources, Is.Empty);
        }

        [Test]
        public void TryMigrate_V20Save_CarriesDonationsToFixedResources()
        {
            // v20's Museum "donatedResources" becomes the Folio's "fixedResources".
            var save = new SaveData { version = 20, donatedResources = new List<string> { "berries", "nuts" } };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.fixedResources, Is.EqualTo(new[] { "berries", "nuts" }));
        }

        [Test]
        public void TryMigrate_V9Save_GetsEmptyRiteProgress()
        {
            // v9 predates the Rite runtime — nothing offered yet.
            var save = new SaveData { version = 9, deedCounts = null, verseProgress = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.deedCounts, Is.Empty);
            Assert.That(save.verseProgress, Is.Empty);
        }

        [Test]
        public void TryMigrate_V8Save_GetsEmptyObservation()
        {
            // v8 predates excavation — nothing dug yet.
            var save = new SaveData { version = 8, digSites = null, insectSketches = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.digSites, Is.Empty);
            Assert.That(save.insectSketches, Is.Empty);
        }

        [Test]
        public void TryMigrate_V7Save_GetsEmptyQualityPools()
        {
            // v7 predates quality rolls — nothing found yet.
            var save = new SaveData { version = 7, fineResources = null, pristineResources = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.fineResources, Is.Empty);
            Assert.That(save.pristineResources, Is.Empty);
        }

        [Test]
        public void Restore_PreQualitySaveWithoutRngState_KeepsAFreshSeed()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.rngState = 0UL; // what any pre-v8 save deserialises to

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.rngState, Is.Not.EqualTo(0UL), "zero is xorshift's fixed point — restore must reseed");
        }

        [Test]
        public void TryMigrate_V6Save_StartsAFreshTrip()
        {
            // v6 predates discrete hauling — no trip was in progress.
            var save = new SaveData { version = 6 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.haulTripProgress, Is.EqualTo(0.0));
        }

        [Test]
        public void TryMigrate_V5Save_ClimbsToCurrent()
        {
            // v5 nodes carried a never-earned masteryLevel — nothing to carry.
            var save = new SaveData { version = 5 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
        }

        [Test]
        public void TryMigrate_V4Save_GetsEmptySkillXp()
        {
            // v4 predates skill XP — nothing earned yet.
            var save = new SaveData { version = 4, skillXp = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.skillXp, Is.Empty);
        }

        [Test]
        public void TryMigrate_V3Save_GetsEmptyBuildingLevels()
        {
            // v3 predates camp buildings — nothing bought yet.
            var save = new SaveData { version = 3, buildingLevels = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.buildingLevels, Is.Empty);
        }

        [Test]
        public void FromJson_GarbageOrEmpty_ReturnsNull()
        {
            Assert.That(SaveCodec.FromJson("not json {{{"), Is.Null);
            Assert.That(SaveCodec.FromJson(""), Is.Null);
            Assert.That(SaveCodec.FromJson(null), Is.Null);
        }

        [Test]
        public void FromJson_MalformedBigDoubleField_ReturnsNullInsteadOfThrowing()
        {
            // Bit-rot inside a BigDouble string must land on the corrupt-file
            // path, not crash the launch: a parse failure here used to escape
            // as FormatException past the JsonException catch.
            Assert.That(SaveCodec.FromJson("{ \"version\": 6, \"renown\": \"1.5e\" }"), Is.Null);
            Assert.That(SaveCodec.FromJson("{ \"version\": 6, \"renown\": \"1.x5e42\" }"), Is.Null);
            Assert.That(SaveCodec.FromJson("{ \"version\": 6, \"renown\": 100 }"), Is.Null);
        }

        [Test]
        public void Restore_ZoneUnlockedSinceTheSave_MaterialisesItsNodesUnstaffed()
        {
            // A data update granted unlockZone to an upgrade the save already
            // owns: the save has none of the zone's nodes. Restore rebuilds them
            // — with no regional kith seed (the kith is a roster, design §2/§4).
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.purchasedUpgradeIds.Add("map-bramble");

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.nodes.Any(n => n.zoneId == "bramble-hedgerows"), Is.True);
            Assert.That(Stationing.CountAssignedTo(restored, restored.nodes.Single(n => n.resourceId == "nuts").id),
                Is.EqualTo(0));
        }

        [Test]
        public void TryMigrate_V19Save_RebuildsAnonymousCountsIntoARoster()
        {
            // v19 predates the kith roster: familiars were per-node/per-camp
            // counts. The v19→v20 step turns each into an individual.
            var save = new SaveData
            {
                version = 19,
                carrierCount = 1,
                nodes = new List<SavedNode> { new SavedNode { id = "sunfield-meadow:berries", familiarCount = 2 } },
            };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.roster.Count, Is.EqualTo(3), "2 gatherers + 1 carrier become three roster familiars");
            Assert.That(save.roster.FindAll(f => f.stationId == "sunfield-meadow:berries").Count, Is.EqualTo(2));
            Assert.That(save.roster.FindAll(f => f.stationId == "trail").Count, Is.EqualTo(1));
        }

        [Test]
        public void Restore_DuplicateSpecies_KeepsOnePerSpecies_BondedAndDeepestKinshipFirst()
        {
            // The roster is a collection now — one familiar per species, ever.
            // A long-lived save run through the v19→v20 rebuild could mint one
            // vole per anonymous head; Restore keeps each species' best (bonded
            // first, then the deepest Kinship) and the rest slip back into the
            // grass.
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.roster.Clear();
            for (var i = 1; i <= 20; i++)
            {
                save.roster.Add(new SavedFamiliar
                {
                    id = "fam-" + i,
                    speciesId = "meadow-vole",
                    kinshipXp = i == 7 ? 500.0 : 0.0,
                    bonded = i == 15,
                    bondId = i == 15 ? "sootwing" : null,
                });
            }

            save.nextFamiliarSeq = 21;

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.roster.Count, Is.EqualTo(1), "one vole, ever");
            Assert.That(restored.roster[0].bondId, Is.EqualTo("sootwing"), "the bonded companion is the one kept");
        }

        [Test]
        public void Restore_MoreStationedThanSlots_RestsTheExtras_BondedKeepingTheirPosts()
        {
            // Slots cap who holds a post, not who belongs. A save from a wider
            // ladder restores with the extras resting at camp — restore against
            // the authored one-slot ladder, not the fixture's wide-open one.
            _data.economy.kith = new EconomyData.KithData
            {
                slotsBase = 1,
                slotsMax = 6,
                verseMilestones = new List<int> { 2, 5, 10 },
            };
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.roster.Clear();
            save.roster.Add(new SavedFamiliar { id = "fam-1", speciesId = "meadow-vole", stationId = "sunfield-meadow:berries" });
            save.roster.Add(new SavedFamiliar { id = "fam-2", speciesId = "pack-raven", stationId = "trail", bonded = true, bondId = "sootwing" });
            save.nextFamiliarSeq = 3;

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(Kith.Slots(restored, _data), Is.EqualTo(1), "no verses sung, nothing purchased");
            Assert.That(Kith.Walking(restored), Is.EqualTo(1), "the extras rest at camp");
            var raven = restored.roster.Single(f => f.speciesId == "pack-raven");
            Assert.That(raven.stationId, Is.EqualTo("trail"), "the bonded companion keeps its post");
            Assert.That(restored.roster.Single(f => f.speciesId == "meadow-vole").IsResting, Is.True);
        }

        [Test]
        public void TryMigrate_FutureVersion_IsRefused()
        {
            var save = new SaveData { version = SaveCodec.CurrentVersion + 1 };

            Assert.That(SaveCodec.TryMigrate(save), Is.False);
        }

        [Test]
        public void TryMigrate_V1Save_GrantsTheSeedCarrier()
        {
            // A v1 save predates carriers entirely — migration must hand the
            // run its regional seed or nothing ever reaches camp again.
            var save = new SaveData { version = 1 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.carrierCount, Is.EqualTo(1));
        }

        [Test]
        public void TryMigrate_MultiZoneV1Save_GrantsACarrierPerZone()
        {
            // The live path grants one seed carrier per zone opened — a v1
            // save that had walked two zones must not come back with one.
            var save = new SaveData
            {
                version = 1,
                nodes = new List<SavedNode>
                {
                    new SavedNode { id = "sunfield-meadow:berries" },
                    new SavedNode { id = "sunfield-meadow:wildflowers" },
                    new SavedNode { id = "bramble-hedgerows:nuts" },
                },
            };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.carrierCount, Is.EqualTo(2));
        }

        [Test]
        public void TryMigrate_OlderVersion_ClimbsTheWholeLadder()
        {
            var save = new SaveData { version = 0 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.carrierCount, Is.EqualTo(1));
        }
    }
}
