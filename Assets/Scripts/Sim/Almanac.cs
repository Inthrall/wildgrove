using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Almanac — the permanent tree bought with Verdure (design §7).
    /// Ownership survives Migration. Verdure is never destroyed: a node
    /// ALLOCATES from the banked total (available = verdurePoints − the sum
    /// of owned node costs), so the +2%/pt passive keeps counting the full
    /// total and Migration's recompute-from-lifetime-Renown stays honest.
    /// </summary>
    public static class Almanac
    {
        /// <summary>Verdure allocated to owned nodes.</summary>
        public static double SpentVerdure(GameState state, GameDataAsset data)
        {
            var spent = 0.0;
            foreach (var nodeId in state.almanacNodeIds)
            {
                // An id this data version doesn't know is skipped, same
                // policy as purchased upgrades.
                if (data.AlmanacById.TryGetValue(nodeId, out var node))
                {
                    spent += node.costVerdure;
                }
            }

            return spent;
        }

        /// <summary>Verdure not yet allocated to a node — what the next purchase can draw on.</summary>
        public static double AvailableVerdure(GameState state, GameDataAsset data)
        {
            return state.verdurePoints - SpentVerdure(state, data);
        }

        public static bool IsOwned(GameState state, AlmanacNodeData node)
        {
            return state.almanacNodeIds.Contains(node.id);
        }

        /// <summary>True when the node can be bought: not owned, prerequisite owned, and unallocated Verdure covers the cost.</summary>
        public static bool CanBuy(GameState state, GameDataAsset data, AlmanacNodeData node)
        {
            if (state == null || data == null || node == null || IsOwned(state, node))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(node.requires)
                && data.AlmanacById.TryGetValue(node.requires, out var prerequisite)
                && !IsOwned(state, prerequisite))
            {
                return false;
            }

            return AvailableVerdure(state, data) >= node.costVerdure;
        }

        /// <summary>
        /// Buy the node: records ownership (allocating its Verdure) and
        /// recomputes the yield multipliers its effects may feed. Returns
        /// false (and changes nothing) when <see cref="CanBuy"/> says no.
        /// </summary>
        public static bool TryBuy(GameState state, GameDataAsset data, AlmanacNodeData node)
        {
            if (!CanBuy(state, data, node))
            {
                return false;
            }

            state.almanacNodeIds.Add(node.id);
            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }
    }
}
