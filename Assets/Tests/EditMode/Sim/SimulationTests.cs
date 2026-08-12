using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
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
                // A wide ladder — these fixtures exercise the gather→haul
                // pipeline, not the slots (staged crowds bypass them anyway).
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                delivery = new EconomyData.DeliveryData { batchSeconds = 1.0 },
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
            TestKith.StageGatherer(state);

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

            Simulation.Advance(state, _data, 5.0);

            // The warden's hands are an action too: 2/s at their seeded post
            // (the first node no familiar held) for 5 s, no tend required.
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
        public void NewGame_OpensWithTheWardenAlone()
        {
            var state = GameStateFactory.NewGame(_data);

            // The kith arrives through play (the recruit rungs), not at birth.
            Assert.That(state.roster, Is.Empty);
            Assert.That(Warden.PostNodeId(state), Is.EqualTo(state.nodes[0].id));
        }

        [Test]
        public void Advance_OneFamiliarNoBonuses_AccruesOnePerSecond()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.StageGatherer(state);

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void Advance_ZeroFamiliarNode_AccruesNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.StageGatherer(state);

            Simulation.Advance(state, _data, 10.0);

            // the wildflowers node has no familiar on it.
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void GrantHaul_AddsEachGainToCampStock()
        {
            var state = GameStateFactory.NewGame(_data);
            state.resources["berries"] = new BigDouble(3.0);
            var gains = new Dictionary<string, BigDouble>
            {
                { "berries", new BigDouble(10.0) },
                { "fibres", new BigDouble(5.0) },
            };

            Simulation.GrantHaul(state, gains);

            // The OfflineBoost reward stacks on the haul already credited (3 + 10)
            // and seeds a new resource line for a gain not yet in camp.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(13.0).Within(Tolerance));
            Assert.That(state.GetResource("fibres").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void GrantHaul_NullGains_LeavesStateUnchanged()
        {
            var state = GameStateFactory.NewGame(_data);
            state.resources["berries"] = new BigDouble(7.0);

            Simulation.GrantHaul(state, null);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(7.0).Within(Tolerance));
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
            TestKith.StageGatherer(state);

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
            TestKith.StageGatherer(state);
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
            TestKith.StageGatherer(state);
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
        public void Advance_WardenGather_BypassesTheBasketTheFamiliarFills()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            _data.economy.delivery.batchSeconds = 60.0; // longer than the tick: the pool can't land
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            Warden.Post(state, state.nodes[1]);
            Simulation.Tend(state.nodes[0], _data.economy);

            Simulation.Advance(state, _data, 2.0);

            // The gatherer's bursted yield (2s · 3×) still waits on the next
            // delivery, but the warden — at their own post next door —
            // pockets 0.5 · 2 wildflowers straight to camp regardless.
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(6.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void NewGame_SeedsTheWarden_OnTheFirstNode()
        {
            var state = GameStateFactory.NewGame(_data);

            // No kith at birth — the warden opens the run on the first node.
            Assert.That(Warden.PostNodeId(state), Is.EqualTo(state.nodes[0].id));
        }

        [Test]
        public void Tend_StateAware_DoesNotMoveTheWardenPost()
        {
            var state = GameStateFactory.NewGame(_data);
            var posted = Warden.PostNodeId(state);

            Simulation.Tend(state, _data, state.nodes[2]);

            // Standing somewhere is an explicit assignment now — tending is
            // just the burst and the deed.
            Assert.That(Warden.PostNodeId(state), Is.EqualTo(posted));
            Assert.That(state.nodes[2].tendBurstRemaining, Is.GreaterThan(0.0));
        }

        [Test]
        public void WardenPost_ANodeAFamiliarHolds_BumpsTheFamiliarToCamp()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[1].id, 1);
            var holder = Stationing.OccupantOf(state, state.nodes[1].id);

            Warden.Post(state, state.nodes[1]);

            // One body per post: the warden takes the node, the holder goes home.
            Assert.That(Warden.PostNodeId(state), Is.EqualTo(state.nodes[1].id));
            Assert.That(holder.IsResting, Is.True);
        }

        [Test]
        public void Warden_Rest_StandsAtCampAndGathersNothing()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);

            Warden.Rest(state);
            Simulation.Advance(state, _data, 4.0);

            Assert.That(Warden.PostNodeId(state), Is.Null);
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_WardenWatching_GathersNothing()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.9 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.Clear(); // only the warden works
            Warden.Watch(state, "old-growth-wood");

            Simulation.Advance(state, _data, 10.0);

            // A watch post is the watch and only the watch: a watching warden
            // picks nothing at any node (the gather-share retired 2026-08-09 —
            // a body that also gathered read as two jobs on one post).
            Assert.That(Warden.IsWatching(state), Is.True);
            foreach (var node in state.nodes)
            {
                Assert.That(state.GetResource(node.resourceId).ToDouble(), Is.EqualTo(0.0).Within(Tolerance), node.id);
                Assert.That(node.basket.ToDouble(), Is.EqualTo(0.0).Within(Tolerance), node.id);
            }
        }

        [Test]
        public void WardenWatch_AFamiliarKeepingThatWatch_StepsBackToCamp()
        {
            var state = GameStateFactory.NewGame(_data);
            var watch = Familiar.WatchStation("old-growth-wood");
            TestKith.Station(state, watch, 1);
            var holder = Stationing.OccupantOf(state, watch);

            Warden.Watch(state, "old-growth-wood");

            // One body per post: the warden takes the site's watch, the familiar
            // that held it goes home — the same rule a node follows.
            Assert.That(Warden.IsWatchingAt(state, "old-growth-wood"), Is.True);
            Assert.That(holder.IsResting, Is.True);
        }

        [Test]
        public void Advance_AWatcher_GathersNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.ClearStations(state);
            TestKith.Station(state, Familiar.WatchStation("old-growth-wood"), 1);

            Simulation.Advance(state, _data, 9.0);

            // A watch post is the watch and only the watch: a watching familiar
            // gathers at no node (the gather-share retired 2026-08-09 — a body
            // that also gathered read as two jobs on one post).
            foreach (var node in state.nodes)
            {
                Assert.That(state.GetResource(node.resourceId).ToDouble(), Is.EqualTo(0.0).Within(Tolerance), node.id);
            }
        }

        [Test]
        public void AdvanceOfflineWithSummary_ReportsCreditAndGains()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.StageGatherer(state);

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, 100.0);

            Assert.That(summary.realSeconds, Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(summary.creditedSeconds, Is.EqualTo(100.0).Within(Tolerance));
            // Only the staged berries node gathers, so it's the sole gain.
            Assert.That(summary.gains.Keys, Is.EquivalentTo(new[] { "berries" }));
            Assert.That(summary.gains["berries"].ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOfflineWithSummary_CappedAbsence_ReportsBothTimes()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.StageGatherer(state);
            var fiveHours = 5.0 * 3600.0;

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, fiveHours);

            // Cap is 4h: the full absence is reported but only the cap credits.
            Assert.That(summary.realSeconds, Is.EqualTo(fiveHours).Within(Tolerance));
            Assert.That(summary.creditedSeconds, Is.EqualTo(4.0 * 3600.0).Within(Tolerance));
            Assert.That(summary.gains["berries"].ToDouble(), Is.EqualTo(4.0 * 3600.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOfflineWithSummary_CountsGoodsStillAwaitingDelivery()
        {
            _data.economy.delivery.batchSeconds = 60.0; // longer than the absence — nothing lands
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, 30.0);

            // Mid-cadence nothing has reached camp yet, but the pool the
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
            var state = TestKith.WithGatherers("n", 1);

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
            TestKith.StageGatherer(state);

            Simulation.Advance(state, _data, 10.0);

            // One familiar gathering 1/s for 10 s, at 0.5 mastery XP per unit.
            Assert.That(state.nodes[0].masteryXp, Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_AppliesTheKithBaseRate()
        {
            _data.economy.kith.gatherPerSecond = 0.1;
            var node = new NodeState { id = "n" };
            var state = TestKith.WithGatherers("n", 1);

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // The authored base rate replaces the historical implicit 1/s —
            // cut to 0.1 when hauling retired and deliveries became lossless.
            Assert.That(perSec.ToDouble(), Is.EqualTo(0.1).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_NoBaseRateConfigured_KeepsTheHistoricalOnePerSecond()
        {
            var node = new NodeState { id = "n" };
            var state = TestKith.WithGatherers("n", 1);

            Assert.That(Simulation.YieldPerSecond(node, state, _data.economy).ToDouble(),
                Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_AppliesVerdureGlobalBonus()
        {
            var node = new NodeState { id = "n" };
            var state = new GameState { verdurePoints = 10 };
            TestKith.Station(state, "n", 1);

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 1 familiar * (1 + 0.02 * 10) = 1.2
            Assert.That(perSec.ToDouble(), Is.EqualTo(1.2).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_ScalesWithFamiliarsAndMultiplier()
        {
            var node = new NodeState { id = "n", yieldMultiplier = 2.0 };
            var state = TestKith.WithGatherers("n", 4);

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 4 familiars * 2.0 tool/gear mult = 8
            Assert.That(perSec.ToDouble(), Is.EqualTo(8.0).Within(Tolerance));
        }

        [Test]
        public void TotalYieldPerSecond_WardenAlone_IsNotZero()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.Clear();
            Warden.Post(state, state.nodes[1]);

            // The basket lane is empty — this is the reading that made a posted
            // warden look inert on the node's own plate.
            Assert.That(Simulation.YieldPerSecond(state.nodes[1], state, _data, _data.economy).ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Simulation.TotalYieldPerSecond(state.nodes[1], state, _data, _data.economy).ToDouble(),
                Is.EqualTo(0.5).Within(Tolerance));
        }

        [Test]
        public void TotalYieldPerSecond_AWatchingWarden_AddsNothingToTheKithsLane()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.Clear();
            TestKith.Station(state, state.nodes[0].id, 1);
            Warden.Watch(state, "old-growth-wood");

            // A watch post is the watch and only the watch (the gather-share
            // retired 2026-08-09): the node's rate is the kith's lane alone.
            var node = state.nodes[0];
            var kith = Simulation.YieldPerSecond(node, state, _data, _data.economy).ToDouble();
            Assert.That(kith, Is.GreaterThan(0.0), "the stationed familiar's lane still runs");
            Assert.That(Simulation.TotalYieldPerSecond(node, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(kith).Within(Tolerance));
        }

        [Test]
        public void TotalYieldPerSecond_WardenAtCamp_MatchesTheKithsLane()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            Warden.Rest(state);

            var node = state.nodes[0];
            Assert.That(Simulation.TotalYieldPerSecond(node, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(Simulation.YieldPerSecond(node, state, _data, _data.economy).ToDouble()).Within(Tolerance));
        }
    }
}
