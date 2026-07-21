using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Compendium's lifetime record (design §5): counters accrue on
    /// the GROSS gather (overflow loses goods, never the record), on the
    /// warden's hand-gather, per crafted batch, and per Pristine unit found;
    /// nothing ever decrements them; discovery is derived from the record.
    /// </summary>
    public class CompendiumTests
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
                // No warden section — the warden-gather test opts in itself so
                // the other records stay exact.
                tending = new EconomyData.TendingData { burstYieldMult = 3, burstDurationSec = 5 },
                crafting = new EconomyData.CraftingData { baseCraftSeconds = 5 },
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
            _data.recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    id = "berry-preserve", output = "berry-preserve", kind = "trade", skill = "foraging",
                    station = "fire", valueMult = 4, defaultKnown = true,
                    inputs = new List<ItemAmount> { new ItemAmount { id = "berries", amount = 4 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Gathering_RecordsTheGross_EvenWhenTheBasketOverflows()
        {
            _data.economy.hauling = new EconomyData.HaulingData { baseCarryCapacity = 15, tripSeconds = 10, basketCapacity = 5 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.IsOnTrail);
            state.roster.RemoveAll(f => f.stationId == state.nodes[0].id);
            TestKith.Station(state, state.nodes[0].id, 10);

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.nodes[0].basket.ToDouble(), Is.EqualTo(5.0).Within(Tolerance), "the basket clamps");
            Assert.That(Compendium.LifetimeGathered(state, "berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance),
                "the record never loses what the basket did");
        }

        [Test]
        public void WardenGather_JoinsTheRecord()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.Clear(); // isolate the warden's own hands

            Simulation.Tend(state, _data, state.nodes[0]);
            Simulation.Advance(state, _data, 5.0);

            // The whole 5 s is bursted at ×3: 0.5 · 5 · 3 = 7.5.
            Assert.That(Compendium.LifetimeGathered(state, "berries").ToDouble(), Is.EqualTo(7.5).Within(Tolerance),
                "the warden's own hands count too");
        }

        [Test]
        public void Crafting_RecordsEveryCompletedBatch()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);
            state.stations.Add(new StationState { stationId = "fire", recipeId = "berry-preserve" });

            Simulation.Advance(state, _data, 10.0);

            Assert.That(Compendium.LifetimeCrafted(state, "berry-preserve"), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void PristineFinds_CountUnitByUnit()
        {
            _data.economy.hauling = new EconomyData.HaulingData { baseCarryCapacity = 15, tripSeconds = 10, basketCapacity = 60 };
            _data.economy.quality = new EconomyData.QualityData { pristineBaseChance = 1.0, fineChance = 0.0, fineValueMult = 1.5, pristineValueMult = 10 };
            var state = GameStateFactory.NewGame(_data);
            state.roster.RemoveAll(f => f.stationId == state.nodes[0].id); // the raven still hauls the manual basket
            state.nodes[0].basket = new BigDouble(30);

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.GetPristine("berries").ToDouble(), Is.EqualTo(15.0).Within(Tolerance), "the whole batch rolled Pristine");
            Assert.That(Compendium.LifetimePristine(state, "berries").ToDouble(), Is.EqualTo(15.0).Within(Tolerance));
        }

        [Test]
        public void Trading_NeverErodesTheRecord()
        {
            _data.exchange = new ExchangeData { spread = 0.15 };
            var state = GameStateFactory.NewGame(_data);
            Simulation.Advance(state, _data, 10.0);
            var recorded = Compendium.LifetimeGathered(state, "berries");
            Assert.That(recorded > BigDouble.Zero, Is.True);

            Exchange.TryTrade(state, _data, "berries", "nuts", state.GetResource("berries"));

            Assert.That(Compendium.LifetimeGathered(state, "berries").ToDouble(),
                Is.EqualTo(recorded.ToDouble()).Within(Tolerance), "counters only ever climb");
        }

        [Test]
        public void Discovery_SpansGatherablesRecipesAndCompanions()
        {
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "burr", displayName = "Burr, a meadow vole", role = "gatherer",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Compendium.TotalEntries(_data), Is.EqualTo(4), "2 gatherables + 1 recipe + 1 companion");
            Assert.That(Compendium.DiscoveredCount(state, _data), Is.EqualTo(0), "discovered by doing, not by starting");

            Compendium.RecordGather(state, "berries", BigDouble.One);
            Compendium.RecordCraft(state, "berry-preserve");
            state.almanacNodeIds.Add("old-friend");

            Assert.That(Compendium.DiscoveredCount(state, _data), Is.EqualTo(3));
        }
    }
}
