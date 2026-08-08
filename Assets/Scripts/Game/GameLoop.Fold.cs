using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    // The Rite and the fold (design §5, §7) — the long game: offerings into a
    // verse, gear worn, Almanac lines learned, bonds earned, a tincture drunk,
    // and the Migration all of it consents to. See GameLoop.cs for the run.
    public sealed partial class GameLoop
    {
        /// <summary>True when offering into this slot could land something now — the verse is open and the camp holds what it asks (the HUD's button gate).</summary>
        public bool CanOffer(RiteVerseData verse, int slotIndex)
        {
            return Rite.CanDeliver(State, Data, verse, slotIndex);
        }

        /// <summary>Offer camp stock into a resource slot of the Rite (design §7). Returns the units delivered.</summary>
        public BigDouble OfferResource(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var given = Rite.DeliverResource(State, Data, verse, slotIndex);
            if (given > BigDouble.Zero)
            {
                Telemetry.LogEvent("offering_made",
                    ("verse", verse.id), ("slot", slotIndex), ("amount", given.ToDouble()));
                AfterOffering(verse, wasComplete);
            }

            return given;
        }

        /// <summary>Offer one Decent/Choice specimen into a specimen slot. Returns true when one was given.</summary>
        public bool OfferSpecimen(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var resourceId = Rite.DeliverSpecimen(State, Data, verse, slotIndex);
            if (resourceId == null)
            {
                return false;
            }

            Telemetry.LogEvent("offering_made",
                ("verse", verse.id), ("slot", slotIndex), ("specimen", resourceId));
            AfterOffering(verse, wasComplete);
            return true;
        }

        /// <summary>Offer one field sketch into a sketch slot — the page is torn out for the spirits, so that portion must be re-observed (design §6). Returns true when one was given.</summary>
        public bool OfferSketch(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var insectId = Rite.DeliverSketch(State, Data, verse, slotIndex);
            if (insectId == null)
            {
                return false;
            }

            Telemetry.LogEvent("offering_made",
                ("verse", verse.id), ("slot", slotIndex), ("sketch_from", insectId));
            AfterOffering(verse, wasComplete);
            return true;
        }

        /// <summary>Fix one Choice specimen into the Folio (design §6 — permanence over the windfall). Returns false when no spread wants it or none is held.</summary>
        public bool FixSpecimen(string resourceId)
        {
            var bondsBefore = EarnedBondIds();
            if (!Folio.TryFix(State, Data, resourceId))
            {
                return false;
            }

            Telemetry.LogEvent("specimen_fixed", ("resource", resourceId));
            Stats.RecordSpecimenFixed(resourceId);
            ReportNewBonds(bondsBefore);
            // A completed Gallery opens kith slot 6 — a bond that was waiting
            // for room steps in now (SyncBonded is idempotent).
            Roster.SyncBonded(State, Data);
            return true;
        }

        /// <summary>Craft and wear a piece of the kit (design §4) — spends its materials, fills its slot, and keeps the displaced piece in the bag. Returns false when it can't be made.</summary>
        public bool CraftGear(GearData gear)
        {
            if (!Gear.TryCraft(State, Data, gear))
            {
                return false;
            }

            Telemetry.LogEvent("gear_crafted", ("gear", gear.id), ("slot", gear.slot));
            return true;
        }

        /// <summary>
        /// Wear a piece already in the kit bag (design §4) — a free swap, since
        /// its materials were spent when it was made. Returns false when the
        /// piece hasn't been made, or is already on the warden.
        /// </summary>
        public bool WearGear(GearData gear)
        {
            if (!Gear.TryWear(State, Data, gear))
            {
                return false;
            }

            // Distinct from gear_crafted: the swap rate is what says whether a
            // slot's contenders are a live decision or a settled one.
            Telemetry.LogEvent("gear_worn", ("gear", gear.id), ("slot", gear.slot));
            return true;
        }

        /// <summary>Verdure not yet allocated to an Almanac node — for the section header and buy buttons.</summary>
        public double AvailableVerdure()
        {
            return Almanac.AvailableVerdure(State, Data);
        }

        /// <summary>Buy an Almanac node with unallocated Verdure (permanent — survives Migration). Returns false when it can't be bought.</summary>
        public bool BuyAlmanacNode(AlmanacNodeData node)
        {
            var bondsBefore = EarnedBondIds();
            // An endless line's price climbs with the level it's about to take,
            // so the cost has to be read before the purchase moves it.
            var cost = Almanac.NextCost(State, Data, node);
            if (!Almanac.TryBuy(State, Data, node))
            {
                return false;
            }

            Telemetry.LogEvent("almanac_node_bought", ("node", node.id), ("verdure_cost", cost),
                ("level", Almanac.Levels(State, node.id)));
            ReportNewBonds(bondsBefore);
            // The Old Friend opens kith slot 5 — a bond that was waiting for
            // room steps in now (SyncBonded is idempotent).
            Roster.SyncBonded(State, Data);
            return true;
        }

        private HashSet<string> EarnedBondIds()
        {
            var earned = new HashSet<string>();
            foreach (var bond in Bonds.Earned(State, Data))
            {
                earned.Add(bond.id);
            }

            return earned;
        }

        /// <summary>
        /// The most recently earned bond awaiting its HUD celebration — a
        /// companion is rare enough to deserve a moment. Null when none is pending.
        /// </summary>
        public BondData PendingBondCelebration => _announce.PendingBondCelebration;

        /// <summary>Claim the pending bond celebration (clears it), or null.</summary>
        public BondData TakePendingBondCelebration()
        {
            return _announce.TakeBondCelebration();
        }

        /// <summary>
        /// A bond is earned the moment its source completes. Its companion is
        /// materialised into the roster by Folio/Almanac restore paths; here we
        /// just surface the celebration and telemetry for the newly earned ones.
        /// </summary>
        private void ReportNewBonds(HashSet<string> bondsBefore)
        {
            foreach (var bond in Bonds.Earned(State, Data))
            {
                if (!bondsBefore.Contains(bond.id))
                {
                    Telemetry.LogEvent("familiar_bonded", ("bond", bond.id));
                    _announce.CelebrateBond(bond);
                    // Materialise the companion now so it's present immediately.
                    Roster.SyncBonded(State, Data);
                    // A bonded companion has its own celebration and a canonical
                    // name — it must never queue for the naming sheet as well.
                    _announce.MarkArrivalsSeen(State.roster);
                }
            }
        }

        /// <summary>True when the Rite has consented — the Migrate button's visibility.</summary>
        public bool CanMigrate()
        {
            return Migration.CanMigrate(State, Data);
        }

        /// <summary>The Verdure total a Migration right now would bank — for the confirm sheet.</summary>
        public double VerdureAfterMigration()
        {
            return Migration.VerdureAfterMigration(State, Data);
        }

        /// <summary>The lifetime Renown the next whole Verdure point asks for — so the fold can show its own curve.</summary>
        public double RenownForNextVerdure()
        {
            return Migration.RenownForNextVerdure(State, Data);
        }

        /// <summary>The sabbat whose tide is open right now (design §15) — null through the fallow weeks, or while the Wheel is inert.</summary>
        public SabbatData OpenTide()
        {
            return Wheel.OpenTide(State, Data);
        }

        /// <summary>
        /// The sabbat night at or ahead of now — the fold forecast's line since
        /// the drawn season retired (design §8): the Wheel turns whether or not
        /// the camp folds. Null while the Wheel is inert or the calendar has
        /// run out.
        /// </summary>
        public SabbatData NextSabbat(out long nightStartUnixMs)
        {
            return Wheel.NextSabbat(State, Data, out nightStartUnixMs);
        }

        /// <summary>
        /// Roster familiars whose Kinship gain at a fold right now would cross
        /// a signature milestone (design §4) — the fold sheet names them, so
        /// the creature's memory argues FOR leaving, in its own voice.
        /// </summary>
        public List<Familiar> FoldSharpenings()
        {
            var sharpening = new List<Familiar>();
            foreach (var familiar in State.roster)
            {
                if (Kinship.MilestonesPassedAt(Kinship.LevelAfterFold(familiar, Data), Data)
                    > Kinship.SignatureMilestonesPassed(familiar, Data))
                {
                    sharpening.Add(familiar);
                }
            }

            return sharpening;
        }

        /// <summary>The plate inscription lines a familiar has earned (design §7) — one per signature milestone passed.</summary>
        public List<string> FamiliarInscriptions(Familiar familiar)
        {
            return Kinship.InscriptionsEarned(familiar, Data);
        }

        /// <summary>A bottle of this tincture is in stock (design §5, Apothecary).</summary>
        public bool CanDrinkTincture(TinctureData tincture)
        {
            return Tinctures.CanDrink(State, tincture);
        }

        /// <summary>Seconds this tincture's buff has left, 0 when not live.</summary>
        public double TinctureRemainingSeconds(TinctureData tincture)
        {
            return tincture == null ? 0.0 : Tinctures.RemainingSeconds(State, tincture.id);
        }

        /// <summary>Drink one bottle: spends a unit of stock; a second bottle adds its duration on top — the clock stacks, the effect never does.</summary>
        public bool DrinkTincture(TinctureData tincture)
        {
            if (!Tinctures.TryDrink(State, Data, tincture))
            {
                return false;
            }

            Telemetry.LogEvent("tincture_drunk", ("tincture", tincture.id));
            return true;
        }

        /// <summary>How far lifetime Renown has climbed towards the next Verdure point, 0..1 — the fold banner's percentage.</summary>
        public double ProgressToNextVerdure()
        {
            return Migration.ProgressToNextVerdure(State, Data);
        }

        /// <summary>
        /// Fold the camp (design §7): swap in the next run's state, keeping the
        /// permanents (and the kith, with run XP banked into Kinship), and save
        /// at once so the old run can't be resumed by force-closing. Returns
        /// false when the Rite hasn't consented.
        /// </summary>
        public bool Migrate()
        {
            var next = Migration.Migrate(State, Data);
            if (next == null)
            {
                return false;
            }

            Telemetry.LogEvent("migration_completed",
                ("number", next.migrationCount),
                ("verdure", next.verdurePoints),
                ("renown", State.renown.ToDouble()));
            Stats.RecordMigration(next.migrationCount);
            State = next;
            // The carried kith has already been met — don't re-prompt naming.
            _announce.MarkArrivalsSeen(State.roster);
            // The ladder crosses the fold intact (slots ride lifetime verses),
            // so re-seed the mark rather than announce it as newly won.
            _announce.MarkKithSlotsSeen();
            SaveNow();
            return true;
        }

        private void AfterOffering(RiteVerseData verse, bool verseWasComplete)
        {
            if (!verseWasComplete && Rite.IsVerseComplete(State, Data, verse))
            {
                Telemetry.LogEvent("verse_completed", ("verse", verse.id));
                Stats.RecordVerseCompleted(verse.id);
                if (Rite.IsRiteComplete(State, Data))
                {
                    Telemetry.LogEvent("rite_completed", ("renown", State.renown.ToDouble()));
                }
            }
        }
    }
}
