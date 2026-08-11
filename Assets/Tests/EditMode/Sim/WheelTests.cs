using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Wheel (design §15): the tide window's edges, the hemisphere
    /// mirror, the inert states (unconfigured data, unset hemisphere, unstamped
    /// cursor), each lane of the ambient touch at its real hook site, and the
    /// sim clock cursor the whole thing reads. The offline half of the story —
    /// a tide edge crossed mid-absence — is pinned in OfflineCatchUpTests,
    /// where the sliced-equals-unsliced property lives.
    /// </summary>
    public class WheelTests
    {
        private const long DayMs = 86400000L;

        // Any date-only epoch day; the calendar is authored data, so tests
        // never need a real "today". Window (offset 0, openDaysBefore 14):
        // [ (NightDay-14)·day , (NightDay+1)·day ).
        private const int NightDay = 20000;
        private static readonly long OpenMs = (NightDay - 14) * DayMs;
        private static readonly long CloseMs = (NightDay + 1) * DayMs;

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
                openDaysBefore = 14,
                sabbats = new List<SabbatData>
                {
                    new SabbatData
                    {
                        id = "beltane",
                        displayName = "Beltane",
                        kind = "fire",
                        sign = "Beltane, by my count.",
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
                        southNightDays = new List<int> { NightDay + 182 },
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

        private GameState Fallow()
        {
            // Well before the tide opens — same run, nothing leaning.
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
        public void OpenTide_WindowEdges_AreOpenInclusiveCloseExclusive()
        {
            var state = InTide();

            state.simNowUnixMs = OpenMs - 1L;
            Assert.That(Wheel.OpenTide(state, _data), Is.Null, "one ms before the tide opens");

            state.simNowUnixMs = OpenMs;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"), "the opening ms is inside");

            state.simNowUnixMs = CloseMs - 1L;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"), "the sabbat night's last ms is inside");

            state.simNowUnixMs = CloseMs;
            Assert.That(Wheel.OpenTide(state, _data), Is.Null, "the tide closes at the fire — midnight after the night");
        }

        [Test]
        public void OpenTide_Hemispheres_ReadTheirOwnNights()
        {
            var north = InTide();
            var south = InTide();
            south.hemisphere = Wheel.HemisphereSouth;
            south.wheelCache = null;

            Assert.That(Wheel.OpenTide(north, _data), Is.Not.Null, "the north's window is open at this cursor");
            Assert.That(Wheel.OpenTide(south, _data), Is.Null, "the south's Beltane sits half a year away");

            south.simNowUnixMs += 182 * DayMs;
            Assert.That(Wheel.OpenTide(south, _data)?.id, Is.EqualTo("beltane"),
                "half a year on, the south's own window is the open one");
        }

        [Test]
        public void OpenTide_UtcOffset_MovesTheEdgesToWardenLocalMidnight()
        {
            var state = InTide();
            state.utcOffsetMinutes = 780; // UTC+13 — a New Zealand summer
            state.wheelCache = null;

            // Local midnight falls 13 hours earlier in UTC.
            state.simNowUnixMs = CloseMs - 780L * 60000L;
            Assert.That(Wheel.OpenTide(state, _data), Is.Null,
                "the tide closes at WARDEN-local midnight, not UTC midnight");

            state.simNowUnixMs -= 1L;
            Assert.That(Wheel.OpenTide(state, _data), Is.Not.Null);
        }

        [Test]
        public void NextSabbat_NamesTheComingNight_AndRunsOutHonestly()
        {
            var state = Fallow();

            var next = Wheel.NextSabbat(state, _data, out var nightStartMs);
            Assert.That(next?.id, Is.EqualTo("beltane"));
            Assert.That(nightStartMs, Is.EqualTo(NightDay * DayMs), "the night itself, not the tide's opening");

            state.simNowUnixMs = CloseMs + DayMs;
            Assert.That(Wheel.NextSabbat(state, _data, out _), Is.Null,
                "a calendar authored out is null, never a guess — the evergreen rule is about topping up data");
        }

        [Test]
        public void NextNightOf_GivesTheNightAndItsTideOpening()
        {
            var state = Fallow();
            var beltane = _data.wheel.sabbats[0];

            Assert.That(Wheel.NextNightOf(state, _data, beltane, out var nightMs, out var opensMs), Is.True);
            Assert.That(nightMs, Is.EqualTo(NightDay * DayMs), "the night itself");
            Assert.That(opensMs, Is.EqualTo(OpenMs),
                "and when its tide opens — the shelf and the rail both count down to the OPENING, "
                + "which is the moment anything can be done about it");
        }

        [Test]
        public void NextNightOf_StillNamesTheNightFromInsideItsOwnTide()
        {
            // The Record's shelf asks about every sabbat, the open one included,
            // and a window the cursor is already inside must not be skipped as
            // past — the night has not fallen yet.
            var state = InTide();
            var beltane = _data.wheel.sabbats[0];

            Assert.That(Wheel.NextNightOf(state, _data, beltane, out var nightMs, out _), Is.True);
            Assert.That(nightMs, Is.EqualTo(NightDay * DayMs));
        }

        [Test]
        public void NextNightOf_RunsOutHonestly_AndStaysInertWhenTheWheelIs()
        {
            var state = Fallow();
            var beltane = _data.wheel.sabbats[0];

            state.simNowUnixMs = CloseMs + DayMs;
            Assert.That(Wheel.NextNightOf(state, _data, beltane, out _, out _), Is.False,
                "past the authored nights it must say so rather than extrapolate a calendar");

            var unset = Fallow();
            unset.hemisphere = Wheel.HemisphereUnset;
            unset.wheelCache = null;
            Assert.That(Wheel.NextNightOf(unset, _data, beltane, out _, out _), Is.False,
                "an unset hemisphere has no dates — the mirror is the whole calendar");
        }

        [Test]
        public void OpenDaysBefore_FallsBackToTheShippedMonth()
        {
            Assert.That(Wheel.OpenDaysBefore(_data), Is.EqualTo(14), "authored data wins");

            _data.wheel.openDaysBefore = 0;
            Assert.That(Wheel.OpenDaysBefore(_data), Is.EqualTo(30),
                "unauthored falls back to the shipped month, not to the fortnight it used to be");
        }

        [Test]
        public void YieldMult_LeansOnlyTheNamedResource_AndOnlyWhileTheTideHolds()
        {
            var open = InTide();
            var fallow = Fallow();

            Assert.That(Wheel.YieldMult(open, _data, "wildflowers"), Is.EqualTo(1.2).Within(1e-12));
            Assert.That(Wheel.YieldMult(open, _data, "berries"), Is.EqualTo(1.0),
                "a lean must not bleed into finds the sabbat never named");
            Assert.That(Wheel.YieldMult(fallow, _data, "wildflowers"), Is.EqualTo(1.0),
                "the fallow weeks are plain — the lean lapses at the fire");
        }

        [Test]
        public void YieldPerSecond_InsideTheTide_PaysExactlyTheLean()
        {
            var open = InTide();
            var fallow = Fallow();
            var node = open.nodes.Find(n => n.resourceId == "wildflowers");
            var plainNode = fallow.nodes.Find(n => n.resourceId == "wildflowers");
            TestKith.Station(open, node.id, 1);
            TestKith.Station(fallow, plainNode.id, 1);
            var leaned = Simulation.YieldPerSecond(node, open, _data, _data.economy).ToDouble();
            var plain = Simulation.YieldPerSecond(plainNode, fallow, _data, _data.economy).ToDouble();

            Assert.That(plain, Is.GreaterThan(0.0), "the fixture must actually gather, or the ratio proves nothing");
            Assert.That(leaned / plain, Is.EqualTo(1.2).Within(1e-9),
                "the tide is one clean factor in the §9 stack — nothing else may move with it");
        }

        [Test]
        public void TouchLanes_ReadAtTheirHookSites()
        {
            var open = InTide();
            var fallow = Fallow();

            Assert.That(Wheel.DigSpeedMult(open, _data), Is.EqualTo(1.2).Within(1e-12), "Samhain's lane: the watch");
            Assert.That(Wheel.CraftSpeedMult(open, _data, "firecraft"), Is.EqualTo(1.2).Within(1e-12), "Yule's lane: the fire");
            Assert.That(Wheel.CraftSpeedMult(open, _data, "bushcraft"), Is.EqualTo(1.0), "a skill the touch never named");
            Assert.That(Wheel.BubbleRewardBonus(open, _data), Is.EqualTo(0.2).Within(1e-12), "Litha's lane: the windfalls");
            Assert.That(Wheel.ReplantCostMult(open, _data), Is.EqualTo(0.8).Within(1e-12), "Ostara's lane: the sowing");
            Assert.That(Wheel.SpreadEase(open, _data), Is.EqualTo(0.05).Within(1e-12), "Mabon's lane: the caravan");

            Assert.That(Wheel.DigSpeedMult(fallow, _data), Is.EqualTo(1.0));
            Assert.That(Wheel.BubbleRewardBonus(fallow, _data), Is.EqualTo(0.0));
            Assert.That(Wheel.ReplantCostMult(fallow, _data), Is.EqualTo(1.0));
            Assert.That(Wheel.SpreadEase(fallow, _data), Is.EqualTo(0.0));
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
            var fallow = Fallow();

            var eased = Exchange.Rate(open, _data, "berries", "wildflowers").ToDouble();
            var plain = Exchange.Rate(fallow, _data, "berries", "wildflowers").ToDouble();

            Assert.That(plain, Is.EqualTo(2.0 / 3.0 * 0.85).Within(1e-9));
            Assert.That(eased, Is.EqualTo(2.0 / 3.0 * 0.90).Within(1e-9),
                "Mabon shaves the spread's points — it must never touch the trade values themselves");
        }

        [Test]
        public void BubbleReward_InsideTheTide_JoinsTheAdditiveBand()
        {
            var open = InTide();
            var fallow = Fallow();

            var leaned = Bubbles.RewardFor(open, _data, open.nodes[0]).ToDouble();
            var plain = Bubbles.RewardFor(fallow, _data, fallow.nodes[0]).ToDouble();

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

            Assert.That(Wheel.OpenTide(state, _data), Is.Not.Null);

            // Walk the cursor over the close — the cached window must expire.
            state.simNowUnixMs = CloseMs + 1L;
            Assert.That(Wheel.OpenTide(state, _data), Is.Null, "the cache must expire at the window's edge");

            // Flip the hemisphere — the cache keys on it.
            state.simNowUnixMs = (NightDay + 182) * DayMs - 1L;
            state.hemisphere = Wheel.HemisphereSouth;
            Assert.That(Wheel.OpenTide(state, _data)?.id, Is.EqualTo("beltane"),
                "a hemisphere change must rebuild the window, not serve the old one");
        }
    }
}
