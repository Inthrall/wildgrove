using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins replanting (design §3): replantCost climbs baseCost·growth^level in
    /// the node's own resource, each level adds richnessPerLevel to yield, the
    /// cost is paid from camp stock, and a short stock refuses. Richness resets
    /// at Migration (a fresh NewGame node starts at 0).
    /// </summary>
    public class ReplantingTests
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
                replant = new EconomyData.ReplantData { baseCost = new BigDouble(20), growth = 1.5, richnessPerLevel = 0.10 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void ReplantCost_ClimbsByGrowthPerLevel()
        {
            var node = new NodeState { resourceId = "berries" };
            Assert.That(Replanting.ReplantCost(node, _data.economy).ToDouble(), Is.EqualTo(20.0).Within(Tolerance));

            node.richnessLevel = 2;
            // 20 · 1.5^2 = 45
            Assert.That(Replanting.ReplantCost(node, _data.economy).ToDouble(), Is.EqualTo(45.0).Within(Tolerance));
        }

        [Test]
        public void RichnessMultiplier_AddsPerLevel()
        {
            var node = new NodeState { richnessLevel = 3 };

            // 1 + 0.10 · 3 = 1.3
            Assert.That(Replanting.RichnessMultiplier(node, _data.economy), Is.EqualTo(1.3).Within(Tolerance));
        }

        [Test]
        public void TryReplant_SpendsOwnResource_AndRaisesRichness()
        {
            var state = GameStateFactory.NewGame(_data);
            var node = state.nodes[0];
            state.AddResource("berries", new BigDouble(50));

            var ok = Replanting.TryReplant(state, _data, node);

            Assert.That(ok, Is.True);
            Assert.That(node.richnessLevel, Is.EqualTo(1));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
            // Next level now costs 20 · 1.5 = 30.
            Assert.That(Replanting.ReplantCost(node, _data.economy).ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
        }

        [Test]
        public void TryReplant_ShortStock_ChangesNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            var node = state.nodes[0];
            state.AddResource("berries", new BigDouble(19));

            Assert.That(Replanting.TryReplant(state, _data, node), Is.False);
            Assert.That(node.richnessLevel, Is.EqualTo(0));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(19.0).Within(Tolerance));
        }

        [Test]
        public void Richness_RaisesNodeYield()
        {
            var node = new NodeState { id = "n", resourceId = "berries", richnessLevel = 5 };
            var state = TestCrew.WithGatherers("n", 1);

            // 1 agent · (1 + 0.10 · 5 richness) = 1.5
            Assert.That(Simulation.YieldPerSecond(node, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(1.5).Within(Tolerance));
        }
    }
}
