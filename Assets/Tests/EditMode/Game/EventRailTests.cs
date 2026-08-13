using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the events rail's contents (design §15, §10): which cells stand, in what
    /// order, and — the part that matters most — the ones that must NOT stand.
    /// A rail cell is a promise on screen at every tab, so a cell that lights
    /// for a reward nobody can collect is worse than no rail at all.
    /// </summary>
    public class EventRailTests
    {
        private const long DayMs = 86400000L;
        private const int NightDay = 20000;
        private const int OpenDays = 30;
        private static readonly long OpenMs = (NightDay - OpenDays) * DayMs;

        private GameDataAsset _data;
        private readonly List<EventRailEntry> _entries = new List<EventRailEntry>();

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
                openDaysBefore = OpenDays,
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

        private GameState At(long atMs)
        {
            var state = GameStateFactory.NewGame(_data);
            state.hemisphere = Wheel.HemisphereNorth;
            state.utcOffsetMinutes = 0;
            state.simNowUnixMs = atMs;
            return state;
        }

        private GameState InTide() => At(OpenMs + DayMs);

        private GameState Fallow() => At(OpenMs - 10 * DayMs);

        private static EventRailEntry? Find(List<EventRailEntry> entries, string id)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id)
                {
                    return entry;
                }
            }

            return null;
        }

        [Test]
        public void Collect_InsideATide_LeadsWithTheTide_AndCountsToItsClose()
        {
            var state = InTide();

            EventRail.Collect(state, _data, state.simNowUnixMs, false, false, _entries);

            Assert.That(_entries[0].id, Is.EqualTo(EventRail.OpenTideId), "the open tide leads the rail");
            Assert.That(_entries[0].kind, Is.EqualTo(EventRailKind.OpenTide));
            Assert.That(_entries[0].title, Is.EqualTo("Beltane"));
            Assert.That(_entries[0].sabbatId, Is.EqualTo("beltane"), "which the cell's face is resolved from");
            Assert.That(_entries[0].remainingSeconds,
                Is.EqualTo((Wheel.OpenTideCloseMs(state, _data) - state.simNowUnixMs) / 1000.0).Within(1e-6));
        }

        [Test]
        public void Collect_ThroughTheFallowWeeks_CountsToTheNextTideOpening()
        {
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, false, false, _entries);

            var coming = Find(_entries, EventRail.ComingSabbatId);
            Assert.That(coming, Is.Not.Null, "the fallow weeks are exactly when the rail has to speak up");
            Assert.That(coming.Value.title, Is.EqualTo("Beltane"));
            Assert.That(coming.Value.sabbatId, Is.EqualTo("beltane"));
            Assert.That(coming.Value.ready, Is.False);
            Assert.That(coming.Value.remainingSeconds,
                Is.EqualTo((OpenMs - state.simNowUnixMs) / 1000.0).Within(1e-6),
                "counted to the OPENING, not the night — the opening is when it can be acted on");
        }

        [Test]
        public void Collect_NeverGeneratesTheKeepingJustToPaintACell()
        {
            var state = InTide();
            Assert.That(state.keeping, Is.Null, "nothing has looked at the keeping yet");

            EventRail.Collect(state, _data, state.simNowUnixMs, false, false, _entries);

            Assert.That(state.keeping, Is.Null,
                "the rail is on screen at every tab — reading through Keeping.Current would move every "
                + "keeping's draw to the instant its tide opened, against whatever content was unlocked "
                + "then. Chrome must not draw the verse.");
            Assert.That(_entries[0].ready, Is.False, "and with no keeping there is nothing to set down");
        }

        [Test]
        public void Collect_NeverStandsBothWheelCells()
        {
            var open = InTide();
            EventRail.Collect(open, _data, open.simNowUnixMs, false, false, _entries);
            Assert.That(Find(_entries, EventRail.ComingSabbatId), Is.Null,
                "a tide is open; the one after it is a month away and must not spend a fingertip");

            var fallow = Fallow();
            EventRail.Collect(fallow, _data, fallow.simNowUnixMs, false, false, _entries);
            Assert.That(Find(_entries, EventRail.OpenTideId), Is.Null);
        }

        [Test]
        public void Collect_WithAnInertWheel_ShowsNoWheelCellAtAll()
        {
            var state = InTide();
            state.hemisphere = Wheel.HemisphereUnset;
            state.wheelCache = null;

            EventRail.Collect(state, _data, state.simNowUnixMs, false, false, _entries);

            Assert.That(Find(_entries, EventRail.OpenTideId), Is.Null);
            Assert.That(Find(_entries, EventRail.ComingSabbatId), Is.Null,
                "absent is inert, everywhere — the rail must not invent a date the Wheel won't give");
        }

        [Test]
        public void Collect_TheCache_NeverPromisesAmberToASignedOutPlayer()
        {
            _data.economy.amber = new EconomyData.AmberData { weeklyCacheAmber = 20.0 };
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, false, false, _entries);
            var signedOut = Find(_entries, EventRail.WeeklyCacheId);
            Assert.That(signedOut, Is.Not.Null, "the cell still stands — signing in is the thing to do about it");
            Assert.That(signedOut.Value.ready, Is.False,
                "Play cannot leave a cache for somebody it does not know");
            Assert.That(signedOut.Value.mark, Is.EqualTo("sign in"));

            EventRail.Collect(state, _data, state.simNowUnixMs, true, false, _entries);
            var signedIn = Find(_entries, EventRail.WeeklyCacheId);
            Assert.That(signedIn.Value.ready, Is.True, "never claimed and signed in — the week is up");
            Assert.That(signedIn.Value.mark, Is.Null,
                "and no word over the clock: the week turning over is OUR reckoning, and what Play has "
                + "actually set out is only known by asking — so a word here would be promising for it");
        }

        [Test]
        public void Collect_TheCache_CountsTheWardensWeekOut()
        {
            _data.economy.amber = new EconomyData.AmberData { weeklyCacheAmber = 20.0 };
            // Epoch day 19960 is a Sunday, and Fallow() stands on that day's own
            // local midnight at offset 0 — so the week turns over a day out.
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, true, false, _entries);

            var cache = Find(_entries, EventRail.WeeklyCacheId);
            Assert.That(cache.Value.remainingSeconds, Is.EqualTo(86400.0).Within(1e-6),
                "counted to the warden's next Monday, NOT to seven days from the last cache: a player "
                + "who has never claimed has no such anchor, and that is the state every new run is in");
        }

        [Test]
        public void Collect_TheCache_StandsDownOnceTheWeekHasBeenClaimed()
        {
            _data.economy.amber = new EconomyData.AmberData { weeklyCacheAmber = 20.0 };
            var state = Fallow();
            var now = state.simNowUnixMs;
            // Yesterday — the Saturday of the same Monday-to-Sunday week `now`
            // stands in, so this is a cache already taken for THIS week.
            state.weeklyCacheClaimedUnixMs = now - DayMs;

            EventRail.Collect(state, _data, now, true, false, _entries);

            Assert.That(Find(_entries, EventRail.WeeklyCacheId), Is.Null,
                "the week is claimed, so there is nothing to act on — and a cell that spends six days "
                + "in seven saying \"not yet\" is what teaches a player to stop reading the rail");
        }

        [Test]
        public void Collect_WithNoCacheAuthored_LeavesTheRailToTheWheel()
        {
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, true, false, _entries);

            Assert.That(_entries, Has.Count.EqualTo(1),
                "an unconfigured economy shows no empty cell — absent is inert (fixtures)");
        }

        [Test]
        public void Collect_TheTimeSkip_OffersTheHoursWhenThereIsAnAdToHand()
        {
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, true, true, _entries);

            var skip = Find(_entries, EventRail.TimeSkipId);
            Assert.That(skip, Is.Not.Null, "never claimed, so the land owes its hours");
            Assert.That(skip.Value.kind, Is.EqualTo(EventRailKind.TimeSkip));
            Assert.That(skip.Value.ready, Is.True);
            Assert.That(skip.Value.mark, Is.EqualTo("+2h"),
                "the reward, not the verb — \"pass the time\" does not fit a 120-unit cell, and the hours "
                + "are what the tap is for");
            Assert.That(skip.Value.remainingSeconds, Is.LessThan(0.0),
                "nothing to count while it is ready: the mark speaks over the clock");
        }

        [Test]
        public void Collect_TheTimeSkip_CountsItsCooldownOut()
        {
            var state = Fallow();
            var now = state.simNowUnixMs;
            state.timeSkipClaimedUnixMs = now;

            EventRail.Collect(state, _data, now, true, true, _entries);

            var skip = Find(_entries, EventRail.TimeSkipId);
            Assert.That(skip, Is.Not.Null, "the cell stands through the cooldown — the countdown IS the cell");
            Assert.That(skip.Value.ready, Is.False, "taken, so there is nothing to take");
            Assert.That(skip.Value.mark, Is.Null, "and no word over the clock, because the clock is the news");
            Assert.That(skip.Value.remainingSeconds,
                Is.EqualTo(Amber.TimeSkipCooldownMs / 1000.0).Within(1e-6));
        }

        [Test]
        public void Collect_TheTimeSkip_StandsDownWhenTheAdLayerHasNothing()
        {
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, true, false, _entries);

            Assert.That(Find(_entries, EventRail.TimeSkipId), Is.Null,
                "off cooldown with no ad filled and no Remove Ads: the hours cannot be had, so the cell "
                + "does not offer them — a greyed plate saying \"no ad to hand\" is what it replaced");
        }

        [Test]
        public void Collect_TheTimeSkip_StandsLastSoAShortBandDropsItFirst()
        {
            _data.economy.amber = new EconomyData.AmberData { weeklyCacheAmber = 20.0 };
            var state = InTide();

            EventRail.Collect(state, _data, state.simNowUnixMs, true, true, _entries);

            Assert.That(_entries, Has.Count.EqualTo(3));
            Assert.That(_entries[2].id, Is.EqualTo(EventRail.TimeSkipId),
                "the band seats two cells at its floor and three at its ceiling, and this is the one of "
                + "the three that cannot be missed: a sabbat not kept is gone for a year and a cache goes "
                + "with its week, while these hours wait to be taken");
        }

        [Test]
        public void Collect_ClearsWhatWasThereBefore()
        {
            _data.economy.amber = new EconomyData.AmberData { weeklyCacheAmber = 20.0 };
            var state = Fallow();

            EventRail.Collect(state, _data, state.simNowUnixMs, true, false, _entries);
            EventRail.Collect(state, _data, state.simNowUnixMs, true, false, _entries);

            Assert.That(_entries, Has.Count.EqualTo(2),
                "the caller holds one list for the run — a Collect that appended would grow it forever");
        }
    }
}
