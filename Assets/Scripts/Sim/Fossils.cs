using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Fossil assembly (design §5): fragments surface from dig sites, a fossil
    /// completes when its target count is reached, and a completed fossil
    /// grants its permanent effects — the run's large late multipliers. The
    /// fossil card (lore) arrives with the Compendium.
    /// </summary>
    public static class Fossils
    {
        public static int FragmentCount(GameState state, string fossilId)
        {
            return state.fossilFragments.TryGetValue(fossilId, out var count) ? count : 0;
        }

        public static bool IsComplete(GameState state, FossilData fossil)
        {
            return fossil.fragments > 0 && FragmentCount(state, fossil.id) >= fossil.fragments;
        }

        /// <summary>Every completed fossil, in data order.</summary>
        public static List<FossilData> Completed(GameState state, GameDataAsset data)
        {
            var completed = new List<FossilData>();
            if (data.fossils == null)
            {
                return completed;
            }

            foreach (var fossil in data.fossils)
            {
                if (IsComplete(state, fossil))
                {
                    completed.Add(fossil);
                }
            }

            return completed;
        }

        /// <summary>
        /// The effects the run's completed fossils grant — fed into the same
        /// accumulators as purchased upgrade effects (yield, Pristine chance).
        /// </summary>
        public static IEnumerable<EffectData> CompletedEffects(GameState state, GameDataAsset data)
        {
            if (data.fossils == null)
            {
                yield break;
            }

            foreach (var fossil in data.fossils)
            {
                if (!IsComplete(state, fossil))
                {
                    continue;
                }

                foreach (var effect in fossil.effects)
                {
                    yield return effect;
                }
            }
        }
    }
}
