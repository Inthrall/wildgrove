using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the skill XP curve (design §4): levels derive from total XP via
    /// baseXp · growth^(L−1) per rung, XP clamps at the max level's total, and
    /// the whole system stays inert when economy.xp is unconfigured.
    /// </summary>
    public class SkillsTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                xp = new EconomyData.XpData
                {
                    baseXp = 100, growth = 1.1, maxLevel = 99, gatherPerUnit = 1, craftPerBatch = 25,
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Level_NoXp_IsOne()
        {
            Assert.That(Skills.Level(new GameState(), _data, "foraging"), Is.EqualTo(1));
        }

        [Test]
        public void Level_JustShortOfTheFirstRung_StaysOne()
        {
            var state = new GameState();
            state.skillXp["foraging"] = 99.9;

            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(1));
        }

        [Test]
        public void Level_RungCostsCompoundByGrowth()
        {
            var state = new GameState();

            // Level 2 costs 100, level 3 costs another 110.
            state.skillXp["foraging"] = 100.0;
            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(2));

            state.skillXp["foraging"] = 209.9;
            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(2));

            state.skillXp["foraging"] = 210.0;
            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(3));
        }

        [Test]
        public void Level_XpUnconfigured_ReadsOne()
        {
            _data.economy.xp = null;
            var state = new GameState();
            state.skillXp["foraging"] = 1e12;

            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(1));
        }

        [Test]
        public void AddXp_Accumulates()
        {
            var state = new GameState();

            Skills.AddXp(state, _data, "foraging", new BigDouble(40.0));
            Skills.AddXp(state, _data, "foraging", new BigDouble(70.0));

            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(110.0).Within(Tolerance));
            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(2));
        }

        [Test]
        public void AddXp_ClampsAtTheMaxLevelTotal()
        {
            _data.economy.xp.maxLevel = 5;
            var state = new GameState();

            // Far beyond double range — the clamp is why XP survives as double.
            Skills.AddXp(state, _data, "foraging", new BigDouble(1.0, 400));

            var cap = Skills.TotalXpForLevel(_data.economy.xp, 5);
            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(cap).Within(Tolerance));
            Assert.That(Skills.Level(state, _data, "foraging"), Is.EqualTo(5));
        }

        [Test]
        public void AddXp_NonPositiveAmount_IsIgnored()
        {
            var state = new GameState();

            Skills.AddXp(state, _data, "foraging", BigDouble.Zero);
            Skills.AddXp(state, _data, "foraging", new BigDouble(-5.0));

            Assert.That(Skills.Xp(state, "foraging"), Is.EqualTo(0.0));
        }

        [Test]
        public void ProgressToNext_MidRung_IsTheFraction()
        {
            var state = new GameState();
            state.skillXp["foraging"] = 50.0;

            Assert.That(Skills.ProgressToNext(state, _data, "foraging"), Is.EqualTo(0.5).Within(Tolerance));
        }

        [Test]
        public void ProgressToNext_AtTheCap_IsZero()
        {
            _data.economy.xp.maxLevel = 3;
            var state = new GameState();
            state.skillXp["foraging"] = Skills.TotalXpForLevel(_data.economy.xp, 3);

            Assert.That(Skills.ProgressToNext(state, _data, "foraging"), Is.EqualTo(0.0));
        }
    }
}
