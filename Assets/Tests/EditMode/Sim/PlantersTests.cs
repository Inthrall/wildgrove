using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins planters (design §3): Bushcraft-gated build (the Carving Bench opens
    /// the recipes), a flat material bundle spent from camp stock, one of each
    /// type per target, and the three effects — a second yield lane, a bigger
    /// node basket, and steadier dig-site sketching.
    /// </summary>
    public class PlantersTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = MakeData(bushcraftUnlocked: true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private static GameDataAsset MakeData(bool bushcraftUnlocked)
        {
            var data = ScriptableObject.CreateInstance<GameDataAsset>();
            data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
            };
            data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries" },
                    unlocks = bushcraftUnlocked
                        ? new List<string> { "foraging", "bushcraft" }
                        : new List<string> { "foraging" },
                },
            };
            data.planters = new List<PlanterData>
            {
                new PlanterData
                {
                    id = "timber-frame", displayName = "Timber Frame", kind = "basketCapacityMult",
                    value = 0.5, target = "node",
                    materials = new List<ItemAmount> { new ItemAmount { id = "timber", amount = 20 } },
                },
                new PlanterData
                {
                    id = "cordage-trellis", displayName = "Cordage Trellis", kind = "nodeYieldMult",
                    value = 0.25, target = "node",
                    materials = new List<ItemAmount> { new ItemAmount { id = "cordage", amount = 6 } },
                },
                new PlanterData
                {
                    id = "reed-screen", displayName = "Reed Screen", kind = "digSpeedMult",
                    value = 0.5, target = "digSite",
                    materials = new List<ItemAmount> { new ItemAmount { id = "reeds", amount = 20 } },
                },
            };
            return data;
        }

        [Test]
        public void Unlocked_TracksTheBushcraftGate()
        {
            var open = GameStateFactory.NewGame(_data);
            Assert.That(Planters.Unlocked(open, _data), Is.True);

            var locked = MakeData(bushcraftUnlocked: false);
            try
            {
                var state = GameStateFactory.NewGame(locked);
                Assert.That(Planters.Unlocked(state, locked), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(locked);
            }
        }

        [Test]
        public void TryBuild_SpendsBundle_AndRecordsPlanter()
        {
            var state = GameStateFactory.NewGame(_data);
            var node = state.nodes[0];
            state.AddResource("timber", new BigDouble(30));
            var planter = _data.PlantersById["timber-frame"];

            var ok = Planters.TryBuild(state, _data, planter, node.id);

            Assert.That(ok, Is.True);
            Assert.That(state.HasPlanter(node.id, "timber-frame"), Is.True);
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
        }

        [Test]
        public void TryBuild_WhenLocked_ChangesNothing()
        {
            var locked = MakeData(bushcraftUnlocked: false);
            try
            {
                var state = GameStateFactory.NewGame(locked);
                var node = state.nodes[0];
                state.AddResource("timber", new BigDouble(30));

                Assert.That(Planters.TryBuild(state, locked, locked.PlantersById["timber-frame"], node.id), Is.False);
                Assert.That(state.builtPlanters, Is.Empty);
                Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
            }
            finally
            {
                Object.DestroyImmediate(locked);
            }
        }

        [Test]
        public void TryBuild_Duplicate_Refused()
        {
            var state = GameStateFactory.NewGame(_data);
            var node = state.nodes[0];
            state.AddResource("timber", new BigDouble(50));
            var planter = _data.PlantersById["timber-frame"];

            Assert.That(Planters.TryBuild(state, _data, planter, node.id), Is.True);
            // Stock still covers a second (50 - 20 = 30 ≥ 20), so a refusal here
            // is the one-per-target guard, not a short bundle.
            Assert.That(Planters.TryBuild(state, _data, planter, node.id), Is.False);
            Assert.That(state.builtPlanters.Count, Is.EqualTo(1));
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
        }

        [Test]
        public void TryBuild_ShortStock_ChangesNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            var node = state.nodes[0];
            state.AddResource("timber", new BigDouble(19));

            Assert.That(Planters.TryBuild(state, _data, _data.PlantersById["timber-frame"], node.id), Is.False);
            Assert.That(state.builtPlanters, Is.Empty);
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(19.0).Within(Tolerance));
        }

        [Test]
        public void NodeYieldMultiplier_AddsTheTrellisLane()
        {
            var state = new GameState();
            var node = new NodeState { id = "n" };
            state.builtPlanters.Add(new BuiltPlanter { planterId = "cordage-trellis", targetId = "n" });

            Assert.That(Planters.NodeYieldMultiplier(state, _data, node), Is.EqualTo(1.25).Within(Tolerance));
            // A timber frame is capacity, not yield — it must not leak here.
            Assert.That(Planters.NodeYieldMultiplier(state, _data, new NodeState { id = "other" }), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void BasketCapacityMultiplier_AddsTheFrame_PerNode()
        {
            var state = new GameState();
            state.builtPlanters.Add(new BuiltPlanter { planterId = "timber-frame", targetId = "n" });

            Assert.That(Planters.BasketCapacityMultiplier(state, _data, new NodeState { id = "n" }), Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(Planters.BasketCapacityMultiplier(state, _data, new NodeState { id = "other" }), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void DigSpeedMultiplier_AddsTheScreen_PerSite()
        {
            var state = new GameState();
            state.builtPlanters.Add(new BuiltPlanter { planterId = "reed-screen", targetId = "old-growth-wood" });

            Assert.That(Planters.DigSpeedMultiplier(state, _data, "old-growth-wood"), Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(Planters.DigSpeedMultiplier(state, _data, "silverrun-shallows"), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void YieldPerSecond_IncludesTheTrellisLane()
        {
            var node = new NodeState { id = "n", resourceId = "berries" };
            var state = TestCrew.WithGatherers("n", 1);
            state.builtPlanters.Add(new BuiltPlanter { planterId = "cordage-trellis", targetId = "n" });

            // 1 agent · (1 + 0.25 trellis) = 1.25
            Assert.That(Simulation.YieldPerSecond(node, state, _data, _data.economy).ToDouble(),
                Is.EqualTo(1.25).Within(Tolerance));
        }
    }
}
