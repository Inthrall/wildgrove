using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The kith's slot ladder (design §4): a slot is the right to hold a post.
    /// One slot from minute one; one more the FIRST time each
    /// economy.kith.slotVerseZones verse is sung; the last two are store
    /// purchases (GameState.purchasedKithSlots). The roster itself is the
    /// collection — at most one familiar per species, never capped by slots —
    /// and companions past the slots rest at camp.
    /// <para>
    /// Named verses rather than a lifetime tally since 2026-08-11. The tally
    /// (2 / 5 / 10 verses sung across every run) paid for folding early and
    /// often, which is the opposite of what the ladder is for: a place at the
    /// warden's side should mark having walked a trail to its end, not having
    /// walked the first stretch of one three times. It also put the second
    /// place in run 2 rather than run 1 (design §8's known consequence). The
    /// tally itself survives, in <see cref="TotalVersesSung"/> — the gift
    /// piles and the verse achievements still count that way, and should.
    /// </para>
    /// </summary>
    public static class Kith
    {
        // Hand-built test data often carries no economy — mirror the authored
        // economy.kith values so sim tests exercise the real ladder shape.
        private const int FallbackSlotsBase = 1;
        private const int FallbackSlotsMax = 6;
        private static readonly string[] FallbackSlotVerseZones =
            { "bramble-hedgerows", "mistfen-marsh", "cloudreach-peaks" };

        /// <summary>The ladder's hard ceiling: base + every milestone + both purchases.</summary>
        public static int SlotsMax(GameDataAsset data)
        {
            return data?.economy?.kith != null && data.economy.kith.slotsMax > 0
                ? data.economy.kith.slotsMax
                : FallbackSlotsMax;
        }

        /// <summary>The verses that open a place, in the order they are meant to land — authored, with the shipped three as the fixture fallback.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> SlotVerseZones(GameDataAsset data)
        {
            var kith = data?.economy?.kith;
            return kith?.slotVerseZones != null && kith.slotVerseZones.Count > 0
                ? (System.Collections.Generic.IReadOnlyList<string>)kith.slotVerseZones
                : FallbackSlotVerseZones;
        }

        /// <summary>Lifetime verses sung: every verse completed in folded runs plus the current run's. The gift piles and the verse achievements read this; the slot ladder no longer does.</summary>
        public static int TotalVersesSung(GameState state, GameDataAsset data)
        {
            if (state == null)
            {
                return 0;
            }

            return state.foldedVersesSung + Rite.CompletedVerseCount(state, data);
        }

        /// <summary>Active slots right now: base + the named verses sung + purchased, never above slotsMax.</summary>
        public static int Slots(GameState state, GameDataAsset data)
        {
            var kith = data?.economy?.kith;
            var slotsBase = kith != null && kith.slotsBase > 0 ? kith.slotsBase : FallbackSlotsBase;
            var slotsMax = SlotsMax(data);

            var slots = slotsBase + EarnedSlots(state, data);
            if (state != null)
            {
                slots += state.purchasedKithSlots;
            }

            return slots < slotsMax ? slots : slotsMax;
        }

        /// <summary>
        /// The earned rungs standing: one per named verse this warden has ever
        /// sung, floored by <see cref="GameState.grandfatheredKithSlots"/> so
        /// that a save written under the old lifetime-tally ladder can never
        /// come back with fewer places than it went away with (save rung 52).
        /// The floor is overtaken and forgotten as the named verses land.
        /// </summary>
        private static int EarnedSlots(GameState state, GameDataAsset data)
        {
            if (state == null)
            {
                return 0;
            }

            var earned = 0;
            foreach (var zoneId in SlotVerseZones(data))
            {
                if (state.sungVerseZones.Contains(zoneId))
                {
                    earned++;
                }
            }

            return earned > state.grandfatheredKithSlots ? earned : state.grandfatheredKithSlots;
        }

        /// <summary>
        /// The zone whose verse opens the next place, or null when every earned
        /// place is already open. The Warden page names it — "a place opens
        /// when the marsh's verse is sung" tells a player where to walk, which
        /// a bare count never did.
        /// </summary>
        public static string NextSlotVerseZone(GameState state, GameDataAsset data)
        {
            if (state == null)
            {
                return null;
            }

            foreach (var zoneId in SlotVerseZones(data))
            {
                if (!state.sungVerseZones.Contains(zoneId))
                {
                    return zoneId;
                }
            }

            return null;
        }

        /// <summary>How many companions the warden keeps altogether — the collection, unbounded by slots.</summary>
        public static int Count(GameState state)
        {
            return state?.roster != null ? state.roster.Count : 0;
        }

        /// <summary>
        /// How many familiars currently hold a post (and so a slot). The fell
        /// pony is not counted: its lane costs no slot (§11), and because it can
        /// stand nowhere else the exemption cannot follow it to a node — so the
        /// ladder stays honest without any check at stationing time.
        /// </summary>
        public static int Walking(GameState state)
        {
            if (state?.roster == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && !familiar.IsPony)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// How many companions wait at camp with no post at all — who a newly
        /// opened place is actually for. The pony is never one of them: she is
        /// always at her own lane (§11), so <see cref="Count"/> minus
        /// <see cref="Walking"/> counted her as idle while
        /// <see cref="Walking"/> was already excusing her, and every reading
        /// of "resting" was one too many from the moment the Halter landed.
        /// </summary>
        public static int Resting(GameState state)
        {
            if (state?.roster == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting)
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
