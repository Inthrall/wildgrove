using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Insect plates (design §6: observe · sketch · release). Field sketches
    /// (portions) are recorded from a zone's observation site; a plate is
    /// recorded once every portion is sketched, and a recorded plate grants
    /// its permanent effects — the run's large late multipliers. Nothing is
    /// taken: the insect is released, and the plate is a book of rubbings, kept
    /// as knowledge. The plate's lore line arrives with the Compendium.
    /// </summary>
    public static class Insects
    {
        public static int SketchCount(GameState state, string insectId)
        {
            return state.insectSketches.TryGetValue(insectId, out var count) ? count : 0;
        }

        public static bool IsRecorded(GameState state, InsectData insect)
        {
            return insect.sketches > 0 && SketchCount(state, insect.id) >= insect.sketches;
        }

        /// <summary>Every recorded plate, in data order.</summary>
        public static List<InsectData> Recorded(GameState state, GameDataAsset data)
        {
            var recorded = new List<InsectData>();
            if (data.insects == null)
            {
                return recorded;
            }

            foreach (var insect in data.insects)
            {
                if (IsRecorded(state, insect))
                {
                    recorded.Add(insect);
                }
            }

            return recorded;
        }

        /// <summary>
        /// The effects the run's recorded plates grant — fed into the same
        /// accumulators as purchased upgrade effects (yield, Pristine chance).
        /// </summary>
        public static IEnumerable<EffectData> RecordedEffects(GameState state, GameDataAsset data)
        {
            if (data.insects == null)
            {
                yield break;
            }

            foreach (var insect in data.insects)
            {
                if (!IsRecorded(state, insect))
                {
                    continue;
                }

                foreach (var effect in insect.effects)
                {
                    yield return effect;
                }
            }
        }
    }
}
