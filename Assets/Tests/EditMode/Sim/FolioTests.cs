using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Folio (design §6): fixing a Pristine specimen consumes it
    /// forever, a spread completes when every entry is fixed, completed spreads
    /// grant permanent effects (scaled by the Curator's Cabinet while owned),
    /// and fixing is the permanence fork of the Pristine three-way choice.
    /// </summary>
    public class FolioTests
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
            _data.folioSpreads = new List<FolioSpreadData>
            {
                new FolioSpreadData
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
                    order = 26, id = "curators-cabinet",
                    effects = { new EffectData { type = EffectType.FolioSpreadBonusMult, value = 1.5 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void TryFix_ConsumesTheSpecimenForever()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 2);

            var fixedOk = Folio.TryFix(state, _data, "berries");

            Assert.That(fixedOk, Is.True);
            Assert.That(state.GetPristine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(Folio.IsFixed(state, "berries"), Is.True);
            // One entry per resource — a second fixing has nowhere to go.
            Assert.That(Folio.TryFix(state, _data, "berries"), Is.False);
            Assert.That(state.GetPristine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryFix_NoSpreadWantsIt_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("fibres", 1); // gathered, but no spread lists it

            Assert.That(Folio.TryFix(state, _data, "fibres"), Is.False);
            Assert.That(state.GetPristine("fibres").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryFix_NoSpecimenHeld_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddFine("berries", 5); // fine isn't pristine

            Assert.That(Folio.TryFix(state, _data, "berries"), Is.False);
        }

        [Test]
        public void CompletedSpread_GrantsItsPermanentEffect()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 1);
            state.AddPristine("wildflowers", 1);

            Folio.TryFix(state, _data, "berries");
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance), "one of two fixed — no bonus yet");

            Folio.TryFix(state, _data, "wildflowers");
            Assert.That(Folio.IsSpreadComplete(state, _data.folioSpreads[0]), Is.True);
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance));
        }

        [Test]
        public void CuratorsCabinet_ScalesTheSpreadBonus()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 1);
            state.AddPristine("wildflowers", 1);
            Folio.TryFix(state, _data, "berries");
            Folio.TryFix(state, _data, "wildflowers");
            state.purchasedUpgradeIds.Add("curators-cabinet");
            Upgrades.RecomputeYieldMultipliers(state, _data);

            // The spread's +10% is displayed through the cabinet's ×1.5 lens.
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.15).Within(Tolerance));
        }
    }
}
