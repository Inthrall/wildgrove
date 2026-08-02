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
    /// record of having gathered them), crafted batches, and Choice units
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

        /// <summary>
        /// Note that a species has been befriended at least once. Idempotent —
        /// the same species recruited again in a later run adds nothing.
        /// </summary>
        public static void RecordSpecies(GameState state, string speciesId)
        {
            if (state == null || string.IsNullOrEmpty(speciesId))
            {
                return;
            }

            if (!state.speciesEverBefriended.Contains(speciesId))
            {
                state.speciesEverBefriended.Add(speciesId);
            }
        }

        /// <summary>Note that a station has finished a batch at least once. Idempotent.</summary>
        public static void RecordStationWorked(GameState state, string stationId)
        {
            if (state == null || string.IsNullOrEmpty(stationId))
            {
                return;
            }

            if (!state.stationsEverWorked.Contains(stationId))
            {
                state.stationsEverWorked.Add(stationId);
            }
        }

        public static void RecordChoice(GameState state, string resourceId, BigDouble amount)
        {
            if (amount <= BigDouble.Zero)
            {
                return;
            }

            state.lifetimeChoice.TryGetValue(resourceId, out var total);
            state.lifetimeChoice[resourceId] = total + amount;
        }

        public static BigDouble LifetimeGathered(GameState state, string resourceId)
        {
            return state.lifetimeGathered.TryGetValue(resourceId, out var total) ? total : BigDouble.Zero;
        }

        public static double LifetimeCrafted(GameState state, string recipeId)
        {
            return state.lifetimeCrafted.TryGetValue(recipeId, out var total) ? total : 0.0;
        }

        public static BigDouble LifetimeChoice(GameState state, string resourceId)
        {
            return state.lifetimeChoice.TryGetValue(resourceId, out var total) ? total : BigDouble.Zero;
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

        /// <summary>
        /// How much of the journal's back pages are written, across every
        /// collection the Record page holds: Compendium entries, Folio presses,
        /// insect plates, the deep amber, and the Almanac's authored lines. Each
        /// card counts its own kind, so the page could say how every collection
        /// was going except the collection itself.
        ///
        /// Folio entries are counted DISTINCT: the Warden's Gallery re-lists
        /// eight specimens the earlier spreads already ask for, and one press
        /// fills the entry everywhere, so counting per spread would charge the
        /// same specimen twice. The Almanac's endless line is left out of both
        /// halves — a verse count with no last verse would hang a ceiling on
        /// the page that isn't there.
        /// </summary>
        public static (int recorded, int total) RecordProgress(GameState state, GameDataAsset data)
        {
            if (state == null || data == null)
            {
                return (0, 0);
            }

            var recorded = DiscoveredCount(state, data);
            var total = TotalEntries(data);

            if (data.folioSpreads != null)
            {
                var wanted = new HashSet<string>();
                foreach (var spread in data.folioSpreads)
                {
                    if (spread.entries == null)
                    {
                        continue;
                    }

                    foreach (var entry in spread.entries)
                    {
                        wanted.Add(entry);
                    }
                }

                total += wanted.Count;
                foreach (var entry in wanted)
                {
                    if (Folio.IsFixed(state, entry))
                    {
                        recorded++;
                    }
                }
            }

            if (data.insects != null)
            {
                total += data.insects.Count;
                foreach (var insect in data.insects)
                {
                    if (Insects.IsRecorded(state, insect))
                    {
                        recorded++;
                    }
                }
            }

            if (DeepAmber.Configured(data))
            {
                var pieces = data.deepAmber.pieces.Count;
                total += pieces;
                recorded += System.Math.Min(DeepAmber.FoundCount(state), pieces);
            }

            if (data.almanac != null)
            {
                foreach (var node in data.almanac)
                {
                    if (node.repeatable)
                    {
                        continue;
                    }

                    total++;
                    if (state.almanacNodeIds.Contains(node.id))
                    {
                        recorded++;
                    }
                }
            }

            return (recorded, total);
        }
    }
}
