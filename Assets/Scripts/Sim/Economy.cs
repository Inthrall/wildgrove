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
        /// True when a gatherer gift can land on the node: camp stock covers
        /// the node's own-resource cost AND the zone's flock is under its cap
        /// (design §8: flockCap per zone, raised by the roosts line).
        /// </summary>
        public static bool CanGiftGatherer(GameState state, GameDataAsset data, NodeState node)
        {
            if (state == null || data == null || node == null)
            {
                return false;
            }

            if (ZoneFamiliarCount(state, node.zoneId) >= Buildings.FlockCap(state, data))
            {
                return false;
            }

            return state.GetResource(node.resourceId) >= GathererGiftCost(node, data.economy);
        }

        /// <summary>
        /// Gift one gatherer onto <paramref name="node"/>, spending the
        /// own-resource cost from camp stock. Returns false (and changes
        /// nothing) when stock is short or the zone's flock is at its cap, so
        /// the caller can leave the button disabled.
        /// </summary>
        public static bool TryGiftGatherer(GameState state, GameDataAsset data, NodeState node)
        {
            if (!CanGiftGatherer(state, data, node))
            {
                return false;
            }

            var cost = GathererGiftCost(node, data.economy);
            state.resources[node.resourceId] = state.GetResource(node.resourceId) - cost;
            node.familiarCount += 1;
            return true;
        }

        private static int ZoneFamiliarCount(GameState state, string zoneId)
        {
            var total = 0;
            foreach (var node in state.nodes)
            {
                if (node.zoneId == zoneId)
                {
                    total += node.familiarCount;
                }
            }

            return total;
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

        /// <summary>
        /// True when the Feeder can be filled: camp stock covers the whole
        /// bundle AND the camp has a free carrier slot (design §8, raised by
        /// the roosts line) — for the gift button's enabled state.
        /// </summary>
        public static bool CanGiftCarrier(GameState state, GameDataAsset data)
        {
            if (state == null || data == null)
            {
                return false;
            }

            if (state.carrierCount >= Buildings.CarrierSlots(state, data))
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

            var costEach = CarrierGiftCostEach(state, data.economy);
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
        /// any part of the bundle is short or the carrier slots are full.
        /// </summary>
        public static bool TryGiftCarrier(GameState state, GameDataAsset data)
        {
            if (!CanGiftCarrier(state, data))
            {
                return false;
            }

            var costEach = CarrierGiftCostEach(state, data.economy);
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
        /// Raw gatherables are priced by resources.json; crafted <b>trade</b>
        /// goods derive their value from the recipe — summed input base value ×
        /// valueMult (recipes.json convention) — so a preserve is always worth
        /// more than its berries. Materials (ingots, cordage) and unknown ids
        /// are unsellable and return zero. Input values in the derivation are
        /// base values: an input's own sellValueBonus never inflates the goods
        /// crafted from it.
        /// </summary>
        public static BigDouble SellValuePerUnit(GameState state, GameDataAsset data, string resourceId)
        {
            var baseValue = BaseUnitValue(data, resourceId, null);
            return baseValue > BigDouble.Zero
                ? baseValue * Upgrades.SellValueMultiplier(state, data, resourceId)
                : BigDouble.Zero;
        }

        private static BigDouble BaseUnitValue(GameDataAsset data, string resourceId, HashSet<string> visiting)
        {
            if (resourceId == null)
            {
                return BigDouble.Zero;
            }

            if (data.ResourcesById.TryGetValue(resourceId, out var resource))
            {
                return resource.sellValue;
            }

            var recipe = RecipeProducing(data, resourceId);
            if (recipe == null || recipe.kind != "trade")
            {
                return BigDouble.Zero;
            }

            // Guard against a recipe cycle in authored data — better an
            // unsellable good than a stack overflow.
            visiting = visiting ?? new HashSet<string>();
            if (!visiting.Add(resourceId))
            {
                return BigDouble.Zero;
            }

            var total = BigDouble.Zero;
            foreach (var input in recipe.inputs)
            {
                total += BaseUnitValue(data, input.id, visiting) * input.amount;
            }

            visiting.Remove(resourceId);
            return total * recipe.valueMult;
        }

        private static RecipeData RecipeProducing(GameDataAsset data, string resourceId)
        {
            if (data.recipes == null)
            {
                return null;
            }

            foreach (var recipe in data.recipes)
            {
                if (recipe.output == resourceId)
                {
                    return recipe;
                }
            }

            return null;
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
