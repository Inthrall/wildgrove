using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Keepsake pages (design §9's sink slate): Amber is resin, and a piece
    /// can be set into the journal as a mounted page that remembers the run —
    /// the region and its season, what the camp was called, the verses sung by
    /// the time it was set. One per run, bought for Amber, and permanent: the
    /// keepsakes list crosses Migration with the rest of the journal, the same
    /// idiom as recorded plates. Commemorative only — a keepsake grants no
    /// multiplier and no Renown, because a keepsake that paid score would be
    /// selling the score.
    /// </summary>
    public static class Keepsakes
    {
        /// <summary>The Amber a keepsake page asks, or 0 when the sink is unconfigured — the page hides then; nothing a player relies on is lost by its absence.</summary>
        public static double Cost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.keepsakePageCostAmber > 0.0 ? amber.keepsakePageCostAmber : 0.0;
        }

        /// <summary>Whether this run's keepsake is already set — one page per run; the next region's is the next run's to mount.</summary>
        public static bool MountedThisRun(GameState state)
        {
            if (state?.keepsakes == null)
            {
                return false;
            }

            foreach (var keepsake in state.keepsakes)
            {
                if (keepsake.migrationCount == state.migrationCount)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a keepsake can be set right now — configured, not yet mounted this run, and affordable.</summary>
        public static bool CanMount(GameState state, GameDataAsset data)
        {
            var cost = Cost(data);
            return cost > 0.0
                && state != null
                && !MountedThisRun(state)
                && state.amber >= cost;
        }

        /// <summary>
        /// Set a piece of amber into the journal: spend the price and mount
        /// this run's page — the run number, the region it wore, what the camp
        /// was called (null while unnamed), and the verses sung so far. The
        /// facts are snapshotted at mounting; the page's line in the land's
        /// voice is the renderer's, from these. Returns the mounted keepsake,
        /// or null when refused.
        /// </summary>
        public static KeepsakeState TryMount(GameState state, GameDataAsset data, long nowUnixMs)
        {
            if (!CanMount(state, data))
            {
                return null;
            }

            var keepsake = new KeepsakeState
            {
                migrationCount = state.migrationCount,
                regionId = Regions.Current(state, data)?.id,
                campName = state.campName,
                versesSung = VersesThisRun(state, data),
                setAtUnixMs = nowUnixMs,
            };

            state.keepsakes.Add(keepsake);
            state.amber -= Cost(data);
            return keepsake;
        }

        /// <summary>The pages set so far, oldest first — the journal's keepsake shelf reads this.</summary>
        public static List<KeepsakeState> All(GameState state)
        {
            return state?.keepsakes ?? new List<KeepsakeState>();
        }

        /// <summary>Verses answered by THIS run — lifetime count less what folded runs banked.</summary>
        private static int VersesThisRun(GameState state, GameDataAsset data)
        {
            var thisRun = Kith.TotalVersesSung(state, data) - state.foldedVersesSung;
            return thisRun > 0 ? thisRun : 0;
        }
    }
}
