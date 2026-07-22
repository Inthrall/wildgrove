using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the carrier/haul bottleneck (design §2 gather → haul → camp) with
    /// deliberately tight haul numbers: goods wait in per-node baskets, carriers
    /// deliver them in discrete single-resource batches (design §5 — the unit a
    /// quality roll will attach to), full baskets overflow and the excess is
    /// lost, and a long offline tick behaves like the same time played live.
    /// The fixture's cadence: one carrier, tripSeconds 2 → one 1-unit delivery
    /// every 2 s (0.5/s average).
    /// </summary>
    public class HaulTests
    {
        private const double Tolerance = 1e-6;

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
                // One carrier moves 0.5 units/sec — half a lone gatherer's rate,
                // so the bottleneck binds immediately.
                // Two slots so the factory stations both seeds (vole + raven) —
                // these fixtures exercise the gather→haul pipeline, not the ladder.
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                hauling = new EconomyData.HaulingData { baseCarryCapacity = 1.0, tripSeconds = 2.0, basketCapacity = 10.0 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries", "wildflowers" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 2, id = "waxed-satchel",
                    effects = { new EffectData { type = EffectType.HaulMult, value = 1.5 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Advance_CarriersSlowerThanGathering_SplitsBetweenCampAndBasket()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 4.0);

            // Gathered 4; deliveries of 1 land at t=2 and t=4 → 2 at camp,
            // 2 still in the basket.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void Advance_FullBasket_OverflowsAndTheExcessIsLost()
        {
            _data.economy.hauling.basketCapacity = 2.0;
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // A 1-unit delivery lands every 2 s (the basket is never empty), so
            // camp holds 5; the basket sits in its steady state and the other
            // 4 gathered units overflowed and are gone.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_NoCarriers_NothingReachesCamp()
        {
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.IsOnTrail);

            Simulation.Advance(state, _data, 5.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Advance_HaulMultUpgrade_RaisesThroughput()
        {
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("waxed-satchel");

            Simulation.Advance(state, _data, 4.0);

            // The upgrade widens the load, not the cadence: 1.5-unit deliveries
            // at t=2 and t=4 → 3 at camp, 1 waiting.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_CarriersAlternateBetweenEquallyBusyNodes()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[1].id, 1); // both nodes gathering at 1/s

            Simulation.Advance(state, _data, 4.0);

            // Each delivery heads for the fullest basket, so two equally busy
            // nodes are visited in turn and neither starves.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.nodes[1].basket.ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
        }

        [Test]
        public void Advance_NothingReachesCampBetweenDeliveries()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 3.0);

            // One delivery has landed (t=2); the continuous model would show
            // 1.5 at camp by now — discreteness is the point (design §5).
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void Advance_ADeliveryIsOneResource()
        {
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.stationId == state.nodes[0].id);
            state.nodes[0].basket = new BigDouble(5.0);
            state.nodes[1].basket = new BigDouble(2.0);

            Simulation.Advance(state, _data, 2.0);

            // The single delivery takes from the fullest basket only — a haul
            // batch is one resource, the unit a quality roll attaches to.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(state.nodes[1].basket.ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void Advance_MoreCarriers_ShortenTheDeliveryCadence()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, Familiar.TrailStation, 1); // second carrier: a delivery every 1 s

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_IdleCarriers_DoNotBankTripProgress()
        {
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.stationId == state.nodes[0].id); // nothing gathering anywhere

            Simulation.Advance(state, _data, 10.0);
            state.nodes[0].basket = new BigDouble(5.0);
            Simulation.Advance(state, _data, 1.0);

            // The 10 idle seconds didn't count as trips-in-waiting: goods
            // appearing start a fresh trip, so 1 s in nothing has landed yet.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_PartialLoad_DeliversWhatIsWaiting()
        {
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.stationId == state.nodes[0].id);
            state.nodes[0].basket = new BigDouble(0.4);

            Simulation.Advance(state, _data, 2.0);

            // A carrier doesn't wait for a full load — the batch is whatever
            // the basket held.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.4).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_LongAbsence_BehavesLikeLivePlay()
        {
            _data.economy.hauling.basketCapacity = 2.0;
            var state = GameStateFactory.NewGame(_data);

            Simulation.AdvanceOffline(state, _data, 100.0);

            // Sub-stepping keeps the catch-up honest: carriers hauled the whole
            // absence (0.5/s → 50 at camp). A single naive tick would clamp the
            // whole absence's gathering into one 2-unit basketful.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
        }
    }
}
