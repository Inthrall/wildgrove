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

        /// <summary>
        /// Record a plate outright, every portion at once — the awarded plate's
        /// way in (design §11), since no one sketches it. Returns true when this
        /// recorded it just now, false when the book already held it, so the
        /// caller can tell a fresh arrival from a re-delivery.
        ///
        /// A plate the data doesn't know is still written down at one portion:
        /// unknown ids survive a save load by convention, so a plate authored
        /// later finds its sketch already waiting rather than being lost here.
        /// </summary>
        public static bool Record(GameState state, GameDataAsset data, string insectId)
        {
            if (state == null || string.IsNullOrEmpty(insectId))
            {
                return false;
            }

            var known = data != null && data.InsectsById.TryGetValue(insectId, out var insect) ? insect : null;
            var portions = known != null && known.sketches > 0 ? known.sketches : 1;
            if (SketchCount(state, insectId) >= portions)
            {
                return false;
            }

            state.insectSketches[insectId] = portions;

            // Same moment as a plate's last portion being sketched: its
            // permanent effects go live at once. With no data there are no
            // effects to raise, but the cached snapshot still has to drop.
            if (data != null)
            {
                Upgrades.RecomputeYieldMultipliers(state, data);
            }
            else
            {
                state.BumpModifiers();
            }

            return true;
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
        /// accumulators as purchased upgrade effects (yield, Choice chance).
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
