using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the windfall bubbles — the active-play reward that replaced
    /// tap-to-tend: a worked node's bubble pays a FLAT haul (rewardSeconds of
    /// a notional rewardRatePerSecond gatherer) straight to camp, credits XP
    /// like any handled goods, and tends the node (so the Rite's tend deeds
    /// and the tend-burst gear stay live). The haul is the same at every node
    /// whoever works it and however developed it is; a fallow node still
    /// drifts nothing, and no config means the system is inert.
    /// </summary>
    public class BubblesTests
    {
        private const double Tolerance = 1e-9;

        // 0.5/s notional hands x 60 s — the flat windfall the fixture pays.
        private const double Windfall = 30.0;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                tending = new EconomyData.TendingData { burstYieldMult = 3.0, burstDurationSec = 5.0, pristineBonusDurationSec = 30.0 },
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                bubbles = new EconomyData.BubblesData
                {
                    spawnIntervalSec = 25.0,
                    lifetimeSec = 18.0,
                    maxLive = 3,
                    rewardSeconds = 60.0,
                    rewardRatePerSecond = 0.5,
                },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries", "wildflowers", "fibres" },
                    unlocks = new List<string> { "foraging" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Configured_WithSection_IsTrue()
        {
            Assert.That(Bubbles.Configured(_data), Is.True);
        }

        [Test]
        public void Configured_WithoutSection_IsFalse()
        {
            _data.economy.bubbles = null;

            Assert.That(Bubbles.Configured(_data), Is.False);
        }

        [Test]
        public void Configured_WithoutARewardRate_IsFalse()
        {
            // Configure the section whole or not at all — a bubble with no
            // notional gatherer behind it would pop for nothing.
            _data.economy.bubbles.rewardRatePerSecond = 0.0;

            Assert.That(Bubbles.Configured(_data), Is.False);
        }

        [Test]
        public void RewardFor_FallowNode_IsZero()
        {
            var state = GameStateFactory.NewGame(_data);

            // No warden economy section and no familiar — nothing works the
            // second node, so nothing drifts up from it. The haul is flat; the
            // bubble itself still has to be earned.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[1]), Is.EqualTo(BigDouble.Zero));
            Assert.That(Bubbles.IsEligible(state, _data, state.nodes[1]), Is.False);
        }

        [Test]
        public void RewardFor_StationedGatherer_PaysTheFlatWindfall()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Windfall).Within(Tolerance));
        }

        [Test]
        public void RewardFor_WardenPost_PaysTheSameFlatWindfall()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 2.0 };
            var state = GameStateFactory.NewGame(_data);

            // The warden opens posted at the first node — their own hands make
            // it eligible, but the haul doesn't track how fast those hands are.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Windfall).Within(Tolerance));
        }

        [Test]
        public void RewardFor_AWanderingWarden_PaysTheSameFlatWindfall()
        {
            // The case that drove the change: a wandering warden spreads their
            // hands across every node (gatherPerSecond / node count), so the
            // old output-proportional haul paid 1-2 units and shrank with every
            // zone opened. Eligible, and now worth a full windfall.
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            Warden.Wander(state);

            Assert.That(Bubbles.IsEligible(state, _data, state.nodes[2]), Is.True);
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[2]).ToDouble(),
                Is.EqualTo(Windfall).Within(Tolerance));
        }

        [Test]
        public void RewardFor_ADevelopedNode_PaysTheSameFlatWindfall()
        {
            _data.economy.mastery = new EconomyData.MasteryData
            {
                yieldBonusPerLevel = 0.05, baseXp = 50, growth = 1.15, maxLevel = 99,
            };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            TestKith.Station(state, state.nodes[1].id, 1);

            // Upgrades, mastery and Verdure all lift the node's live output —
            // none of them touch the windfall, so the two nodes pay alike.
            state.nodes[0].yieldMultiplier = 10.0;
            state.nodes[0].masteryXp = 5000.0;
            state.verdurePoints = 40;

            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Bubbles.RewardFor(state, _data, state.nodes[1]).ToDouble()).Within(Tolerance));
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Windfall).Within(Tolerance));
        }

        [Test]
        public void RewardFor_AWalkingRaven_FattensTheWindfall()
        {
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
                    trait = new TraitData { displayName = "Deep pockets", kind = "bubbleRewardBonus", value = 0.25 },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.roster.Add(new Familiar { id = "fam-raven", speciesId = "pack-raven", stationId = state.nodes[1].id });

            // The flat windfall x (1 + the raven's 0.25) — she fetches the
            // windfalls home from wherever she happens to be posted, so she
            // lifts every node's haul alike rather than her own node's.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Windfall * 1.25).Within(Tolerance));
        }

        [Test]
        public void RewardFor_TheLongReach_PaysPerLevelHeld()
        {
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "the-long-reach", displayName = "The Long Reach", costVerdure = 6, repeatable = true,
                    effects = { new EffectData { type = EffectType.BubbleRewardBonus, value = 0.15 } },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.almanacLevels["the-long-reach"] = 4;

            // The second endless line pays per level held — the meta's answer
            // to a flat haul that rides none of the yield multipliers.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Windfall * 1.6).Within(Tolerance));
        }

        [Test]
        public void RewardFor_TheLongReachAndARaven_ShareOneAdditiveBand()
        {
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
                    trait = new TraitData { displayName = "Deep pockets", kind = "bubbleRewardBonus", value = 0.25 },
                },
            };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "the-long-reach", displayName = "The Long Reach", costVerdure = 6, repeatable = true,
                    effects = { new EffectData { type = EffectType.BubbleRewardBonus, value = 0.15 } },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.roster.Add(new Familiar { id = "fam-raven", speciesId = "pack-raven", stationId = state.nodes[1].id });
            state.almanacLevels["the-long-reach"] = 2;

            // 0.25 + 2 x 0.15 = 0.55 — the tree and the kith sum into one band
            // rather than compounding, so neither runs away from the other.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(Windfall * 1.55).Within(Tolerance));
        }

        [Test]
        public void Pop_GrantsTheRewardAsCampStock()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            var gained = Bubbles.Pop(state, _data, state.nodes[0]);

            Assert.That(gained.ToDouble(), Is.EqualTo(Windfall).Within(Tolerance));
            // Straight to camp — the warden's own catch, no basket, no carrier.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(Windfall).Within(Tolerance));
            Assert.That(state.nodes[0].basket, Is.EqualTo(BigDouble.Zero));
        }

        [Test]
        public void Pop_TendsTheNode()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Bubbles.Pop(state, _data, state.nodes[0]);

            // The catch is the tend act now — burst and Pristine window ride along.
            Assert.That(state.nodes[0].tendBurstRemaining, Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(state.nodes[0].pristineBonusRemaining, Is.EqualTo(30.0).Within(Tolerance));
        }

        [Test]
        public void Pop_GrantsGatherXp()
        {
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99, gatherPerUnit = 1 };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Bubbles.Pop(state, _data, state.nodes[0]);

            // XP from every action (§4) — the caught goods count as handled.
            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(Windfall).Within(Tolerance));
        }

        [Test]
        public void Pop_FallowNode_IsARefusedNoOp()
        {
            var state = GameStateFactory.NewGame(_data);

            var gained = Bubbles.Pop(state, _data, state.nodes[1]);

            Assert.That(gained, Is.EqualTo(BigDouble.Zero));
            Assert.That(state.GetResource("wildflowers"), Is.EqualTo(BigDouble.Zero));
            Assert.That(state.nodes[1].tendBurstRemaining, Is.EqualTo(0.0));
        }

        [Test]
        public void Pop_Unconfigured_IsANoOp()
        {
            _data.economy.bubbles = null;
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            var gained = Bubbles.Pop(state, _data, state.nodes[0]);

            Assert.That(gained, Is.EqualTo(BigDouble.Zero));
            Assert.That(state.GetResource("berries"), Is.EqualTo(BigDouble.Zero));
        }
    }
}
