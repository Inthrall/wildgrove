using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the modifier snapshot cache itself — the perf feature's whole
    /// point is that repeated reads REUSE one snapshot, and its whole risk is
    /// a mutation that fails to invalidate. Both directions are asserted by
    /// instance identity, which the behavioural suites can't see.
    /// </summary>
    public class ModifiersTests
    {
        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
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
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 1, id = "flint-sickle", costCoin = 100,
                    effects = { new EffectData { type = EffectType.YieldMult, skill = "foraging", value = 2 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Of_NothingChanged_ReturnsTheSameSnapshotInstance()
        {
            var state = GameStateFactory.NewGame(_data);

            var first = Modifiers.Of(state, _data);

            Assert.That(Modifiers.Of(state, _data), Is.SameAs(first),
                "unchanged state must reuse the cached snapshot, not rebuild per read");
        }

        [Test]
        public void Of_ExplicitBump_Rebuilds()
        {
            var state = GameStateFactory.NewGame(_data);
            var first = Modifiers.Of(state, _data);

            state.BumpModifiers();

            Assert.That(Modifiers.Of(state, _data), Is.Not.SameAs(first));
        }

        [Test]
        public void Of_DirectPurchaseListMutation_RebuildsViaTheFingerprint()
        {
            var state = GameStateFactory.NewGame(_data);
            var first = Modifiers.Of(state, _data);

            // No BumpModifiers — the count fingerprint is the backstop for
            // mutations that bypass the purchase path (as several tests do).
            state.purchasedUpgradeIds.Add("flint-sickle");
            var second = Modifiers.Of(state, _data);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.unlockedSkills, Is.EqualTo(first.unlockedSkills));
        }

        [Test]
        public void Of_DataAssetSwap_Rebuilds()
        {
            var state = GameStateFactory.NewGame(_data);
            var first = Modifiers.Of(state, _data);

            var other = ScriptableObject.CreateInstance<GameDataAsset>();
            try
            {
                other.economy = _data.economy;
                other.zones = _data.zones;
                other.upgrades = _data.upgrades;

                Assert.That(Modifiers.Of(state, other), Is.Not.SameAs(first),
                    "a reloaded data asset must not serve a snapshot built from the old one");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
