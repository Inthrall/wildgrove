using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// The durable half of the Play Games Rewards path (design §11): a
    /// single-use reward is an entitlement the store re-resolves on every
    /// device, so folding it in must be additive, idempotent, and never able to
    /// take back what the save already remembers.
    /// </summary>
    public class PlayRewardsTests
    {
        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = Familiar.PonySpecies, displayName = "fell pony", roleLean = "carrier",
                    suggestedNames = new List<string> { "Moss" },
                    trait = new TraitData { displayName = "Half-broke", kind = "wardenYieldBonus", value = 0.5 },
                },
            };
            _data.insects = new List<InsectData>
            {
                new InsectData
                {
                    id = PlayRewards.WayfarersPlateId, displayName = "The Wayfarer's Plate",
                    sketches = 1, rarity = 0, rewarded = true,
                    effects = new List<EffectData>
                    {
                        new EffectData { type = EffectType.ChoiceChanceBonus, value = 0.005 },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void ApplyDroversHalter_WhenOwned_LandsHerOnceAndReportsIt()
        {
            var state = new GameState();

            Assert.That(PlayRewards.ApplyDroversHalter(state, _data, true), Is.True, "it landed just now");
            Assert.That(state.droversHalterOwned, Is.True);

            var pony = Roster.OfSpecies(state, Familiar.PonySpecies);
            Assert.That(pony, Is.Not.Null, "the pony arrives with the halter");
            Assert.That(pony.stationId, Is.EqualTo(Familiar.PonyStation), "and stands in her lane");

            Assert.That(PlayRewards.ApplyDroversHalter(state, _data, true), Is.False,
                "a re-delivered entitlement has nothing new to say");
            Assert.That(state.roster.Count, Is.EqualTo(1), "and never mints a second pony");
        }

        [Test]
        public void ApplyDroversHalter_WhenNotOwned_ChangesNothing()
        {
            var state = new GameState();

            Assert.That(PlayRewards.ApplyDroversHalter(state, _data, false), Is.False);
            Assert.That(state.droversHalterOwned, Is.False);
            Assert.That(Roster.OfSpecies(state, Familiar.PonySpecies), Is.Null);
        }

        [Test]
        public void ApplyDroversHalter_NeverRevokesWhatTheSaveRemembers()
        {
            // Offline, mid-connect, or a billing hiccup: the store answering
            // "not owned" must not turn a redeemed pony back out.
            var state = new GameState();
            PlayRewards.ApplyDroversHalter(state, _data, true);

            Assert.That(PlayRewards.ApplyDroversHalter(state, _data, false), Is.False);
            Assert.That(state.droversHalterOwned, Is.True, "the redemption holds");
            Assert.That(Roster.OfSpecies(state, Familiar.PonySpecies), Is.Not.Null);
        }

        [Test]
        public void ApplyDroversHalter_OnANullRun_IsSafe()
        {
            // The store can resolve entitlements before a run is loaded.
            Assert.That(PlayRewards.ApplyDroversHalter(null, _data, true), Is.False);
        }

        [Test]
        public void ApplyWayfarersPlate_WhenOwned_RecordsThePlateOnceAndReportsIt()
        {
            var state = new GameState();

            Assert.That(PlayRewards.ApplyWayfarersPlate(state, _data, true), Is.True, "it landed just now");
            Assert.That(state.wayfarersPlateOwned, Is.True);
            Assert.That(Insects.IsRecorded(state, _data.insects[0]), Is.True, "and arrives finished");

            Assert.That(PlayRewards.ApplyWayfarersPlate(state, _data, true), Is.False,
                "a re-delivered entitlement has nothing new to say");
            Assert.That(Insects.SketchCount(state, PlayRewards.WayfarersPlateId), Is.EqualTo(1));
        }

        [Test]
        public void ApplyWayfarersPlate_WhenNotOwned_ChangesNothing()
        {
            var state = new GameState();

            Assert.That(PlayRewards.ApplyWayfarersPlate(state, _data, false), Is.False);
            Assert.That(state.wayfarersPlateOwned, Is.False);
            Assert.That(Insects.SketchCount(state, PlayRewards.WayfarersPlateId), Is.EqualTo(0));
        }

        [Test]
        public void ApplyWayfarersPlate_NeverRevokesWhatTheSaveRemembers()
        {
            var state = new GameState();
            PlayRewards.ApplyWayfarersPlate(state, _data, true);

            Assert.That(PlayRewards.ApplyWayfarersPlate(state, _data, false), Is.False);
            Assert.That(state.wayfarersPlateOwned, Is.True, "the redemption holds");
            Assert.That(Insects.IsRecorded(state, _data.insects[0]), Is.True, "and the page stays in the book");
        }

        [Test]
        public void ApplyWayfarersPlate_OnANullRun_IsSafe()
        {
            Assert.That(PlayRewards.ApplyWayfarersPlate(null, _data, true), Is.False);
        }

    }
}
