using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Museum (design §5): donating a Pristine specimen consumes it
    /// forever, a set completes when every entry is donated, completed sets
    /// grant permanent effects (scaled by the Curator's Cabinet while owned),
    /// and donations are the permanence fork of the Pristine three-way choice.
    /// </summary>
    public class MuseumTests
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
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "wildflowers" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.museumSets = new List<MuseumSetData>
            {
                new MuseumSetData
                {
                    id = "meadow-blooms", displayName = "Meadow Blooms",
                    entries = new List<string> { "berries", "wildflowers" },
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "foraging", value = 0.10 } },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 26, id = "curators-cabinet", costCoin = 100,
                    effects = { new EffectData { type = EffectType.MuseumSetBonusMult, value = 1.5 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void TryDonate_ConsumesTheSpecimenForever()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 2);

            var donated = Museum.TryDonate(state, _data, "berries");

            Assert.That(donated, Is.True);
            Assert.That(state.GetPristine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(Museum.IsDonated(state, "berries"), Is.True);
            // One entry per resource — a second donation has nowhere to go.
            Assert.That(Museum.TryDonate(state, _data, "berries"), Is.False);
            Assert.That(state.GetPristine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryDonate_NoSetWantsIt_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("fibres", 1); // gathered, but no set lists it

            Assert.That(Museum.TryDonate(state, _data, "fibres"), Is.False);
            Assert.That(state.GetPristine("fibres").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryDonate_NoSpecimenHeld_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddFine("berries", 5); // fine isn't pristine

            Assert.That(Museum.TryDonate(state, _data, "berries"), Is.False);
        }

        [Test]
        public void CompletedSet_GrantsItsPermanentEffect()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 1);
            state.AddPristine("wildflowers", 1);

            Museum.TryDonate(state, _data, "berries");
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance), "one of two donated — no bonus yet");

            Museum.TryDonate(state, _data, "wildflowers");
            Assert.That(Museum.IsSetComplete(state, _data.museumSets[0]), Is.True);
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance));
        }

        [Test]
        public void CuratorsCabinet_ScalesTheSetBonus()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 1);
            state.AddPristine("wildflowers", 1);
            Museum.TryDonate(state, _data, "berries");
            Museum.TryDonate(state, _data, "wildflowers");
            state.purchasedUpgradeIds.Add("curators-cabinet");
            Upgrades.RecomputeYieldMultipliers(state, _data);

            // The set's +10% is displayed through the cabinet's ×1.5 lens.
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.15).Within(Tolerance));
        }
    }
}
