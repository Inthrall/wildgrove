using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the windfall bubbles — the active-play reward that replaced
    /// tap-to-tend: a worked node's bubble pays rewardSeconds of its current
    /// output straight to camp, credits XP like any handled goods, and tends
    /// the node (so the Rite's tend deeds and the tend-burst gear stay live).
    /// A fallow node drifts nothing; no config means the system is inert.
    /// </summary>
    public class BubblesTests
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
                tending = new EconomyData.TendingData { burstYieldMult = 3.0, burstDurationSec = 5.0, pristineBonusDurationSec = 30.0 },
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                bubbles = new EconomyData.BubblesData
                {
                    spawnIntervalSec = 25.0,
                    lifetimeSec = 18.0,
                    maxLive = 3,
                    rewardSeconds = 60.0,
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
        public void RewardFor_FallowNode_IsZero()
        {
            var state = GameStateFactory.NewGame(_data);

            // No warden economy section and no familiar — nothing works the
            // second node, so nothing drifts up from it.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[1]), Is.EqualTo(BigDouble.Zero));
            Assert.That(Bubbles.IsEligible(state, _data, state.nodes[1]), Is.False);
        }

        [Test]
        public void RewardFor_StationedGatherer_PaysRewardSecondsOfOutput()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            // One familiar at 1/s x 60 rewardSeconds.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(60.0).Within(Tolerance));
        }

        [Test]
        public void RewardFor_WardenPost_CountsTheWardensHands()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 2.0 };
            var state = GameStateFactory.NewGame(_data);

            // The warden opens posted at the first node — 2/s x 60 s, no kith needed.
            Assert.That(Bubbles.RewardFor(state, _data, state.nodes[0]).ToDouble(),
                Is.EqualTo(120.0).Within(Tolerance));
        }

        [Test]
        public void Pop_GrantsTheRewardAsCampStock()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            var gained = Bubbles.Pop(state, _data, state.nodes[0]);

            Assert.That(gained.ToDouble(), Is.EqualTo(60.0).Within(Tolerance));
            // Straight to camp — the warden's own catch, no basket, no carrier.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(60.0).Within(Tolerance));
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
            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(60.0).Within(Tolerance));
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
