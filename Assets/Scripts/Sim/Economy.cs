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

            // Diggers live in the zone too — they share its flock cap.
            foreach (var site in state.digSites)
            {
                if (site.zoneId == zoneId)
                {
                    total += site.familiarCount;
                }
            }

            return total;
        }

        /// <summary>
        /// Per-resource size of the next digger's gift: the gatherer curve
        /// (base · gathererGift^n, n = diggers already at the site), paid in a
        /// bundle of <b>each</b> of the zone's resources — a dig site yields no
        /// resource of its own to leave a pile of, so the pile is what the
        /// zone's ground gives (interpretation flagged in todo.md).
        /// </summary>
        public static BigDouble DiggerGiftCostEach(DigSiteState site, EconomyData economy)
        {
            var growth = BigDouble.Pow(economy.costGrowth.gathererGift, site.familiarCount);
            return economy.gifts.gathererBaseGoods * growth;
        }

        /// <summary>The resources a digger's gift bundle asks for: the site's zone yields.</summary>
        public static List<string> DiggerGiftResources(GameDataAsset data, string zoneId)
        {
            return data.ZonesById.TryGetValue(zoneId, out var zone)
                ? new List<string>(zone.resources)
                : new List<string>();
        }

        /// <summary>
        /// True when a digger gift can land: camp stock covers the whole
        /// zone-resource bundle AND the zone's flock (gatherers + diggers) is
        /// under its cap.
        /// </summary>
        public static bool CanGiftDigger(GameState state, GameDataAsset data, DigSiteState site)
        {
            if (state == null || data == null || site == null)
            {
                return false;
            }

            if (ZoneFamiliarCount(state, site.zoneId) >= Buildings.FlockCap(state, data))
            {
                return false;
            }

            var resources = DiggerGiftResources(data, site.zoneId);
            if (resources.Count == 0)
            {
                return false;
            }

            var costEach = DiggerGiftCostEach(site, data.economy);
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
        /// Gift one digger onto the site, spending the zone-resource bundle
        /// from camp stock. Returns false (and changes nothing) when stock is
        /// short or the zone's flock is at its cap.
        /// </summary>
        public static bool TryGiftDigger(GameState state, GameDataAsset data, DigSiteState site)
        {
            if (!CanGiftDigger(state, data, site))
            {
                return false;
            }

            var costEach = DiggerGiftCostEach(site, data.economy);
            foreach (var resourceId in DiggerGiftResources(data, site.zoneId))
            {
                state.resources[resourceId] = state.GetResource(resourceId) - costEach;
            }

            site.familiarCount += 1;
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
                          * MasteryValueMultiplier(state, data, resourceId)
                : BigDouble.Zero;
        }

        /// <summary>
        /// Mastery's value bonus (design §4: "+yield/value per level"), from
        /// the node gathering this resource. Applies to the raw gatherable's
        /// direct sale only — like sellValueBonus, it never inflates goods
        /// crafted from it (BaseUnitValue stays a base value).
        /// </summary>
        private static double MasteryValueMultiplier(GameState state, GameDataAsset data, string resourceId)
        {
            if (!Mastery.Configured(data.economy))
            {
                return 1.0;
            }

            // Camp stock is pooled, so goods carry no provenance — when the
            // same resource grows in more than one zone, price by the most
            // practised node rather than whichever happens to sit first.
            var bestLevel = -1;
            foreach (var node in state.nodes)
            {
                if (node.resourceId == resourceId)
                {
                    var level = Mastery.Level(node, data.economy);
                    if (level > bestLevel)
                    {
                        bestLevel = level;
                    }
                }
            }

            return bestLevel >= 0
                ? 1.0 + data.economy.mastery.yieldBonusPerLevel * bestLevel
                : 1.0;
        }

        /// <summary>
        /// The trade value the Provisioner would pay per unit, before any
        /// state bonuses — zero for materials. The Rite generator uses this
        /// to decide whether a goods slot needs an explicit renownGrant.
        /// </summary>
        public static BigDouble TradeUnitValue(GameDataAsset data, string resourceId)
        {
            return BaseUnitValue(data, resourceId, null);
        }

        /// <summary>
        /// A good's notional worth per unit, for pricing Rite demands: raw
        /// finds at their sellValue, crafted goods — INCLUDING materials,
        /// which trade at zero — at their input-derived worth (summed input
        /// notional value × the recipe's valueMult). This is the "notional
        /// input-derived worth" the authored rites.json grants approximate.
        /// </summary>
        public static BigDouble NotionalUnitValue(GameDataAsset data, string resourceId)
        {
            return NotionalValue(data, resourceId, null);
        }

        private static BigDouble NotionalValue(GameDataAsset data, string resourceId, HashSet<string> visiting)
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
            if (recipe == null)
            {
                return BigDouble.Zero;
            }

            visiting = visiting ?? new HashSet<string>();
            if (!visiting.Add(resourceId))
            {
                return BigDouble.Zero;
            }

            var total = BigDouble.Zero;
            foreach (var input in recipe.inputs)
            {
                total += NotionalValue(data, input.id, visiting) * input.amount;
            }

            visiting.Remove(resourceId);
            return total * (recipe.valueMult > 0 ? recipe.valueMult : 1.0);
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
        /// Sell the whole stock of one resource to the Provisioner — common
        /// units at the unit value, Fine units alongside at the design §5
        /// quality bonus — moving the Coin into the purse and clearing both
        /// pools. Pristine specimens are deliberately NOT included: the
        /// windfall sale is <see cref="SellPristine"/>, an explicit act, so
        /// sell-all can never spend the sell/donate/offer choice for the
        /// player. A no-op for unsellable resources (leaves the stock
        /// untouched). Returns the Coin gained.
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

            var coin = state.GetResource(resourceId) * unitValue;
            coin += state.GetFine(resourceId) * unitValue * FineValueMult(data);
            if (coin <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            state.coin += coin;
            state.resources[resourceId] = BigDouble.Zero;
            state.fineResources[resourceId] = BigDouble.Zero;
            return coin;
        }

        /// <summary>
        /// The windfall (design §5): sell every Pristine specimen of one
        /// resource at quality.pristineValueMult × the unit value. Only ever
        /// called explicitly — donation and offering will compete for these
        /// specimens when their systems land. Returns the Coin gained.
        /// </summary>
        public static BigDouble SellPristine(GameState state, GameDataAsset data, string resourceId)
        {
            if (state == null || data == null)
            {
                return BigDouble.Zero;
            }

            var unitValue = SellValuePerUnit(state, data, resourceId);
            var held = state.GetPristine(resourceId);
            if (unitValue <= BigDouble.Zero || held <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            var mult = data.economy?.quality != null ? data.economy.quality.pristineValueMult : 1.0;
            var coin = held * unitValue * mult;
            state.coin += coin;
            state.pristineResources[resourceId] = BigDouble.Zero;
            return coin;
        }

        private static double FineValueMult(GameDataAsset data)
        {
            return data.economy?.quality != null ? data.economy.quality.fineValueMult : 1.0;
        }

        /// <summary>
        /// Sell every sellable resource on hand — common and Fine pools;
        /// Pristine specimens stay (see <see cref="SellResource"/>). Returns
        /// the total Coin gained.
        /// </summary>
        public static BigDouble SellAll(GameState state, GameDataAsset data)
        {
            if (state == null || data == null)
            {
                return BigDouble.Zero;
            }

            var total = BigDouble.Zero;

            // Snapshot the keys — SellResource mutates the dictionaries as it
            // goes — and include resources held only as Fine finds.
            var resourceIds = new HashSet<string>(state.resources.Keys);
            resourceIds.UnionWith(state.fineResources.Keys);

            foreach (var resourceId in resourceIds)
            {
                total += SellResource(state, data, resourceId);
            }

            return total;
        }
    }
}
