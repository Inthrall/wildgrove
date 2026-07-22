using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Gift piles (design §4): every verse sung — across every run — earns the
    /// warden one pile, and one yes. Leave a pile of a node's own resource and
    /// that resource's specialist answers, taking the node as its post. Which
    /// species comes is chosen by where the pile is left; a pile is refused
    /// where the specialist already walks (each species joins once, ever) or
    /// where no specialist exists. Piles answered are counted from the roster
    /// (the gifted flag), so a retuned save self-heals. No-ops when
    /// economy.gifts is absent (fixtures).
    /// </summary>
    public static class Gifts
    {
        public static bool Configured(EconomyData economy)
        {
            return economy?.gifts != null && economy.gifts.pileGoods > BigDouble.Zero;
        }

        /// <summary>Units of the node's own resource one pile costs — flat, never a curve.</summary>
        public static BigDouble PileCost(EconomyData economy)
        {
            return Configured(economy) ? economy.gifts.pileGoods : BigDouble.Zero;
        }

        /// <summary>Piles already answered — the arrivals walking with the kith.</summary>
        public static int GiftedCount(GameState state)
        {
            if (state?.roster == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (familiar.gifted)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Piles the verses have earned and no one has answered yet.</summary>
        public static int PilesRemaining(GameState state, GameDataAsset data)
        {
            var remaining = Kith.TotalVersesSung(state, data) - GiftedCount(state);
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>A pile is on offer somewhere: configured, and a verse-earned pile is unanswered. The node plates' pile lines show on this.</summary>
        public static bool IsAvailable(GameState state, GameDataAsset data)
        {
            return Configured(data?.economy) && PilesRemaining(state, data) > 0;
        }

        /// <summary>
        /// The specialist a pile at <paramref name="node"/> would call: the
        /// species whose trait names the node's resource — null when none does.
        /// </summary>
        public static SpeciesData SpecialistFor(GameDataAsset data, NodeState node)
        {
            if (node == null || data?.species == null)
            {
                return null;
            }

            foreach (var species in data.species)
            {
                if (species.trait != null
                    && species.trait.kind == "nodeYieldBonus"
                    && species.trait.resource == node.resourceId)
                {
                    return species;
                }
            }

            return null;
        }

        /// <summary>
        /// True when a pile at this node could ever be answered: a specialist
        /// exists and hasn't already joined. (Affordability and slot room are
        /// CanLeavePile's business — this is the "worth showing the line" test.)
        /// </summary>
        public static bool NodeCanCall(GameState state, GameDataAsset data, NodeState node)
        {
            var specialist = SpecialistFor(data, node);
            return specialist != null && Roster.OfSpecies(state, specialist.id) == null;
        }

        /// <summary>True when camp stock covers a pile at this node, someone new would answer it, and a slot is open for them.</summary>
        public static bool CanLeavePile(GameState state, GameDataAsset data, NodeState node)
        {
            return node != null
                && IsAvailable(state, data)
                && NodeCanCall(state, data, node)
                && Kith.HasRoom(state, data)
                && state.GetResource(node.resourceId) >= PileCost(data.economy);
        }

        /// <summary>
        /// Leave the pile: spend the node's own resource from camp stock and
        /// the resource's specialist says yes — stationed at that node, to be
        /// named on the arrival sheet like any recruit. Returns null (and
        /// changes nothing) when the gift can't happen.
        /// </summary>
        public static Familiar LeavePile(GameState state, GameDataAsset data, NodeState node)
        {
            if (!CanLeavePile(state, data, node))
            {
                return null;
            }

            var familiar = Roster.Recruit(state, data, SpecialistFor(data, node).id, node.id);
            if (familiar == null)
            {
                return null;
            }

            familiar.gifted = true;
            state.resources[node.resourceId] = state.GetResource(node.resourceId) - PileCost(data.economy);
            return familiar;
        }
    }
}
