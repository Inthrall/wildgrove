using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the one-off upgrade ladder under money→XP (design §9): purchasing is
    /// gated by a skill level plus a material bundle — no Coin — and drives the
    /// yield-multiplier recompute, sell-value bonuses, and the offline-cap raise.
    /// Trail maps open a zone's nodes (no regional crew seed — the crew is a
    /// roster stationed by the player). Hand-built content asset, no scene.
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
                    order = 4, id = "map-bramble",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "bramble-hedgerows" } },
                },
                new UpgradeData
                {
                    order = 1, id = "flint-sickle",
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 2, id = "waxed-satchel",
                    effects = { new EffectData { type = EffectType.HaulMult, value = 1.5 } },
                },
                new UpgradeData
                {
                    order = 3, id = "drying-rack",
                    effects = { new EffectData { type = EffectType.SellValueBonus, resource = "berries", value = 0.25 } },
                },
                new UpgradeData
                {
                    order = 5, id = "rawhide-gloves",
                    effects = { new EffectData { type = EffectType.YieldMult, zone = GameStateFactory.StartingZoneId, value = 1.5 } },
                },
                new UpgradeData
                {
                    order = 8, id = "copper-sickle",
                    materials = { new ItemAmount { id = "copper-ingot", amount = 5 } },
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
                new UpgradeData
                {
                    order = 9, id = "root-cellar",
                    effects = { new EffectData { type = EffectType.OfflineCapHours, value = 6 } },
                },
                new UpgradeData
                {
                    order = 15, id = "whetstone",
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.25 } },
                },
                new UpgradeData
                {
                    order = 7, id = "camp-fire-ring",
                    effects = { new EffectData { type = EffectType.UnlockSkill, skill = "firecraft" } },
                },
                new UpgradeData
                {
                    order = 18, id = "bellows-forge",
                    effects = { new EffectData { type = EffectType.CraftSpeedMult, skill = "firecraft", value = 2 } },
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
        public void TryPurchase_WhenAffordable_RecordsUpgrade()
        {
            var state = GameStateFactory.NewGame(_data);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(bought, Is.True);
            Assert.That(state.HasUpgrade("flint-sickle"), Is.True);
        }

        [Test]
        public void TryPurchase_WhenSkillGateUnmet_LeavesStateUnchanged()
        {
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99 };
            Upgrade("flint-sickle").gateSkill = "foraging";
            Upgrade("flint-sickle").gateLevel = 3;
            var state = GameStateFactory.NewGame(_data); // foraging level 1

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(bought, Is.False);
            Assert.That(state.HasUpgrade("flint-sickle"), Is.False);
            Assert.That(Stationing.GatherAgentsAt(state, _data, state.nodes[0]) > 0, Is.True);
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));

            // Levels 2 and 3 cost 100 + 110 — earn them and the gate opens.
            state.skillXp["foraging"] = 210.0;
            Assert.That(Upgrades.MeetsSkillGate(state, _data, Upgrade("flint-sickle")), Is.True);
            Assert.That(Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle")), Is.True);
        }

        [Test]
        public void TryPurchase_WhenAlreadyOwned_RefusesSecondPurchase()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle")), Is.False);
        }

        [Test]
        public void TryPurchase_WithMaterials_SpendsMaterials()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("copper-ingot", 7);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("copper-sickle"));

            Assert.That(bought, Is.True);
            Assert.That(state.GetResource("copper-ingot").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_WhenMaterialsShort_LeavesStateUnchanged()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("copper-ingot", 3);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("copper-sickle"));

            Assert.That(bought, Is.False);
            Assert.That(state.GetResource("copper-ingot").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.HasUpgrade("copper-sickle"), Is.False);
        }

        [Test]
        public void TryPurchase_SkillYieldMult_AppliesToMatchingSkillOnly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.nodes.Add(new NodeState { zoneId = "elsewhere", skill = "mining" });

            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.nodes[state.nodes.Count - 1].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_ZoneYieldMult_AppliesToThatZoneOnly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.nodes.Add(new NodeState { zoneId = "elsewhere", skill = "foraging" });

            Upgrades.TryPurchase(state, _data, Upgrade("rawhide-gloves"));

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(state.nodes[state.nodes.Count - 1].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_StackedUpgrades_MultiplyMultsAndAddBonuses()
        {
            var state = GameStateFactory.NewGame(_data);

            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));
            Upgrades.TryPurchase(state, _data, Upgrade("rawhide-gloves"));
            Upgrades.TryPurchase(state, _data, Upgrade("whetstone"));

            // 2 (skill mult) * 1.5 (zone mult) * (1 + 0.25 all-gathering bonus) = 3.75
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(3.75).Within(Tolerance));
        }

        [Test]
        public void TradeValuePerUnit_WithSellValueBonus_IsBoosted()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, Upgrade("drying-rack"));

            // The Drying Rack raises berries' trade value 2 → 2 · 1.25 = 2.5.
            Assert.That(Economy.TradeValuePerUnit(state, _data, "berries").ToDouble(), Is.EqualTo(2.5).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_WithOfflineCapUpgrade_UsesRaisedCap()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, Upgrade("root-cellar"));

            // Away 10 h, base cap 4 h, Root Cellar raises it to 6 h.
            var credited = Simulation.AdvanceOffline(state, _data, 10 * 3600.0);

            Assert.That(credited, Is.EqualTo(6 * 3600.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_HaulMultUpgrade_LeavesYieldMultipliersAlone()
        {
            var state = GameStateFactory.NewGame(_data);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("waxed-satchel"));

            Assert.That(bought, Is.True);
            foreach (var node in state.nodes)
            {
                Assert.That(node.yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
            }
        }

        [Test]
        public void TryPurchase_TrailMap_CreatesTheZonesNodesWithoutSeedingCrew()
        {
            var state = GameStateFactory.NewGame(_data);

            var bought = Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));

            Assert.That(bought, Is.True);
            Assert.That(state.nodes.Count, Is.EqualTo(5));
            var nuts = state.nodes[3];
            var copper = state.nodes[4];
            Assert.That(nuts.id, Is.EqualTo("bramble-hedgerows:nuts"));
            // A newly opened zone waits for the player to station the crew — no
            // regional seed (design §2/§4: the crew is a roster, not per-zone).
            Assert.That(Stationing.CountAssignedTo(state, nuts.id), Is.EqualTo(0));
            Assert.That(Stationing.CountAssignedTo(state, copper.id), Is.EqualTo(0));
            // Node skills come from the resources, not the zone's unlock list.
            Assert.That(nuts.skill, Is.EqualTo("foraging"));
            Assert.That(copper.skill, Is.EqualTo("mining"));
        }

        [Test]
        public void TryPurchase_TrailMap_NewNodesGetOwnedUpgradeMultipliers()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, Upgrade("flint-sickle"));

            Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));

            Assert.That(state.nodes[3].yieldMultiplier, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.nodes[4].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryPurchase_TrailMap_BoughtTwiceNeverDuplicatesNodes()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));

            Upgrades.TryPurchase(state, _data, Upgrade("map-bramble"));
            GameStateFactory.SyncUnlockedZones(state, _data);

            Assert.That(state.nodes.Count, Is.EqualTo(5));
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
        public void UnlockedSkills_StartingZoneUnlocksPlusOwnedEffects()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.UnlockedSkills(state, _data), Is.EquivalentTo(new[] { "foraging" }));

            state.purchasedUpgradeIds.Add("camp-fire-ring");

            Assert.That(Upgrades.UnlockedSkills(state, _data),
                Is.EquivalentTo(new[] { "foraging", "firecraft" }));
        }

        [Test]
        public void CraftSpeedMultiplier_AppliesToTheTargetSkillOnly()
        {
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("bellows-forge");

            Assert.That(Upgrades.CraftSpeedMultiplier(state, _data, "firecraft"), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(Upgrades.CraftSpeedMultiplier(state, _data, "foraging"), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void HaulCapacityMultiplier_ReflectsOwnedHaulMultUpgrades()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.HaulCapacityMultiplier(state, _data), Is.EqualTo(1.0).Within(Tolerance));

            state.purchasedUpgradeIds.Add("waxed-satchel");

            Assert.That(Upgrades.HaulCapacityMultiplier(state, _data), Is.EqualTo(1.5).Within(Tolerance));
        }

        /// <summary>Turn on the design §3 tool gate: Bramble demands copper tools.</summary>
        private void EnableToolGate()
        {
            _data.economy.tools = new EconomyData.ToolsData
            {
                tiers = new List<string> { "flint", "copper", "bronze" },
            };
            _data.zones[1].requiredTool = "copper";
            _data.upgrades[1].toolTier = "flint";  // flint-sickle
            _data.upgrades[5].toolTier = "copper"; // copper-sickle
        }

        [Test]
        public void ToolTierIndex_TracksTheBestToolOwned()
        {
            EnableToolGate();
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.ToolTierIndex(state, _data), Is.EqualTo(-1), "a fresh run owns no tools");

            state.purchasedUpgradeIds.Add("flint-sickle");
            Assert.That(Upgrades.ToolTierIndex(state, _data), Is.EqualTo(0));

            state.purchasedUpgradeIds.Add("copper-sickle");
            Assert.That(Upgrades.ToolTierIndex(state, _data), Is.EqualTo(1));
        }

        [Test]
        public void TryPurchase_TrailMapBehindTheToolGate_Refuses()
        {
            EnableToolGate();
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("flint-sickle"); // one tier short

            var bought = Upgrades.TryPurchase(state, _data, _data.upgrades[0]);

            // Design §3: the trail map demands the tool tier.
            Assert.That(bought, Is.False);
            Assert.That(state.nodes.TrueForAll(n => n.zoneId == GameStateFactory.StartingZoneId), Is.True);
        }

        [Test]
        public void TryPurchase_TrailMapWithTheRequiredTool_Succeeds()
        {
            EnableToolGate();
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("copper-sickle");

            var bought = Upgrades.TryPurchase(state, _data, _data.upgrades[0]);

            Assert.That(bought, Is.True);
            Assert.That(state.nodes.Exists(n => n.zoneId == "bramble-hedgerows"), Is.True);
        }

        [Test]
        public void MissingToolTier_NamesTheBlockingTier()
        {
            EnableToolGate();
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.MissingToolTier(state, _data, _data.upgrades[0]), Is.EqualTo("copper"));

            state.purchasedUpgradeIds.Add("copper-sickle");
            Assert.That(Upgrades.MissingToolTier(state, _data, _data.upgrades[0]), Is.Null);
        }

        [Test]
        public void MeetsToolRequirement_DataWithoutTools_NeverGates()
        {
            _data.zones[1].requiredTool = "copper";
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.MeetsToolRequirement(state, _data, _data.upgrades[0]), Is.True);
        }
    }
}
