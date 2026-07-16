using BreakInfinity;
using NUnit.Framework;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins per-resource mastery (design §4): 0-based bonus levels derive from
    /// the node's XP on the shared curve, gathering XP scales by xpPerUnit and
    /// clamps at the cap, and the whole system stays inert when the curve is
    /// unconfigured.
    /// </summary>
    public class MasteryTests
    {
        private const double Tolerance = 1e-9;

        private EconomyData _economy;

        [SetUp]
        public void SetUp()
        {
            _economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData
                {
                    yieldBonusPerLevel = 0.05, baseXp = 50, growth = 1.15, maxLevel = 99, xpPerUnit = 0.25,
                },
            };
        }

        [Test]
        public void Level_NoXp_IsZero()
        {
            Assert.That(Mastery.Level(new NodeState(), _economy), Is.EqualTo(0));
        }

        [Test]
        public void Level_RungCostsCompoundByGrowth()
        {
            var node = new NodeState();

            // Rung 0 costs 50, rung 1 another 57.5.
            node.masteryXp = 49.9;
            Assert.That(Mastery.Level(node, _economy), Is.EqualTo(0));

            node.masteryXp = 50.0;
            Assert.That(Mastery.Level(node, _economy), Is.EqualTo(1));

            node.masteryXp = 107.5;
            Assert.That(Mastery.Level(node, _economy), Is.EqualTo(2));
        }

        [Test]
        public void AddGatherXp_MultipliesUnitsByXpPerUnit()
        {
            var node = new NodeState();

            Mastery.AddGatherXp(node, _economy, new BigDouble(8.0));

            Assert.That(node.masteryXp, Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void AddGatherXp_ClampsAtTheMaxLevelTotal()
        {
            _economy.mastery.maxLevel = 3;
            var node = new NodeState();

            Mastery.AddGatherXp(node, _economy, new BigDouble(1.0, 400));

            var cap = XpCurve.TotalForRungs(50, 1.15, 3);
            Assert.That(node.masteryXp, Is.EqualTo(cap).Within(Tolerance));
            Assert.That(Mastery.Level(node, _economy), Is.EqualTo(3));
        }

        [Test]
        public void Unconfigured_StaysInert()
        {
            _economy.mastery.baseXp = 0;
            var node = new NodeState { masteryXp = 1e12 };

            Mastery.AddGatherXp(node, _economy, new BigDouble(100.0));

            // No XP accrues and the level reads 0 — the pre-mastery behaviour
            // hand-built test data relies on.
            Assert.That(node.masteryXp, Is.EqualTo(1e12));
            Assert.That(Mastery.Level(node, _economy), Is.EqualTo(0));
        }

        [Test]
        public void ProgressToNext_MidRung_IsTheFraction()
        {
            var node = new NodeState { masteryXp = 25.0 };

            Assert.That(Mastery.ProgressToNext(node, _economy), Is.EqualTo(0.5).Within(Tolerance));
        }
    }
}
