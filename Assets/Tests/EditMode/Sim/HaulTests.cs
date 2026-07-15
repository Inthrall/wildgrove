using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the carrier/haul bottleneck (design §2 gather → haul → camp) with
    /// deliberately tight haul numbers: goods wait in per-node baskets, carriers
    /// move them at their throughput, full baskets overflow and the excess is
    /// lost, and a long offline tick behaves like the same time played live.
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
                    order = 2, id = "waxed-satchel", costCoin = 150,
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

            // Gathered 4, hauled at 0.5/s → 2 at camp, 2 still in the basket.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void Advance_FullBasket_OverflowsAndTheExcessIsLost()
        {
            _data.economy.hauling.basketCapacity = 2.0;
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // Hauling moves 0.5 every second throughout (the basket is never
            // empty), so camp holds 5; the basket sits in its steady state and
            // the other 3.5 gathered units overflowed and are gone.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(1.5).Within(Tolerance));
        }

        [Test]
        public void Advance_NoCarriers_NothingReachesCamp()
        {
            var state = GameStateFactory.NewGame(_data);
            state.carrierCount = 0;

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

            // 0.5/s × 1.5 = 0.75/s → 3 at camp, 1 waiting.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Advance_SplitsHaulAcrossBasketsProportionally()
        {
            var state = GameStateFactory.NewGame(_data);
            state.nodes[1].familiarCount = 1; // both nodes gathering at 1/s

            Simulation.Advance(state, _data, 4.0);

            // The 0.5/s of carriage is shared evenly between two equal baskets.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.nodes[1].basket.ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
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
