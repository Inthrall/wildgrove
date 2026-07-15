using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the one-off upgrade ladder: purchasing (Coin + materials), the
    /// yield-multiplier recompute, sell-value bonuses and the offline-cap
    /// raise. Uses a hand-built content asset so no scene or Resources asset
    /// is loaded.
    /// </summary>
    public class UpgradesTests
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
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2 },
                new ResourceData { id = "nuts", sellValue = 3, skill = "foraging" },
                new ResourceData { id = "copper-scree", sellValue = 5, skill = "mining" },
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
                // Deliberately lists a non-gathering skill first (like the real
                // Bramble) — node skills must come from the resources, not here.
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
                    order = 4, id = "map-bramble", costCoin = 400,
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "bramble-hedgerows" } },
                },
                new UpgradeData
                {
                    order = 1, id = "flint-sickle", costCoin = 100,
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 2, id = "waxed-satchel", costCoin = 150,
                    effects = { new EffectData { type = EffectType.HaulMult, value = 1.5 } },
                },
                new UpgradeData
                {
                    order = 3, id = "drying-rack", costCoin = 250,
                    effects = { new EffectData { type = EffectType.SellValueBonus, resource = "berries", value = 0.25 } },
                },
                new UpgradeData
                {
                    order = 5, id = "rawhide-gloves", costCoin = 50,
                    effects = { new EffectData { type = EffectType.YieldMult, zone = GameStateFactory.StartingZoneId, value = 1.5 } },
                },
                new UpgradeData
                {
                    order = 8, id = "copper-sickle", costCoin = 100,
                    materials = { new ItemAmount { id = "copper-ingot", amount = 5 } },
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 9, id = "root-cellar", costCoin = 300,
                    effects = { new EffectData { type = EffectType.OfflineCapHours, value = 6 } },
                },
                new UpgradeData
                {
                    order = 15, id = "whetstone", costCoin = 50,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.25 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private UpgradeData Upgrade(string id)
        {
            return _data.UpgradesById[id];
        }

        [Test]
        public void TryPurchase_WhenAffordable_SpendsCoinAndRecordsUpgrade()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 150;

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(bought, Is.True);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
            Assert.That(state.HasUpgrade("flint-sickle"), Is.True);
        }

        [Test]
        public void TryPurchase_WhenCoinShort_LeavesStateUnchanged()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 99;

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(bought, Is.False);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(99.0).Within(Tolerance));
            Assert.That(state.HasUpgrade("flint-sickle"), Is.False);
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_WhenAlreadyOwned_RefusesSecondPurchase()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 300;
            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            var boughtAgain = Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(boughtAgain, Is.False);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(200.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_WithMaterials_SpendsCoinAndMaterials()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 200;
            state.AddResource("copper-ingot", 7);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("copper-sickle"));

            Assert.That(bought, Is.True);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("copper-ingot").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_WhenMaterialsShort_LeavesStateUnchanged()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 200;
            state.AddResource("copper-ingot", 3);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("copper-sickle"));

            Assert.That(bought, Is.False);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(200.0).Within(Tolerance));
            Assert.That(state.GetResource("copper-ingot").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.HasUpgrade("copper-sickle"), Is.False);
        }

        [Test]
        public void TryPurchase_SkillYieldMult_AppliesToMatchingSkillOnly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.nodes.Add(new NodeState { zoneId = "elsewhere", skill = "mining" });
            state.coin = 100;

            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.nodes[state.nodes.Count - 1].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_ZoneYieldMult_AppliesToThatZoneOnly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.nodes.Add(new NodeState { zoneId = "elsewhere", skill = "foraging" });
            state.coin = 50;

            Upgrades.TryPurchase(state, _data, Upgrade("rawhide-gloves"));

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(state.nodes[state.nodes.Count - 1].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_StackedUpgrades_MultiplyMultsAndAddBonuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 200;

            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));
            Upgrades.TryPurchase(state, _data, Upgrade("rawhide-gloves"));
            Upgrades.TryPurchase(state, _data, Upgrade("whetstone"));

            // 2 (skill mult) * 1.5 (zone mult) * (1 + 0.25 all-gathering bonus) = 3.75
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(3.75).Within(Tolerance));
        }

        [Test]
        public void SellResource_WithSellValueBonus_PaysBoostedValue()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 250;
            Upgrades.TryPurchase(state, _data, Upgrade("drying-rack"));
            state.AddResource("berries", 4);

            var gained = Economy.SellResource(state, _data, "berries");

            // 4 * 2 * 1.25 = 10
            Assert.That(gained.ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_WithOfflineCapUpgrade_UsesRaisedCap()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 300;
            Upgrades.TryPurchase(state, _data, Upgrade("root-cellar"));

            // Away 10 h, base cap 4 h, Root Cellar raises it to 6 h.
            var credited = Simulation.AdvanceOffline(state, _data, 10 * 3600.0);

            Assert.That(credited, Is.EqualTo(6 * 3600.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_HaulMultUpgrade_LeavesYieldMultipliersAlone()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 150;

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("waxed-satchel"));

            Assert.That(bought, Is.True);
            Assert.That(state.HasUpgrade("waxed-satchel"), Is.True);
            // Carry capacity is the haul sim's lever — gather rates don't move.
            foreach (var node in state.nodes)
            {
                Assert.That(node.yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
            }
        }

        [Test]
        public void TryPurchase_TrailMap_CreatesTheZonesNodesWithRegionalSeed()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 400;

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));

            Assert.That(bought, Is.True);
            Assert.That(state.nodes.Count, Is.EqualTo(5));
            var nuts = state.nodes[3];
            var copper = state.nodes[4];
            Assert.That(nuts.id, Is.EqualTo("bramble-hedgerows:nuts"));
            // The design §2 regional seed: one gatherer on the zone's first
            // node, one carrier joining the camp pool (1 starting + 1).
            Assert.That(nuts.familiarCount, Is.EqualTo(1));
            Assert.That(copper.familiarCount, Is.EqualTo(0));
            Assert.That(state.carrierCount, Is.EqualTo(2));
            // Node skills come from the resources, not the zone's unlock list.
            Assert.That(nuts.skill, Is.EqualTo("foraging"));
            Assert.That(copper.skill, Is.EqualTo("mining"));
        }

        [Test]
        public void TryPurchase_TrailMap_NewNodesGetOwnedUpgradeMultipliers()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 500;
            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));

            // The foraging sickle covers the new zone's foraging node too;
            // its mining node is untouched.
            Assert.That(state.nodes[3].yieldMultiplier, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.nodes[4].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_TrailMap_BoughtTwiceNeverDuplicatesNodes()
        {
            var state = GameStateFactory.NewGame(_data);
            state.coin = 800;
            Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));

            Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));
            GameStateFactory.SyncUnlockedZones(state, _data);

            Assert.That(state.nodes.Count, Is.EqualTo(5));
            Assert.That(state.carrierCount, Is.EqualTo(2));
        }

        [Test]
        public void UnlockedZoneIds_StartingZonePlusOwnedTrailMaps()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.UnlockedZoneIds(state, _data),
                Is.EquivalentTo(new[] { GameStateFactory.StartingZoneId }));

            state.purchasedUpgradeIds.Add("map-bramble");

            Assert.That(Upgrades.UnlockedZoneIds(state, _data),
                Is.EquivalentTo(new[] { GameStateFactory.StartingZoneId, "bramble-hedgerows" }));
        }

        [Test]
        public void HaulCapacityMultiplier_ReflectsOwnedHaulMultUpgrades()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.HaulCapacityMultiplier(state, _data), Is.EqualTo(1.0).Within(Tolerance));

            state.purchasedUpgradeIds.Add("waxed-satchel");

            Assert.That(Upgrades.HaulCapacityMultiplier(state, _data), Is.EqualTo(1.5).Within(Tolerance));
        }
    }
}
