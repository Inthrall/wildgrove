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
                    order = 1, id = "flint-sickle", costCoin = 100,
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 4, id = "map-bramble", costCoin = 400,
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
            state.coin = new BigDouble(1234.5);
            state.verdurePoints = 7.5;
            state.carrierCount = 3;
            state.AddResource("berries", new BigDouble(42.25));
            state.nodes[1].familiarCount = 3;
            state.nodes[1].masteryXp = 107.5;
            state.nodes[1].basket = new BigDouble(7.5);
            state.nodes[2].tendBurstRemaining = 1.5;

            var restored = RoundTrip(state);

            Assert.That(restored.coin.ToDouble(), Is.EqualTo(1234.5).Within(Tolerance));
            Assert.That(restored.verdurePoints, Is.EqualTo(7.5).Within(Tolerance));
            Assert.That(restored.carrierCount, Is.EqualTo(3));
            Assert.That(restored.GetResource("berries").ToDouble(), Is.EqualTo(42.25).Within(Tolerance));
            Assert.That(restored.nodes[1].familiarCount, Is.EqualTo(3));
            Assert.That(restored.nodes[1].masteryXp, Is.EqualTo(107.5).Within(Tolerance));
            Assert.That(restored.nodes[1].basket.ToDouble(), Is.EqualTo(7.5).Within(Tolerance));
            Assert.That(restored.nodes[2].tendBurstRemaining, Is.EqualTo(1.5).Within(Tolerance));
            // The starter familiar was captured on node 0, not re-seeded on top.
            Assert.That(restored.nodes[0].familiarCount, Is.EqualTo(1));
        }

        [Test]
        public void RoundTrip_PreservesBigDoublesBeyondDoubleRange_Exactly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = new BigDouble(1.2345678901234567, 3000);

            var restored = RoundTrip(state);

            Assert.That(restored.coin.Mantissa, Is.EqualTo(1.2345678901234567));
            Assert.That(restored.coin.Exponent, Is.EqualTo(3000L));
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
            Assert.That(fibres.familiarCount, Is.EqualTo(0));
        }

        [Test]
        public void Restore_RebuildsUnlockedZoneNodes_AndOverlaysTheirProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 400;
            Upgrades.TryPurchase(state, _data, _data.UpgradesById["map-bramble"]);
            var nuts = state.nodes.Single(n => n.resourceId == "nuts");
            nuts.familiarCount = 4;
            state.carrierCount = 7;

            var restored = RoundTrip(state);

            // The unlocked zone's nodes exist again, carrying the saved
            // progress — not the fresh regional seed, and no seed carrier
            // inflating the saved pool.
            Assert.That(restored.nodes.Count, Is.EqualTo(5));
            Assert.That(restored.nodes.Single(n => n.resourceId == "nuts").familiarCount, Is.EqualTo(4));
            Assert.That(restored.carrierCount, Is.EqualTo(7));
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
        public void RoundTrip_RestoresDigSitesFragmentsAndFossilEffects()
        {
            _data.zones[1].digSite = true;
            _data.upgrades[1].effects.Add(new EffectData { type = EffectType.UnlockDigSite, zone = "bramble-hedgerows" });
            _data.fossils = new List<FossilData>
            {
                new FossilData
                {
                    id = "antler-crown", fragments = 3,
                    digSites = new List<string> { "bramble-hedgerows" }, strataRarity = 1.0,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all", value = 0.10 } },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            state.coin = 1000;
            Upgrades.TryPurchase(state, _data, _data.upgrades[1]); // opens the zone and its dig site
            state.digSites[0].familiarCount = 2;
            state.digSites[0].pityHours = 1.5;
            state.fossilFragments["antler-crown"] = 3; // assembled

            var restored = RoundTrip(state);

            Assert.That(restored.digSites, Has.Count.EqualTo(1));
            Assert.That(restored.digSites[0].zoneId, Is.EqualTo("bramble-hedgerows"));
            Assert.That(restored.digSites[0].familiarCount, Is.EqualTo(2));
            Assert.That(restored.digSites[0].pityHours, Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(Fossils.FragmentCount(restored, "antler-crown"), Is.EqualTo(3));
            // The completed fossil's +10% all yields folds into the restored
            // multipliers alongside owned upgrades.
            Assert.That(restored.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance));
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
        public void RoundTrip_RestoresMuseumDonations()
        {
            var state = GameStateFactory.NewGame(_data);
            state.donatedResources.Add("berries");

            Assert.That(RoundTrip(state).donatedResources, Is.EqualTo(new[] { "berries" }));
        }

        [Test]
        public void RoundTrip_RestoresTheWardenPost()
        {
            var state = GameStateFactory.NewGame(_data);
            state.wardenPostNodeId = state.nodes[0].id;

            Assert.That(RoundTrip(state).wardenPostNodeId, Is.EqualTo(state.nodes[0].id));
        }

        [Test]
        public void Restore_DanglingWardenPost_ClearsToTheFirstNodeFallback()
        {
            var state = GameStateFactory.NewGame(_data);
            // A content update renamed/removed the posted node — the saved id
            // matches nothing in the rebuilt list.
            state.wardenPostNodeId = "retired-zone:retired-resource";

            var restored = RoundTrip(state);

            // Cleared, not kept: a dangling post would strand the warden (and
            // every bonded gatherer sharing it) matching no node at all.
            Assert.That(restored.wardenPostNodeId, Is.Null);
            Assert.That(Warden.IsPosted(restored, restored.nodes[0]), Is.True);
        }

        [Test]
        public void RoundTrip_NaNPoisonedValue_SavesAsZeroInsteadOfCorruptingTheFile()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = new BigDouble(double.NaN, 0);
            state.AddResource("berries", new BigDouble(42.0));

            // Must not throw on reload — a non-finite write would fail the
            // reader's TryParse and condemn the whole save as corrupt.
            var restored = RoundTrip(state);

            Assert.That(restored.coin.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(restored.GetResource("berries").ToDouble(), Is.EqualTo(42.0).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_RestoresReadWaystones()
        {
            var state = GameStateFactory.NewGame(_data);
            state.seenWaystoneZoneIds.Add(GameStateFactory.StartingZoneId);

            Assert.That(RoundTrip(state).seenWaystoneZoneIds, Is.EqualTo(new[] { GameStateFactory.StartingZoneId }));
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
        public void TryMigrate_V13Save_GetsAnEmptyMuseum()
        {
            // v13 predates the Museum — nothing donated yet.
            var save = new SaveData { version = 13, donatedResources = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.donatedResources, Is.Empty);
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
        public void TryMigrate_V8Save_GetsEmptyExcavation()
        {
            // v8 predates excavation — nothing dug yet.
            var save = new SaveData { version = 8, digSites = null, fossilFragments = null };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.digSites, Is.Empty);
            Assert.That(save.fossilFragments, Is.Empty);
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
            Assert.That(SaveCodec.FromJson("{ \"version\": 6, \"coin\": \"1.5e\" }"), Is.Null);
            Assert.That(SaveCodec.FromJson("{ \"version\": 6, \"coin\": \"1.x5e42\" }"), Is.Null);
            Assert.That(SaveCodec.FromJson("{ \"version\": 6, \"coin\": 100 }"), Is.Null);
        }

        [Test]
        public void Restore_ZoneUnlockedSinceTheSave_KeepsItsSeedCarrier()
        {
            // A data update granted unlockZone to an upgrade the save already
            // owns: the save has none of the zone's nodes. The live purchase
            // path seeds a gatherer AND a carrier — restore must agree.
            var save = SaveCodec.Capture(GameStateFactory.NewGame(_data), 0);
            save.purchasedUpgradeIds.Add("map-bramble");

            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.nodes.Any(n => n.zoneId == "bramble-hedgerows"), Is.True);
            Assert.That(restored.nodes.Single(n => n.resourceId == "nuts").familiarCount, Is.EqualTo(1));
            Assert.That(restored.carrierCount, Is.EqualTo(2));
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
