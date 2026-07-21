using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The gift event (design §4): once the run's first verse is answered —
    /// by the warden's own hands — leave a pile of a node's own resource and
    /// something says yes. One pile, one yes: a recruitment event, not a cost
    /// curve, and the arrival takes the chosen node as its post. Availability
    /// is derived from the roster (a gifted companion walking = the gift is
    /// answered), so a clamped or retuned save self-heals. No-ops when
    /// economy.gifts is absent (fixtures).
    /// </summary>
    public static class Gifts
    {
        public static bool Configured(EconomyData economy)
        {
            return economy?.gifts != null
                && economy.gifts.pileGoods > BigDouble.Zero
                && !string.IsNullOrEmpty(economy.gifts.species);
        }

        /// <summary>Units of the node's own resource one pile costs — flat, never a curve.</summary>
        public static BigDouble PileCost(EconomyData economy)
        {
            return Configured(economy) ? economy.gifts.pileGoods : BigDouble.Zero;
        }

        /// <summary>The land answers gifts only after the warden has answered it first (§4: verse 1 is sung by the warden's own hands).</summary>
        public static bool IsUnlocked(GameState state, GameDataAsset data)
        {
            var rite = Rite.CurrentRite(state, data);
            if (rite == null)
            {
                return false;
            }

            foreach (var verse in rite.verses)
            {
                if (Rite.IsVerseComplete(state, data, verse))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True while the gift event's arrival walks with the kith — one pile, one yes.</summary>
        public static bool HasGifted(GameState state)
        {
            if (state?.roster == null)
            {
                return false;
            }

            foreach (var familiar in state.roster)
            {
                if (familiar.gifted)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The gift event is live: configured, unanswered, a slot open, and a verse sung. The node plates' pile line shows on this.</summary>
        public static bool IsAvailable(GameState state, GameDataAsset data)
        {
            return Configured(data?.economy)
                && !HasGifted(state)
                && Kith.HasRoom(state, data)
                && IsUnlocked(state, data);
        }

        /// <summary>True when camp stock covers a pile at this node.</summary>
        public static bool CanLeavePile(GameState state, GameDataAsset data, NodeState node)
        {
            return node != null
                && IsAvailable(state, data)
                && state.GetResource(node.resourceId) >= PileCost(data.economy);
        }

        /// <summary>
        /// Leave the pile: spend the node's own resource from camp stock and
        /// something says yes — the arrival is stationed at that node, to be
        /// named on the arrival sheet like any recruit. Returns null (and
        /// changes nothing) when the gift can't happen.
        /// </summary>
        public static Familiar LeavePile(GameState state, GameDataAsset data, NodeState node)
        {
            if (!CanLeavePile(state, data, node))
            {
                return null;
            }

            var familiar = Roster.Recruit(state, data, data.economy.gifts.species, node.id);
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
