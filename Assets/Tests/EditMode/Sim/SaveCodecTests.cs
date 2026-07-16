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
            state.nodes[1].masteryLevel = 2;
            state.nodes[1].basket = new BigDouble(7.5);
            state.nodes[2].tendBurstRemaining = 1.5;

            var restored = RoundTrip(state);

            Assert.That(restored.coin.ToDouble(), Is.EqualTo(1234.5).Within(Tolerance));
            Assert.That(restored.verdurePoints, Is.EqualTo(7.5).Within(Tolerance));
            Assert.That(restored.carrierCount, Is.EqualTo(3));
            Assert.That(restored.GetResource("berries").ToDouble(), Is.EqualTo(42.25).Within(Tolerance));
            Assert.That(restored.nodes[1].familiarCount, Is.EqualTo(3));
            Assert.That(restored.nodes[1].masteryLevel, Is.EqualTo(2));
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
        public void TryMigrate_OlderVersion_ClimbsTheWholeLadder()
        {
            var save = new SaveData { version = 0 };

            Assert.That(SaveCodec.TryMigrate(save), Is.True);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.carrierCount, Is.EqualTo(1));
        }
    }
}
