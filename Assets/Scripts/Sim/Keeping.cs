using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The keeping (design §15): the open tide's own offering verse — a page
    /// of whole-ask slots beside the Rite, never of it. Answered slots climb
    /// the tiers (kept the eve · the day · the wheel), each tier pays its
    /// little Amber once, and the first tier writes the (sabbat, year,
    /// hemisphere) claim that is the ledger a kept sabbat can never be kept
    /// twice against.
    /// <para>
    /// Containment is structural, not flagged: the keeping lives in
    /// <see cref="GameState.keeping"/> and its slots are its own, so nothing
    /// that walks the Rite (verse milestones, gift piles, zone openings,
    /// achievements, stats, Migration's banked count) can ever see it.
    /// Offerings still credit Renown at full trade value — the wheel never
    /// taxes prestige either — and everything here no-ops without wheel and
    /// observance data (fixtures), an unset hemisphere, or a closed tide.
    /// </para>
    /// </summary>
    public static class Keeping
    {
        public static bool Configured(GameDataAsset data)
        {
            var observance = data?.wheel?.observance;
            return Wheel.Configured(data) && observance != null
                && observance.slotCount >= 2
                && observance.tierSlots != null && observance.tierSlots.Count > 0;
        }

        /// <summary>
        /// The open tide's keeping — generated on first read, redrawn (open
        /// slots only) when a fold moved under it, and replaced outright the
        /// midnight the next sabbat takes the wheel. Null only where the
        /// calendar does not reach. The returned state is the persisted one:
        /// its slots are facts, so a reload can never reroll them.
        /// </summary>
        public static KeepingState Current(GameState state, GameDataAsset data)
        {
            if (state == null || !Configured(data))
            {
                return null;
            }

            var tide = Wheel.OpenTide(state, data);
            if (tide == null)
            {
                return null;
            }

            var year = YearOfEpochDay(Wheel.OpenNightDay(state, data));
            var keeping = state.keeping;
            if (keeping == null || keeping.sabbatId != tide.id
                || keeping.year != year || keeping.hemisphere != state.hemisphere)
            {
                keeping = RiteGenerator.GenerateKeeping(state, data, tide, year, state.hemisphere);
                state.keeping = keeping;
            }
            else if (keeping.generatedForMigration != state.migrationCount)
            {
                RiteGenerator.RedrawKeeping(state, data, tide, keeping);
            }

            return keeping;
        }

        /// <summary>
        /// True when the open season's keeping has had anything set down in it.
        /// The inside cover's lock on turning the reckoning hangs off this
        /// (design §15): the mirrored sabbat holds the very same weeks, so a
        /// flip after an offering is one span of the year claimed twice, while a
        /// flip before one costs and earns nothing.
        /// <para>
        /// It reads the PERSISTED keeping rather than asking
        /// <see cref="Current"/>, which generates one on first read — the same
        /// guard the events rail keeps. A settings row that drew the season's
        /// slots by being looked at would move every keeping's draw from "the
        /// first time the player opened it" to whenever the inside cover was
        /// opened, which is a balance change made by a piece of chrome.
        /// </para>
        /// </summary>
        public static bool Begun(GameState state, GameDataAsset data)
        {
            var tide = Wheel.OpenTide(state, data);
            var keeping = state?.keeping;
            if (tide == null || keeping == null
                || keeping.sabbatId != tide.id
                || keeping.hemisphere != state.hemisphere
                || keeping.year != YearOfEpochDay(Wheel.OpenNightDay(state, data)))
            {
                return false;
            }

            return CompletedSlotCount(keeping) > 0;
        }

        public static bool IsSlotComplete(KeepingSlotState slot)
        {
            return slot != null && slot.delivered >= slot.target;
        }

        public static int CompletedSlotCount(KeepingState keeping)
        {
            var done = 0;
            for (var i = 0; keeping?.slots != null && i < keeping.slots.Count; i++)
            {
                if (IsSlotComplete(keeping.slots[i]))
                {
                    done++;
                }
            }

            return done;
        }

        /// <summary>Tiers reached by the answered slots, 0..tierSlots.Count — the display number, independent of what has been paid.</summary>
        public static int TierReached(GameDataAsset data, KeepingState keeping)
        {
            var tiers = data?.wheel?.observance?.tierSlots;
            if (tiers == null || keeping == null)
            {
                return 0;
            }

            var done = CompletedSlotCount(keeping);
            var reached = 0;
            while (reached < tiers.Count && done >= tiers[reached])
            {
                reached++;
            }

            return reached;
        }

        /// <summary>
        /// True when offering into this slot right now would answer it: a tide
        /// open, the slot unanswered, and the camp holding the WHOLE
        /// outstanding ask — the keeping takes a whole offering or none, the
        /// Rite's own rule.
        /// </summary>
        public static bool CanOffer(GameState state, GameDataAsset data, int slotIndex)
        {
            var keeping = Current(state, data);
            if (keeping == null || slotIndex < 0 || slotIndex >= keeping.slots.Count)
            {
                return false;
            }

            var slot = keeping.slots[slotIndex];
            if (IsSlotComplete(slot))
            {
                return false;
            }

            if (slot.kind == KeepingSlotState.SpecimenKind)
            {
                return LargestHolding(state.decentResources) != null;
            }

            var remaining = slot.target - slot.delivered;
            return remaining > 0.0 && !string.IsNullOrEmpty(slot.goodsId)
                && state.GetResource(slot.goodsId) >= new BigDouble(remaining);
        }

        /// <summary>
        /// Set the whole ask down: consumes camp stock (or one Decent find for
        /// the specimen slot), credits Renown exactly as the Rite does, then
        /// settles the tiers — each newly-crossed tier pays its Amber once,
        /// and the first writes the claim. Returns true when anything landed.
        /// </summary>
        public static bool TryOffer(GameState state, GameDataAsset data, int slotIndex)
        {
            if (!CanOffer(state, data, slotIndex))
            {
                return false;
            }

            var keeping = state.keeping;
            var slot = keeping.slots[slotIndex];
            if (slot.kind == KeepingSlotState.SpecimenKind)
            {
                var resourceId = LargestHolding(state.decentResources);
                state.decentResources[resourceId] -= BigDouble.One;
                slot.delivered += 1.0;
                state.renown += slot.renownGrant;
            }
            else
            {
                var units = slot.target - slot.delivered;
                var giving = new BigDouble(units);
                state.resources[slot.goodsId] = state.GetResource(slot.goodsId) - giving;
                slot.delivered += units;

                // Renown at full trade value (design §8's no-double-tax rule);
                // material slots carry their authored grant instead.
                if (slot.renownGrant > 0L)
                {
                    state.renown += slot.renownGrant * (units / slot.target);
                }
                else
                {
                    state.renown += giving * Economy.TradeValuePerUnit(state, data, slot.goodsId);
                }
            }

            SettleTiers(state, data, keeping);
            return true;
        }

        /// <summary>True when this exact sabbat night has already been kept — the double-claim guard (design §15).</summary>
        public static bool IsKept(GameState state, string sabbatId, int year, int hemisphere)
        {
            for (var i = 0; state?.sabbatClaims != null && i < state.sabbatClaims.Count; i++)
            {
                var claim = state.sabbatClaims[i];
                if (claim.sabbatId == sabbatId && claim.year == year && claim.hemisphere == hemisphere)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The years a sabbat has been kept, oldest first — the Record's margin ticks.</summary>
        public static List<int> KeptYears(GameState state, string sabbatId)
        {
            var years = new List<int>();
            for (var i = 0; state?.sabbatClaims != null && i < state.sabbatClaims.Count; i++)
            {
                if (state.sabbatClaims[i].sabbatId == sabbatId)
                {
                    years.Add(state.sabbatClaims[i].year);
                }
            }

            years.Sort();
            return years;
        }

        /// <summary>The calendar year of a days-since-epoch date — pure date math, no wall clock.</summary>
        public static int YearOfEpochDay(int epochDay)
        {
            return new System.DateTime(1970, 1, 1).AddDays(epochDay).Year;
        }

        private static void SettleTiers(GameState state, GameDataAsset data, KeepingState keeping)
        {
            var observance = data.wheel.observance;
            var done = CompletedSlotCount(keeping);
            while (keeping.tierGranted < observance.tierSlots.Count
                   && done >= observance.tierSlots[keeping.tierGranted])
            {
                var grant = keeping.tierGranted < observance.tierAmber.Count
                    ? observance.tierAmber[keeping.tierGranted]
                    : 0.0;
                if (grant > 0.0)
                {
                    state.amber += grant;
                }

                keeping.tierGranted++;
                if (keeping.tierGranted == 1 && !IsKept(state, keeping.sabbatId, keeping.year, keeping.hemisphere))
                {
                    state.sabbatClaims.Add(new SabbatClaim
                    {
                        sabbatId = keeping.sabbatId,
                        year = keeping.year,
                        hemisphere = keeping.hemisphere,
                    });
                }
            }
        }

        private static string LargestHolding(Dictionary<string, BigDouble> pool)
        {
            string largest = null;
            var most = BigDouble.Zero;
            foreach (var pair in pool)
            {
                if (pair.Value >= BigDouble.One && pair.Value > most)
                {
                    most = pair.Value;
                    largest = pair.Key;
                }
            }

            return largest;
        }
    }
}
