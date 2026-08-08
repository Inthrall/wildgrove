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
        /// <summary>
        /// The unnamed warden's name — what every line called them before a
        /// rename could be bought, and what they are called again if the name
        /// is ever cleared. Lower case because it reads mid-sentence far more
        /// often than it opens one ("walk the warden here"), and a real name
        /// carries its own capital.
        /// </summary>
        public const string Anonymous = "the warden";

        /// <summary>
        /// What to call the warden on the page: their bought name, or
        /// <see cref="Anonymous"/> while they have none. Every surface that
        /// names the warden goes through here, so an unnamed run reads exactly
        /// as it always did and a named one changes everywhere at once.
        /// </summary>
        /// <remarks>
        /// A display word in the sim, like <c>Roster</c>'s fallback familiar
        /// name: the name is state, "unnamed" is a fact about that state, and
        /// one answer for every caller is worth more here than keeping the last
        /// English out of the assembly.
        /// </remarks>
        public static string DisplayName(GameState state)
        {
            var name = state?.wardenName;
            return string.IsNullOrWhiteSpace(name) ? Anonymous : name.Trim();
        }

        /// <summary>
        /// The warden's name in the possessive — "the warden's own hands", or
        /// "Rowan's". Always <c>'s</c>, including after a trailing s ("Ross's"),
        /// which is the modern convention and the one that never reads as a
        /// plural.
        /// </summary>
        public static string PossessiveName(GameState state)
        {
            return DisplayName(state) + "'s";
        }

        /// <summary>Whether the warden has been named — the rename sheet asks, to tell a first naming from a change.</summary>
        public static bool IsNamed(GameState state)
        {
            return !string.IsNullOrWhiteSpace(state?.wardenName);
        }

        /// <summary>The warden's post — a node id, <see cref="Familiar.WanderStation"/>, or null while they stand at camp.</summary>
        public static string PostNodeId(GameState state)
        {
            return string.IsNullOrEmpty(state.wardenPostNodeId) ? null : state.wardenPostNodeId;
        }

        /// <summary>Whether the warden stands at <paramref name="node"/> (never true while wandering — the wander post is no single node).</summary>
        public static bool IsPosted(GameState state, NodeState node)
        {
            return node != null && node.id == PostNodeId(state);
        }

        /// <summary>Whether the warden roams the wander post (design §2: the warden may now take it, like any familiar).</summary>
        public static bool IsWandering(GameState state)
        {
            return state.wardenPostNodeId == Familiar.WanderStation;
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

        /// <summary>
        /// Send the warden to the wander post — roaming every node and watch
        /// site (design §2). One body per post: a familiar already wandering
        /// steps back to camp (its slot frees), same as taking a node.
        /// </summary>
        public static void Wander(GameState state)
        {
            var occupant = Stationing.OccupantOf(state, Familiar.WanderStation);
            if (occupant != null)
            {
                occupant.stationId = null;
            }

            state.wardenPostNodeId = Familiar.WanderStation;
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
        /// burst boost — the full rate at their posted node, an even share of
        /// that rate across every node while wandering (roaming, averaged out
        /// like a wandering familiar), and zero at camp or when unconfigured
        /// (pre-warden fixtures stay inert).
        /// </summary>
        public static double GatherPerSecond(GameState state, EconomyData economy, NodeState node)
        {
            if (economy?.warden == null)
            {
                return 0.0;
            }

            if (IsPosted(state, node))
            {
                return economy.warden.gatherPerSecond;
            }

            var nodeCount = state.nodes.Count;
            if (IsWandering(state) && nodeCount > 0)
            {
                return economy.warden.gatherPerSecond / nodeCount;
            }

            return 0.0;
        }

        /// <summary>
        /// Data-aware overload: the base rate, quickened by whatever carries
        /// for the warden — the fell pony's wardenYieldBonus trait (§11) and
        /// worn-gear wardenYieldBonus effects (the Birch Frame Pack) sum into
        /// one additive band.
        /// </summary>
        public static double GatherPerSecond(GameState state, GameDataAsset data, EconomyData economy, NodeState node)
        {
            var rate = GatherPerSecond(state, economy, node);
            if (rate <= 0.0 || data == null)
            {
                return rate;
            }

            // The tide's lean is the node's, not the agent's — the warden's
            // hands feel Beltane at a flower node the same as the kith's do.
            return rate * (1.0 + Traits.WardenYieldBonus(state, data) + Upgrades.WardenYieldBonus(state, data))
                        * Wheel.YieldMult(state, data, node.resourceId);
        }
    }
}
