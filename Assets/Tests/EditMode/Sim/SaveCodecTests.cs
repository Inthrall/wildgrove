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
        public void RoundTrip_RestoresTheDroversHalterAndStandsThePonyInHerLane()
        {
            var state = GameStateFactory.NewGame(_data);
            state.droversHalterOwned = true;
            Roster.SyncDroversHalter(state, _data);

            var restored = RoundTrip(state);

            Assert.That(restored.droversHalterOwned, Is.True, "the redemption survives a save");
            var pony = Roster.OfSpecies(restored, Familiar.PonySpecies);
            Assert.That(pony, Is.Not.Null, "and the pony with it");
            Assert.That(pony.stationId, Is.EqualTo(Familiar.PonyStation),
                "her lane is a valid station, so the codec must not clear it to resting");
        }

        [Test]
        public void RoundTrip_AZoneOpenedByASungVerse_KeepsThatZonesNodeProgress()
        {
            // A trail opened by the Rite rather than by a map rung is the
            // awkward case for restore: node rows are matched by id against the
            // baseline node set, so if the zones are synced before the verse
            // progress is in hand this zone's nodes don't exist yet, and a whole
            // zone's mastery, richness and baskets go quietly missing.
            var verse = new RiteVerseData
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
                    new RiteData { id = "first-rite", migration = 0, verses = { verse } },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 10);
            Rite.DeliverResource(state, _data, verse, 0);

            var bramble = state.nodes.Find(node => node.zoneId == "bramble-hedgerows");
            Assert.That(bramble, Is.Not.Null, "the verse opened the trail");
            bramble.masteryXp = 250.0;
            bramble.richnessLevel = 3;
            bramble.basket = new BigDouble(9.5);

            var restored = RoundTrip(state);

            var restoredBramble = restored.nodes.Find(node => node.id == bramble.id);
            Assert.That(restoredBramble, Is.Not.Null, "the zone comes back with no map ever having been bought");
            Assert.That(restoredBramble.masteryXp, Is.EqualTo(250.0).Within(Tolerance));
            Assert.That(restoredBramble.richnessLevel, Is.EqualTo(3));
            Assert.That(restoredBramble.basket.ToDouble(), Is.EqualTo(9.5).Within(Tolerance));
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
            state.timeSkipBudgetHours = 12.5;
            state.timeSkipBudgetStampUnixMs = 1_700_000_003_000L;
            state.playedMs = 5_400_000L;
            TestKith.Station(state, state.nodes[1].id, 1);

            var restored = RoundTrip(state);

            Assert.That(restored.amber, Is.EqualTo(33.0).Within(Tolerance));
            Assert.That(restored.weeklyCacheClaimedUnixMs, Is.EqualTo(1_700_000_000_000L), "the weekly-cache claim time survives a save");
            Assert.That(restored.adDripClaimedUnixMs, Is.EqualTo(1_700_000_001_000L), "the drip cooldown survives a save (no relaunch bypass)");
            Assert.That(restored.timeSkipClaimedUnixMs, Is.EqualTo(1_700_000_002_000L), "the time-skip cooldown survives a save");
            Assert.That(restored.timeSkipBudgetHours, Is.EqualTo(12.5).Within(Tolerance), "the paid-skip budget survives a save (no relaunch refill)");
            Assert.That(restored.timeSkipBudgetStampUnixMs, Is.EqualTo(1_700_000_003_000L), "and its stamp with it");
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

        /// <summary>
        /// Open an observation site at the second zone and buy the map that
        /// opens it — the watch tests need a real site, and the shared fixture
        /// deliberately has none (a site would put a watch post into every
        /// posting assertion in the file).
        /// </summary>
        private GameState StateWithASite()
        {
            _data.zones[1].digSite = true;
            _data.upgrades[1].effects.Add(new EffectData
            {
                type = EffectType.UnlockDigSite, zone = "bramble-hedgerows",
            });
            var state = GameStateFactory.NewGame(_data);
            Assert.That(Upgrades.TryPurchase(state, _data, _data.upgrades[1]), Is.True);
            Assert.That(state.digSites, Has.Count.EqualTo(1), "the site the watch tests stand at");
            return state;
        }

        [Test]
        public void RoundTrip_RestoresASketchingWarden()
        {
            var state = StateWithASite();
            Warden.Sketch(state, "bramble-hedgerows");

            var restored = RoundTrip(state);

            // A watch post is a valid warden post — it survives the save's
            // node-existence scrub rather than dropping the warden to camp.
            Assert.That(Warden.IsSketchingAt(restored, "bramble-hedgerows"), Is.True);
        }

        [Test]
        public void RoundTrip_ASketchPostAtASiteThisBuildDoesNotOpen_RestsTheSketcher()
        {
            // The mirror of the dangling-node rule: content retuned under a save
            // can take a site away, and a body left watching a place that isn't
            // there is a walked slot doing nothing, with nothing to say so.
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, Familiar.SketchStation("mistfen-marsh"), 1);

            var restored = RoundTrip(state);

            Assert.That(restored.roster[0].IsResting, Is.True, "the watcher comes home");
        }

        [Test]
        public void TryMigrate_V52_ThenRestore_PutsARoamingSketcherDownAtTheFirstSite()
        {
            // v53 made the watch a place. The roaming post that watched every
            // site is gone, and its holder must keep working rather than be
            // quietly rested — which is what a bare station id would do, since
            // "wander" resolves to no node and no site.
            var state = StateWithASite();
            TestKith.Station(state, Familiar.LegacyWanderStation, 1);
            var save = SaveCodec.Capture(state, 0);
            save.version = 52;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.roster[0].stationId,
                Is.EqualTo(Familiar.SketchStation("bramble-hedgerows")),
                "the wanderer keeps a watch — the first site's");
        }

        [Test]
        public void TryMigrate_V52_ThenRestore_PutsARoamingWardenDownAtTheFirstSite()
        {
            var state = StateWithASite();
            state.wardenPostNodeId = Familiar.LegacyWanderStation;
            var save = SaveCodec.Capture(state, 0);
            save.version = 52;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            var restored = SaveCodec.Restore(save, _data);

            Assert.That(Warden.IsSketchingAt(restored, "bramble-hedgerows"), Is.True);
        }

        [Test]
        public void TryMigrate_V52_ThenRestore_WithNoSiteOpen_SendsARoamingSketcherHome()
        {
            // A save whose watcher roamed a map with no site on it (hand-built,
            // or a build whose sites all moved) has nowhere to put them down, and
            // camp is the honest answer — never a post id that resolves to
            // nothing.
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, Familiar.LegacyWanderStation, 1);
            state.wardenPostNodeId = Familiar.LegacyWanderStation;
            var save = SaveCodec.Capture(state, 0);
            save.version = 52;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.roster[0].IsResting, Is.True, "the companion rests");
            Assert.That(Warden.PostNodeId(restored), Is.Null, "and the warden stands at camp");
        }

        [Test]
        public void TryMigrate_V53_MovesEverySitePostOntoTheSketchingId()
        {
            // v54 renamed the work and the id with it. A v53 save writes its
            // site posts as "dig:{zone}"; nothing in this build matches that
            // prefix any more, so a save that climbed the rung without being
            // rewritten would rest every sketcher on sight.
            var state = StateWithASite();
            var save = SaveCodec.Capture(state, 0);
            save.roster.Add(new SavedFamiliar
            {
                id = "fam-legacy",
                speciesId = "test-species-legacy",
                stationId = Familiar.LegacyWatchStationPrefix + "bramble-hedgerows",
            });
            save.wardenPostNodeId = Familiar.LegacyWatchStationPrefix + "bramble-hedgerows";
            save.version = 53;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);

            Assert.That(save.roster[save.roster.Count - 1].stationId,
                Is.EqualTo(Familiar.SketchStation("bramble-hedgerows")),
                "the companion's post is rewritten");
            Assert.That(save.wardenPostNodeId,
                Is.EqualTo(Familiar.SketchStation("bramble-hedgerows")),
                "and so is the warden's");
        }

        [Test]
        public void TryMigrate_V53_LeavesEveryOtherPostIdAlone()
        {
            // The rung renames one thing. A node id, the pony's lane and the
            // retired roaming post all pass through — a migration that started
            // inventing second meanings is one nobody could read years later.
            var state = StateWithASite();
            var nodeId = state.nodes[0].id;
            TestKith.Station(state, nodeId, 1);
            TestKith.Station(state, Familiar.LegacyWanderStation, 1);
            state.wardenPostNodeId = nodeId;
            var save = SaveCodec.Capture(state, 0);
            save.version = 53;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);

            Assert.That(save.roster[0].stationId, Is.EqualTo(nodeId), "a node id is untouched");
            Assert.That(save.roster[1].stationId, Is.EqualTo(Familiar.LegacyWanderStation),
                "and so is the retired roaming post — Restore is what puts it down");
            Assert.That(save.wardenPostNodeId, Is.EqualTo(nodeId), "and the warden's node");
        }

        [Test]
        public void TryMigrate_V53_LeavesTheCraftQueueAlone()
        {
            // SavedStation.stationId is a workbench, not a place a body stands.
            // The two fields share a name and nothing else, and rewriting the
            // wrong one would empty a player's queue on load without erroring —
            // so this pins the one confusion the rung could make.
            var state = StateWithASite();
            state.stations.Add(new StationState { stationId = "dig:not-a-post", recipeId = "test-recipe" });
            var save = SaveCodec.Capture(state, 0);
            save.version = 53;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);

            Assert.That(save.stations[0].stationId, Is.EqualTo("dig:not-a-post"),
                "a craft station keeps its id even when it reads like a site post");
        }

        [Test]
        public void TryMigrate_V53_ThenRestore_KeepsTheSketcherAtTheirOwnSite()
        {
            // End to end: the rewritten id has to be one StationValid accepts,
            // or the rung would hand Restore a post it rests anyway.
            var state = StateWithASite();
            var save = SaveCodec.Capture(state, 0);
            save.roster.Add(new SavedFamiliar
            {
                id = "fam-legacy",
                speciesId = "test-species-legacy",
                stationId = Familiar.LegacyWatchStationPrefix + "bramble-hedgerows",
            });
            save.version = 53;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.roster[restored.roster.Count - 1].IsSketchingAt("bramble-hedgerows"),
                Is.True, "the sketcher keeps the site they were drawing at");
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
            save.nodes.Add(new SavedNode { id = "gone-zone:gone-resource", masteryXp = 400.0 });

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
        public void RoundTrip_RestoresTheFinalWaystonesAndTheirFold()
        {
            var state = GameStateFactory.NewGame(_data);
            state.finalWaystonesRead = 2;
            state.finalWaystoneLastFold = 6;

            var restored = RoundTrip(state);

            Assert.That(restored.finalWaystonesRead, Is.EqualTo(2));
            Assert.That(restored.finalWaystoneLastFold, Is.EqualTo(6),
                "without the fold the chain would hand over another stone the moment the save reloaded");
        }

        [Test]
        public void Restore_WithNoFinalWaystoneRead_HasNeverTakenOne()
        {
            var restored = RoundTrip(GameStateFactory.NewGame(_data));

            Assert.That(restored.finalWaystonesRead, Is.EqualTo(0));
            Assert.That(restored.finalWaystoneLastFold, Is.EqualTo(-1),
                "a zeroed stamp would read as 'one was taken on fold 0' and hold the first stone back a whole fold");
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
        public void RoundTrip_RestoresDeliveryProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.deliveryProgress = 1.25;

            var restored = RoundTrip(state);

            // A save mid-cadence resumes mid-cadence — the next delivery isn't
            // pushed back (or brought forward) by quitting and reloading.
            Assert.That(restored.deliveryProgress, Is.EqualTo(1.25).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresQualityPoolsRngAndChoiceWindow()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddDecent("berries", new BigDouble(12.5));
            state.AddChoice("berries", new BigDouble(3.0));
            state.rngState = 987654321UL;
            state.nodes[0].choiceBonusRemaining = 17.5;

            var restored = RoundTrip(state);

            Assert.That(restored.GetDecent("berries").ToDouble(), Is.EqualTo(12.5).Within(Tolerance));
            Assert.That(restored.GetChoice("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            // The rng must resume exactly — a reload can't reroll fate.
            Assert.That(restored.rngState, Is.EqualTo(987654321UL));
            Assert.That(restored.nodes[0].choiceBonusRemaining, Is.EqualTo(17.5).Within(Tolerance));
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
            TestKith.Station(state, Familiar.SketchStation("bramble-hedgerows"), 1);
            state.digSites[0].pityHours = 1.5;
            state.insectSketches["stags-herald"] = 3; // assembled

            var restored = RoundTrip(state);

            Assert.That(restored.digSites, Has.Count.EqualTo(1));
            Assert.That(restored.digSites[0].zoneId, Is.EqualTo("bramble-hedgerows"));
            Assert.That(Stationing.SketchersAt(restored, "bramble-hedgerows"), Is.EqualTo(1));
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
                    new SlotProgressState { delivered = 3.0, granted = true, deedBaseline = 4.0, deedBaselineSet = true },
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
            // Nor may a reload re-stamp the baseline: at 7 lifetime tends against
            // a baseline of 4, the slot has answered 3 — losing the line would
            // hand the verse all seven.
            Assert.That(verse.slots[1].deedBaseline, Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(verse.slots[1].deedBaselineSet, Is.True);
        }

        [Test]
        public void RoundTrip_RestoresTheMigrationCount()
        {
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 3;

            Assert.That(RoundTrip(state).migrationCount, Is.EqualTo(3));
        }

        [Test]
        public void RoundTrip_RestoresAlmanacOwnership()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-songs-i");

            Assert.That(RoundTrip(state).almanacNodeIds, Is.EqualTo(new[] { "old-songs-i" }));
        }

        [Test]
        public void RoundTrip_RestoresTheWayfarersPlateAndItsPage()
        {
            var state = GameStateFactory.NewGame(_data);
            PlayRewards.ApplyWayfarersPlate(state, _data, true);

            var restored = RoundTrip(state);

            Assert.That(restored.wayfarersPlateOwned, Is.True, "the redemption survives a save");
            Assert.That(Insects.SketchCount(restored, PlayRewards.WayfarersPlateId), Is.EqualTo(1),
                "and so does the page, which is where the plate actually lives");
        }

        [Test]
        public void RoundTrip_RestoresTheEndlessAlmanacLine()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacLevels["the-long-song"] = 3;

            var restored = RoundTrip(state);

            Assert.That(Almanac.Levels(restored, "the-long-song"), Is.EqualTo(3));
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
        public void RoundTrip_RestoresTheKitBag()
        {
            var state = GameStateFactory.NewGame(_data);
            state.gearBySlot["camp"] = "oilskin-tarp";
            state.gearCrafted.Add("oilskin-tarp");
            // Made, then displaced from the camp slot — the bag is the only
            // record that it's paid for.
            state.gearCrafted.Add("pitch-torch");

            var restored = RoundTrip(state);

            Assert.That(restored.gearCrafted, Is.EquivalentTo(new[] { "oilskin-tarp", "pitch-torch" }));
        }

        [Test]
        public void Restore_WornPieceMissingFromTheBag_IsAddedBack()
        {
            var state = GameStateFactory.NewGame(_data);
            state.gearBySlot["hands"] = "cordage-wraps";

            // A worn piece was certainly made, whatever the bag says.
            var restored = RoundTrip(state);

            Assert.That(restored.gearCrafted, Is.EquivalentTo(new[] { "cordage-wraps" }));
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
        public void RoundTrip_RestoresAmber()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 42.0;

            Assert.That(RoundTrip(state).amber, Is.EqualTo(42.0));
        }

        [Test]
        public void RoundTrip_RestoresTheCompendium()
        {
            var state = GameStateFactory.NewGame(_data);
            state.lifetimeGathered["berries"] = new BigDouble(1e42);
            state.lifetimeCrafted["berry-preserve"] = 7.0;
            state.lifetimeChoice["berries"] = new BigDouble(2.0);

            var restored = RoundTrip(state);

            Assert.That(restored.lifetimeGathered["berries"].ToDouble(), Is.EqualTo(1e42).Within(1e33));
            Assert.That(restored.lifetimeCrafted["berry-preserve"], Is.EqualTo(7.0));
            Assert.That(restored.lifetimeChoice["berries"].ToDouble(), Is.EqualTo(2.0));
        }

        [Test]
        public void Restore_SaveWithoutRngState_KeepsAFreshSeed()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.rngState = 0UL; // what a save missing the field deserialises to

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.rngState, Is.Not.EqualTo(0UL), "zero is xorshift's fixed point — restore must reseed");
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
            Assert.That(SaveCodec.FromJson("{ \"version\": 42, \"renown\": \"1.5e\" }"), Is.Null);
            Assert.That(SaveCodec.FromJson("{ \"version\": 42, \"renown\": \"1.x5e42\" }"), Is.Null);
            Assert.That(SaveCodec.FromJson("{ \"version\": 42, \"renown\": 100 }"), Is.Null);
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

        /// <summary>
        /// Narrow the fixture's deliberately wide-open ladder to the authored
        /// shape — one place to start, three named verses, two purchasable.
        /// <para>
        /// Any test that asserts on <see cref="Kith.Slots"/> MUST call this. The
        /// fixture opens with slotsBase == slotsMax == 6 on purpose (see SetUp:
        /// the round-trip tests stage stationed crowds and want no clamp), which
        /// pins Slots at its ceiling and answers 6 to every question — so a
        /// ladder assertion left on the fixture's data passes or fails for
        /// reasons that have nothing to do with the ladder.
        /// </para>
        /// </summary>
        private void NarrowToTheNamedVerseLadder()
        {
            _data.economy.kith = new EconomyData.KithData
            {
                slotsBase = 1,
                slotsMax = 6,
                slotVerseZones = new List<string> { "hedgerow", "marsh", "crags" },
            };
        }

        [Test]
        public void Restore_MoreStationedThanSlots_RestsTheExtras_BondedKeepingTheirPosts()
        {
            // Slots cap who holds a post, not who belongs. A save from a wider
            // ladder restores with the extras resting at camp — restore against
            // the authored one-slot ladder, not the fixture's wide-open one.
            NarrowToTheNamedVerseLadder();
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.roster.Clear();
            save.roster.Add(new SavedFamiliar { id = "fam-1", speciesId = "meadow-vole", stationId = "sunfield-meadow:berries" });
            save.roster.Add(new SavedFamiliar { id = "fam-2", speciesId = "pack-raven", stationId = "sunfield-meadow:wildflowers", bonded = true, bondId = "sootwing" });
            save.nextFamiliarSeq = 3;

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(Kith.Slots(restored, _data), Is.EqualTo(1), "no verses sung, nothing purchased");
            Assert.That(Kith.Walking(restored), Is.EqualTo(1), "the extras rest at camp");
            var raven = restored.roster.Single(f => f.speciesId == "pack-raven");
            Assert.That(raven.stationId, Is.EqualTo("sunfield-meadow:wildflowers"), "the bonded companion keeps its post");
            Assert.That(restored.roster.Single(f => f.speciesId == "meadow-vole").IsResting, Is.True);
        }

        [Test]
        public void RoundTrip_KeepsTheVersesThatOpenedThePlaces()
        {
            NarrowToTheNamedVerseLadder();
            var state = GameStateFactory.NewGame(_data);
            state.sungVerseZones.Add("hedgerow");
            state.sungVerseZones.Add("marsh");

            var restored = RoundTrip(state);

            Assert.That(restored.sungVerseZones, Is.EquivalentTo(new[] { "hedgerow", "marsh" }));
            Assert.That(Kith.Slots(restored, _data), Is.EqualTo(3), "and the places they opened stand");
        }

        [Test]
        public void Restore_DedupesAndDropsBlanksFromTheSungVerses()
        {
            // The ladder counts membership, so a duplicate would be a free
            // place and a blank a rung nothing can ever match.
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.sungVerseZones = new List<string> { "hedgerow", "hedgerow", " ", null, "marsh" };

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.sungVerseZones, Is.EquivalentTo(new[] { "hedgerow", "marsh" }));
        }

        [Test]
        public void Restore_KeepsASungVerseForAZoneThisBuildNoLongerHas()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.sungVerseZones = new List<string> { "a-trail-that-was-renamed" };

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.sungVerseZones, Is.EquivalentTo(new[] { "a-trail-that-was-renamed" }),
                "a verse sung is never unsung - content renamed underneath a save is not the warden's doing");
        }

        [Test]
        public void TryMigrate_V51_KeepsThePlacesTheOldTallyHadEarned()
        {
            // v52 moved the ladder off the lifetime tally onto named verses. A
            // v51 save cannot say WHICH verses it sang, so the rung records how
            // many places were standing and Kith.Slots floors on that.
            NarrowToTheNamedVerseLadder();
            var save = new SaveData { version = 51, foldedVersesSung = 5 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.grandfatheredKithSlots, Is.EqualTo(2),
                "5 lifetime verses cleared the old 2 and 5 milestones, so two earned places were standing");
            Assert.That(save.sungVerseZones, Is.Empty,
                "empty says \"unknown\", which is true - it must not invent three verse names");

            var restored = SaveCodec.Restore(save, _data);
            Assert.That(Kith.Slots(restored, _data), Is.EqualTo(3), "base one plus the two it came in with");
        }

        [Test]
        public void TryMigrate_V51_WithNothingEarned_GrandfathersNothing()
        {
            var save = new SaveData { version = 51, foldedVersesSung = 1 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.grandfatheredKithSlots, Is.EqualTo(0),
                "one verse never cleared the old first milestone, so there is no place to keep");
        }

        [Test]
        public void TryMigrate_FutureVersion_IsRefused()
        {
            var save = new SaveData { version = SaveCodec.CurrentVersion + 1 };

            Assert.That(SaveCodec.TryMigrate(save), Is.False);
        }

        [Test]
        public void TryMigrate_V42_ClimbsToCurrentWithNoClockMark()
        {
            // The ladder's first real rung. A v42 save predates the clock
            // ratchet, so it must arrive with the mark at zero — "this run has
            // never been told the time" — rather than with one invented for it,
            // which would freeze its cooldowns until real time passed the
            // invention.
            var save = new SaveData { version = 42 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.clockHighWaterUnixMs, Is.Zero);
        }

        [Test]
        public void Restore_V42Save_LeavesTheClockRatchetUnset()
        {
            var save = new SaveData { version = 42 };
            Assert.That(SaveCodec.TryMigrate(save), Is.True);

            var state = SaveCodec.Restore(save, _data);

            Assert.That(state.clockHighWaterUnixMs, Is.Zero,
                "so the first reading on the new build sets the mark honestly, from the device");
        }

        [Test]
        public void Capture_RoundTripsTheClockRatchet()
        {
            var state = GameStateFactory.NewGame(_data);
            state.clockHighWaterUnixMs = 1_770_000_000_000L;

            var restored = RoundTrip(state);

            Assert.That(restored.clockHighWaterUnixMs, Is.EqualTo(1_770_000_000_000L),
                "a mark that didn't survive the save would reset every launch, which is no guard at all");
        }

        [Test]
        public void Restore_NegativeClockRatchet_ReadsAsNeverSet()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0L);
            save.clockHighWaterUnixMs = -5L;

            Assert.That(SaveCodec.Restore(save, _data).clockHighWaterUnixMs, Is.Zero);
        }

        [Test]
        public void TryMigrate_V43_ClimbsToCurrentUnnamed()
        {
            // v44 added the warden's bought name. A save written before it
            // belongs to a warden who was never named, and null is exactly how
            // an un-renamed v44 run reads — so the migration invents nothing.
            var save = new SaveData { version = 43 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.wardenName, Is.Null);
        }

        [Test]
        public void Restore_V43Save_ReadsAsTheAnonymousWarden()
        {
            var save = new SaveData { version = 43 };
            Assert.That(SaveCodec.TryMigrate(save), Is.True);

            var state = SaveCodec.Restore(save, _data);

            Assert.That(Warden.DisplayName(state), Is.EqualTo(Warden.Anonymous),
                "an older save must read exactly as it did before naming existed");
        }

        [Test]
        public void Capture_RoundTripsTheWardenName()
        {
            var state = GameStateFactory.NewGame(_data);
            state.wardenName = "Rowan";

            var restored = RoundTrip(state);

            Assert.That(restored.wardenName, Is.EqualTo("Rowan"),
                "a name that didn't survive the save would be bought again every launch");
        }

        [Test]
        public void Restore_BlankWardenName_ReadsAsNoNameRatherThanAWardenCalledNothing()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0L);
            save.wardenName = "   ";

            var state = SaveCodec.Restore(save, _data);

            Assert.That(state.wardenName, Is.Null);
            Assert.That(Warden.DisplayName(state), Is.EqualTo(Warden.Anonymous));
        }

        [Test]
        public void Restore_PaddedWardenName_ComesBackTrimmed()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0L);
            save.wardenName = "  Rowan  ";

            Assert.That(SaveCodec.Restore(save, _data).wardenName, Is.EqualTo("Rowan"));
        }

        [Test]
        public void TryMigrate_V44_ClimbsToCurrentWithAnUnnamedCamp()
        {
            // v45 added the camp's bought name. A save written before it
            // describes a camp that was never named, and null is exactly how
            // an un-named v45 run reads — so the migration invents nothing.
            var save = new SaveData { version = 44 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.campName, Is.Null);
        }

        [Test]
        public void Capture_RoundTripsTheCampName()
        {
            var state = GameStateFactory.NewGame(_data);
            state.campName = "Thistledown";

            var restored = RoundTrip(state);

            Assert.That(restored.campName, Is.EqualTo("Thistledown"),
                "a name that didn't survive the save would be bought again every launch");
        }

        [Test]
        public void Capture_RoundTripsTheWheel()
        {
            var state = GameStateFactory.NewGame(_data);
            state.hemisphere = Wheel.HemisphereSouth;
            state.sabbatClaims.Add(new SabbatClaim
            {
                sabbatId = "beltane",
                year = 2026,
                hemisphere = Wheel.HemisphereSouth,
            });

            var restored = RoundTrip(state);

            Assert.That(restored.hemisphere, Is.EqualTo(Wheel.HemisphereSouth),
                "the warden's reckoning survives the save");
            Assert.That(restored.sabbatClaims, Has.Count.EqualTo(1),
                "a kept sabbat that didn't survive the save could be kept twice");
            Assert.That(restored.sabbatClaims[0].sabbatId, Is.EqualTo("beltane"));
            Assert.That(restored.sabbatClaims[0].year, Is.EqualTo(2026));
            Assert.That(restored.sabbatClaims[0].hemisphere, Is.EqualTo(Wheel.HemisphereSouth));
        }

        [Test]
        public void Capture_RoundTripsTheKeeping()
        {
            var state = GameStateFactory.NewGame(_data);
            state.keeping = new KeepingState
            {
                sabbatId = "beltane",
                year = 2026,
                hemisphere = Wheel.HemisphereNorth,
                generatedForMigration = 2,
                tierGranted = 1,
            };
            state.keeping.slots.Add(new KeepingSlotState
            {
                kind = KeepingSlotState.ResourceKind,
                goodsId = "wildflowers",
                target = 120.0,
                delivered = 120.0,
                renownGrant = 0L,
            });
            state.keeping.slots.Add(new KeepingSlotState
            {
                kind = KeepingSlotState.SpecimenKind,
                target = 1.0,
                renownGrant = 40L,
            });

            var restored = RoundTrip(state);

            Assert.That(restored.keeping, Is.Not.Null, "a keeping that didn't survive the save would reroll on relaunch");
            Assert.That(restored.keeping.sabbatId, Is.EqualTo("beltane"));
            Assert.That(restored.keeping.year, Is.EqualTo(2026));
            Assert.That(restored.keeping.generatedForMigration, Is.EqualTo(2));
            Assert.That(restored.keeping.tierGranted, Is.EqualTo(1));
            Assert.That(restored.keeping.slots, Has.Count.EqualTo(2));
            Assert.That(restored.keeping.slots[0].goodsId, Is.EqualTo("wildflowers"));
            Assert.That(restored.keeping.slots[0].delivered, Is.EqualTo(120.0));
            Assert.That(restored.keeping.slots[1].kind, Is.EqualTo(KeepingSlotState.SpecimenKind));
            Assert.That(restored.keeping.slots[1].renownGrant, Is.EqualTo(40L));
        }

        [Test]
        public void Restore_ShapelessKeeping_IsDroppedWhole()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0L);
            save.keeping = new SavedKeeping { sabbatId = "" };

            var state = SaveCodec.Restore(save, _data);

            Assert.That(state.keeping, Is.Null,
                "a page with no sabbat is shapeless — dropped whole, and the next tide simply generates fresh");
        }

        [Test]
        public void Restore_ShapelessWheelFields_ReadAsUnsetAndDropped()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0L);
            save.hemisphere = 9;
            save.sabbatClaims = new System.Collections.Generic.List<SavedSabbatClaim>
            {
                null,
                new SavedSabbatClaim { sabbatId = "", year = 2026, hemisphere = Wheel.HemisphereNorth },
                new SavedSabbatClaim { sabbatId = "yule", year = 0, hemisphere = Wheel.HemisphereNorth },
                new SavedSabbatClaim { sabbatId = "yule", year = 2026, hemisphere = 7 },
                new SavedSabbatClaim { sabbatId = "yule", year = 2026, hemisphere = Wheel.HemisphereNorth },
            };

            var state = SaveCodec.Restore(save, _data);

            Assert.That(state.hemisphere, Is.EqualTo(Wheel.HemisphereUnset),
                "a hemisphere that never existed reads as unset — the host re-derives it from locale");
            Assert.That(state.sabbatClaims, Has.Count.EqualTo(1),
                "only the structurally whole claim survives — the run keeps its real record and nothing shapeless");
            Assert.That(state.sabbatClaims[0].sabbatId, Is.EqualTo("yule"));
        }

        [Test]
        public void Restore_BlankCampName_ReadsAsNoNameRatherThanACampCalledNothing()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0L);
            save.campName = "   ";

            var state = SaveCodec.Restore(save, _data);

            Assert.That(state.campName, Is.Null);
            Assert.That(Camp.DisplayName(state), Is.EqualTo(Camp.Anonymous));
        }

        [Test]
        public void Capture_RoundTripsTheConfirmationsTheRunStillOwes()
        {
            // The whole point of the rung: a reward is credited the moment Play
            // hands it over, and its sheet can only be shown by the HUD. The
            // debt has to survive the process, or a session that dies in
            // between leaves the player paid and never told.
            var state = GameStateFactory.NewGame(_data);
            state.rewardsOwedTelling.Add("reward_drovers_halter");
            state.rewardsOwedTelling.Add("reward_weekly_amber_cache");
            state.rewardsOwedTelling.Add("reward_weekly_amber_cache");

            var restored = SaveCodec.Restore(SaveCodec.Capture(state, 0), _data);

            Assert.That(restored.rewardsOwedTelling, Is.EqualTo(new[]
            {
                "reward_drovers_halter",
                "reward_weekly_amber_cache",
                "reward_weekly_amber_cache",
            }), "in order, and not deduped: two deliveries are two tellings");
        }

        [Test]
        public void Restore_ShapelessConfirmationsOwed_ComeBackAsNoneAndCapped()
        {
            var state = GameStateFactory.NewGame(_data);
            var save = SaveCodec.Capture(state, 0);
            save.rewardsOwedTelling = new List<string> { null, string.Empty, "  ", "reward_drovers_halter" };

            Assert.That(SaveCodec.Restore(save, _data).rewardsOwedTelling,
                Is.EqualTo(new[] { "reward_drovers_halter" }),
                "a blank is a sheet with nothing on it, so it never comes in");

            var flooded = SaveCodec.Capture(state, 0);
            flooded.rewardsOwedTelling = new List<string>();
            for (var i = 0; i < 200; i++)
            {
                flooded.rewardsOwedTelling.Add("reward_weekly_amber_cache");
            }

            Assert.That(SaveCodec.Restore(flooded, _data).rewardsOwedTelling.Count, Is.EqualTo(16),
                "every entry is a sheet to dismiss, so a corrupt list is capped rather than obeyed");
        }

        [Test]
        public void Restore_MissingConfirmationsOwed_ReadsAsOwingNothing()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.rewardsOwedTelling = null;

            Assert.That(SaveCodec.Restore(save, _data).rewardsOwedTelling, Is.Empty);
        }

        [Test]
        public void TryMigrate_V54_ClimbsToCurrentOwingNothing()
        {
            // v55 persists the tellings owed. A v54 save held them in memory
            // alone, so whatever it owed died with the process that wrote it:
            // empty is the honest shape, and the rung fills nothing in.
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.version = 54;
            save.rewardsOwedTelling = null;

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(SaveCodec.Restore(save, _data).rewardsOwedTelling, Is.Empty);
        }

        [Test]
        public void TryMigrate_CurrentVersion_NeedsNoRung()
        {
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);

            Assert.That(SaveCodec.TryMigrate(save), Is.True, "the shape this build writes is the shape it reads");
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion), "and nothing moved it");
        }

        [Test]
        public void TryMigrate_VersionBelowTheFloor_IsRefusedRatherThanHalfRead()
        {
            // The ladder that carried these up has been retired. Refusing is the
            // whole point: a save read without its migrations looks healthy and
            // is quietly wrong, which is worse than starting again.
            var save = new SaveData { version = SaveCodec.EarliestReadableVersion - 1 };

            Assert.That(SaveCodec.TryMigrate(save), Is.False);
        }

        [Test]
        public void TryMigrate_EveryVersionBelowTheFloor_IsRefused()
        {
            for (var version = 0; version < SaveCodec.EarliestReadableVersion; version++)
            {
                var save = new SaveData { version = version };

                Assert.That(SaveCodec.TryMigrate(save), Is.False,
                    "v" + version + " is below the floor and must not climb");
            }
        }

        [Test]
        public void EarliestReadableVersion_IsNeverAheadOfWhatThisBuildWrites()
        {
            // Raising the floor past CurrentVersion would refuse the build's own
            // saves — every launch would start again and set the last one aside.
            Assert.That(SaveCodec.EarliestReadableVersion, Is.LessThanOrEqualTo(SaveCodec.CurrentVersion));
        }
    }
}
