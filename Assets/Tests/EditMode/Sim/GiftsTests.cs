using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the gift piles (design §4): every verse sung earns one pile, one
    /// yes. The pile is left at a node and that resource's specialist answers,
    /// stationed there — where the pile is left is who comes. A pile is
    /// refused where the specialist already walks; piles answered derive from
    /// the roster, so they survive the save round trip.
    /// </summary>
    public class GiftsTests
    {
        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                kith = new EconomyData.KithData
                {
                    slotsBase = 1,
                    slotsMax = 6,
                    verseMilestones = new List<int> { 2, 5, 10 },
                },
                gifts = new EconomyData.GiftsData { pileGoods = 10 },
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
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = "meadow-vole", displayName = "meadow vole", roleLean = "gatherer",
                    suggestedNames = new List<string> { "Bramble", "Clover" },
                    trait = new TraitData { displayName = "Berry-wise", kind = "nodeYieldBonus", value = 0.4, resource = "berries" },
                },
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
                    trait = new TraitData { displayName = "Deep pockets", kind = "trailThroughputBonus", value = 0.25 },
                },
                new SpeciesData
                {
                    id = "red-squirrel", displayName = "red squirrel", roleLean = "gatherer",
                    suggestedNames = new List<string> { "Cob" },
                    trait = new TraitData { displayName = "Mast-hoarder", kind = "nodeYieldBonus", value = 0.4, resource = "nuts" },
                },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                rites = new List<RiteData>
                {
                    new RiteData
                    {
                        id = "first-rite",
                        migration = 0,
                        verses =
                        {
                            new RiteVerseData
                            {
                                id = "verse-sunfield",
                                zone = GameStateFactory.StartingZoneId,
                                slots =
                                {
                                    new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 10 },
                                },
                            },
                        },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        /// <summary>Answer the first verse directly — the pile count cares that a verse is sung, not how.</summary>
        private static void AnswerFirstVerse(GameState state)
        {
            state.verseProgress.Add(new VerseProgressState
            {
                verseId = "verse-sunfield",
                slots = { new SlotProgressState { delivered = 10 } },
            });
        }

        /// <summary>The nut node, with stock for a pile and a slot opened by resting the seed vole.</summary>
        private NodeState ReadyNutNode(GameState state)
        {
            state.resources["nuts"] = 25;
            Roster.Station(state, _data, Roster.OfSpecies(state, "meadow-vole"), null);
            return state.nodes[1];
        }

        [Test]
        public void IsAvailable_BeforeAVerseIsAnswered_IsFalse()
        {
            var state = GameStateFactory.NewGame(_data);
            state.resources["berries"] = 25;

            Assert.That(Gifts.IsAvailable(state, _data), Is.False, "no verse sung — no pile earned");
        }

        [Test]
        public void IsAvailable_OnceAVerseIsAnswered_IsTrue()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);

            Assert.That(Gifts.PilesRemaining(state, _data), Is.EqualTo(1));
            Assert.That(Gifts.IsAvailable(state, _data), Is.True);
        }

        [Test]
        public void IsAvailable_WithoutGiftConfig_IsFalse()
        {
            _data.economy.gifts = null;
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);

            Assert.That(Gifts.IsAvailable(state, _data), Is.False, "hand-built fixture data no-ops");
        }

        [Test]
        public void NodeCanCall_PointsAtTheUnmetSpecialist()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);

            Assert.That(Gifts.NodeCanCall(state, _data, state.nodes[0]), Is.False,
                "the berry specialist already walks — no one new answers a berry pile");
            Assert.That(Gifts.NodeCanCall(state, _data, state.nodes[1]), Is.True);
            Assert.That(Gifts.SpecialistFor(_data, state.nodes[1]).id, Is.EqualTo("red-squirrel"));
        }

        [Test]
        public void CanLeavePile_WithNoSlotOpen_WaitsForRoom()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            state.resources["nuts"] = 25;
            Assert.That(Kith.HasRoom(state, _data), Is.False);

            Assert.That(Gifts.CanLeavePile(state, _data, state.nodes[1]), Is.False,
                "the arrival takes the node as its post — it needs a slot");

            Roster.Station(state, _data, Roster.OfSpecies(state, "meadow-vole"), null);
            Assert.That(Gifts.CanLeavePile(state, _data, state.nodes[1]), Is.True);
        }

        [Test]
        public void LeavePile_SpendsTheNodesOwnResourceAndStationsTheSpecialist()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            var node = ReadyNutNode(state);

            var arrived = Gifts.LeavePile(state, _data, node);

            Assert.That(arrived, Is.Not.Null);
            Assert.That(arrived.speciesId, Is.EqualTo("red-squirrel"), "the pile calls the node resource's specialist");
            Assert.That(arrived.gifted, Is.True);
            Assert.That(arrived.stationId, Is.EqualTo(node.id), "the newcomer works where the pile was left");
            Assert.That(arrived.name, Is.Not.Empty, "named on arrival like any recruit");
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(15.0), "the pile is spent from camp stock");
            Assert.That(state.roster.Count, Is.EqualTo(3));
        }

        [Test]
        public void LeavePile_StockShort_RefusesAndChangesNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            var node = ReadyNutNode(state);
            state.resources["nuts"] = 9;

            var arrived = Gifts.LeavePile(state, _data, node);

            Assert.That(arrived, Is.Null);
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(9.0));
            Assert.That(state.roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void LeavePile_OnePilePerVerse_ASecondNeedsAnotherSung()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            var node = ReadyNutNode(state);

            Assert.That(Gifts.LeavePile(state, _data, node), Is.Not.Null);

            Assert.That(Gifts.GiftedCount(state), Is.EqualTo(1));
            Assert.That(Gifts.PilesRemaining(state, _data), Is.EqualTo(0));
            Assert.That(Gifts.IsAvailable(state, _data), Is.False, "the verse's pile is answered");

            // Another verse sung (banked from a folded run here) earns another pile.
            state.foldedVersesSung = 1;
            Assert.That(Gifts.PilesRemaining(state, _data), Is.EqualTo(1));
            Assert.That(Gifts.IsAvailable(state, _data), Is.True);
        }

        [Test]
        public void Gifted_SurvivesTheSaveRoundTrip()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            var node = ReadyNutNode(state);
            Assert.That(Gifts.LeavePile(state, _data, node), Is.Not.Null);

            var restored = SaveCodec.Restore(SaveCodec.Capture(state, 0L), _data);

            Assert.That(Gifts.GiftedCount(restored), Is.EqualTo(1), "one pile, one yes — across app restarts too");
            Assert.That(Gifts.IsAvailable(restored, _data), Is.False);
        }
    }
}
