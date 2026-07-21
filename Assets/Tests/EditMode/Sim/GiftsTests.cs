using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the gift event (design §4): unlocked when the run's first verse is
    /// answered, one pile of the node's own resource recruits one deterministic
    /// arrival stationed at that node — one pile, one yes, never a cost curve.
    /// Availability derives from the roster, so it survives the save round trip.
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
                kith = new EconomyData.KithData { slotsBase = 4, slotsMax = 6 },
                gifts = new EconomyData.GiftsData { pileGoods = 10, species = "meadow-vole" },
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
                },
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
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

        /// <summary>Answer the first verse directly — the unlock cares that a verse is sung, not how.</summary>
        private static void AnswerFirstVerse(GameState state)
        {
            state.verseProgress.Add(new VerseProgressState
            {
                verseId = "verse-sunfield",
                slots = { new SlotProgressState { delivered = 10 } },
            });
        }

        [Test]
        public void IsAvailable_BeforeAVerseIsAnswered_IsFalse()
        {
            var state = GameStateFactory.NewGame(_data);
            state.resources["berries"] = 25;

            Assert.That(Gifts.IsAvailable(state, _data), Is.False,
                "the land answers gifts only after the warden has answered it first");
        }

        [Test]
        public void IsAvailable_OnceAVerseIsAnswered_IsTrue()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);

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
        public void IsAvailable_WithNoSlotOpen_WaitsForRoom()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            TestKith.Station(state, state.nodes[0].id, 2);

            Assert.That(Kith.HasRoom(state, _data), Is.False);
            Assert.That(Gifts.IsAvailable(state, _data), Is.False, "recruitment waits for an open slot");
        }

        [Test]
        public void LeavePile_SpendsTheNodesOwnResourceAndStationsTheArrival()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            var node = state.nodes[1];
            Assert.That(node.resourceId, Is.EqualTo("nuts"), "the pile is left where the player chooses, not the first node");
            state.resources["nuts"] = 25;

            var arrived = Gifts.LeavePile(state, _data, node);

            Assert.That(arrived, Is.Not.Null);
            Assert.That(arrived.speciesId, Is.EqualTo("meadow-vole"));
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
            var node = state.nodes[0];
            state.resources["berries"] = 9;

            var arrived = Gifts.LeavePile(state, _data, node);

            Assert.That(arrived, Is.Null);
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(9.0));
            Assert.That(state.roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void LeavePile_OnePileOneYes_ASecondPileTemptsNoOne()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            state.resources["berries"] = 100;

            Assert.That(Gifts.LeavePile(state, _data, state.nodes[0]), Is.Not.Null);

            Assert.That(Gifts.HasGifted(state), Is.True);
            Assert.That(Gifts.IsAvailable(state, _data), Is.False);
            Assert.That(Gifts.LeavePile(state, _data, state.nodes[0]), Is.Null);
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(90.0), "the refused pile spends nothing");
        }

        [Test]
        public void Gifted_SurvivesTheSaveRoundTrip()
        {
            var state = GameStateFactory.NewGame(_data);
            AnswerFirstVerse(state);
            state.resources["berries"] = 25;
            var arrived = Gifts.LeavePile(state, _data, state.nodes[0]);
            Assert.That(arrived, Is.Not.Null);

            var restored = SaveCodec.Restore(SaveCodec.Capture(state, 0L), _data);

            Assert.That(Gifts.HasGifted(restored), Is.True, "one pile, one yes — across app restarts too");
            Assert.That(Gifts.IsAvailable(restored, _data), Is.False);
        }
    }
}
