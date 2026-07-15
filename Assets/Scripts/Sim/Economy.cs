using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The two Coin-facing player actions of the core loop: hiring crew (a Coin
    /// sink whose price climbs with each hire) and selling gathered resources to
    /// the Provisioner (the Coin source). Pure and deterministic like the tick —
    /// the MonoBehaviour driver wires these to input, the tests pin the maths.
    /// </summary>
    public static class Economy
    {
        /// <summary>
        /// Cost of the next crew hire, per design doc §8: cost(n) = base · r^n,
        /// where n is the crew already hired this run and r is the crew-hire
        /// growth factor. The first hire (n = 0) costs exactly the base.
        /// </summary>
        public static BigDouble CrewHireCost(GameState state, EconomyData economy)
        {
            var growth = BigDouble.Pow(economy.costGrowth.crewHire, state.TotalCrew());
            return economy.hires.crewBaseCoin * growth;
        }

        /// <summary>
        /// Hire one crew onto <paramref name="node"/> if the run can afford it,
        /// spending the Coin. Returns false (and changes nothing) when Coin is
        /// short or the node is null, so the caller can leave the button disabled.
        /// </summary>
        public static bool TryHireCrew(GameState state, EconomyData economy, NodeState node)
        {
            if (state == null || economy == null || node == null)
            {
                return false;
            }

            var cost = CrewHireCost(state, economy);
            if (state.coin < cost)
            {
                return false;
            }

            state.coin -= cost;
            node.crewCount += 1;
            return true;
        }

        /// <summary>
        /// Provisioner sell value in Coin for one unit of a resource. Only raw
        /// gatherables are priced (resources.json); anything else — crafted
        /// materials, ingots, an unknown id — is unsellable and returns zero.
        /// </summary>
        public static BigDouble SellValuePerUnit(GameDataAsset data, string resourceId)
        {
            if (resourceId != null && data.ResourcesById.TryGetValue(resourceId, out var resource))
            {
                return resource.sellValue;
            }

            return BigDouble.Zero;
        }

        /// <summary>
        /// Sell the whole stock of one resource to the Provisioner, moving its
        /// Coin value into the purse and clearing the stock. A no-op for
        /// unsellable resources (leaves the stock untouched). Returns the Coin gained.
        /// </summary>
        public static BigDouble SellResource(GameState state, GameDataAsset data, string resourceId)
        {
            if (state == null || data == null)
            {
                return BigDouble.Zero;
            }

            var unitValue = SellValuePerUnit(data, resourceId);
            if (unitValue <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            var held = state.GetResource(resourceId);
            if (held <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            var coin = held * unitValue;
            state.coin += coin;
            state.resources[resourceId] = BigDouble.Zero;
            return coin;
        }

        /// <summary>Sell every sellable resource on hand. Returns the total Coin gained.</summary>
        public static BigDouble SellAll(GameState state, GameDataAsset data)
        {
            if (state == null || data == null)
            {
                return BigDouble.Zero;
            }

            var total = BigDouble.Zero;

            // Snapshot the keys — SellResource mutates the dictionary as it goes.
            var resourceIds = new string[state.resources.Count];
            state.resources.Keys.CopyTo(resourceIds, 0);

            foreach (var resourceId in resourceIds)
            {
                total += SellResource(state, data, resourceId);
            }

            return total;
        }
    }
}
