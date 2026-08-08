using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the keeping (design §15): generation as persisted facts (a reload
    /// never rerolls), the lean's bias, whole-ask offering with the Rite's own
    /// Renown rules, one-shot tier grants and the year's claim, the fold
    /// redraw that keeps answered slots, the closed-tide gate — and the
    /// containment that matters most: nothing the Rite counts ever moves.
    /// </summary>
    public class KeepingTests
    {
        private const long DayMs = 86400000L;
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
            _data.wheel = new WheelData
            {
                openDaysBefore = 14,
                observance = new ObservanceData
                {
                    slotCount = 5,
                    tierSlots = new List<int> { 1, 3, 5 },
                    tierAmber = new List<double> { 5.0, 10.0, 10.0 },
                    slotValueMult = 2.0,
                    specimenRenown = 40L,
                },
                sabbats = new List<SabbatData>
                {
                    new SabbatData
                    {
                        id = "beltane",
                        displayName = "Beltane",
                        kind = "fire",
                        sign = "Beltane, by my count.",
                        verseLean = new List<string> { "wildflowers" },
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

        private GameState InTide()
        {
            var state = GameStateFactory.NewGame(_data);
            state.hemisphere = Wheel.HemisphereNorth;
            state.utcOffsetMinutes = 0;
            state.simNowUnixMs = OpenMs + DayMs;
            return state;
        }

        [Test]
        public void Current_FallowOrUnconfigured_IsNull()
        {
            var state = InTide();
            state.simNowUnixMs = OpenMs - 30 * DayMs;
            Assert.That(Keeping.Current(state, _data), Is.Null, "the fallow weeks keep nothing");

            state.simNowUnixMs = OpenMs + DayMs;
            _data.wheel.observance = null;
            Assert.That(Keeping.Current(state, _data), Is.Null,
                "a wheel without an observance is touch-only — absent is inert");
        }

        [Test]
        public void Current_GeneratesFactsOnce_AndLeansTheTideWay()
        {
            var state = InTide();

            var keeping = Keeping.Current(state, _data);
            Assert.That(keeping, Is.Not.Null);
            Assert.That(ReferenceEquals(keeping, Keeping.Current(state, _data)), Is.True,
                "a second read must return the same persisted page — nothing re-derives");
            Assert.That(keeping.sabbatId, Is.EqualTo("beltane"));
            Assert.That(keeping.year, Is.EqualTo(Keeping.YearOfEpochDay(NightDay)));

            // Two reachable goods + the specimen slot; the lean draws first.
            Assert.That(keeping.slots, Has.Count.EqualTo(3),
                "two candidate goods and the closing specimen slot — a lean fixture generates a lean page");
            Assert.That(keeping.slots[0].goodsId, Is.EqualTo("wildflowers"),
                "the tide's own goods are drawn first — the authored lean is the theme");
            Assert.That(keeping.slots[2].kind, Is.EqualTo(KeepingSlotState.SpecimenKind),
                "the specimen slot closes the page");
            Assert.That(keeping.slots[0].target, Is.GreaterThan(0.0), "a slot with no ask is not an offering");
        }

        [Test]
        public void TryOffer_TakesTheWholeAskOrNothing_AndCreditsTradeValue()
        {
            var state = InTide();
            var keeping = Keeping.Current(state, _data);
            var slot = keeping.slots[0];
            var short_ = new BreakInfinity.BigDouble(slot.target - 1.0);
            state.resources[slot.goodsId] = short_;

            Assert.That(Keeping.CanOffer(state, _data, 0), Is.False, "a store short of the ask cannot answer it");
            Assert.That(Keeping.TryOffer(state, _data, 0), Is.False);
            Assert.That(state.GetResource(slot.goodsId).ToDouble(), Is.EqualTo(slot.target - 1.0).Within(1e-9),
                "a refused offering must not eat stock");

            state.resources[slot.goodsId] = new BreakInfinity.BigDouble(slot.target + 7.0);
            var renownBefore = state.renown;
            Assert.That(Keeping.TryOffer(state, _data, 0), Is.True);
            Assert.That(state.GetResource(slot.goodsId).ToDouble(), Is.EqualTo(7.0).Within(1e-9),
                "exactly the ask leaves the stores");
            Assert.That(Keeping.IsSlotComplete(keeping.slots[0]), Is.True);

            var value = Economy.TradeValuePerUnit(state, _data, slot.goodsId).ToDouble();
            Assert.That((state.renown - renownBefore).ToDouble(), Is.EqualTo(slot.target * value).Within(1e-6),
                "offerings credit Renown at full trade value — the wheel never taxes prestige either");
        }

        [Test]
        public void Tiers_PayOnce_AndTheFirstWritesTheClaim()
        {
            var state = InTide();
            var keeping = Keeping.Current(state, _data);
            var amberBefore = state.amber;

            state.resources[keeping.slots[0].goodsId] = new BreakInfinity.BigDouble(keeping.slots[0].target);
            Assert.That(Keeping.TryOffer(state, _data, 0), Is.True);

            Assert.That(keeping.tierGranted, Is.EqualTo(1), "one slot answered keeps the eve");
            Assert.That(state.amber - amberBefore, Is.EqualTo(5.0).Within(1e-9), "the eve's little Amber, once");
            Assert.That(Keeping.IsKept(state, "beltane", keeping.year, Wheel.HemisphereNorth), Is.True,
                "the first tier writes the year's claim");
            Assert.That(state.sabbatClaims, Has.Count.EqualTo(1));

            // The remaining goods slot and the specimen cross the second tier —
            // and nothing pays twice.
            state.resources[keeping.slots[1].goodsId] = new BreakInfinity.BigDouble(keeping.slots[1].target);
            Assert.That(Keeping.TryOffer(state, _data, 1), Is.True);
            state.decentResources["berries"] = BreakInfinity.BigDouble.One;
            Assert.That(Keeping.TryOffer(state, _data, 2), Is.True);

            Assert.That(keeping.tierGranted, Is.EqualTo(2), "three answered keeps the day (the wheel needs five)");
            Assert.That(state.amber - amberBefore, Is.EqualTo(15.0).Within(1e-9), "5 at the eve + 10 at the day, each once");
            Assert.That(state.sabbatClaims, Has.Count.EqualTo(1), "one claim per (sabbat, year, hemisphere), ever");
        }

        [Test]
        public void Specimen_TakesOneDecentFind_AndPaysItsAuthoredRenown()
        {
            var state = InTide();
            var keeping = Keeping.Current(state, _data);
            var specimenIndex = keeping.slots.Count - 1;

            Assert.That(Keeping.CanOffer(state, _data, specimenIndex), Is.False, "no Decent find in hand");

            state.decentResources["berries"] = new BreakInfinity.BigDouble(2.0);
            var renownBefore = state.renown;
            Assert.That(Keeping.TryOffer(state, _data, specimenIndex), Is.True);
            Assert.That(state.decentResources["berries"].ToDouble(), Is.EqualTo(1.0).Within(1e-9));
            Assert.That((state.renown - renownBefore).ToDouble(), Is.EqualTo(40.0).Within(1e-9),
                "the specimen slot pays its authored grant — the luck lane stays the cheap one");
        }

        [Test]
        public void Keeping_NeverTouchesTheRitesLedgers()
        {
            var state = InTide();
            var keeping = Keeping.Current(state, _data);
            var nodesBefore = state.nodes.Count;

            state.resources[keeping.slots[0].goodsId] = new BreakInfinity.BigDouble(keeping.slots[0].target);
            Keeping.TryOffer(state, _data, 0);

            Assert.That(state.verseProgress, Is.Empty,
                "the keeping's slots are its own — verseProgress is the Rite's ledger and must never see them");
            Assert.That(state.foldedVersesSung, Is.EqualTo(0), "no verse was sung — a kept slot is not a Rite verse");
            Assert.That(state.nodes.Count, Is.EqualTo(nodesBefore), "no ground opens for a keeping");
        }

        [Test]
        public void Fold_KeepsAnsweredSlots_AndRedrawsTheRest()
        {
            var state = InTide();
            var keeping = Keeping.Current(state, _data);
            state.resources[keeping.slots[0].goodsId] = new BreakInfinity.BigDouble(keeping.slots[0].target);
            Keeping.TryOffer(state, _data, 0);
            var keptGoods = keeping.slots[0].goodsId;
            var keptDelivered = keeping.slots[0].delivered;

            state.migrationCount += 1;
            var redrawn = Keeping.Current(state, _data);

            Assert.That(ReferenceEquals(redrawn, keeping), Is.True, "the page itself crosses the fold");
            Assert.That(redrawn.generatedForMigration, Is.EqualTo(state.migrationCount));
            Assert.That(redrawn.slots[0].goodsId, Is.EqualTo(keptGoods), "an answered slot stays answered, verbatim");
            Assert.That(redrawn.slots[0].delivered, Is.EqualTo(keptDelivered));
            Assert.That(redrawn.slots[redrawn.slots.Count - 1].kind, Is.EqualTo(KeepingSlotState.SpecimenKind),
                "the redraw closes with the specimen slot again");
            for (var i = 1; i < redrawn.slots.Count - 1; i++)
            {
                Assert.That(redrawn.slots[i].goodsId, Is.Not.EqualTo(keptGoods),
                    "a page never asks for the same offering twice");
                Assert.That(redrawn.slots[i].delivered, Is.EqualTo(0.0), "a fresh ask starts unanswered");
            }
        }

        [Test]
        public void ClosedTide_GatesEveryOffer_ButTheRecordRemains()
        {
            var state = InTide();
            Keeping.Current(state, _data);

            state.simNowUnixMs = CloseMs + 1L;
            Assert.That(Keeping.Current(state, _data), Is.Null, "the tide has closed — the fire takes no more");
            Assert.That(Keeping.CanOffer(state, _data, 0), Is.False);
            Assert.That(state.keeping, Is.Not.Null, "the page stays behind as the record until the next tide");
        }
    }
}
