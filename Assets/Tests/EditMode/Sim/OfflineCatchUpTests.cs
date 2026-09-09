using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the sliced offline catch-up. The property that matters most is that
    /// slicing changes NOTHING: a run credited a frame at a time must land on
    /// the same grove as one credited in a single call, or a slow device plays
    /// a different game from a fast one and every quality roll in the absence
    /// falls somewhere else. The rest pins the shape callers depend on — gains
    /// only once complete, and a fixed amount of work whatever the slices.
    /// </summary>
    public class OfflineCatchUpTests
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
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                delivery = new EconomyData.DeliveryData { batchSeconds = 10.0 },
                // A live quality roll, so a shifted sub-step boundary would show
                // up as a different split across the three pools rather than
                // being hidden by an all-Poor fixture.
                quality = new EconomyData.QualityData
                {
                    decentChance = 0.25,
                    choiceBaseChance = 0.1,
                    decentValueMult = 2.0,
                    choiceValueMult = 5.0,
                },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2 },
                new ResourceData { id = "wildflowers", sellValue = 3 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "wildflowers" },
                    unlocks = new List<string> { "foraging" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState Staffed(ulong seed)
        {
            var state = GameStateFactory.NewGame(_data);
            state.rngState = seed;
            for (var i = 0; i < state.roster.Count && i < state.nodes.Count; i++)
            {
                state.roster[i].stationId = state.nodes[i].id;
            }

            return state;
        }

        private static Dictionary<string, double> Holdings(GameState state)
        {
            var holdings = new Dictionary<string, double>();
            foreach (var node in state.nodes)
            {
                holdings["plain:" + node.resourceId] = state.GetResource(node.resourceId).ToDouble();
                holdings["decent:" + node.resourceId] = state.GetDecent(node.resourceId).ToDouble();
                holdings["choice:" + node.resourceId] = state.GetChoice(node.resourceId).ToDouble();
                holdings["basket:" + node.id] = node.basket.ToDouble();
            }

            holdings["rng"] = state.rngState;
            return holdings;
        }

        [TestCase(1.0)]
        [TestCase(7.0)]
        [TestCase(60.0)]
        [TestCase(999.0)]
        public void Advance_WhateverTheSliceSize_LandsWhereOneCallWould(double sliceSeconds)
        {
            const double away = 3 * 3600.0;

            var whole = Staffed(12345UL);
            Simulation.AdvanceOffline(whole, _data, away);

            var sliced = Staffed(12345UL);
            var catchUp = OfflineCatchUp.Begin(sliced, _data, away);
            var guard = 0;
            while (!catchUp.IsComplete && guard++ < 100_000)
            {
                catchUp.Advance(sliceSeconds);
            }

            Assert.That(catchUp.IsComplete, "the catch-up must terminate, whatever the slice");
            Assert.That(Holdings(sliced), Is.EqualTo(Holdings(whole)).Within(Tolerance),
                "slicing must not move a single sub-step boundary — including the rng, "
                + "whose position proves the quality rolls fell in the same order");
        }

        [TestCase(1.0)]
        [TestCase(60.0)]
        [TestCase(999.0)]
        public void Advance_AcrossATideEdge_LandsWhereOneCallWould(double sliceSeconds)
        {
            // A season (design §15) hands the wheel over one hour into a
            // three-hour absence: the leaned find must earn the season's rate
            // for exactly the hour it held, however the catch-up is sliced. The
            // sim clock cursor is what carries the edge — Simulation.Step walks
            // it per sub-step.
            const double away = 3 * 3600.0;
            const long dayMs = 86400000L;
            const int nightDay = 20000;
            // Seasons run night to night, so the edge is the NEXT sabbat's own
            // midnight — which is what the second entry here is for.
            var closeMs = (nightDay + 1) * dayMs;
            var leaveMs = closeMs - 3600000L;
            _data.wheel = new WheelData
            {
                sabbats = new List<SabbatData>
                {
                    new SabbatData
                    {
                        id = "beltane",
                        displayName = "Beltane",
                        kind = "fire",
                        sign = "the hedge went white overnight",
                        touch = new List<EffectData>
                        {
                            new EffectData { type = EffectType.YieldMult, resource = "wildflowers", value = 1.2 },
                        },
                        northNightDays = new List<int> { nightDay },
                        southNightDays = new List<int> { nightDay },
                    },
                    new SabbatData
                    {
                        id = "samhain",
                        displayName = "Samhain",
                        kind = "fire",
                        sign = "the dark half",
                        // A lane nothing in this fixture crafts at: what the
                        // probe below measures is the leaned hour ENDING, so
                        // the season that takes over must move no yield at all.
                        touch = new List<EffectData>
                        {
                            new EffectData { type = EffectType.CraftSpeedMult, skill = "firecraft", value = 1.2 },
                        },
                        northNightDays = new List<int> { nightDay + 1 },
                        southNightDays = new List<int> { nightDay + 1 },
                    },
                },
            };

            var whole = StaffedInTide(12345UL, leaveMs);
            Simulation.AdvanceOffline(whole, _data, away);

            var sliced = StaffedInTide(12345UL, leaveMs);
            var catchUp = OfflineCatchUp.Begin(sliced, _data, away);
            var guard = 0;
            while (!catchUp.IsComplete && guard++ < 100_000)
            {
                catchUp.Advance(sliceSeconds);
            }

            Assert.That(catchUp.IsComplete, "the catch-up must terminate, whatever the slice");
            Assert.That(sliced.simNowUnixMs, Is.EqualTo(whole.simNowUnixMs),
                "the cursor is part of the grove now — slicing must land it on the same ms");
            Assert.That(whole.simNowUnixMs, Is.EqualTo(leaveMs + (long)(away * 1000.0)),
                "the whole absence ticked, so the cursor stands at the return");
            Assert.That(Holdings(sliced), Is.EqualTo(Holdings(whole)).Within(Tolerance),
                "a tide edge mid-absence must fall on the same sub-step for every slice size");

            // And the edge did real work: the leaned hour beat a wheel-less
            // run on the leaned find alone, and moved nothing else.
            _data.wheel = null;
            var plain = StaffedInTide(12345UL, leaveMs);
            Simulation.AdvanceOffline(plain, _data, away);
            Assert.That(TotalOf(whole, "wildflowers"), Is.GreaterThan(TotalOf(plain, "wildflowers")),
                "one leaned hour must show on the leaned find");
            Assert.That(TotalOf(whole, "berries"), Is.EqualTo(TotalOf(plain, "berries")).Within(Tolerance),
                "a find the sabbat never named must not move at all");
        }

        private GameState StaffedInTide(ulong seed, long cursorMs)
        {
            var state = Staffed(seed);
            // Staffed() can only station the roster NewGame seeded, and this
            // fixture authors no species or bonds — so put one staged gatherer
            // on every node, or the leaned-hour probe below compares 0 to 0.
            foreach (var node in state.nodes)
            {
                TestKith.Station(state, node.id, 1);
            }

            state.hemisphere = Wheel.HemisphereNorth;
            state.utcOffsetMinutes = 0;
            state.simNowUnixMs = cursorMs;
            return state;
        }

        private static double TotalOf(GameState state, string resourceId)
        {
            var total = state.GetResource(resourceId).ToDouble()
                        + state.GetDecent(resourceId).ToDouble()
                        + state.GetChoice(resourceId).ToDouble();
            foreach (var node in state.nodes)
            {
                if (node.resourceId == resourceId)
                {
                    total += node.basket.ToDouble();
                }
            }

            return total;
        }

        [Test]
        public void Advance_ASliceUnderASecond_StillFinishes()
        {
            var state = Staffed(7UL);
            var catchUp = OfflineCatchUp.Begin(state, _data, 30.0);

            // A caller whose frame budget bought it nothing must not be able to
            // spin forever: a slice always advances at least one sub-step.
            var guard = 0;
            while (!catchUp.IsComplete && guard++ < 1000)
            {
                catchUp.Advance(0.01);
            }

            Assert.That(catchUp.IsComplete);
            Assert.That(guard, Is.LessThanOrEqualTo(31), "a sub-second slice should still take a whole second");
        }

        [Test]
        public void Summary_BeforeCompletion_ReportsNoGains()
        {
            var state = Staffed(3UL);
            var catchUp = OfflineCatchUp.Begin(state, _data, 3600.0);

            catchUp.Advance(60.0);

            Assert.That(catchUp.IsComplete, Is.False);
            Assert.That(catchUp.Summary.gains, Is.Empty,
                "a half-credited absence is not a smaller absence — the sheet must not quote it");
            Assert.That(catchUp.Summary.creditedSeconds, Is.EqualTo(3600.0).Within(Tolerance),
                "how much WILL be credited is fixed at the start, so the cap can't move mid-slice");
        }

        [Test]
        public void Summary_OnCompletion_MatchesTheOneShotPath()
        {
            var sliced = Staffed(99UL);
            var catchUp = OfflineCatchUp.Begin(sliced, _data, 1800.0);
            while (!catchUp.IsComplete)
            {
                catchUp.Advance(120.0);
            }

            var whole = Simulation.AdvanceOfflineWithSummary(Staffed(99UL), _data, 1800.0);

            Assert.That(catchUp.Summary.gains.Count, Is.EqualTo(whole.gains.Count));
            foreach (var pair in whole.gains)
            {
                Assert.That(catchUp.Summary.gains[pair.Key].ToDouble(),
                    Is.EqualTo(pair.Value.ToDouble()).Within(Tolerance), pair.Key);
            }
        }

        [Test]
        public void Begin_BeyondTheAwayCap_FixesTheWorkAtTheCap()
        {
            var state = Staffed(5UL);

            var catchUp = OfflineCatchUp.Begin(state, _data, 100 * 3600.0);

            Assert.That(catchUp.Summary.realSeconds, Is.EqualTo(100 * 3600.0).Within(Tolerance));
            Assert.That(catchUp.Summary.creditedSeconds, Is.EqualTo(4 * 3600.0).Within(Tolerance),
                "the cap is resolved once, at the start — it can't move under a slice");
        }

        [Test]
        public void Begin_NothingToCredit_IsAlreadyComplete()
        {
            var state = Staffed(1UL);

            var catchUp = OfflineCatchUp.Begin(state, _data, -30.0);

            Assert.That(catchUp.IsComplete, "callers must have one shape to handle, not two");
            Assert.That(catchUp.Progress, Is.EqualTo(1.0));
            Assert.That(catchUp.Summary.gains, Is.Empty);
            Assert.That(catchUp.Summary.creditedSeconds, Is.Zero);
        }

        [Test]
        public void Progress_ClimbsFromZeroToOne()
        {
            var state = Staffed(2UL);
            var catchUp = OfflineCatchUp.Begin(state, _data, 3600.0);

            Assert.That(catchUp.Progress, Is.EqualTo(0.0).Within(Tolerance));
            catchUp.Advance(1800.0);
            Assert.That(catchUp.Progress, Is.EqualTo(0.5).Within(Tolerance));
            catchUp.RunToCompletion();
            Assert.That(catchUp.Progress, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void DeferThreshold_IsFiveMinutes()
        {
            // The bar GameLoop reads to decide inline-or-sliced, and the bar the
            // welcome-back sheet's own is set against — pinned so a tuning nudge
            // to one doesn't silently invert the pair. The two coincide now that
            // the sheet waits five minutes, which is allowed: an absence sliced
            // across frames is exactly one the sheet will greet. A sheet bar
            // ABOVE this one is not — that spends frames crediting an absence
            // and then says nothing about it.
            Assert.That(OfflineCatchUp.DeferThresholdSeconds, Is.EqualTo(300.0));
            Assert.That(OfflineCatchUp.DeferThresholdSeconds,
                Is.GreaterThanOrEqualTo(SessionLogWelcomeBar),
                "an absence worth slicing must be one the sheet greets");
        }

        // SessionLog lives in Wildgrove.Game, which the sim tests don't reference —
        // the bar is restated rather than imported, and the assertion above is
        // what keeps the restatement honest if either moves.
        private const double SessionLogWelcomeBar = 300.0;
    }
}
