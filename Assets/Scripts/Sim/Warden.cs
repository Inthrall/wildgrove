using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The warden's post — where the player character stands and works. The
    /// post is assigned from the node strip like any familiar's (design §2:
    /// every post holds at most one body), and the warden gathers at it
    /// passively: the early game's kickstart is being somewhere, not a tap
    /// surge. Their pickings go straight to camp (no basket, no carrier —
    /// they pocket what they pick), which is also how a bare node in a fresh
    /// zone earns its first own-resource gift (design §13). An unassigned
    /// warden stands at camp and gathers nothing.
    /// </summary>
    public static class Warden
    {
        /// <summary>The warden's node, or null while they stand at camp.</summary>
        public static string PostNodeId(GameState state)
        {
            return string.IsNullOrEmpty(state.wardenPostNodeId) ? null : state.wardenPostNodeId;
        }

        /// <summary>Whether the warden stands at <paramref name="node"/>.</summary>
        public static bool IsPosted(GameState state, NodeState node)
        {
            return node != null && node.id == PostNodeId(state);
        }

        /// <summary>
        /// Walk the warden to <paramref name="node"/>. One body per post: a
        /// familiar already working it steps back to camp (its slot frees).
        /// </summary>
        public static void Post(GameState state, NodeState node)
        {
            if (node == null)
            {
                return;
            }

            var occupant = Stationing.OccupantOf(state, node.id);
            if (occupant != null)
            {
                occupant.stationId = null;
            }

            state.wardenPostNodeId = node.id;
            state.BumpModifiers();
        }

        /// <summary>Send the warden back to camp — no post, no picking.</summary>
        public static void Rest(GameState state)
        {
            state.wardenPostNodeId = null;
            state.BumpModifiers();
        }

        /// <summary>
        /// The warden's own gather rate at <paramref name="node"/> before any
        /// burst boost — zero away from the post, at camp, or when
        /// unconfigured (pre-warden fixtures stay inert).
        /// </summary>
        public static double GatherPerSecond(GameState state, EconomyData economy, NodeState node)
        {
            return economy?.warden != null && IsPosted(state, node)
                ? economy.warden.gatherPerSecond
                : 0.0;
        }
    }
}
