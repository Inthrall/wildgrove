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
    /// burst and Choice window ride along with the goods. The bubbles
    /// themselves are ephemeral presentation (spawn timing and float live in
    /// the world layer, nothing persists) — this is the pure spend/grant
    /// maths. economy.bubbles absent or zeroed = the system is inert
    /// (hand-built fixtures; the Configured pattern, like Amber).
    ///
    /// The payout is a FLAT haul: rewardSeconds of a notional gatherer's hands
    /// (rewardRatePerSecond), the same at every node no matter who works it or
    /// how developed it is. It used to be rewardSeconds of the node's own live
    /// output, which made most windfalls worth 1–2 units — a node worked only
    /// by the wandering warden earns gatherPerSecond/nodeCount, so the payout
    /// shrank every time a zone opened. Whether a bubble rises at all is still
    /// earned (see <see cref="IsWorked"/>); only its size is now fixed.
    /// </summary>
    public static class Bubbles
    {
        /// <summary>True when the economy carries a live bubbles section.</summary>
        public static bool Configured(GameDataAsset data)
        {
            var bubbles = data?.economy?.bubbles;
            return bubbles != null && bubbles.spawnIntervalSec > 0.0
                && bubbles.rewardSeconds > 0.0 && bubbles.rewardRatePerSecond > 0.0;
        }

        /// <summary>
        /// True when someone — a stationed familiar or the warden's own hands —
        /// works <paramref name="node"/>. A fallow node drifts nothing, so the
        /// bubble still has to be earned even though its size no longer varies.
        /// </summary>
        public static bool IsWorked(GameState state, GameDataAsset data, NodeState node)
        {
            if (state == null || node == null || data?.economy == null)
            {
                return false;
            }

            // The same union the node's own plate reads — both lanes in one
            // number, so "worked" and the rate the plate shows can't drift.
            return Simulation.TotalYieldPerSecond(node, state, data, data.economy) > BigDouble.Zero;
        }

        /// <summary>
        /// What catching a bubble at <paramref name="node"/> pays: rewardSeconds
        /// of the notional gatherer's rewardRatePerSecond hands — the same haul
        /// at every node, whoever works it — fattened by one additive
        /// bubbleRewardBonus band: any such trait walking the kith (the pack
        /// raven fetches the windfalls home, and she does it from wherever she
        /// is posted, so she is a property of the kith rather than of this
        /// node) summed with the effect sources (the Almanac's Long Reach, the
        /// endless line that keeps a flat haul worth catching as the run's
        /// passive income outgrows it). Zero at a fallow node.
        /// </summary>
        public static BigDouble RewardFor(GameState state, GameDataAsset data, NodeState node)
        {
            if (!IsEligible(state, data, node))
            {
                return BigDouble.Zero;
            }

            var bubbles = data.economy.bubbles;
            var bonus = Traits.BubbleRewardBonus(state, data) + Upgrades.BubbleRewardBonus(state, data);
            return new BigDouble(bubbles.rewardRatePerSecond * bubbles.rewardSeconds) * (1.0 + bonus);
        }

        /// <summary>True when a bubble can rise here — someone (kith or warden) is working the node.</summary>
        public static bool IsEligible(GameState state, GameDataAsset data, NodeState node)
        {
            return Configured(data) && IsWorked(state, data, node);
        }

        /// <summary>
        /// Catch a bubble at <paramref name="node"/>: the reward lands as camp
        /// stock (the warden's own catch — no waiting on a delivery), credits
        /// gather XP, mastery and the Compendium like any handled goods, and
        /// tends the node (burst + Choice window + Rite deed). Returns the
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
