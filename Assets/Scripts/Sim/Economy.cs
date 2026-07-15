using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The spending actions of the core loop, all goods sinks per design §13:
    /// gatherer gifts are paid in the node's own resource (depth), carrier
    /// gifts fill the camp Feeder with a bundle of every worked resource
    /// (breadth), and selling camp stock to the Provisioner is the Coin
    /// source. Prices climb with each familiar befriended, on separate
    /// curves; Coin never buys a creature (design §9). Pure and deterministic
    /// like the tick — the MonoBehaviour driver wires these to input, the
    /// tests pin the maths.
    /// </summary>
    public static class Economy
    {
        /// <summary>
        /// Size of the next gatherer's gift, per design doc §8: cost(n) = base · r^n,
        /// where n is the gatherers already befriended at <paramref name="node"/>
        /// and r is the gatherer-gift growth factor. Depth pricing is per node:
        /// a virgin trail's first gift always costs exactly the base however
        /// large the flock is elsewhere (so a new node is a few taps of
        /// hand-gather from starting, not a camp-inflated grind), and each
        /// spirit at a worked node asks more than the last. The units are the
        /// node's own resource (§13 decision: you leave a pile of what the
        /// flock likes), paid from camp stock.
        /// </summary>
        public static BigDouble GathererGiftCost(NodeState node, EconomyData economy)
        {
            var growth = BigDouble.Pow(economy.costGrowth.gathererGift, node.familiarCount);
            return economy.gifts.gathererBaseGoods * growth;
        }

        /// <summary>
        /// Gift one gatherer onto <paramref name="node"/> if camp stock holds
        /// enough of the node's own resource, spending it. Returns false (and
        /// changes nothing) when stock is short or the node is null, so the
        /// caller can leave the button disabled.
        /// </summary>
        public static bool TryGiftGatherer(GameState state, EconomyData economy, NodeState node)
        {
            if (state == null || economy == null || node == null)
            {
                return false;
            }

            var cost = GathererGiftCost(node, economy);
            if (state.GetResource(node.resourceId) < cost)
            {
                return false;
            }

            state.resources[node.resourceId] = state.GetResource(node.resourceId) - cost;
            node.familiarCount += 1;
            return true;
        }

        /// <summary>
        /// Per-resource size of the next carrier's gift — the gatherer curve's
        /// shape with the carrier base and growth (design §8: gifts and
        /// carriers scale separately), where n is the carriers already hauling
        /// this run. The camp Feeder takes this many units of <b>each</b>
        /// resource in <see cref="FeederResources"/>.
        /// </summary>
        public static BigDouble CarrierGiftCostEach(GameState state, EconomyData economy)
        {
            var growth = BigDouble.Pow(economy.costGrowth.carrierGift, state.carrierCount);
            return economy.gifts.carrierBaseGoods * growth;
        }

        /// <summary>
        /// What the camp Feeder is stocked with: one of each distinct resource
        /// the flock currently works (every node with a gatherer). Gatherers
        /// are priced in depth (their node's own resource), carriers in
        /// breadth — they serve every trail (design §13).
        /// </summary>
        public static List<string> FeederResources(GameState state)
        {
            var resources = new List<string>();
            foreach (var node in state.nodes)
            {
                if (node.familiarCount > 0 && node.resourceId != null && !resources.Contains(node.resourceId))
                {
                    resources.Add(node.resourceId);
                }
            }

            return resources;
        }

        /// <summary>True when camp stock covers the whole Feeder bundle — for the gift button's enabled state.</summary>
        public static bool CanGiftCarrier(GameState state, EconomyData economy)
        {
            if (state == null || economy == null)
            {
                return false;
            }

            var resources = FeederResources(state);
            if (resources.Count == 0)
            {
                // Nothing worked means nothing to stock the Feeder with — and
                // never a free carrier.
                return false;
            }

            var costEach = CarrierGiftCostEach(state, economy);
            foreach (var resourceId in resources)
            {
                if (state.GetResource(resourceId) < costEach)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Fill the camp Feeder to gift one carrier into the camp-wide pool:
        /// spends <see cref="CarrierGiftCostEach"/> units of each worked
        /// resource from camp stock. Returns false (and changes nothing) when
        /// any part of the bundle is short.
        /// </summary>
        public static bool TryGiftCarrier(GameState state, EconomyData economy)
        {
            if (!CanGiftCarrier(state, economy))
            {
                return false;
            }

            var costEach = CarrierGiftCostEach(state, economy);
            foreach (var resourceId in FeederResources(state))
            {
                state.resources[resourceId] = state.GetResource(resourceId) - costEach;
            }

            state.carrierCount += 1;
            return true;
        }

        /// <summary>
        /// Provisioner sell value in Coin for one unit of a resource, including
        /// any sellValueBonus upgrades the run owns (e.g. the Drying Rack).
        /// Only raw gatherables are priced (resources.json); anything else —
        /// crafted materials, ingots, an unknown id — is unsellable and returns zero.
        /// </summary>
        public static BigDouble SellValuePerUnit(GameState state, GameDataAsset data, string resourceId)
        {
            if (resourceId != null && data.ResourcesById.TryGetValue(resourceId, out var resource))
            {
                return resource.sellValue * Upgrades.SellValueMultiplier(state, data, resourceId);
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

            var unitValue = SellValuePerUnit(state, data, resourceId);
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
