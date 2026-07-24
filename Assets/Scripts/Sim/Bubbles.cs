using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Windfall bubbles — the active-play reward that replaced tap-to-tend:
    /// a worked node drifts a bubble up into the strip now and then, and
    /// catching it pockets a burst of that node's goods straight to camp.
    /// Catching one still counts as the warden tending the node (the Rite's
    /// tend deeds and the Cordage Wraps' burst bonus stay live), so the tend
    /// burst and Pristine window ride along with the goods. The bubbles
    /// themselves are ephemeral presentation (spawn timing and float live in
    /// the world layer, nothing persists) — this is the pure spend/grant
    /// maths. economy.bubbles absent or zeroed = the system is inert
    /// (hand-built fixtures; the Configured pattern, like Amber).
    /// </summary>
    public static class Bubbles
    {
        /// <summary>True when the economy carries a live bubbles section.</summary>
        public static bool Configured(GameDataAsset data)
        {
            var bubbles = data?.economy?.bubbles;
            return bubbles != null && bubbles.spawnIntervalSec > 0.0 && bubbles.rewardSeconds > 0.0;
        }

        /// <summary>
        /// What catching a bubble at <paramref name="node"/> pays: rewardSeconds
        /// of the node's current output — the stationed familiar's rate plus the
        /// warden's own hands. Zero at a fallow node (nothing works it, so
        /// nothing drifts up).
        /// </summary>
        public static BigDouble RewardFor(GameState state, GameDataAsset data, NodeState node)
        {
            if (state == null || node == null || !Configured(data))
            {
                return BigDouble.Zero;
            }

            var economy = data.economy;
            var rate = Simulation.YieldPerSecond(node, state, data, economy)
                + new BigDouble(Warden.GatherPerSecond(state, economy, node));
            return rate * economy.bubbles.rewardSeconds;
        }

        /// <summary>True when a bubble can rise here — someone (kith or warden) is working the node.</summary>
        public static bool IsEligible(GameState state, GameDataAsset data, NodeState node)
        {
            return RewardFor(state, data, node) > BigDouble.Zero;
        }

        /// <summary>
        /// Catch a bubble at <paramref name="node"/>: the reward lands as camp
        /// stock (the warden's own catch — no basket, no carrier), credits
        /// gather XP, mastery and the Compendium like any handled goods, and
        /// tends the node (burst + Pristine window + Rite deed). Returns the
        /// amount granted, zero when nothing was due.
        /// </summary>
        public static BigDouble Pop(GameState state, GameDataAsset data, NodeState node)
        {
            var reward = RewardFor(state, data, node);
            if (reward <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            state.AddResource(node.resourceId, reward);
            Skills.AddGatherXp(state, data, node.skill, reward);
            Mastery.AddGatherXp(node, data.economy, reward);
            Compendium.RecordGather(state, node.resourceId, reward);
            Simulation.Tend(state, data, node);
            return reward;
        }
    }
}
