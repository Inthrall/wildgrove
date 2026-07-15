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
        /// Credit time away since the last session, per design doc §8:
        /// offlineEarn = rate · min(t, cap). Real elapsed time is capped at the
        /// offline cap (base cap hours for now; gear/building/Almanac bonuses
        /// multiply in with their systems) and the offline rate multiplier is
        /// applied, then the tick runs once with that effective delta. Returns
        /// the capped wall-clock seconds credited (before the rate multiplier),
        /// so the welcome-back summary can report how much of the absence paid out.
        /// </summary>
        public static double AdvanceOffline(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            if (state == null || data == null || realElapsedSeconds <= 0.0)
            {
                return 0.0;
            }

            var capSeconds = data.economy.offline.baseCapHours * 3600.0;
            var creditedSeconds = System.Math.Min(realElapsedSeconds, capSeconds);

            Advance(state, data, creditedSeconds * data.economy.offline.rateMultiplier);
            return creditedSeconds;
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
