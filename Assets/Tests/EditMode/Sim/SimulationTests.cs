using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Exercises the core tick and the starting-state factory against a
    /// hand-built content asset, so the maths is pinned without loading the
    /// real Resources asset or a scene.
    /// </summary>
    public class SimulationTests
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
                // No warden section: keeps the burst-maths tests exact; the
                // warden-gather tests below opt in per test.
                tending = new EconomyData.TendingData { burstYieldMult = 3.0, burstDurationSec = 5.0 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                // Effectively unbounded, so yield-focused tests see goods at camp
                // the same tick they're gathered; HaulTests pins the tight case.
                hauling = new EconomyData.HaulingData { baseCarryCapacity = 1e9, tripSeconds = 1.0, basketCapacity = 1e18 },
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
        public void Advance_Gathering_GrantsSkillXpPerUnit()
        {
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99, gatherPerUnit = 1 };
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // One familiar gathering 1/s for 10 s — XP from every action (§4).
            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WardenGather_AlsoGrantsSkillXp()
        {
            _data.economy.xp = new EconomyData.XpData { baseXp = 100, growth = 1.1, maxLevel = 99, gatherPerUnit = 1 };
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 2.0 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.Clear(); // isolate the warden — no crew gathering or wandering

            Simulation.Advance(state, _data, 5.0);

            // The warden's hands are an action too: 2/s at their default post
            // (the first node) for 5 s, no tend required.
            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void NewGame_StartingZone_CreatesNodePerResource()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(state.nodes.Select(n => n.resourceId),
                Is.EqualTo(new[] { "berries", "wildflowers", "fibres" }));
            Assert.That(state.nodes.All(n => n.skill == "foraging"), Is.True);
            Assert.That(state.nodes.All(n => n.zoneId == GameStateFactory.StartingZoneId), Is.True);
        }

        [Test]
        public void NewGame_SeedsOneGathererOnFirstNodeOnly()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Stationing.CountAssignedTo(state, state.nodes[0].id), Is.EqualTo(1));
            Assert.That(state.nodes.Skip(1).All(n => Stationing.CountAssignedTo(state, n.id) == 0), Is.True);
        }

        [Test]
        public void NewGame_SeedsOneOnTheTrail()
        {
            var state = GameStateFactory.NewGame(_data);

            // Slots 1 & 2 (design §4): a vole gathers, a raven takes the trail.
            Assert.That(Stationing.OnTrail(state), Is.EqualTo(1));
            Assert.That(state.roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void Advance_OneFamiliarNoBonuses_AccruesOnePerSecond()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void Advance_ZeroFamiliarNode_AccruesNothing()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // wildflowers node has no familiar seeded.
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_NonPositiveDelta_LeavesStateUnchanged()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 0.0);
            Simulation.Advance(state, _data, -5.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_Accumulates_AcrossTicks()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 3.0);
            Simulation.Advance(state, _data, 2.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Tend_SetsBurstRemainingToConfiguredDuration()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Tend(state.nodes[0], _data.economy);

            Assert.That(state.nodes[0].tendBurstRemaining, Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WithActiveBurst_MultipliesYieldForBurstSeconds()
        {
            var state = GameStateFactory.NewGame(_data);
            Simulation.Tend(state.nodes[0], _data.economy);

            Simulation.Advance(state, _data, 2.0);

            // 1 familiar * 2s fully inside the burst window * 3x = 6, burst 5 - 2 left.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(6.0).Within(Tolerance));
            Assert.That(state.nodes[0].tendBurstRemaining, Is.EqualTo(3.0).Within(Tolerance));
        }

        [Test]
        public void Advance_BurstExpiresMidTick_SplitsBurstedAndNormalYield()
        {
            var state = GameStateFactory.NewGame(_data);
            Simulation.Tend(state.nodes[0], _data.economy);

            Simulation.Advance(state, _data, 8.0);

            // 5s bursted (5 * 3 = 15) + 3s normal (3 * 1 = 3) = 18; burst spent.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(18.0).Within(Tolerance));
            Assert.That(state.nodes[0].tendBurstRemaining, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Tend_RefreshesRatherThanStacks()
        {
            var state = GameStateFactory.NewGame(_data);
            Simulation.Tend(state.nodes[0], _data.economy);
            Simulation.Advance(state, _data, 2.0);

            Simulation.Tend(state.nodes[0], _data.economy);

            Assert.That(state.nodes[0].tendBurstRemaining, Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WardenAtBareNode_GathersStraightToCamp()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.wardenPostNodeId = state.nodes[1].id; // wildflowers: no familiars

            Simulation.Advance(state, _data, 2.0);

            // The warden's hands are the bare node's only source — 0.5/s just
            // for standing there, bypassing basket and carriers (design §13).
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.nodes[1].basket.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WardenGather_IsBoostedWhileTheBurstLives()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.wardenPostNodeId = state.nodes[1].id;
            Simulation.Tend(state.nodes[1], _data.economy);

            Simulation.Advance(state, _data, 8.0);

            // 5 bursted seconds at ×3 plus 3 plain seconds: 0.5 · (15 + 3) = 9.
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(9.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WardenGather_OnlyAtThePost()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.wardenPostNodeId = state.nodes[1].id;
            state.roster.Clear(); // only the warden works, and only at its post

            Simulation.Advance(state, _data, 2.0);

            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WardenGather_AddsToFamiliarYieldWithoutCarriers()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.IsOnTrail); // no carrier: basket can't drain
            Simulation.Tend(state.nodes[0], _data.economy); // berries: 1 gatherer, the default post

            Simulation.Advance(state, _data, 2.0);

            // The familiar's bursted yield (2s · 3×) waits in the basket with no
            // carriers, but the warden's bursted 0.5 · 2 · 3 reaches camp regardless.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(6.0).Within(Tolerance));
        }

        [Test]
        public void Tend_StateAware_MovesTheWardenPost()
        {
            var state = GameStateFactory.NewGame(_data);
            Assert.That(Warden.PostNodeId(state), Is.EqualTo(state.nodes[0].id), "defaults to the first node");

            Simulation.Tend(state, _data, state.nodes[2]);

            Assert.That(Warden.PostNodeId(state), Is.EqualTo(state.nodes[2].id));
        }

        [Test]
        public void AdvanceOfflineWithSummary_ReportsCreditAndGains()
        {
            var state = GameStateFactory.NewGame(_data);

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, 100.0);

            Assert.That(summary.realSeconds, Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(summary.creditedSeconds, Is.EqualTo(100.0).Within(Tolerance));
            // Only the seeded berries node gathers, so it's the sole gain.
            Assert.That(summary.gains.Keys, Is.EquivalentTo(new[] { "berries" }));
            Assert.That(summary.gains["berries"].ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOfflineWithSummary_CappedAbsence_ReportsBothTimes()
        {
            var state = GameStateFactory.NewGame(_data);
            var fiveHours = 5.0 * 3600.0;

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, fiveHours);

            // Cap is 4h: the full absence is reported but only the cap credits.
            Assert.That(summary.realSeconds, Is.EqualTo(fiveHours).Within(Tolerance));
            Assert.That(summary.creditedSeconds, Is.EqualTo(4.0 * 3600.0).Within(Tolerance));
            Assert.That(summary.gains["berries"].ToDouble(), Is.EqualTo(4.0 * 3600.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOfflineWithSummary_CountsGoodsStillInBaskets()
        {
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.IsOnTrail); // no carrier: goods stay in the basket

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, 30.0);

            // With no carriers nothing reaches camp, but the basketful the
            // familiar gathered is still a gain — the welcome-back sheet
            // shouldn't under-report the absence.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.gains["berries"].ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOfflineWithSummary_NoTimeAway_HasNoGains()
        {
            var state = GameStateFactory.NewGame(_data);

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, -30.0);

            Assert.That(summary.realSeconds, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.creditedSeconds, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.gains, Is.Empty);
        }

        [Test]
        public void YieldPerSecond_AppliesMasteryBonus()
        {
            _data.economy.mastery = new EconomyData.MasteryData
            {
                yieldBonusPerLevel = 0.05, baseXp = 50, growth = 1.15, maxLevel = 99, xpPerUnit = 0.25,
            };
            // Rungs 0 and 1 cost 50 + 57.5 — mastery level 2.
            var node = new NodeState { id = "n", masteryXp = 107.5 };
            var state = TestCrew.WithGatherers("n", 1);

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 1 familiar * (1 + 0.05 * 2) = 1.1
            Assert.That(perSec.ToDouble(), Is.EqualTo(1.1).Within(Tolerance));
        }

        [Test]
        public void Advance_Gathering_GrantsMasteryXpPerUnit()
        {
            _data.economy.mastery = new EconomyData.MasteryData
            {
                yieldBonusPerLevel = 0.05, baseXp = 50, growth = 1.15, maxLevel = 99, xpPerUnit = 0.5,
            };
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // One familiar gathering 1/s for 10 s, at 0.5 mastery XP per unit.
            Assert.That(state.nodes[0].masteryXp, Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_AppliesVerdureGlobalBonus()
        {
            var node = new NodeState { id = "n" };
            var state = new GameState { verdurePoints = 10 };
            TestCrew.Station(state, "n", 1);

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 1 familiar * (1 + 0.02 * 10) = 1.2
            Assert.That(perSec.ToDouble(), Is.EqualTo(1.2).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_ScalesWithFamiliarsAndMultiplier()
        {
            var node = new NodeState { id = "n", yieldMultiplier = 2.0 };
            var state = TestCrew.WithGatherers("n", 4);

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 4 familiars * 2.0 tool/gear mult = 8
            Assert.That(perSec.ToDouble(), Is.EqualTo(8.0).Within(Tolerance));
        }
    }
}
