using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the familiar level curve's two ENDS, which are where anything
    /// drawing it goes wrong: <see cref="Familiars.ProgressToNextLevel"/>
    /// answers 0 both to a level just begun and to a curve fully climbed, and
    /// it answers 0 again to a fixture with no curve at all. A reading that
    /// cannot tell those apart draws an empty band over a finished companion.
    /// The fixture's curve: two levels, the second bought at 60 xp.
    /// </summary>
    public class FamiliarsTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                familiarXp = new EconomyData.FamiliarXpData
                {
                    baseXp = 60, growth = 1.12, maxLevel = 2, xpPerSecond = 1.0,
                    kinshipDivisor = 12000, kinshipXpRatePerLevel = 0.02,
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void AtMaxLevel_PartWayUpTheCurve_IsFalseAndTheProgressIsReal()
        {
            var familiar = new Familiar { id = "fam-1", speciesId = "meadow-vole", xp = 30.0 };

            Assert.That(Familiars.AtMaxLevel(familiar, _data), Is.False, "half of the first rung is not the top");
            Assert.That(Familiars.ProgressToNextLevel(familiar, _data), Is.EqualTo(0.5).Within(Tolerance));
        }

        [Test]
        public void AtMaxLevel_TheWholeCurveClimbed_IsTrueWhereTheProgressReadsZero()
        {
            var familiar = new Familiar { id = "fam-1", speciesId = "meadow-vole", xp = 60.0 };

            Assert.That(Familiars.Level(familiar, _data), Is.EqualTo(2), "60 xp buys the second and last level");
            // The zero a band must not draw: nothing is owed, rather than
            // nothing earned yet.
            Assert.That(Familiars.ProgressToNextLevel(familiar, _data), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Familiars.AtMaxLevel(familiar, _data), Is.True);
        }

        [Test]
        public void AtMaxLevel_NoCurveAuthored_IsTrueSoNothingDrawsAGaugeItCannotFill()
        {
            _data.economy.familiarXp = null;
            var familiar = new Familiar { id = "fam-1", speciesId = "meadow-vole", xp = 30.0 };

            Assert.That(Familiars.AtMaxLevel(familiar, _data), Is.True);
        }
    }
}
