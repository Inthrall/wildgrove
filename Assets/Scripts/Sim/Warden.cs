using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The warden's post — where the player character stands and works. The
    /// post is assigned from the node strip like any familiar's (design §2:
    /// every post holds at most one body), and the warden gathers at it
    /// passively: the early game's kickstart is being somewhere, not a tap
    /// surge. A bare node in a fresh zone earns its first own-resource gift
    /// off these hands (design §13). An unassigned warden stands at camp and
    /// gathers nothing.
    ///
    /// The hands are one pair among the kith's: they ride the node's whole
    /// multiplier stack (tools, mastery, richness, planters, tide, Verdure)
    /// exactly as a stationed familiar's do — see
    /// <see cref="Simulation.YieldPerSecond"/>. What is the warden's alone is
    /// the base rate and the wardenYieldBonus band below.
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

        /// <summary>The warden's post — a node id, a sketching post, or null while they stand at camp.</summary>
        public static string PostNodeId(GameState state)
        {
            return string.IsNullOrEmpty(state.wardenPostNodeId) ? null : state.wardenPostNodeId;
        }

        /// <summary>Whether the warden stands at <paramref name="node"/> (never true at a sketching post — a site is not a node).</summary>
        public static bool IsPosted(GameState state, NodeState node)
        {
            return node != null && node.id == PostNodeId(state);
        }

        /// <summary>Whether the warden draws at some site (design §2: the warden may take a sketching post, like any familiar).</summary>
        public static bool IsSketching(GameState state)
        {
            return Familiar.IsSketchStation(state.wardenPostNodeId);
        }

        /// <summary>Whether the warden draws at <paramref name="zoneId"/>'s site in particular.</summary>
        public static bool IsSketchingAt(GameState state, string zoneId)
        {
            return state.wardenPostNodeId == Familiar.SketchStation(zoneId);
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
        /// Stand the warden at <paramref name="zoneId"/>'s site — drawing at that
        /// one place, gathering nothing (design §2/§6). One body per post: a
        /// familiar already drawing here steps back to camp (its slot frees),
        /// same as taking a node.
        /// </summary>
        public static void Sketch(GameState state, string zoneId)
        {
            var station = Familiar.SketchStation(zoneId);
            var occupant = Stationing.OccupantOf(state, station);
            if (occupant != null)
            {
                occupant.stationId = null;
            }

            state.wardenPostNodeId = station;
            state.BumpModifiers();
        }

        /// <summary>Send the warden back to camp — no post, no picking.</summary>
        public static void Rest(GameState state)
        {
            state.wardenPostNodeId = null;
            state.BumpModifiers();
        }

        /// <summary>
        /// The warden's own hands at <paramref name="node"/>, in the same units
        /// as a familiar's <c>economy.kith.gatherPerSecond</c>: their base rate
        /// widened by whatever carries for them — the fell pony's
        /// wardenYieldBonus trait (§11) and worn-gear wardenYieldBonus effects
        /// (the Birch Frame Pack) sum into one additive band. Zero anywhere but
        /// their posted node: at camp, while holding a sketching post (which is
        /// the drawing and only the drawing), or when unconfigured (pre-warden
        /// fixtures stay inert).
        /// </summary>
        /// <remarks>
        /// Deliberately NOT a finished per-second rate — the node's multiplier
        /// stack is applied once, by <see cref="Simulation.YieldPerSecond"/>,
        /// over the kith's hands and the warden's together. Reapplying any of
        /// it here (the tide used to be) would double-count it.
        /// </remarks>
        public static double HandsAt(GameState state, GameDataAsset data, EconomyData economy, NodeState node)
        {
            if (economy?.warden == null || !IsPosted(state, node))
            {
                return 0.0;
            }

            var hands = economy.warden.gatherPerSecond;
            if (data == null)
            {
                return hands;
            }

            return hands * (1.0 + Traits.WardenYieldBonus(state, data) + Upgrades.WardenYieldBonus(state, data));
        }
    }
}
