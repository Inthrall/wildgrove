using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Almanac (design §7): nodes allocate banked Verdure (never
    /// destroy it — the +2%/pt passive keeps counting the full total),
    /// prerequisites gate the tree, and a bought node's permanent effects
    /// join the same accumulators as upgrades and fossils.
    /// </summary>
    public class AlmanacTests
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
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "old-songs-i", displayName = "Old Songs I", costVerdure = 2,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.10 } },
                },
                new AlmanacNodeData
                {
                    id = "old-songs-ii", displayName = "Old Songs II", costVerdure = 6, requires = "old-songs-i",
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.15 } },
                },
                new AlmanacNodeData
                {
                    id = "long-watch-i", displayName = "The Long Watch I", costVerdure = 2,
                    effects = { new EffectData { type = EffectType.OfflineCapHours, value = 6 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void TryBuy_AllocatesVerdureWithoutDestroyingIt()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 5.0;

            var bought = Almanac.TryBuy(state, _data, _data.almanac[0]);

            // The banked total is untouched (the +2%/pt passive keeps counting
            // it); only the free pool shrinks.
            Assert.That(bought, Is.True);
            Assert.That(state.verdurePoints, Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(Almanac.SpentVerdure(state, _data), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(Almanac.AvailableVerdure(state, _data), Is.EqualTo(3.0).Within(Tolerance));
        }

        [Test]
        public void TryBuy_WithoutFreeVerdure_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 3.0;
            Almanac.TryBuy(state, _data, _data.almanac[0]); // allocates 2 of 3

            // long-watch-i costs 2 but only 1 is free.
            Assert.That(Almanac.TryBuy(state, _data, _data.almanac[2]), Is.False);
            Assert.That(state.almanacNodeIds, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryBuy_PrerequisiteGatesTheTree()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 20.0;

            Assert.That(Almanac.TryBuy(state, _data, _data.almanac[1]), Is.False, "old-songs-ii needs old-songs-i first");

            Almanac.TryBuy(state, _data, _data.almanac[0]);
            Assert.That(Almanac.TryBuy(state, _data, _data.almanac[1]), Is.True);
        }

        [Test]
        public void TryBuy_OwnedNode_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 20.0;
            Almanac.TryBuy(state, _data, _data.almanac[0]);

            Assert.That(Almanac.TryBuy(state, _data, _data.almanac[0]), Is.False);
            Assert.That(Almanac.SpentVerdure(state, _data), Is.EqualTo(2.0).Within(Tolerance), "no double allocation");
        }

        [Test]
        public void TryBuy_YieldEffects_ApplyImmediately()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 20.0;

            Almanac.TryBuy(state, _data, _data.almanac[0]);

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance));
        }

        [Test]
        public void OfflineCap_RisesWithAnAlmanacNode()
        {
            var state = GameStateFactory.NewGame(_data);
            state.verdurePoints = 20.0;

            Assert.That(Upgrades.OfflineCapHours(state, _data), Is.EqualTo(4.0).Within(Tolerance));

            Almanac.TryBuy(state, _data, _data.almanac[2]);

            Assert.That(Upgrades.OfflineCapHours(state, _data), Is.EqualTo(6.0).Within(Tolerance));
        }
    }
}
