using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Compendium (design §5): every gatherable, recipe, and bonded
    /// companion has an entry with lifetime counters — one of the two
    /// collection axes that never reset ("you keep: the Compendium"). This is
    /// the system layer: counters and discovery. The hand-drawn plates and
    /// entry text arrive with the art/narrative pass. Counters record the
    /// GROSS gather (like skill XP — a full basket loses the goods, not the
    /// record of having gathered them), crafted batches, and Pristine units
    /// found; nothing ever decrements them.
    /// </summary>
    public static class Compendium
    {
        public static void RecordGather(GameState state, string resourceId, BigDouble amount)
        {
            if (amount <= BigDouble.Zero)
            {
                return;
            }

            state.lifetimeGathered.TryGetValue(resourceId, out var total);
            state.lifetimeGathered[resourceId] = total + amount;
        }

        public static void RecordCraft(GameState state, string recipeId)
        {
            state.lifetimeCrafted.TryGetValue(recipeId, out var total);
            state.lifetimeCrafted[recipeId] = total + 1.0;
        }

        public static void RecordPristine(GameState state, string resourceId, BigDouble amount)
        {
            if (amount <= BigDouble.Zero)
            {
                return;
            }

            state.lifetimePristine.TryGetValue(resourceId, out var total);
            state.lifetimePristine[resourceId] = total + amount;
        }

        public static BigDouble LifetimeGathered(GameState state, string resourceId)
        {
            return state.lifetimeGathered.TryGetValue(resourceId, out var total) ? total : BigDouble.Zero;
        }

        public static double LifetimeCrafted(GameState state, string recipeId)
        {
            return state.lifetimeCrafted.TryGetValue(recipeId, out var total) ? total : 0.0;
        }

        public static BigDouble LifetimePristine(GameState state, string resourceId)
        {
            return state.lifetimePristine.TryGetValue(resourceId, out var total) ? total : BigDouble.Zero;
        }

        /// <summary>An entry is discovered by doing, never by reading: gather it, craft it, or bond with it.</summary>
        public static bool IsResourceDiscovered(GameState state, string resourceId)
        {
            return LifetimeGathered(state, resourceId) > BigDouble.Zero;
        }

        public static bool IsRecipeDiscovered(GameState state, string recipeId)
        {
            return LifetimeCrafted(state, recipeId) > 0.0;
        }

        /// <summary>Discovered entries across all three pages: gatherables, recipes, companions.</summary>
        public static int DiscoveredCount(GameState state, GameDataAsset data)
        {
            var count = 0;
            if (data.resources != null)
            {
                foreach (var resource in data.resources)
                {
                    if (IsResourceDiscovered(state, resource.id))
                    {
                        count++;
                    }
                }
            }

            if (data.recipes != null)
            {
                foreach (var recipe in data.recipes)
                {
                    if (IsRecipeDiscovered(state, recipe.id))
                    {
                        count++;
                    }
                }
            }

            foreach (var _ in Bonds.Earned(state, data))
            {
                count++;
            }

            return count;
        }

        /// <summary>Every entry the Compendium will ever hold: one per gatherable, per recipe, per bondable companion.</summary>
        public static int TotalEntries(GameDataAsset data)
        {
            return (data.resources?.Count ?? 0) + (data.recipes?.Count ?? 0) + (data.bonds?.Count ?? 0);
        }
    }
}
