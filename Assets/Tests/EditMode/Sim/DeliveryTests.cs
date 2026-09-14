using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the delivery cadence that replaced carrier hauling (design §2
    /// gather → camp): goods pool at their node and land at camp as one
    /// batch per node every batchSeconds (design §5 — the unit a quality
    /// roll attaches to). A cadence, not a cap: nothing gathered is ever
    /// lost, no body is spent carrying, and a long offline tick behaves
    /// like the same time played live. The fixture's cadence: one batch
    /// every 2 s.
    /// </summary>
    public class DeliveryTests
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
                kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 },
                delivery = new EconomyData.DeliveryData { batchSeconds = 2.0 },
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
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Advance_GatheringPoolsAtTheNodeBetweenBatches()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Simulation.Advance(state, _data, 1.0);

            // Mid-cadence nothing has landed yet — discreteness is the point
            // (design §5): the batch is the unit a quality roll attaches to.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_EveryBatchLandsEverythingPooled_NothingIsEverLost()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Simulation.Advance(state, _data, 4.0);

            // Gathered 4 over two full cadences; every unit reaches camp.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_EveryNodeDeliversOnTheSameCadence()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            TestKith.Station(state, state.nodes[1].id, 1);

            Simulation.Advance(state, _data, 2.0);

            // One batch per node per cadence — no node waits its turn behind
            // another, because no one is walking the trail any more.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void Advance_IdleGrove_DoesNotBankDeliveryProgress()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);
            state.nodes[0].basket = new BigDouble(5.0);
            Simulation.Advance(state, _data, 1.0);

            // The 10 idle seconds didn't count as batches-in-waiting: goods
            // appearing start a fresh cadence, so 1 s in nothing has landed.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void Advance_PartialPool_DeliversWhatIsWaiting()
        {
            var state = GameStateFactory.NewGame(_data);
            state.nodes[0].basket = new BigDouble(0.4);

            Simulation.Advance(state, _data, 2.0);

            // A batch is whatever pooled — there is no load to fill.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.4).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_NoDeliveryConfig_GoodsGoStraightToCamp()
        {
            _data.economy.delivery = null;
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Simulation.Advance(state, _data, 3.0);

            // Hand-built fixtures without a delivery section keep the
            // un-batched behaviour.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void AdvanceOffline_LongAbsence_BehavesLikeLivePlay()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Simulation.AdvanceOffline(state, _data, 100.0);

            // Sub-stepping keeps the catch-up honest: the whole absence's
            // gathering reaches camp, batch by batch, exactly as live play
            // would have landed it.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void DeliveryProgress_WalksTheCadenceAndStartsOverOnLanding()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Simulation.Advance(state, _data, 1.5);

            // Three quarters through the fixture's 2 s cadence — what the node
            // card's band is drawing.
            Assert.That(Simulation.DeliveryProgress(state, _data), Is.EqualTo(0.75).Within(Tolerance));

            Simulation.Advance(state, _data, 0.5);

            // The batch landed and the count starts over rather than sitting
            // full: a band left at 100% would say a delivery is still owed.
            Assert.That(Simulation.DeliveryProgress(state, _data), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void DeliveryProgress_IdleGrove_ReadsEmpty()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // Nothing is pooled anywhere, so there is nothing to count down to
            // — the same parked clock Advance_IdleGrove_DoesNotBankDeliveryProgress
            // pins from the sim's side.
            Assert.That(Simulation.DeliveryProgress(state, _data), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void DeliveryProgress_NoDeliveryConfig_ReadsEmptyAndSaysSo()
        {
            _data.economy.delivery = null;
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);

            Simulation.Advance(state, _data, 3.0);

            // Goods went straight to camp, so there is no cadence to draw and
            // the card leaves the gauge off rather than standing an empty track.
            Assert.That(Simulation.DeliveriesConfigured(_data), Is.False);
            Assert.That(Simulation.DeliveryProgress(state, _data), Is.EqualTo(0.0).Within(Tolerance));
        }
    }
}
