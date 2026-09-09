using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Wheel (design §15): the season's edges — night to night, back
    /// to back — the hemisphere mirror, the inert states (unconfigured data,
    /// unset hemisphere, unstamped cursor, a cursor the calendar does not
    /// reach), each lane of the ambient touch at its real hook site, and the
    /// sim clock cursor the whole thing reads. The offline half of the story —
    /// a season's edge crossed mid-absence — is pinned in OfflineCatchUpTests,
    /// where the sliced-equals-unsliced property lives.
    /// </summary>
    public class WheelTests
    {
        private const long DayMs = 86400000L;

        // Any date-only epoch days; the calendar is authored data, so tests
        // never need a real "today". Two sabbats, mirrored the way the real
        // eight are — each sits on the other's date in the other hemisphere —
        // so that a season has something to end AT, which is the whole rule
        // under test. Beltane's northern window is [NightDay, MirrorDay).
        private const int NightDay = 20000;
        private const int MirrorDay = NightDay + 182;
        private static readonly long OpenMs = NightDay * DayMs;
        private static readonly long CloseMs = MirrorDay * DayMs;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                replant = new EconomyData.ReplantData { baseCost = 10, growth = 1.5, richnessPerLevel = 0.1 },
                bubbles = new EconomyData.BubblesData
                {
                    spawnIntervalSec = 30.0,
                    rewardSeconds = 5.0,
                    rewardRatePerSecond = 1.0,
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
            _data.exchange = new ExchangeData { spread = 0.15, offerMinutes = 5 };
            _data.wheel = new WheelData
            {
                sabbats = new List<SabbatData>
                {
                    new SabbatData
                    {
                        id = "beltane",
                        displayName = "Beltane",
                        kind = "fire",
                        sign = "The hedge went white overnight.",
                        // Every lane at once, so one fixture can prove each of
                        // them reads at its own hook site.
                        touch = new List<EffectData>
                        {
                            new EffectData { type = EffectType.YieldMult, resource = "wildflowers", value = 1.2 },
                            new EffectData { type = EffectType.DigSpeedMult, value = 1.2 },
                            new EffectData { type = EffectType.CraftSpeedMult, skill = "firecraft", value = 1.2 },
                            new EffectData { type = EffectType.BubbleRewardBonus, value = 0.2 },
                            new EffectData { type = EffectType.ReplantCostMult, value = 0.8 },
                            new EffectData { type = EffectType.ExchangeSpreadEase, value = 0.05 },
                        },
                        northNightDays = new List<int> { NightDay },
                        southNightDays = new List<int> { MirrorDay },
                    },
                    new SabbatData
                    {
                        id = "samhain",
                        displayName = "Samhain",
                        kind = "fire",
                        sign = "The dark half.",
                        // A lean on the find Beltane never names, so which
                        // season is holding is readable from the yields alone.
                        touch = new List<EffectData>
                        {
                            new EffectData { type = EffectType.YieldMult, resource = "berries", value = 1.5 },
                        },
                        northNightDays = new List<int> { MirrorDay },
                        southNightDays = new List<int> { NightDay },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState InTide(long atMs = 0L)
        {
            var state = GameStateFactory.NewGame(_data);
            state.hemisphere = Wheel.HemisphereNorth;
            state.utcOffsetMinutes = 0;
            state.simNowUnixMs = atMs > 0L ? atMs : OpenMs + DayMs;
            return state;
        }

        /// <summary>
        /// Before the calendar's first night — the one state left in which no
        /// season holds the wheel, now that they run end to end. Same run,
        /// nothing leaning.
        /// </summary>
        private GameState BeforeTheWheel()
        {
            return InTide(OpenMs - 30 * DayMs);
        }

        [Test]
        public void OpenTide_Unconfigured_IsNull()
        {
            _data.wheel = null;
            var state = InTide();

            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "absent wheel data must be inert — every hand-built fixture depends on it");
        }

        [Test]
        public void OpenTide_HemisphereUnset_IsNull()
        {
            var state = InTide();
            state.hemisphere = Wheel.HemisphereUnset;
            state.wheelCache = null;

            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "until the host defaults the hemisphere, the Wheel must not guess");
        }

        [Test]
        public void OpenTide_CursorUnstamped_IsNull()
        {
            var state = InTide();
            state.simNowUnixMs = 0L;
            state.wheelCache = null;

            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "an unstamped cursor is 1970, not a tide — the Wheel waits for the host's stamp");
        }

        [Test]
        public void OpenTide_WindowEdges_RunNightToNight_WithNoGapBetweenThem()
        {
            var state = InTide();

            state.simNowUnixMs = OpenMs - 1L;
            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "one ms before Beltane's own midnight, and the calendar has not started");

            state.simNowUnixMs = OpenMs;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"),
                "the season opens ON the night, at the warden's own midnight");

            state.simNowUnixMs = CloseMs - 1L;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"),
                "and holds every ms up to the next sabbat's midnight");

            state.simNowUnixMs = CloseMs;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("samhain"),
                "which is where the next season starts — the wheel is handed over, never put down: "
                + "there is no ms of the authored year with nothing holding it");

            state.simNowUnixMs = OpenMs + DayMs;
            Assert.That(Wheel.OpenTideCloseMs(state, _data), Is.EqualTo(CloseMs),
                "and a season's close is the next sabbat's own night, not a fire of its own");
        }

        [Test]
        public void NextTide_NamesWhoTakesTheWheel_AndWhen()
        {
            var state = InTide();

            var next = Wheel.NextTide(state, _data, out var takesAtMs);

            Assert.That(next?.id, Is.EqualTo("samhain"), "the sabbat this season ends in");
            Assert.That(takesAtMs, Is.EqualTo(CloseMs), "at the moment it ends — one edge, said the other way about");
            Assert.That(takesAtMs, Is.EqualTo(Wheel.OpenTideCloseMs(state, _data)),
                "the two must be the same number, or the countdown and the name come apart");
        }

        [Test]
        public void OpenTide_TheLastAuthoredNight_HoldsItsSpanAndThenTheWheelGoesQuiet()
        {
            // Samhain's is the last night in the north, so nothing takes the
            // wheel off it — it holds for the fallback span and stops.
            var state = InTide(CloseMs + DayMs);
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("samhain"),
                "the last authored night must still open, or it is the one sabbat that silently never runs");

            state.simNowUnixMs = CloseMs + 45 * DayMs;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("samhain"), "and holds its 46 days");

            state.simNowUnixMs = CloseMs + 46 * DayMs;
            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "then the wheel is quiet — a calendar authored out is topped up, never extrapolated");
        }

        [Test]
        public void OpenTide_BeforeTheFirstAuthoredNight_HoldsNothing()
        {
            var state = BeforeTheWheel();

            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "a cursor the authored calendar does not reach has no season, and the Wheel must not guess one");
            Assert.That(Wheel.OpenTideCloseMs(state, _data), Is.EqualTo(0L),
                "and no close to count down to");
        }

        [Test]
        public void OpenTide_CacheHoldsUntilTheCalendarStarts()
        {
            var state = BeforeTheWheel();
            Assert.That(Wheel.OpenTide(state, _data), Is.Null);

            state.simNowUnixMs = OpenMs;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"),
                "the cache spans the whole run-up to the first night and must expire ON it, "
                + "not hold the emptiness it was built in");
        }

        [Test]
        public void OpenTide_Hemispheres_ReadTheirOwnNights()
        {
            var north = InTide();
            var south = InTide();
            south.hemisphere = Wheel.HemisphereSouth;
            south.wheelCache = null;

            Assert.That(Wheel.OpenTide(north, _data)?.id, Is.EqualTo("beltane"), "the north's own night has fallen");
            Assert.That(Wheel.OpenTide(south, _data)?.id, Is.EqualTo("samhain"),
                "and the south is holding the opposite sabbat over the very same weeks — which is why "
                + "turning the reckoning after an offering would be one span of the year claimed twice");

            south.simNowUnixMs += 182 * DayMs;
            Assert.That(Wheel.OpenTide(south, _data)?.id, Is.EqualTo("beltane"),
                "half a year on, the mirror has turned the other way about");
        }

        [Test]
        public void OpenTide_UtcOffset_MovesTheEdgesToWardenLocalMidnight()
        {
            var state = InTide();
            state.utcOffsetMinutes = 780; // UTC+13 — a New Zealand summer
            state.wheelCache = null;

            // Local midnight falls 13 hours earlier in UTC.
            state.simNowUnixMs = CloseMs - 780L * 60000L;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("samhain"),
                "the wheel is handed over at WARDEN-local midnight, not UTC midnight");

            state.simNowUnixMs -= 1L;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"));
        }

        [Test]
        public void WeekStartMs_IsTheMondayTheMomentFallsIn()
        {
            // Epoch day 4 is 1970-01-05, the epoch's first Monday; day 10 is the
            // Sunday that closes its week.
            Assert.That(Wheel.WeekStartMs(4L * DayMs, 0), Is.EqualTo(4L * DayMs), "a Monday is its own week's start");
            Assert.That(Wheel.WeekStartMs(10L * DayMs + 3600000L, 0), Is.EqualTo(4L * DayMs),
                "and the Sunday six days later belongs to the same week — which is what makes a claim "
                + "taken then read as taken, right up to midnight");
            Assert.That(Wheel.WeekStartMs(11L * DayMs, 0), Is.EqualTo(11L * DayMs), "the next Monday starts the next");
            Assert.That(Wheel.WeekStartMs(11L * DayMs - 1L, 780), Is.EqualTo(11L * DayMs - 780L * 60000L),
                "the instant before UTC's Monday, a warden at UTC+13 is already half a day into it — "
                + "the boundary moves with the offset, like every other midnight here");
        }

        [Test]
        public void NextWeekStartMs_IsTheWardensNextMonday_AndNeverToday()
        {
            // Epoch day 4 is 1970-01-05, the epoch's first Monday.
            Assert.That(Wheel.NextWeekStartMs(4L * DayMs, 0), Is.EqualTo(11L * DayMs),
                "asked ON a Monday it gives the one after — a week that could answer \"now\" would "
                + "hand the rail a countdown of zero to draw");
            Assert.That(Wheel.NextWeekStartMs(4L * DayMs + DayMs - 1L, 0), Is.EqualTo(11L * DayMs),
                "and every moment of that Monday agrees");
            Assert.That(Wheel.NextWeekStartMs(10L * DayMs, 0), Is.EqualTo(11L * DayMs),
                "the Sunday before it is a day out, not eight");

            // UTC+13: warden-local Monday midnight is 13 hours ahead of UTC's.
            Assert.That(Wheel.NextWeekStartMs(10L * DayMs, 780), Is.EqualTo(11L * DayMs - 780L * 60000L),
                "the week turns over at the warden's own midnight, like the tides");
            Assert.That(Wheel.NextWeekStartMs(11L * DayMs - 780L * 60000L, 780), Is.EqualTo(18L * DayMs - 780L * 60000L),
                "and the moment it turns, it is a whole week to the next");

            // An hour into Sunday 1969-12-28 (epoch day −4). Truncating toward
            // zero instead of flooring would file it under the Monday after,
            // and answer with the Monday after THAT — a week wrong, and only
            // ever wrong before the epoch, which is where nobody looks.
            Assert.That(Wheel.NextWeekStartMs(-4L * DayMs + 3600000L, 0), Is.EqualTo(-3L * DayMs),
                "a local day before the epoch is floored into its own day, not the one above it");
        }

        [Test]
        public void NextSabbat_NamesTheComingNight_AndRunsOutHonestly()
        {
            var state = BeforeTheWheel();

            var next = Wheel.NextSabbat(state, _data, out var nightStartMs);
            Assert.That(next?.id, Is.EqualTo("beltane"));
            Assert.That(nightStartMs, Is.EqualTo(OpenMs), "the night its season opens on — they are one midnight now");

            state.simNowUnixMs = CloseMs + DayMs;
            Assert.That(Wheel.NextSabbat(state, _data, out _), Is.Null,
                "a calendar authored out is null, never a guess — the evergreen rule is about topping up data");
        }

        [Test]
        public void NextNightOf_GivesTheNightItsSeasonOpensOn()
        {
            var state = BeforeTheWheel();
            var beltane = _data.wheel.sabbats[0];

            Assert.That(Wheel.NextNightOf(state, _data, beltane, out var nightMs), Is.True);
            Assert.That(nightMs, Is.EqualTo(OpenMs),
                "the rail counts down to this, and it is the moment anything can be done about it — "
                + "a season and its night start together");
        }

        [Test]
        public void NextNightOf_FromInsideASeason_LooksPastTheNightAlreadyFallen()
        {
            // Beltane's own night is what opened the season the cursor stands
            // in, so it is behind, not ahead: the next thing to happen to this
            // wheel is Samhain taking it.
            var state = InTide();

            Assert.That(Wheel.NextNightOf(state, _data, _data.wheel.sabbats[0], out _), Is.False,
                "its one authored night has fallen — a season's own night is not still coming");
            Assert.That(Wheel.NextNightOf(state, _data, _data.wheel.sabbats[1], out var nightMs), Is.True);
            Assert.That(nightMs, Is.EqualTo(CloseMs), "and the night ahead is the one that ends this season");
        }

        [Test]
        public void NextNightOf_RunsOutHonestly_AndStaysInertWhenTheWheelIs()
        {
            var state = BeforeTheWheel();
            var beltane = _data.wheel.sabbats[0];

            state.simNowUnixMs = CloseMs + DayMs;
            Assert.That(Wheel.NextNightOf(state, _data, beltane, out _), Is.False,
                "past the authored nights it must say so rather than extrapolate a calendar");

            var unset = BeforeTheWheel();
            unset.hemisphere = Wheel.HemisphereUnset;
            unset.wheelCache = null;
            Assert.That(Wheel.NextNightOf(unset, _data, beltane, out _), Is.False,
                "an unset hemisphere has no dates — the mirror is the whole calendar");
        }

        [Test]
        public void YieldMult_LeansOnlyTheNamedResource_AndOnlyWhileTheTideHolds()
        {
            var open = InTide();
            var before = BeforeTheWheel();

            Assert.That(Wheel.YieldMult(open, _data, "wildflowers"), Is.EqualTo(1.2).Within(1e-12));
            Assert.That(Wheel.YieldMult(open, _data, "berries"), Is.EqualTo(1.0),
                "a lean must not bleed into finds the sabbat never named");
            Assert.That(Wheel.YieldMult(before, _data, "wildflowers"), Is.EqualTo(1.0),
                "before the calendar reaches, nothing leans");
        }

        [Test]
        public void YieldPerSecond_InsideTheTide_PaysExactlyTheLean()
        {
            var open = InTide();
            var before = BeforeTheWheel();
            var node = open.nodes.Find(n => n.resourceId == "wildflowers");
            var plainNode = before.nodes.Find(n => n.resourceId == "wildflowers");
            TestKith.Station(open, node.id, 1);
            TestKith.Station(before, plainNode.id, 1);
            var leaned = Simulation.YieldPerSecond(node, open, _data, _data.economy).ToDouble();
            var plain = Simulation.YieldPerSecond(plainNode, before, _data, _data.economy).ToDouble();

            Assert.That(plain, Is.GreaterThan(0.0), "the fixture must actually gather, or the ratio proves nothing");
            Assert.That(leaned / plain, Is.EqualTo(1.2).Within(1e-9),
                "the tide is one clean factor in the §9 stack — nothing else may move with it");
        }

        [Test]
        public void TouchLanes_ReadAtTheirHookSites()
        {
            var open = InTide();
            var before = BeforeTheWheel();

            Assert.That(Wheel.DigSpeedMult(open, _data), Is.EqualTo(1.2).Within(1e-12), "Samhain's lane: the watch");
            Assert.That(Wheel.CraftSpeedMult(open, _data, "firecraft"), Is.EqualTo(1.2).Within(1e-12), "Yule's lane: the fire");
            Assert.That(Wheel.CraftSpeedMult(open, _data, "bushcraft"), Is.EqualTo(1.0), "a skill the touch never named");
            Assert.That(Wheel.BubbleRewardBonus(open, _data), Is.EqualTo(0.2).Within(1e-12), "Litha's lane: the windfalls");
            Assert.That(Wheel.ReplantCostMult(open, _data), Is.EqualTo(0.8).Within(1e-12), "Ostara's lane: the sowing");
            Assert.That(Wheel.SpreadEase(open, _data), Is.EqualTo(0.05).Within(1e-12), "Mabon's lane: the caravan");

            Assert.That(Wheel.DigSpeedMult(before, _data), Is.EqualTo(1.0));
            Assert.That(Wheel.BubbleRewardBonus(before, _data), Is.EqualTo(0.0));
            Assert.That(Wheel.ReplantCostMult(before, _data), Is.EqualTo(1.0));
            Assert.That(Wheel.SpreadEase(before, _data), Is.EqualTo(0.0));
        }

        [Test]
        public void ReplantCost_InsideTheTide_IsEased()
        {
            var open = InTide();
            var node = open.nodes[0];

            var plain = Replanting.ReplantCost(node, _data.economy).ToDouble();
            var eased = Replanting.ReplantCost(open, _data, node).ToDouble();

            Assert.That(eased / plain, Is.EqualTo(0.8).Within(1e-9),
                "sowing weather eases the ask; the economy-only overload stays the plain curve");
        }

        [Test]
        public void ExchangeRate_InsideTheTide_EasesTheSpread()
        {
            var open = InTide();
            var before = BeforeTheWheel();

            var eased = Exchange.Rate(open, _data, "berries", "wildflowers").ToDouble();
            var plain = Exchange.Rate(before, _data, "berries", "wildflowers").ToDouble();

            Assert.That(plain, Is.EqualTo(2.0 / 3.0 * 0.85).Within(1e-9));
            Assert.That(eased, Is.EqualTo(2.0 / 3.0 * 0.90).Within(1e-9),
                "Mabon shaves the spread's points — it must never touch the trade values themselves");
        }

        [Test]
        public void BubbleReward_InsideTheTide_JoinsTheAdditiveBand()
        {
            var open = InTide();
            var before = BeforeTheWheel();

            var leaned = Bubbles.RewardFor(open, _data, open.nodes[0]).ToDouble();
            var plain = Bubbles.RewardFor(before, _data, before.nodes[0]).ToDouble();

            Assert.That(plain, Is.EqualTo(5.0).Within(1e-9), "rewardSeconds × ratePerSecond, un-leaned");
            Assert.That(leaned, Is.EqualTo(6.0).Within(1e-9),
                "the tide's +20% joins the same additive band as the raven's trait — never a second multiplier");
        }

        [Test]
        public void SimulationStep_AdvancesTheCursor_ByExactlyTheStep()
        {
            var state = InTide();
            var before = state.simNowUnixMs;

            Simulation.Advance(state, _data, 2.5);

            Assert.That(state.simNowUnixMs, Is.EqualTo(before + 2500L),
                "the cursor walks with the tick — whole seconds exactly, the last fraction rounded");
        }

        [Test]
        public void SimulationStep_LeavesAnUnstampedCursorAlone()
        {
            var state = InTide();
            state.simNowUnixMs = 0L;

            Simulation.Advance(state, _data, 5.0);

            Assert.That(state.simNowUnixMs, Is.EqualTo(0L),
                "a fixture that never stamps the cursor must not start counting from 1970");
        }

        [Test]
        public void Cache_FollowsTheCursorAcrossTheEdge_AndTheHemisphereFlip()
        {
            var state = InTide();

            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"));

            // Walk the cursor over the handover — the cached window must expire.
            state.simNowUnixMs = CloseMs + 1L;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("samhain"),
                "the cache must expire at the window's edge, and the edge is the next sabbat's own night");

            // Flip the hemisphere — the cache keys on it.
            state.simNowUnixMs = CloseMs - 1L;
            state.hemisphere = Wheel.HemisphereSouth;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("samhain"),
                "a hemisphere change must rebuild the window, not serve the old one — the south holds "
                + "the opposite sabbat over the same weeks");
        }
    }
}
