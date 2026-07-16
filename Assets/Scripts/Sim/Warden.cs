using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The warden's post — where the player character stands and works. The
    /// post follows tending (tap a node and the warden walks there), and the
    /// warden gathers at it passively: the early game's kickstart is being
    /// somewhere, not a tap surge. Their pickings go straight to camp (no
    /// basket, no carrier — they pocket what they pick), which is also how a
    /// bare node in a fresh zone earns its first own-resource gift
    /// (design §13). Bonded gatherers work at the warden's side, so they share
    /// this post.
    /// </summary>
    public static class Warden
    {
        /// <summary>The warden's node: the last tended, or the first node until any tend.</summary>
        public static string PostNodeId(GameState state)
        {
            if (!string.IsNullOrEmpty(state.wardenPostNodeId))
            {
                return state.wardenPostNodeId;
            }

            return state.nodes.Count > 0 ? state.nodes[0].id : null;
        }

        /// <summary>Whether the warden stands at <paramref name="node"/>.</summary>
        public static bool IsPosted(GameState state, NodeState node)
        {
            return node != null && node.id == PostNodeId(state);
        }

        /// <summary>
        /// The warden's own gather rate at <paramref name="node"/> before any
        /// burst boost — zero away from the post or when unconfigured
        /// (pre-warden fixtures stay inert).
        /// </summary>
        public static double GatherPerSecond(GameState state, EconomyData economy, NodeState node)
        {
            return economy?.warden != null && IsPosted(state, node)
                ? economy.warden.gatherPerSecond
                : 0.0;
        }
    }
}
