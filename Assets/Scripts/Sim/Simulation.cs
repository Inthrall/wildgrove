using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The core game tick. Pure and deterministic: given a state, the content
    /// database and an elapsed time, it accrues gathered resources. Called both
    /// per-frame with a small delta and once on load with the offline delta.
    /// </summary>
    public static class Simulation
    {
        /// <summary>
        /// Advance the run by <paramref name="deltaSeconds"/>, accruing every
        /// active node's yield into the inventory. Non-positive deltas are a
        /// no-op so a paused or clock-skewed tick can't rewind progress.
        /// </summary>
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            if (state == null || data == null || deltaSeconds <= 0.0)
            {
                return;
            }

            foreach (var node in state.nodes)
            {
                if (node.crewCount <= 0)
                {
                    continue;
                }

                var gained = YieldPerSecond(node, state, data.economy) * deltaSeconds;
                state.AddResource(node.resourceId, gained);
            }
        }

        /// <summary>
        /// Gather rate for a node, per design doc §8:
        /// yield/sec = crew · tool/gear mult · (1 + masteryBonus·mastery) · global.
        /// Base rate is one unit per crew per second; global folds in the
        /// permanent Verdure bonus (almanac / museum / fossil / boost factors
        /// arrive with their systems and multiply in here later).
        /// </summary>
        public static BigDouble YieldPerSecond(NodeState node, GameState state, EconomyData economy)
        {
            var masteryBonus = 1.0 + economy.mastery.yieldBonusPerLevel * node.masteryLevel;
            var global = 1.0 + economy.verdure.yieldBonusPerPoint * state.verdurePoints;

            return new BigDouble(node.crewCount) * node.yieldMultiplier * masteryBonus * global;
        }
    }
}
