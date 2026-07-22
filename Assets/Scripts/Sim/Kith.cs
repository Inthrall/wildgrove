using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The kith's slot ladder (design §4): a slot is the right to hold a post.
    /// One slot from minute one; one more as lifetime verses sung cross each
    /// economy.kith.verseMilestones entry; the last two are store purchases
    /// (GameState.purchasedKithSlots). The roster itself is the collection —
    /// at most one familiar per species, never capped by slots — and
    /// companions past the slots rest at camp.
    /// </summary>
    public static class Kith
    {
        // Hand-built test data often carries no economy — mirror the authored
        // economy.kith values so sim tests exercise the real ladder shape.
        private const int FallbackSlotsBase = 1;
        private const int FallbackSlotsMax = 6;
        private static readonly int[] FallbackVerseMilestones = { 2, 5, 10 };

        /// <summary>The ladder's hard ceiling: base + every milestone + both purchases.</summary>
        public static int SlotsMax(GameDataAsset data)
        {
            return data?.economy?.kith != null && data.economy.kith.slotsMax > 0
                ? data.economy.kith.slotsMax
                : FallbackSlotsMax;
        }

        /// <summary>Lifetime verses sung (§4 ladder): every verse completed in folded runs plus the current run's.</summary>
        public static int TotalVersesSung(GameState state, GameDataAsset data)
        {
            if (state == null)
            {
                return 0;
            }

            return state.foldedVersesSung + Rite.CompletedVerseCount(state, data);
        }

        /// <summary>Active slots right now: base + verse milestones passed + purchased, never above slotsMax.</summary>
        public static int Slots(GameState state, GameDataAsset data)
        {
            var kith = data?.economy?.kith;
            var slotsBase = kith != null && kith.slotsBase > 0 ? kith.slotsBase : FallbackSlotsBase;
            var slotsMax = SlotsMax(data);

            var slots = slotsBase;

            var versesSung = TotalVersesSung(state, data);
            var milestones = kith != null && kith.verseMilestones != null && kith.verseMilestones.Count > 0
                ? (System.Collections.Generic.IReadOnlyList<int>)kith.verseMilestones
                : FallbackVerseMilestones;
            foreach (var milestone in milestones)
            {
                if (versesSung >= milestone)
                {
                    slots++;
                }
            }

            if (state != null)
            {
                slots += state.purchasedKithSlots;
            }

            return slots < slotsMax ? slots : slotsMax;
        }

        /// <summary>The next verse-milestone still ahead, or 0 when every earned slot is open.</summary>
        public static int NextVerseMilestone(GameState state, GameDataAsset data)
        {
            var kith = data?.economy?.kith;
            var milestones = kith != null && kith.verseMilestones != null && kith.verseMilestones.Count > 0
                ? (System.Collections.Generic.IReadOnlyList<int>)kith.verseMilestones
                : FallbackVerseMilestones;

            var versesSung = TotalVersesSung(state, data);
            foreach (var milestone in milestones)
            {
                if (versesSung < milestone)
                {
                    return milestone;
                }
            }

            return 0;
        }

        /// <summary>How many companions the warden keeps altogether — the collection, unbounded by slots.</summary>
        public static int Count(GameState state)
        {
            return state?.roster != null ? state.roster.Count : 0;
        }

        /// <summary>How many familiars currently hold a post (and so a slot).</summary>
        public static int Walking(GameState state)
        {
            if (state?.roster == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True when another familiar can take a post — stationing and stationed arrivals both ask first.</summary>
        public static bool HasRoom(GameState state, GameDataAsset data)
        {
            return Walking(state) < Slots(state, data);
        }
    }
}
