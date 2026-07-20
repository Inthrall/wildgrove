using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Replanting (design §3): a node's richness is raised by feeding its own
    /// resource back into it — replantCost(L) = baseCost·growth^L units from camp
    /// stock, per node per run, each level adding richnessPerLevel to the node's
    /// base yield. No Renown (it pays back in yield, not the ledger). Richness
    /// resets at Migration. No-ops when economy.replant is absent (fixtures).
    /// </summary>
    public static class Replanting
    {
        public static bool Configured(EconomyData economy)
        {
            return economy?.replant != null && economy.replant.baseCost > BigDouble.Zero;
        }

        /// <summary>Units of the node's own resource the next richness level costs: baseCost·growth^level.</summary>
        public static BigDouble ReplantCost(NodeState node, EconomyData economy)
        {
            if (!Configured(economy) || node == null)
            {
                return BigDouble.Zero;
            }

            return economy.replant.baseCost * BigDouble.Pow(economy.replant.growth, node.richnessLevel);
        }

        /// <summary>The node's richness yield multiplier: 1 + richnessPerLevel · richnessLevel.</summary>
        public static double RichnessMultiplier(NodeState node, EconomyData economy)
        {
            if (!Configured(economy) || node == null)
            {
                return 1.0;
            }

            return 1.0 + economy.replant.richnessPerLevel * node.richnessLevel;
        }

        /// <summary>True when camp stock covers the node's next replant cost.</summary>
        public static bool CanReplant(GameState state, GameDataAsset data, NodeState node)
        {
            if (state == null || data == null || node == null || !Configured(data.economy))
            {
                return false;
            }

            return state.GetResource(node.resourceId) >= ReplantCost(node, data.economy);
        }

        /// <summary>
        /// Spend the node's own resource from camp stock to raise its richness one
        /// level. Returns false (and changes nothing) when stock is short or
        /// replanting isn't configured.
        /// </summary>
        public static bool TryReplant(GameState state, GameDataAsset data, NodeState node)
        {
            if (!CanReplant(state, data, node))
            {
                return false;
            }

            var cost = ReplantCost(node, data.economy);
            state.resources[node.resourceId] = state.GetResource(node.resourceId) - cost;
            node.richnessLevel += 1;
            return true;
        }
    }
}
