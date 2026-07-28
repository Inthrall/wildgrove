using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The deep amber (design §6/§7): the one window the ground still opens
    /// onto the deep past. Its authored pieces surface strictly in order at a
    /// single zone's observation site — the sequence is the story, so only the
    /// timing rolls. A pity clock guarantees the next piece after
    /// pityHoursWatched watched hours without one: the lore is load-bearing
    /// and must not starve. The completed set is a plate — its effects join
    /// the active-effect union and, like every plate, survive Migration.
    /// </summary>
    public static class DeepAmber
    {
        /// <summary>Unity serializes an authored-empty section as a zeroed object — piece-less or rate-less reads as "no deep amber", never as a broken window.</summary>
        public static bool Configured(GameDataAsset data)
        {
            var amber = data?.deepAmber;
            return amber != null && amber.pieces != null && amber.pieces.Count > 0 && amber.findsPerHour > 0.0;
        }

        public static int FoundCount(GameState state)
        {
            return state.deepAmberFound;
        }

        public static bool IsComplete(GameState state, GameDataAsset data)
        {
            return Configured(data) && state.deepAmberFound >= data.deepAmber.pieces.Count;
        }

        /// <summary>The pieces already surfaced, in authored order.</summary>
        public static IEnumerable<AmberPieceData> FoundPieces(GameState state, GameDataAsset data)
        {
            if (!Configured(data))
            {
                yield break;
            }

            var count = System.Math.Min(state.deepAmberFound, data.deepAmber.pieces.Count);
            for (var i = 0; i < count; i++)
            {
                yield return data.deepAmber.pieces[i];
            }
        }

        /// <summary>The completed plate's effects — nothing until the last piece is up.</summary>
        public static IEnumerable<EffectData> CompletedEffects(GameState state, GameDataAsset data)
        {
            if (!IsComplete(state, data))
            {
                yield break;
            }

            foreach (var effect in data.deepAmber.effects)
            {
                yield return effect;
            }
        }

        /// <summary>
        /// One observation site's slice of the tick (called by
        /// <see cref="Observation.Advance"/> per site). Only the authored zone
        /// rolls, and a complete set draws no rng — sequences stay stable once
        /// the window has shut.
        /// </summary>
        public static void AdvanceSite(GameState state, GameDataAsset data, string zoneId,
            double watchers, double siteDigMult, double deltaSeconds)
        {
            if (!Configured(data) || data.deepAmber.zoneId != zoneId
                || deltaSeconds <= 0.0 || IsComplete(state, data))
            {
                return;
            }

            var hoursWatched = deltaSeconds / 3600.0;
            state.deepAmberPityHours += hoursWatched;

            var chance = watchers * data.deepAmber.findsPerHour * siteDigMult * hoursWatched;
            var found = Rng.NextDouble(ref state.rngState) < chance;
            if (!found && data.deepAmber.pityHoursWatched > 0.0
                && state.deepAmberPityHours >= data.deepAmber.pityHoursWatched)
            {
                found = true;
            }

            if (!found)
            {
                return;
            }

            state.deepAmberPityHours = 0.0;
            state.deepAmberFound++;
            state.deepAmberFoundUnlogged++;

            if (IsComplete(state, data))
            {
                // The plate's permanent effects go live the moment the last
                // piece is up, like an insect plate's final sketch.
                Upgrades.RecomputeYieldMultipliers(state, data);
            }
        }
    }
}
