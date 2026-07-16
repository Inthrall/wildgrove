using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins bonded familiars (design §7): earned — never bought — from a
    /// completed Museum set or an owned Almanac node, with earned state
    /// DERIVED from the source (so bonds cross Migration for free); a bonded
    /// carrier hauls outside the fleet count, its slots, and its gift curve;
    /// a bonded gatherer works the warden's last-tended node outside the
    /// flock count.
    /// </summary>
    public class BondsTests
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
                verdure = new EconomyData.VerdureData { renownDivisor = 5000, exponent = 0.5, yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                hauling = new EconomyData.HaulingData { baseCarryCapacity = 15, tripSeconds = 10, basketCapacity = 60 },
                costGrowth = new EconomyData.CostGrowthData { gathererGift = 1.09, carrierGift = 1.10 },
                gifts = new EconomyData.GiftsData { gathererBaseGoods = 10, carrierBaseGoods = 8 },
                tending = new EconomyData.TendingData { burstYieldMult = 3, burstDurationSec = 5 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
                new ResourceData { id = "nuts", sellValue = 3, skill = "foraging" },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "nuts" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.museumSets = new List<MuseumSetData>
            {
                new MuseumSetData
                {
                    id = "meadow-blooms",
                    displayName = "Meadow Blooms",
                    entries = new List<string> { "berries", "nuts" },
                },
            };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData { id = "old-friend", displayName = "The Old Friend", costVerdure = 12 },
            };
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "sootwing", displayName = "Sootwing, a pack raven", role = "carrier",
                    source = new BondSourceData { type = "museumSet", id = "meadow-blooms" },
                },
                new BondData
                {
                    id = "burr", displayName = "Burr, a meadow vole", role = "gatherer",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void IsEarned_CarrierBond_ArrivesWithTheCompletedMuseumSet()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Bonds.BondedCarriers(state, _data), Is.EqualTo(0));

            state.donatedResources.Add("berries");
            Assert.That(Bonds.BondedCarriers(state, _data), Is.EqualTo(0), "a half-done set bonds nothing");

            state.donatedResources.Add("nuts");
            Assert.That(Bonds.BondedCarriers(state, _data), Is.EqualTo(1));
        }

        [Test]
        public void IsEarned_GathererBond_ArrivesWithTheAlmanacNode()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Bonds.BondedGatherersAt(state, _data, state.nodes[0]), Is.EqualTo(0));

            state.almanacNodeIds.Add("old-friend");
            Assert.That(Bonds.BondedGatherersAt(state, _data, state.nodes[0]), Is.EqualTo(1));
        }

        [Test]
        public void BondedCarrier_HaulsWithNoGiftedCarrierAtAll()
        {
            var state = GameStateFactory.NewGame(_data);
            state.carrierCount = 0;
            state.donatedResources.Add("berries");
            state.donatedResources.Add("nuts");
            state.nodes[0].basket = new BigDouble(30);
            state.nodes[0].familiarCount = 0;

            // One bonded carrier: a delivery every tripSeconds.
            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.GetResource(state.nodes[0].resourceId).ToDouble(), Is.EqualTo(15.0).Within(Tolerance),
                "Sootwing hauls from minute one — no gifted fleet needed");
        }

        [Test]
        public void BondedCarrier_SitsOutsideTheGiftCurveAndTheSlots()
        {
            _data.economy.familiarCaps = new EconomyData.FamiliarCapsData
            {
                flockCapBase = 8,
                carrierSlotsBase = 1,
            };
            var state = GameStateFactory.NewGame(_data);
            state.nodes[0].familiarCount = 1;
            var costBefore = Economy.CarrierGiftCostEach(state, _data.economy);

            state.donatedResources.Add("berries");
            state.donatedResources.Add("nuts");

            Assert.That(Economy.CarrierGiftCostEach(state, _data.economy).ToDouble(),
                Is.EqualTo(costBefore.ToDouble()).Within(Tolerance),
                "the bond never climbs the gift curve");
            Assert.That(state.carrierCount, Is.EqualTo(1), "the seeded carrier fills the only slot");
            Assert.That(Economy.CanGiftCarrier(state, _data), Is.False,
                "the slot check counts gifted carriers only — the bond holds no slot, and takes none");
        }

        [Test]
        public void BondedGatherer_WorksTheWardenLastTendedNode()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");
            var first = state.nodes[0];
            var second = state.nodes[1];
            second.familiarCount = 0;

            // Before any tend the companion waits at the first node.
            Assert.That(Simulation.YieldPerSecond(first, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(first.familiarCount + 1.0).Within(Tolerance));
            Assert.That(Simulation.YieldPerSecond(second, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));

            Simulation.Tend(state, _data, second);

            Assert.That(Simulation.YieldPerSecond(first, state, _data, _data.economy).ToDouble(),
                Is.EqualTo((double)first.familiarCount).Within(Tolerance), "Burr follows the warden");
            Assert.That(Simulation.YieldPerSecond(second, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void BondedGatherer_GathersAloneAtAnEmptyNode()
        {
            // Regression: Step used to gate gathering on familiarCount > 0,
            // so a bonded gatherer at an empty node showed a rate in the HUD
            // while accruing nothing.
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");
            state.carrierCount = 0;
            state.nodes[0].familiarCount = 0;

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(10.0).Within(Tolerance),
                "Burr gathers with or without a flock");
        }

        [Test]
        public void BondedGatherer_StaysOutsideTheFlockCount()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            var flockBefore = state.TotalFamiliars();
            Assert.That(flockBefore, Is.EqualTo(1), "only the seeded gatherer counts — the bond is the warden's, not the flock's");
            Assert.That(Economy.GathererGiftCost(state.nodes[0], _data.economy).ToDouble(),
                Is.EqualTo(10.9).Within(Tolerance), "gift pricing sees only the gifted flock, never the companion");
        }
    }
}
