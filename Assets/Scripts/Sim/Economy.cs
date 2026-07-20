using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Goods valuation (design §9: money becomes XP — there is no Coin). This is
    /// the single trade-value table the Exchange barters against and the Rite
    /// credits Renown by: raw finds at their resources.json trade value (× owned
    /// bonuses and mastery), crafted trade goods at their input-derived worth.
    /// Selling-for-Coin and the old gift/Feeder curves are gone — recruitment
    /// lives in <see cref="Roster"/>, bartering in <see cref="Exchange"/>. Pure
    /// and deterministic like the tick.
    /// </summary>
    public static class Economy
    {
        /// <summary>
        /// Trade value per unit of a resource, including any sellValueBonus
        /// upgrades (the Drying Rack) and mastery. Raw gatherables are priced by
        /// resources.json; crafted <b>trade</b> goods derive value from the recipe
        /// (summed input base value × valueMult) — a preserve is always worth more
        /// than its berries. Materials (ingots, cordage) and unknown ids are
        /// unpriced and return zero. Input values in the derivation are base
        /// values: an input's own bonus never inflates the goods crafted from it.
        /// </summary>
        public static BigDouble TradeValuePerUnit(GameState state, GameDataAsset data, string resourceId)
        {
            var baseValue = BaseUnitValue(data, resourceId, null);
            return baseValue > BigDouble.Zero
                ? baseValue * Upgrades.SellValueMultiplier(state, data, resourceId)
                          * MasteryValueMultiplier(state, data, resourceId)
                : BigDouble.Zero;
        }

        /// <summary>
        /// Mastery's value bonus (design §4), from the node gathering this
        /// resource. Applies to the raw gatherable's trade value only — like
        /// sellValueBonus it never inflates goods crafted from it. Prices by the
        /// most-practised node when a resource grows in more than one zone.
        /// </summary>
        private static double MasteryValueMultiplier(GameState state, GameDataAsset data, string resourceId)
        {
            if (!Mastery.Configured(data.economy))
            {
                return 1.0;
            }

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
        /// The trade value per unit before any state bonuses — zero for
        /// materials. The Rite generator uses this to decide whether a goods slot
        /// needs an explicit renownGrant.
        /// </summary>
        public static BigDouble TradeUnitValue(GameDataAsset data, string resourceId)
        {
            return BaseUnitValue(data, resourceId, null);
        }

        /// <summary>
        /// A good's notional worth per unit for pricing Rite demands: raw finds at
        /// their trade value, crafted goods — INCLUDING materials, which trade at
        /// zero — at their input-derived worth (summed input notional value ×
        /// valueMult). This is the "notional input-derived worth" the authored
        /// rites.json grants approximate.
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
            // unpriced good than a stack overflow.
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
    }
}
