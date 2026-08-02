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
    /// warden's hand-gather, per crafted batch, and per Choice unit found;
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
        public void Gathering_RecordsTheGross_WhichNowAllReachesCamp()
        {
            _data.economy.kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 };
            _data.economy.delivery = new EconomyData.DeliveryData { batchSeconds = 10 };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 10);

            Simulation.Advance(state, _data, 10.0);

            // Deliveries are lossless: the record and the camp agree.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance), "nothing gathered is ever lost");
            Assert.That(Compendium.LifetimeGathered(state, "berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void WardenGather_JoinsTheRecord()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 0.5 };
            var state = GameStateFactory.NewGame(_data);
            Warden.Post(state, state.nodes[0]); // tending no longer moves the post

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
        public void ChoiceFinds_CountUnitByUnit()
        {
            _data.economy.kith = new EconomyData.KithData { slotsBase = 2, slotsMax = 6 };
            _data.economy.delivery = new EconomyData.DeliveryData { batchSeconds = 10 };
            _data.economy.quality = new EconomyData.QualityData { choiceBaseChance = 1.0, decentChance = 0.0, decentValueMult = 1.5, choiceValueMult = 10 };
            var state = GameStateFactory.NewGame(_data);
            state.nodes[0].basket = new BigDouble(30);

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.GetChoice("berries").ToDouble(), Is.EqualTo(30.0).Within(Tolerance), "the whole batch rolled Choice");
            Assert.That(Compendium.LifetimeChoice(state, "berries").ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
        }

        [Test]
        public void Trading_NeverErodesTheRecord()
        {
            _data.exchange = new ExchangeData { spread = 0.15 };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
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

        [Test]
        public void RecordProgress_SpansTheBackPages_AndChargesEachPressOnce()
        {
            _data.folioSpreads = new List<FolioSpreadData>
            {
                new FolioSpreadData { id = "meadow-blooms", entries = new List<string> { "berries", "nuts" } },
                // The gallery re-lists a specimen the first spread asks for.
                new FolioSpreadData { id = "wardens-gallery", entries = new List<string> { "berries" } },
            };
            _data.insects = new List<InsectData> { new InsectData { id = "silver-skimmer", sketches = 2 } };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData { id = "old-songs-i", costVerdure = 2 },
                new AlmanacNodeData { id = "the-long-song", costVerdure = 8, repeatable = true },
            };
            _data.deepAmber = new DeepAmberData
            {
                zoneId = GameStateFactory.StartingZoneId, findsPerHour = 1, plateName = "The Deep Amber",
                pieces = new List<AmberPieceData>
                {
                    new AmberPieceData { id = "the-wing" },
                    new AmberPieceData { id = "the-seed" },
                },
            };
            var state = GameStateFactory.NewGame(_data);

            var empty = Compendium.RecordProgress(state, _data);

            // 2 gatherables + 1 recipe + 2 DISTINCT folio entries + 1 plate
            // + 2 amber pieces + 1 authored Almanac line. The gallery's second
            // ask for berries and the endless line count for nothing.
            Assert.That(empty.total, Is.EqualTo(9));
            Assert.That(empty.recorded, Is.EqualTo(0), "a new camp has written nothing");

            Compendium.RecordGather(state, "berries", BigDouble.One);
            state.AddChoice("berries", 1);
            Assert.That(Folio.TryFix(state, _data, "berries"), Is.True);
            state.insectSketches["silver-skimmer"] = 2;
            state.deepAmberFound = 1;
            state.almanacNodeIds.Add("old-songs-i");

            var written = Compendium.RecordProgress(state, _data);

            Assert.That(written.recorded, Is.EqualTo(5),
                "the gathered berry, its press, the plate, one amber piece, one line");
            Assert.That(written.total, Is.EqualTo(empty.total), "the ceiling never moves");
        }

        [Test]
        public void RecordProgress_OnNothing_IsEmptyRatherThanThrowing()
        {
            Assert.That(Compendium.RecordProgress(null, _data), Is.EqualTo((0, 0)));
            Assert.That(Compendium.RecordProgress(GameStateFactory.NewGame(_data), null), Is.EqualTo((0, 0)));
        }
    }
}
