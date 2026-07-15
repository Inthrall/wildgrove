using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Exercises the core tick and the starting-state factory against a
    /// hand-built content asset, so the maths is pinned without loading the
    /// real Resources asset or a scene.
    /// </summary>
    public class SimulationTests
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
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries", "wildflowers", "fibres" },
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
        public void NewGame_StartingZone_CreatesNodePerResource()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(state.nodes.Select(n => n.resourceId),
                Is.EqualTo(new[] { "berries", "wildflowers", "fibres" }));
            Assert.That(state.nodes.All(n => n.skill == "foraging"), Is.True);
            Assert.That(state.nodes.All(n => n.zoneId == GameStateFactory.StartingZoneId), Is.True);
        }

        [Test]
        public void NewGame_SeedsOneCrewOnFirstNodeOnly()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(state.nodes[0].crewCount, Is.EqualTo(1));
            Assert.That(state.nodes.Skip(1).All(n => n.crewCount == 0), Is.True);
        }

        [Test]
        public void Advance_OneCrewNoBonuses_AccruesOnePerSecond()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void Advance_ZeroCrewNode_AccruesNothing()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 10.0);

            // wildflowers node has no crew seeded.
            Assert.That(state.GetResource("wildflowers").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_NonPositiveDelta_LeavesStateUnchanged()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 0.0);
            Simulation.Advance(state, _data, -5.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_Accumulates_AcrossTicks()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 3.0);
            Simulation.Advance(state, _data, 2.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_AppliesMasteryBonus()
        {
            var node = new NodeState { crewCount = 1, masteryLevel = 2 };
            var state = new GameState();

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 1 crew * (1 + 0.05 * 2) = 1.1
            Assert.That(perSec.ToDouble(), Is.EqualTo(1.1).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_AppliesVerdureGlobalBonus()
        {
            var node = new NodeState { crewCount = 1 };
            var state = new GameState { verdurePoints = 10 };

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 1 crew * (1 + 0.02 * 10) = 1.2
            Assert.That(perSec.ToDouble(), Is.EqualTo(1.2).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_ScalesWithCrewAndMultiplier()
        {
            var node = new NodeState { crewCount = 4, yieldMultiplier = 2.0 };
            var state = new GameState();

            var perSec = Simulation.YieldPerSecond(node, state, _data.economy);

            // 4 crew * 2.0 tool/gear mult = 8
            Assert.That(perSec.ToDouble(), Is.EqualTo(8.0).Within(Tolerance));
        }
    }
}
