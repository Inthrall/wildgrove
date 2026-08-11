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
    /// eligibility (Migration itself is the prestige build). An offering is
    /// WHOLE: a slot takes its entire ask in one act, from camp stock (or the
    /// quality pools, or recorded sketches), and nothing less — holding part
    /// of an ask is not progress, so no part-answer is banked for the verses
    /// behind it to inherit. Offerings credit Renown as they land: plain
    /// resources at their trade value, everything else via the slot's
    /// authored renownGrant (deeds grant once, on completion). Amber can
    /// never fill a slot — the gate is not for sale.
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

        /// <summary>
        /// What the slot still asks for. In play that is always its whole
        /// target — an offering is whole, so a slot is either untouched or
        /// answered — but a save written while offerings were incremental can
        /// hold a part-filled slot, and that one owes only the rest.
        /// </summary>
        public static double SlotRemaining(GameState state, RiteVerseData verse, int slotIndex)
        {
            var remaining = SlotTarget(verse.slots[slotIndex]) - SlotDelivered(state, verse, slotIndex);
            return remaining > 0.0 ? remaining : 0.0;
        }

        /// <summary>
        /// What the camp could hand into this slot right now: stock for a
        /// resource slot, whole specimens across the matching quality pool,
        /// loose pages from plates still being assembled. Zero for a deed slot
        /// — a deed is counted as the work is done, never handed over.
        /// </summary>
        public static double SlotHeld(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    return state.GetResource(slot.resource).ToDouble();
                case RiteSlotType.Specimen:
                    return PoolTotal(slot.quality == "choice" ? state.choiceResources : state.decentResources);
                case RiteSlotType.Sketch:
                    return LooseSketchCount(state, data);
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// What the journal shows against a slot's target — its "have / asked"
        /// pair. With a whole ask there is no partial delivery to report, so the
        /// honest number is what the camp is holding towards it (plus a deed's
        /// counted work, or whatever an older save part-filled). Clamped to the
        /// target: a full store reads "300 / 300", never "9000 / 300".
        /// </summary>
        public static double SlotInHand(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var target = SlotTarget(verse.slots[slotIndex]);
            var inHand = SlotDelivered(state, verse, slotIndex) + SlotHeld(state, data, verse, slotIndex);
            return inHand < target ? inHand : target;
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
        /// Set the whole ask down at once: consumes the slot's outstanding
        /// amount from camp stock, credits Renown as it lands (trade value per
        /// unit, or the authored grant for material slots), and returns the
        /// units delivered. Zero unless the camp holds the entire ask — the
        /// site takes a whole offering or none, so a store half the size of the
        /// ask is not half a verse — and zero when the verse isn't revealed or
        /// is already answered (any three finish it — the unchosen slots
        /// expire, §8, and must never keep eating stock), or the slot is
        /// complete or the wrong type.
        /// </summary>
        public static BigDouble DeliverResource(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Resource || !CanDeliver(state, data, verse, slotIndex))
            {
                return BigDouble.Zero;
            }

            var units = SlotRemaining(state, verse, slotIndex);
            var giving = new BigDouble(units);
            state.resources[slot.resource] = state.GetResource(slot.resource) - giving;
            SlotProgress(state, verse, slotIndex).delivered += units;

            // Renown at full trade value (no double-tax, design §7); material
            // slots (trade value zero) carry an authored grant instead. The
            // grant is still pro-rated against the ask, which is the whole
            // grant in play — it only bites for a save that part-filled a slot
            // before offerings became whole, so such a slot pays out once over
            // rather than twice.
            if (slot.renownGrant > 0)
            {
                state.renown += slot.renownGrant * (units / SlotTarget(slot));
            }
            else
            {
                state.renown += giving * Economy.TradeValuePerUnit(state, data, slot.resource);
            }

            SyncIfVerseJustSung(state, data, verse);
            return giving;
        }

        /// <summary>
        /// Offer the slot's whole count of specimens into a Decent/Choice
        /// specimen slot, each taken from the largest matching-quality pool.
        /// Returns the resource id of the last one given, or null when the camp
        /// can't answer the whole ask (verse unrevealed, slot complete, too few
        /// of that quality held).
        /// </summary>
        public static string DeliverSpecimen(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Specimen || !CanDeliver(state, data, verse, slotIndex))
            {
                return null;
            }

            var pool = slot.quality == "choice" ? state.choiceResources : state.decentResources;
            var progress = SlotProgress(state, verse, slotIndex);
            var wanted = WholeUnitsAsked(state, verse, slotIndex);
            string offered = null;
            for (var given = 0; given < wanted; given++)
            {
                var resourceId = LargestHolding(pool);
                if (resourceId == null)
                {
                    break;
                }

                pool[resourceId] -= BigDouble.One;
                progress.delivered += 1.0;
                offered = resourceId;
                if (slot.count > 0)
                {
                    state.renown += slot.renownGrant / (double)slot.count;
                }
            }

            SyncIfVerseJustSung(state, data, verse);
            return offered;
        }

        /// <summary>
        /// Offer the slot's whole count of field sketches — each torn from the
        /// richest insect plate still being recorded, so a completed plate is
        /// never broken up. A real sacrifice: the pages leave the record and
        /// those portions must be re-observed. Returns the insect id the last
        /// page came from, or null when the camp can't answer the whole ask.
        /// </summary>
        public static string DeliverSketch(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            if (slot.type != RiteSlotType.Sketch || !CanDeliver(state, data, verse, slotIndex))
            {
                return null;
            }

            var progress = SlotProgress(state, verse, slotIndex);
            var wanted = WholeUnitsAsked(state, verse, slotIndex);
            string offeredFrom = null;
            for (var given = 0; given < wanted; given++)
            {
                var richest = RichestUnrecordedPlate(state, data);
                if (richest == null)
                {
                    break;
                }

                state.insectSketches[richest] = Insects.SketchCount(state, richest) - 1;
                progress.delivered += 1.0;
                offeredFrom = richest;
                if (slot.count > 0)
                {
                    state.renown += slot.renownGrant / (double)slot.count;
                }
            }

            SyncIfVerseJustSung(state, data, verse);
            return offeredFrom;
        }

        /// <summary>
        /// True when offering into this slot right now would answer it: the
        /// verse is revealed and unanswered, the slot is open, and the camp
        /// holds the WHOLE outstanding ask — the HUD's button gate. Holding
        /// part of an ask buys nothing; the site takes a whole offering.
        /// </summary>
        public static bool CanDeliver(GameState state, GameDataAsset data, RiteVerseData verse, int slotIndex)
        {
            if (!IsVerseRevealed(state, data, verse) || IsVerseComplete(state, data, verse)
                || IsSlotComplete(state, verse, slotIndex))
            {
                return false;
            }

            // Deeds are earned at the nodes, never pressed by a button.
            if (verse.slots[slotIndex].type == RiteSlotType.Deed)
            {
                return false;
            }

            var asked = SlotRemaining(state, verse, slotIndex);
            return asked > 0.0 && SlotHeld(state, data, verse, slotIndex) >= asked;
        }

        /// <summary>
        /// Record a warden deed (e.g. "tend") and refresh every revealed
        /// verse's deed slots: their progress mirrors the run's deed count,
        /// and a slot that just reached its count credits its grant once.
        /// </summary>
        public static void RecordDeed(GameState state, GameDataAsset data, string deed)
        {
            // Baseline first, against the count BEFORE this deed lands. Run 1's
            // opening verse is revealed from the off, with no zone unlock or
            // restore to baseline it, so without this its first deed would set
            // the baseline and then go uncounted.
            BaselineDeedSlots(state, data);
            state.deedCounts.TryGetValue(deed, out var count);
            state.deedCounts[deed] = count + 1;

            // The deed about to be credited may be the slot that sings the
            // verse, and a sung verse opens the next trail. Settle after the
            // sync rather than instead of it — the ground can only be read off
            // progress the sync has already written — but only when a verse
            // actually landed: tending is a tap, and every other tap would
            // otherwise rebuild every node's multiplier for nothing.
            var sungBefore = SungVerseCount(state, data);
            SyncDeedSlots(state, data);
            if (SungVerseCount(state, data) > sungBefore)
            {
                Settle(state, data);
            }
        }

        /// <summary>
        /// Verses of the current rite that are answered, in-play or not — the
        /// cheap "did that change anything" probe. Deliberately blind to
        /// <see cref="IsVerseInPlay"/>, which reads the unlocked zones back and
        /// would make this the expensive thing it exists to avoid.
        /// </summary>
        private static int SungVerseCount(GameState state, GameDataAsset data)
        {
            var rite = CurrentRite(state, data);
            if (rite?.verses == null)
            {
                return 0;
            }

            var sung = 0;
            foreach (var verse in rite.verses)
            {
                if (IsVerseComplete(state, data, verse))
                {
                    sung++;
                }
            }

            return sung;
        }

        /// <summary>
        /// Bring every revealed verse's deed slots up to date with the work
        /// done SINCE that verse revealed. Called on each deed, and when a
        /// verse reveals (zone unlock, restore, the verse before it being
        /// sung) — a slot's baseline is taken on that first sync, so no verse
        /// inherits the deeds an earlier verse of the same fold was paid for.
        /// </summary>
        public static void SyncDeedSlots(GameState state, GameDataAsset data)
        {
            BaselineDeedSlots(state, data);
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

                    var progress = SlotProgress(state, verse, i);
                    state.deedCounts.TryGetValue(slot.deed ?? string.Empty, out var count);

                    // A verse can unseal inside this very loop — the grant below
                    // completes the one before it — and the baseline pass at the
                    // top ran while it was still sealed, so its slots have no
                    // line to measure from yet. Stamp it here and let it count
                    // from now. Reading an unset baseline as zero would hand the
                    // verse the whole run's tally at once: a 25-tend slot filled
                    // outright on a run that has tended forty times, paying its
                    // renown for work the earlier verses were already paid for.
                    if (!progress.deedBaselineSet)
                    {
                        progress.deedBaseline = count;
                        progress.deedBaselineSet = true;
                        continue;
                    }

                    var since = count - progress.deedBaseline;
                    if (since <= 0.0)
                    {
                        continue;
                    }

                    progress.delivered = System.Math.Min(since, slot.count);
                    if (!progress.granted && progress.delivered >= slot.count)
                    {
                        progress.granted = true;
                        state.renown += slot.renownGrant;
                    }
                }
            }
        }

        /// <summary>
        /// Stamp each revealed verse's deed slots with the run's deed count as
        /// it stands now — the line its own tally is measured from. Idempotent:
        /// a slot is baselined once, on the first sync after its verse reveals,
        /// and never re-stamped.
        /// </summary>
        private static void BaselineDeedSlots(GameState state, GameDataAsset data)
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

                    var progress = SlotProgress(state, verse, i);
                    if (progress.deedBaselineSet)
                    {
                        continue;
                    }

                    state.deedCounts.TryGetValue(slot.deed ?? string.Empty, out var count);
                    progress.deedBaseline = count;
                    progress.deedBaselineSet = true;
                }
            }
        }

        /// <summary>
        /// Bring the run into line with the ground it has earned: open the zones
        /// the sung verses and the owned trail maps unlock between them, fold
        /// the new nodes into the multipliers, and baseline the verse that newly
        /// revealed. Every path that can open ground ends here — an offering, a
        /// deed, a purchase, an Almanac grant, a restore — and it is idempotent,
        /// so calling it twice costs a walk and changes nothing.
        /// </summary>
        public static void Settle(GameState state, GameDataAsset data)
        {
            GameStateFactory.SyncUnlockedZones(state, data);
            Upgrades.RecomputeYieldMultipliers(state, data);
            SyncDeedSlots(state, data);
            RecordSungVerseZones(state, data);
        }

        /// <summary>
        /// Write every currently-answered verse's zone into the warden's
        /// lifetime record (design §4 ladder) — what the kith's earned places
        /// open off since 2026-08-11.
        /// <para>
        /// A sweep rather than an event, and deliberately: it hangs off
        /// <see cref="Settle"/>, which is already the one funnel every path
        /// that can sing a verse runs through (a delivered slot, a recorded
        /// deed, a zone unlock, a restore). Being idempotent is what lets it
        /// sit there — the extra calls cost a walk of the rite's verses and
        /// write nothing. The alternative, a hook at each completion site, is
        /// three call sites to keep in step and a fourth to forget.
        /// </para>
        /// <para>
        /// It records the answered verse's zone whether or not that zone is
        /// still in play: a verse sung is never unsung, and a warden who sang
        /// it does not owe it again because the trail was later re-gated.
        /// </para>
        /// </summary>
        private static void RecordSungVerseZones(GameState state, GameDataAsset data)
        {
            var rite = CurrentRite(state, data);
            if (rite?.verses == null)
            {
                return;
            }

            foreach (var verse in rite.verses)
            {
                if (verse.zone != null && IsVerseComplete(state, data, verse)
                    && !state.sungVerseZones.Contains(verse.zone))
                {
                    state.sungVerseZones.Add(verse.zone);
                }
            }
        }

        /// <summary>
        /// Singing a verse opens the next trail along (design §7 — the Rite is
        /// the way on) and unseals the verse behind it, so settle now: the same
        /// sync a zone unlock or a restore does. The verse that just opened takes
        /// its deed baseline in that sync and counts only the work done from
        /// here on — the deeds gathered while it was sealed belonged to the
        /// verses that were open at the time, and were paid for there.
        /// </summary>
        private static void SyncIfVerseJustSung(GameState state, GameDataAsset data, RiteVerseData verse)
        {
            if (IsVerseComplete(state, data, verse))
            {
                Settle(state, data);
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

        /// <summary>
        /// A count slot's outstanding ask as whole units — what the offering
        /// loops hand over. Rounded because the ask is a count, and a double
        /// subtraction of two whole numbers can land a hair under.
        /// </summary>
        private static int WholeUnitsAsked(GameState state, RiteVerseData verse, int slotIndex)
        {
            return (int)System.Math.Round(SlotRemaining(state, verse, slotIndex));
        }

        /// <summary>The unrecorded insect plate holding the most loose pages — where a torn sketch comes from.</summary>
        private static string RichestUnrecordedPlate(GameState state, GameDataAsset data)
        {
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

            return richest;
        }

        /// <summary>Pages the record can spare: those on plates still being assembled — a finished plate is never broken up.</summary>
        private static double LooseSketchCount(GameState state, GameDataAsset data)
        {
            var loose = 0.0;
            if (data?.insects != null)
            {
                foreach (var insect in data.insects)
                {
                    if (!Insects.IsRecorded(state, insect))
                    {
                        loose += Insects.SketchCount(state, insect.id);
                    }
                }
            }

            return loose;
        }

        /// <summary>Whole specimens a quality pool can offer, across every find in it.</summary>
        private static double PoolTotal(Dictionary<string, BigDouble> pool)
        {
            var total = 0.0;
            foreach (var pair in pool)
            {
                if (pair.Value >= BigDouble.One)
                {
                    total += System.Math.Floor(pair.Value.ToDouble());
                }
            }

            return total;
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
