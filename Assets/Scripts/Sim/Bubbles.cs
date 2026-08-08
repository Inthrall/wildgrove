using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Windfall bubbles — the active-play reward that replaced tap-to-tend:
    /// a node the run can reach drifts a bubble up into the strip now and
    /// then, and catching it pockets a burst of that node's goods straight to
    /// camp.
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
    /// how developed it is. Scaling it to the node's own live output instead
    /// makes most windfalls worth 1–2 units — at the time, a node worked only
    /// by the wandering warden earned gatherPerSecond/nodeCount (the wander
    /// gather-share, retired 2026-08-09 when wandering became watch-only), so
    /// the payout would have shrunk every time a zone opened. Nor is the
    /// bubble earned by staffing
    /// the ground it rises from (see <see cref="IsEligible"/>, reopened
    /// 2026-08-06): any node the run can reach drifts one, and what meters the
    /// reward is the spawn interval, not how much of the land is posted.
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
        /// What catching a bubble at <paramref name="node"/> pays: rewardSeconds
        /// of the notional gatherer's rewardRatePerSecond hands — the same haul
        /// at every node, whoever works it — fattened by one additive
        /// bubbleRewardBonus band: any such trait walking the kith (the pack
        /// raven fetches the windfalls home, and she does it from wherever she
        /// is posted, so she is a property of the kith rather than of this
        /// node) summed with the effect sources (the Almanac's Long Reach, the
        /// endless line that keeps a flat haul worth catching as the run's
        /// passive income outgrows it). Zero at ground this run cannot reach —
        /// which, on a live windfall, means the fold moved under it mid-drift.
        /// </summary>
        public static BigDouble RewardFor(GameState state, GameDataAsset data, NodeState node)
        {
            if (!IsEligible(state, data, node))
            {
                return BigDouble.Zero;
            }

            var bubbles = data.economy.bubbles;
            // Litha's tide (design §15) joins the same additive band as the
            // raven's trait and the Almanac's line — the long light drifts more in.
            var bonus = Traits.BubbleRewardBonus(state, data) + Upgrades.BubbleRewardBonus(state, data)
                        + Wheel.BubbleRewardBonus(state, data);
            return new BigDouble(bubbles.rewardRatePerSecond * bubbles.rewardSeconds) * (1.0 + bonus);
        }

        /// <summary>
        /// True when a bubble can rise here: the system is configured and the
        /// node is ground this run can actually reach. Reachable is the whole
        /// test — <c>state.nodes</c> only ever holds the unlocked zones' nodes
        /// (<see cref="GameStateFactory.SyncUnlockedZones"/>), so being in that
        /// list IS being accessible, and a node from a folded-away run fails it
        /// by reference.
        ///
        /// It asked whether anyone WORKED the node until 2026-08-06 (design §2,
        /// "a fallow node drifts nothing — the bubble is still earned").
        /// Reopened because of what that gate did in practice: the only nodes
        /// staffed are the two or three already drawn on the strip, so every
        /// windfall in the game rose from the handful of plates the player was
        /// already looking at, and a camp with nobody posted got none at all. A
        /// windfall is the LAND handing something over, not a wage — it comes
        /// off any ground the run can walk to. The pace is unchanged: spawn
        /// interval and maxLive still meter it, so this widens where a windfall
        /// comes from without paying out any faster.
        /// </summary>
        public static bool IsEligible(GameState state, GameDataAsset data, NodeState node)
        {
            return Configured(data) && state != null && node != null && state.nodes.Contains(node);
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
