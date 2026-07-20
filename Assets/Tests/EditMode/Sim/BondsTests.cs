using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins bonded familiars (design §4): earned — never bought — from a
    /// completed Museum set or an owned Almanac node, with earned state DERIVED
    /// from the source (so a bond crosses Migration for free). Under the roster
    /// model a bond materialises into the crew as an ordinary stationed
    /// familiar (Roster.SyncBonded), idempotently.
    /// </summary>
    public class BondsTests
    {
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
                    id = "sootwing", displayName = "Sootwing", species = "pack-raven", role = "carrier",
                    source = new BondSourceData { type = "museumSet", id = "meadow-blooms" },
                },
                new BondData
                {
                    id = "burr", displayName = "Burr", species = "meadow-vole", role = "gatherer",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private BondData Bond(string id)
        {
            return _data.bonds.Find(b => b.id == id);
        }

        [Test]
        public void IsEarned_CarrierBond_ArrivesWithTheCompletedMuseumSet()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Bonds.IsEarned(state, _data, Bond("sootwing")), Is.False);

            state.donatedResources.Add("berries");
            Assert.That(Bonds.IsEarned(state, _data, Bond("sootwing")), Is.False, "a half-done set bonds nothing");

            state.donatedResources.Add("nuts");
            Assert.That(Bonds.IsEarned(state, _data, Bond("sootwing")), Is.True);
        }

        [Test]
        public void IsEarned_GathererBond_ArrivesWithTheAlmanacNode()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Bonds.IsEarned(state, _data, Bond("burr")), Is.False);

            state.almanacNodeIds.Add("old-friend");
            Assert.That(Bonds.IsEarned(state, _data, Bond("burr")), Is.True);
        }

        [Test]
        public void SyncBonded_MaterialisesEarnedBondsIntoTheRoster()
        {
            var state = GameStateFactory.NewGame(_data);
            state.donatedResources.Add("berries");
            state.donatedResources.Add("nuts");

            Roster.SyncBonded(state, _data);

            var sootwing = state.roster.Find(f => f.bondId == "sootwing");
            Assert.That(sootwing, Is.Not.Null);
            Assert.That(sootwing.bonded, Is.True);
            Assert.That(sootwing.speciesId, Is.EqualTo("pack-raven"));
            Assert.That(sootwing.name, Is.EqualTo("Sootwing"));
        }

        [Test]
        public void SyncBonded_IsIdempotent()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            Roster.SyncBonded(state, _data);
            Roster.SyncBonded(state, _data);

            Assert.That(state.roster.FindAll(f => f.bondId == "burr").Count, Is.EqualTo(1),
                "a bond's companion is materialised exactly once");
        }

        [Test]
        public void BondedFamiliar_UnearnedBond_IsNotInTheRoster()
        {
            var state = GameStateFactory.NewGame(_data);

            Roster.SyncBonded(state, _data);

            Assert.That(state.roster.Exists(f => f.bonded), Is.False, "no source satisfied — no companion");
        }

        [Test]
        public void BondedFamiliar_IsStationedLikeAnyOther()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");
            Roster.SyncBonded(state, _data);
            var burr = state.roster.Find(f => f.bondId == "burr");

            Roster.Station(state, _data, burr, state.nodes[0].id);

            Assert.That(Stationing.CountAssignedTo(state, state.nodes[0].id), Is.GreaterThanOrEqualTo(1),
                "a bonded familiar gathers exactly like a gifted one — carrying is a post, not a species");
        }
    }
}
