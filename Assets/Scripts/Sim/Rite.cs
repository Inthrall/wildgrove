using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Rite runtime (design §7): each unlocked zone reveals its verse —
    /// five offering slots, any chooseCount of which complete it — and the
    /// Rite completes when every verse is sung, granting Migration
    /// eligibility (Migration itself is the prestige build). Offerings are
    /// delivered incrementally, consumed from camp stock (or the quality
    /// pools, or dug fragments), and credit Renown as they land: plain
    /// resources at their trade value, everything else via the slot's
    /// authored renownGrant (pro-rata for partial deliveries; deeds grant
    /// once, on completion). Amber can never fill a slot — the gate is not
    /// for sale.
    /// </summary>
    public static class Rite
    {
        /// <summary>
        /// The rite this run is walking: an authored rite matching the
        /// migration count when one exists (run 1 = migration 0), otherwise
        /// the generated rite for this migration (memoised on the state —
        /// generation is deterministic, so regenerating is safe but wasteful,
        /// and a per-state cache keeps the sim free of static mutable state).
        /// Data without generator tuning re-walks the authored first rite.
        /// Null when the data has no rites at all.
        /// </summary>
        public static RiteData CurrentRite(GameState state, GameDataAsset data)
        {
            var rites = data.rites?.rites;
            if (rites == null || rites.Count == 0)
            {
                return null;
            }

            foreach (var rite in rites)
            {
                if (rite.migration == state.migrationCount)
                {
                    return rite;
                }
            }

            if (!RiteGenerator.Configured(data.rites))
            {
                return rites[0];
            }

            if (state.generatedRite == null
                || state.generatedRiteMigration != state.migrationCount
                || !ReferenceEquals(state.generatedRiteFrom, data))
            {
                state.generatedRite = RiteGenerator.Generate(data, state.migrationCount);
                state.generatedRiteMigration = state.migrationCount;
                state.generatedRiteFrom = data;
            }

            return state.generatedRite;
        }

        /// <summary>A verse is revealed when the run has opened its zone (the warden has stood at the verse site).</summary>
        public static bool IsVerseRevealed(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            return Upgrades.UnlockedZoneIds(state, data).Contains(verse.zone);
        }

        /// <summary>The slot's completion target: units for resource slots, count for the rest.</summary>
        public static double SlotTarget(RiteSlotData slot)
        {
            return slot.type == RiteSlotType.Resource ? slot.amount : slot.count;
        }

        public static double SlotDelivered(GameState state, RiteVerseData verse, int slotIndex)
        {
            var progress = FindProgress(state, verse.id);
            return progress != null && slotIndex < progress.slots.Count
                ? progress.slots[slotIndex].delivered
                : 0.0;
        }

        public static bool IsSlotComplete(GameState state, RiteVerseData verse, int slotIndex)
        {
            return SlotDelivered(state, verse, slotIndex) >= SlotTarget(verse.slots[slotIndex]);
        }

        public static int CompletedSlotCount(GameState state, RiteVerseData verse)
        {
            var complete = 0;
            for (var i = 0; i < verse.slots.Count; i++)
            {
                if (IsSlotComplete(state, verse, i))
                {
                    complete++;
                }
            }

            return complete;
        }

        /// <summary>A verse completes when chooseCount of its slots are filled (design §7: choose 3 of 5).</summary>
        public static bool IsVerseComplete(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            return CompletedSlotCount(state, verse) >= data.rites.chooseCount;
        }

        /// <summary>
        /// The Rite completes — Migration eligibility — when every one of its
        /// verses is complete (the all-revealed-verses rule; the deepest
        /// verses force the trail to be walked to its end).
        /// </summary>
        public static bool IsRiteComplete(GameState state, GameDataAsset data)
        {
            var rite = CurrentRite(state, data);
            if (rite == null || rite.verses.Count == 0)
            {
                return false;
            }

            foreach (var verse in rite.verses)
            {
                if (!IsVerseComplete(state, data, verse))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Deliver camp stock into a resource slot: consumes up to the slot's
        /// remaining need, credits Renown as it lands (trade value per unit,
        /// or the authored grant pro-rata for material slots), and returns the
        /// units delivered. Zero when the verse isn't revealed, the slot is
        /// complete or the wrong type, or the camp holds none.
        /// </summary>
        public static BigDouble DeliverResource(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Resource || !IsVerseRevealed(state, data, verse))
            {
                return BigDouble.Zero;
            }

            var progress = SlotProgress(state, verse, slotIndex);
            var remaining = slot.amount - progress.delivered;
            var held = state.GetResource(slot.resource);
            var giving = BigDouble.Min(held, new BigDouble(remaining));
            if (giving <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            var units = giving.ToDouble();
            state.resources[slot.resource] = held - giving;
            progress.delivered += units;

            // Renown at full trade value (no double-tax, design §7); material
            // slots (trade value zero) carry an authored grant instead,
            // credited pro-rata so partial offerings aren't a renown dead-zone.
            if (slot.renownGrant > 0)
            {
                state.renown += slot.renownGrant * (units / slot.amount);
            }
            else
            {
                state.renown += giving * Economy.TradeValuePerUnit(state, data, slot.resource);
            }

            return giving;
        }

        /// <summary>
        /// Offer one specimen into a Fine/Pristine specimen slot, consumed
        /// from the largest matching-quality pool. Returns the resource id
        /// offered, or null when nothing qualifies (verse unrevealed, slot
        /// complete, pools empty).
        /// </summary>
        public static string DeliverSpecimen(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Specimen || !IsVerseRevealed(state, data, verse)
                || IsSlotComplete(state, verse, slotIndex))
            {
                return null;
            }

            var pool = slot.quality == "pristine" ? state.pristineResources : state.fineResources;
            var resourceId = LargestHolding(pool);
            if (resourceId == null)
            {
                return null;
            }

            pool[resourceId] -= BigDouble.One;
            var progress = SlotProgress(state, verse, slotIndex);
            progress.delivered += 1.0;
            if (slot.count > 0)
            {
                state.renown += slot.renownGrant / (double)slot.count;
            }

            return resourceId;
        }

        /// <summary>
        /// Offer one dug fossil fragment — taken from the richest fossil still
        /// assembling, so a completed fossil is never broken up. A real
        /// sacrifice: the fragment leaves the dig chase. Returns the fossil id
        /// it came from, or null when nothing can be offered.
        /// </summary>
        public static string DeliverFragment(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Fragment || !IsVerseRevealed(state, data, verse)
                || IsSlotComplete(state, verse, slotIndex))
            {
                return null;
            }

            string richest = null;
            var most = 0;
            if (data.fossils != null)
            {
                foreach (var fossil in data.fossils)
                {
                    var held = Fossils.FragmentCount(state, fossil.id);
                    if (held > most && !Fossils.IsComplete(state, fossil))
                    {
                        richest = fossil.id;
                        most = held;
                    }
                }
            }

            if (richest == null)
            {
                return null;
            }

            state.fossilFragments[richest] = most - 1;
            var progress = SlotProgress(state, verse, slotIndex);
            progress.delivered += 1.0;
            if (slot.count > 0)
            {
                state.renown += slot.renownGrant / (double)slot.count;
            }

            return richest;
        }

        /// <summary>
        /// Record a warden deed (e.g. "tend") and refresh every revealed
        /// verse's deed slots: their progress mirrors the run's deed count,
        /// and a slot that just reached its count credits its grant once.
        /// </summary>
        public static void RecordDeed(GameState state, GameDataAsset data, string deed)
        {
            state.deedCounts.TryGetValue(deed, out var count);
            state.deedCounts[deed] = count + 1;
            SyncDeedSlots(state, data);
        }

        /// <summary>
        /// Mirror the run's lifetime deed counts into every revealed verse's
        /// deed slots. Called on each deed, and when a verse reveals (zone
        /// unlock, restore) — deeds done before the reveal still count toward
        /// the slot rather than waiting for the next deed to sync them.
        /// </summary>
        public static void SyncDeedSlots(GameState state, GameDataAsset data)
        {
            var rite = CurrentRite(state, data);
            if (rite == null)
            {
                return;
            }

            foreach (var verse in rite.verses)
            {
                if (!IsVerseRevealed(state, data, verse))
                {
                    continue;
                }

                for (var i = 0; i < verse.slots.Count; i++)
                {
                    var slot = verse.slots[i];
                    if (slot.type != RiteSlotType.Deed)
                    {
                        continue;
                    }

                    state.deedCounts.TryGetValue(slot.deed ?? string.Empty, out var count);
                    if (count <= 0)
                    {
                        continue;
                    }

                    var progress = SlotProgress(state, verse, i);
                    progress.delivered = System.Math.Max(progress.delivered, System.Math.Min(count, slot.count));
                    if (!progress.granted && progress.delivered >= slot.count)
                    {
                        progress.granted = true;
                        state.renown += slot.renownGrant;
                    }
                }
            }
        }

        /// <summary>This verse's progress record, created (with a slot entry per data slot) on first touch.</summary>
        private static SlotProgressState SlotProgress(GameState state, RiteVerseData verse, int slotIndex)
        {
            var progress = FindProgress(state, verse.id);
            if (progress == null)
            {
                progress = new VerseProgressState { verseId = verse.id };
                state.verseProgress.Add(progress);
            }

            while (progress.slots.Count < verse.slots.Count)
            {
                progress.slots.Add(new SlotProgressState());
            }

            return progress.slots[slotIndex];
        }

        private static VerseProgressState FindProgress(GameState state, string verseId)
        {
            foreach (var progress in state.verseProgress)
            {
                if (progress.verseId == verseId)
                {
                    return progress;
                }
            }

            return null;
        }

        private static string LargestHolding(Dictionary<string, BigDouble> pool)
        {
            string largest = null;
            var most = BigDouble.Zero;
            foreach (var pair in pool)
            {
                if (pair.Value >= BigDouble.One && pair.Value > most)
                {
                    largest = pair.Key;
                    most = pair.Value;
                }
            }

            return largest;
        }
    }
}
