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
                    order = 1, id = "flint-sickle",
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
        public void Of_ManyBuildingLevels_StillReusesTheSnapshot()
        {
            // The fingerprint used to pack each count into its own power-of-ten
            // band and sum them, so a band reaching its 1,000 stride spilled
            // into the next: a thousand building levels read as one field
            // sketch. The Store's line is endless, so a thousand levels is a
            // place a real run goes. Counts and a hash have no bands to spill.
            var state = GameStateFactory.NewGame(_data);
            state.buildingLevels["store"] = 1000;
            state.BumpModifiers();
            var first = Modifiers.Of(state, _data);

            state.buildingLevels["store"] = 2000;

            Assert.That(Modifiers.Of(state, _data), Is.SameAs(first),
                "a level-up changes no count, so it is the explicit bump's job — "
                + "and it must not alias into a neighbouring band either way");
        }

        [Test]
        public void Of_CountsThatWouldHaveAliased_AreToldApart()
        {
            var thousandLevels = GameStateFactory.NewGame(_data);
            thousandLevels.buildingLevels["store"] = 1000;

            var oneSketch = GameStateFactory.NewGame(_data);
            oneSketch.insectSketches["apollo"] = 1;

            // Under the old packing these two summed to the same long. They are
            // different states, so they must fingerprint differently — asserted
            // through the cache, which is the only thing the fingerprint feeds.
            var first = Modifiers.Of(thousandLevels, _data);
            thousandLevels.insectSketches["apollo"] = 1;

            Assert.That(Modifiers.Of(thousandLevels, _data), Is.Not.SameAs(first),
                "adding the sketch changed a count, so the snapshot must rebuild");
        }

        [Test]
        public void Of_ATinctureDrunk_RebuildsViaTheFingerprint()
        {
            // Tinctures.TryDrink adds to the active list without bumping, so the
            // count is the only thing that catches it — and tinctures do carry
            // effects, unlike the value-only cases counts deliberately miss.
            var state = GameStateFactory.NewGame(_data);
            var first = Modifiers.Of(state, _data);

            state.activeTinctures.Add(new ActiveTincture { tinctureId = "cordial", remainingSeconds = 60.0 });

            Assert.That(Modifiers.Of(state, _data), Is.Not.SameAs(first));
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
