using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins The Drover's Halter (design §11): the reward grants a fell pony who
    /// walks at the warden's side, always, for no slot — carrying what the
    /// warden picks, so their own gathering runs +50% while she's owned. The
    /// pony is the reason the exemption is safe: she can stand nowhere else,
    /// so it cannot follow her to a node.
    /// </summary>
    public class DroversHalterTests
    {
        private const double Tolerance = 1e-9;

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
            };
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
                    trait = new TraitData { displayName = "Deep pockets", kind = "bubbleRewardBonus", value = 0.25 },
                },
                new SpeciesData
                {
                    id = Familiar.PonySpecies, displayName = "fell pony", roleLean = "carrier",
                    suggestedNames = new List<string> { "Moss" },
                    trait = new TraitData { displayName = "Half-broke", kind = "wardenYieldBonus", value = 0.5 },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState Owned()
        {
            var state = new GameState { droversHalterOwned = true };
            Roster.SyncDroversHalter(state, _data);
            return state;
        }

        [Test]
        public void WardenYieldBonus_WhileSheWalks_QuickensTheWardensHands()
        {
            var state = Owned();

            Assert.That(Traits.WardenYieldBonus(state, _data), Is.EqualTo(0.5).Within(Tolerance));
        }

        [Test]
        public void GatherPerSecond_WithThePonyOwned_RunsHalfAgain()
        {
            _data.economy.warden = new EconomyData.WardenData { gatherPerSecond = 1.0 };
            var state = Owned();
            state.nodes.Add(new NodeState { id = "n1", resourceId = "berries" });
            state.wardenPostNodeId = "n1";

            Assert.That(Warden.GatherPerSecond(state, _data, _data.economy, state.nodes[0]),
                Is.EqualTo(1.5).Within(Tolerance));
        }

        [Test]
        public void Walking_DoesNotCountThePony_SoHerLaneCostsNoSlot()
        {
            var state = Owned();

            // One slot, the pony walking, and the slot is still free — the
            // reward lands the moment it is redeemed instead of waiting for the
            // ladder.
            Assert.That(Kith.Walking(state), Is.EqualTo(0), "the pony holds no slot");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(1));
            Assert.That(Kith.HasRoom(state, _data), Is.True, "her lane must not consume the starting slot");
        }

        [Test]
        public void Station_RefusesToMoveThePony()
        {
            var state = Owned();
            state.nodes.Add(new NodeState { id = "n1", resourceId = "berries" });
            var pony = Roster.OfSpecies(state, Familiar.PonySpecies);

            Assert.That(Roster.Station(state, _data, pony, "n1"), Is.False, "she will not be posted to a node");
            Assert.That(Roster.Station(state, _data, pony, null), Is.False, "she will not be sent back to camp");
            Assert.That(pony.stationId, Is.EqualTo(Familiar.PonyStation), "and she has not budged");
        }

        [Test]
        public void Station_RefusesAnyoneElseIntoHerLane()
        {
            var state = Owned();
            var raven = new Familiar { id = "fam-9", speciesId = "pack-raven" };
            state.roster.Add(raven);

            Assert.That(Roster.Station(state, _data, raven, Familiar.PonyStation), Is.False);
            Assert.That(raven.IsResting, Is.True, "her lane is hers alone");
        }

        [Test]
        public void SyncDroversHalter_WhenOwned_BringsHerAndIsIdempotent()
        {
            var state = Owned();
            Roster.SyncDroversHalter(state, _data);

            var pony = Roster.OfSpecies(state, Familiar.PonySpecies);
            Assert.That(state.roster.Count, Is.EqualTo(1), "one pony, however many times the entitlement resolves");
            Assert.That(pony.stationId, Is.EqualTo(Familiar.PonyStation));
            Assert.That(pony.name, Is.EqualTo("Moss"));
        }

        [Test]
        public void SyncDroversHalter_KeepsThePlayersName_AndReAssertsHerLane()
        {
            var state = Owned();
            var pony = Roster.OfSpecies(state, Familiar.PonySpecies);
            Roster.Rename(pony, "Bracken");

            // A save that somehow stored her resting is corrected on load, but
            // the name is the player's and is never overwritten.
            pony.stationId = null;
            Roster.SyncDroversHalter(state, _data);

            Assert.That(pony.stationId, Is.EqualTo(Familiar.PonyStation));
            Assert.That(pony.name, Is.EqualTo("Bracken"));
        }

        [Test]
        public void SyncDroversHalter_WhenNotOwned_SheIsAbsent()
        {
            var state = Owned();
            state.droversHalterOwned = false;

            Roster.SyncDroversHalter(state, _data);

            Assert.That(Roster.OfSpecies(state, Familiar.PonySpecies), Is.Null,
                "the entitlement is the source of truth, not the roster");
        }
    }
}
