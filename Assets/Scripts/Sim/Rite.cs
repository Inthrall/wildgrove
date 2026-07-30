using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Rite runtime (design §7): each unlocked zone reveals its verse —
    /// five offering slots, any chooseCount of which complete it — verses
    /// are sung strictly in order (each sealed until the one before it is
    /// answered), and the Rite completes when every verse is sung, granting Migration
    /// eligibility (Migration itself is the prestige build). Offerings are
    /// delivered incrementally, consumed from camp stock (or the quality
    /// pools, or recorded sketches), and credit Renown as they land: plain
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

        /// <summary>
        /// A verse is revealed when the run has opened its zone (the warden
        /// has stood at the verse site) AND every verse before it is sung —
        /// the Rite is walked in order, each verse locked behind the last.
        /// </summary>
        public static bool IsVerseRevealed(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            return Upgrades.UnlockedZoneIds(state, data).Contains(verse.zone)
                && !IsVerseSealed(state, data, verse);
        }

        /// <summary>
        /// Whether this verse belongs to THIS run at all (design §8's fold
        /// gate): its zone's gate is met, or the zone is already open.
        ///
        /// This is load-bearing, not a nicety. A Rite completes only when every
        /// one of its verses is sung, and a verse can only be sung once its zone
        /// is open — so a verse for a zone this fold cannot reach would seal the
        /// Rite, and Migration with it, permanently. The whole fold-gating idea
        /// therefore rests on the deep verses standing aside until their trail
        /// exists. They rejoin the Rite on the fold their zone opens, which is
        /// the point: the run gets longer as the warden gets stronger.
        ///
        /// The "or already open" half covers the save whose data was retuned
        /// underneath it. Opening a zone is never taken back (see
        /// <see cref="Upgrades.UnlockedZoneIds"/>), so a run holding a map for a
        /// now-gated trail keeps its verse rather than being handed a zone it
        /// can work and a verse that does not count.
        /// </summary>
        public static bool IsVerseInPlay(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            if (verse == null)
            {
                return false;
            }

            if (data != null && data.ZonesById.TryGetValue(verse.zone, out var zone)
                && zone.minMigration > (state != null ? state.migrationCount : 0))
            {
                return Upgrades.UnlockedZoneIds(state, data).Contains(verse.zone);
            }

            return true;
        }

        /// <summary>
        /// The current Rite's verses that belong to this run, in Rite order —
        /// what the journal numbers and counts. Empty when there is no rite.
        /// </summary>
        public static List<RiteVerseData> VersesInPlay(GameState state, GameDataAsset data)
        {
            var verses = new List<RiteVerseData>();
            var rite = CurrentRite(state, data);
            if (rite?.verses == null)
            {
                return verses;
            }

            foreach (var verse in rite.verses)
            {
                if (IsVerseInPlay(state, data, verse))
                {
                    verses.Add(verse);
                }
            }

            return verses;
        }

        /// <summary>
        /// Sealed = an earlier verse of the current rite is still unsung, so
        /// this one hasn't had its turn — regardless of whether its site has
        /// been reached. Verses not in the current rite are never sealed.
        /// </summary>
        public static bool IsVerseSealed(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            var rite = CurrentRite(state, data);
            if (rite == null)
            {
                return false;
            }

            foreach (var earlier in rite.verses)
            {
                if (ReferenceEquals(earlier, verse) || earlier.id == verse.id)
                {
                    return false;
                }

                // A verse this fold cannot reach never had its turn, so it
                // cannot be holding up the ones behind it.
                if (!IsVerseInPlay(state, data, earlier))
                {
                    continue;
                }

                if (!IsVerseComplete(state, data, earlier))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// How many of THIS verse's slots must be filled: the migration-ramped
        /// chooseCount (design §8's breadth lever — see
        /// <see cref="RiteGenerator.ScaledChooseCount"/>), clamped so the verse
        /// always keeps at least one slot of choice.
        ///
        /// The clamp is the guarantee, not a tidy-up. The ramp is authored
        /// against verses the generator widened in step, but a lean zone can
        /// leave it too few candidates to widen with; without the clamp such a
        /// verse would quietly become "fill every slot", and a verse asking for
        /// a slot it doesn't have would seal the Rite — and with it Migration —
        /// forever.
        /// </summary>
        public static int RequiredSlots(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            var required = RiteGenerator.ScaledChooseCount(data.rites, state != null ? state.migrationCount : 0);
            if (verse != null && verse.slots.Count > 0)
            {
                required = System.Math.Min(required, verse.slots.Count - 1);
            }

            return System.Math.Max(1, required);
        }

        /// <summary>A verse completes when <see cref="RequiredSlots"/> of its slots are filled (design §7: choose 3 of 5, widening with the fold).</summary>
        public static bool IsVerseComplete(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            return CompletedSlotCount(state, verse) >= RequiredSlots(state, data, verse);
        }

        /// <summary>
        /// Verses sung this run — the current rite's completed verses. The
        /// lifetime total (the kith ladder's currency, §4) adds
        /// GameState.foldedVersesSung on top; see Kith.TotalVersesSung.
        /// </summary>
        public static int CompletedVerseCount(GameState state, GameDataAsset data)
        {
            var rite = CurrentRite(state, data);
            if (rite == null)
            {
                return 0;
            }

            var complete = 0;
            foreach (var verse in rite.verses)
            {
                if (IsVerseInPlay(state, data, verse) && IsVerseComplete(state, data, verse))
                {
                    complete++;
                }
            }

            return complete;
        }

        /// <summary>
        /// The Rite completes — Migration eligibility — when every verse in play
        /// this run is complete (the all-revealed-verses rule; the deepest
        /// verses force the trail to be walked to its end). Verses whose trail
        /// this fold cannot reach stand aside — see
        /// <see cref="IsVerseInPlay"/>. A rite with nothing in play never
        /// completes, which is why the validator refuses one.
        /// </summary>
        public static bool IsRiteComplete(GameState state, GameDataAsset data)
        {
            var rite = CurrentRite(state, data);
            if (rite == null || rite.verses.Count == 0)
            {
                return false;
            }

            var inPlay = 0;
            foreach (var verse in rite.verses)
            {
                if (!IsVerseInPlay(state, data, verse))
                {
                    continue;
                }

                inPlay++;
                if (!IsVerseComplete(state, data, verse))
                {
                    return false;
                }
            }

            return inPlay > 0;
        }

        /// <summary>
        /// Deliver camp stock into a resource slot: consumes up to the slot's
        /// remaining need, credits Renown as it lands (trade value per unit,
        /// or the authored grant pro-rata for material slots), and returns the
        /// units delivered. Zero when the verse isn't revealed or already
        /// answered (any three finish it — the unchosen slots expire, §8, and
        /// must never keep eating stock), the slot is complete or the wrong
        /// type, or the camp holds none.
        /// </summary>
        public static BigDouble DeliverResource(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Resource || !IsVerseRevealed(state, data, verse)
                || IsVerseComplete(state, data, verse))
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

            SyncIfVerseJustSung(state, data, verse);
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
                || IsSlotComplete(state, verse, slotIndex) || IsVerseComplete(state, data, verse))
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

            SyncIfVerseJustSung(state, data, verse);
            return resourceId;
        }

        /// <summary>
        /// Offer one field sketch — torn from the richest insect plate still
        /// being recorded, so a completed plate is never broken up. A real
        /// sacrifice: the page leaves the record and that portion must be
        /// re-observed. Returns the insect id it came from, or null when
        /// nothing can be offered.
        /// </summary>
        public static string DeliverSketch(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Sketch || !IsVerseRevealed(state, data, verse)
                || IsSlotComplete(state, verse, slotIndex) || IsVerseComplete(state, data, verse))
            {
                return null;
            }

            string richest = null;
            var most = 0;
            if (data.insects != null)
            {
                foreach (var insect in data.insects)
                {
                    var held = Insects.SketchCount(state, insect.id);
                    if (held > most && !Insects.IsRecorded(state, insect))
                    {
                        richest = insect.id;
                        most = held;
                    }
                }
            }

            if (richest == null)
            {
                return null;
            }

            state.insectSketches[richest] = most - 1;
            var progress = SlotProgress(state, verse, slotIndex);
            progress.delivered += 1.0;
            if (slot.count > 0)
            {
                state.renown += slot.renownGrant / (double)slot.count;
            }

            SyncIfVerseJustSung(state, data, verse);
            return richest;
        }

        /// <summary>
        /// True when offering into this slot right now could land something:
        /// the verse is revealed and unanswered, the slot is open, and the
        /// camp holds whatever the slot asks for — the HUD's button gate.
        /// </summary>
        public static bool CanDeliver(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            if (!IsVerseRevealed(state, data, verse) || IsVerseComplete(state, data, verse)
                || IsSlotComplete(state, verse, slotIndex))
            {
                return false;
            }

            var slot = verse.slots[slotIndex];
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    return state.GetResource(slot.resource) > BigDouble.Zero;
                case RiteSlotType.Specimen:
                    return LargestHolding(slot.quality == "pristine" ? state.pristineResources : state.fineResources) != null;
                case RiteSlotType.Sketch:
                    if (data.insects != null)
                    {
                        foreach (var insect in data.insects)
                        {
                            if (Insects.SketchCount(state, insect.id) > 0 && !Insects.IsRecorded(state, insect))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                default:
                    // Deeds are earned at the nodes, never pressed by a button.
                    return false;
            }
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

                    // An unanswered deed slot in an already-answered verse has
                    // expired (§8) — its progress freezes and its grant is
                    // forfeit, like every other unchosen slot.
                    if (IsVerseComplete(state, data, verse) && !IsSlotComplete(state, verse, i))
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

        /// <summary>
        /// Completing a verse unseals the next one — mirror lifetime deeds
        /// into its slots now, the same sync a zone unlock or restore does,
        /// so deeds done while it was sealed count the moment it opens.
        /// </summary>
        private static void SyncIfVerseJustSung(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            if (IsVerseComplete(state, data, verse))
            {
                SyncDeedSlots(state, data);
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
