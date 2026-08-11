using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Migration — the prestige reset (design §7). Gated by the completed
    /// Rite; the camp folds and the run starts over (Coin, familiars, tools,
    /// gear, zone progress, skill levels — all wiped back to a fresh region),
    /// keeping what the design says the land remembers: Verdure (recomputed
    /// from lifetime Renown), the Renown itself, and every insect plate. The
    /// Compendium and Museum join the kept list when they exist.
    /// </summary>
    public static class Migration
    {
        /// <summary>Migration unlocks when the Rite is complete (the gate, not the timer — the player picks the moment).</summary>
        public static bool CanMigrate(GameState state, GameDataAsset data)
        {
            return Rite.IsRiteComplete(state, data);
        }

        /// <summary>
        /// The Verdure total a Migration banks, per design §8:
        /// floor((lifetimeRenown / renownDivisor) ^ exponent). Recomputed from
        /// the lifetime total each time — Renown is never spent, so leaving
        /// later always banks at least as much.
        /// </summary>
        public static double VerdureAfterMigration(GameState state, GameDataAsset data)
        {
            var verdure = data.economy?.verdure;
            if (verdure == null || verdure.renownDivisor <= 0.0)
            {
                return state.verdurePoints;
            }

            // The power is taken in BigDouble and only its RESULT converted.
            // Renown is a BigDouble for a reason, and collapsing it to a double
            // first put the whole lifetime total through a type it can outgrow —
            // the exponent (0.5) then brings the answer back into easy range, so
            // the only value that ever needed the wider type was the one being
            // narrowed.
            var banked = BigDouble.Pow(state.renown / verdure.renownDivisor, verdure.exponent).ToDouble();
            if (double.IsNaN(banked) || double.IsInfinity(banked))
            {
                // Past what a double holds even after the exponent. Keep what is
                // banked rather than let the Max below lock in an infinity that
                // nothing could walk back — a verdure total never shrinks.
                return state.verdurePoints;
            }

            // The land never forgets: an already-banked total can't shrink.
            return System.Math.Max(state.verdurePoints, System.Math.Floor(banked));
        }

        /// <summary>
        /// The lifetime Renown a given Verdure total asks for — the inverse of
        /// <see cref="VerdureAfterMigration"/>. Zero when the fold economy is
        /// inert. Lets the UI name the next point instead of leaving the
        /// player to infer the curve from two folds that banked 2 and 34.
        /// </summary>
        public static double RenownForVerdure(GameDataAsset data, double verdure)
        {
            var config = data?.economy?.verdure;
            if (config == null || config.renownDivisor <= 0.0 || config.exponent <= 0.0 || verdure <= 0.0)
            {
                return 0.0;
            }

            return System.Math.Pow(verdure, 1.0 / config.exponent) * config.renownDivisor;
        }

        /// <summary>
        /// The lifetime Renown the NEXT whole Verdure point asks for, given
        /// what this fold would already bank.
        /// </summary>
        public static double RenownForNextVerdure(GameState state, GameDataAsset data)
        {
            return RenownForVerdure(data, System.Math.Floor(VerdureAfterMigration(state, data)) + 1.0);
        }

        /// <summary>
        /// How far the lifetime Renown has climbed from the point just banked
        /// towards the next one, 0..1 — the fold banner's "62% to the next".
        /// A percentage is what a player can read at a glance; the raw
        /// thresholds mean nothing without the curve in your head. Zero when
        /// the fold economy is inert or the banked total already runs ahead of
        /// the curve (an old save whose Verdure can't shrink).
        /// </summary>
        public static double ProgressToNextVerdure(GameState state, GameDataAsset data)
        {
            var banked = System.Math.Floor(VerdureAfterMigration(state, data));
            var next = RenownForVerdure(data, banked + 1.0);
            if (next <= 0.0)
            {
                return 0.0;
            }

            var from = RenownForVerdure(data, banked);
            var span = next - from;
            if (span <= 0.0)
            {
                return 0.0;
            }

            var climbed = state.renown.ToDouble() - from;
            return System.Math.Max(0.0, System.Math.Min(1.0, climbed / span));
        }

        /// <summary>
        /// Fold the camp: returns the next run's fresh state carrying the
        /// permanents (Verdure banked from lifetime Renown, the Renown itself,
        /// every field sketch recorded, the rng thread, and the migration
        /// count), or null when the Rite hasn't consented. The caller swaps
        /// its live state for the returned one and saves.
        /// </summary>
        public static GameState Migrate(GameState state, GameDataAsset data)
        {
            if (state == null || data == null || !CanMigrate(state, data))
            {
                return null;
            }

            var next = GameStateFactory.NewGame(data);

            // The kith crosses the fold (design §4): the roster and every
            // Kinship level persist — run XP banks into Kinship (√), and each
            // familiar returns to a clean run build (level and station reset).
            // This replaces the fresh run's seed kith with the carried one.
            // (Presence-lapse — benching non-bonded familiars to re-meet — is
            // a v1.1 refinement; at MVP the whole roster stays present.)
            // Folded into COPIES, and the copies are what cross. Folding the
            // roster in place would empty every station and reset every run
            // level on the state the caller still holds — wrecking the run being
            // retired by the act of reading what it was worth — and would leave
            // the two states sharing one roster, so a later edit to either
            // reaches both.
            next.roster = new List<Familiar>(state.roster.Count);
            foreach (var familiar in state.roster)
            {
                var carried = familiar.Copy();
                Kinship.Fold(carried, data);
                next.roster.Add(carried);
            }

            next.nextFamiliarSeq = state.nextFamiliarSeq;

            // The ladder's currency and the store's slots both survive the
            // fold (§4): verses sung this run bank into the lifetime count,
            // and a purchase is a purchase.
            next.foldedVersesSung = state.foldedVersesSung + Rite.CompletedVerseCount(state, data);
            // The ladder itself: which verses have ever been sung. A verse sung
            // is never unsung (§4), so this only ever grows — and it is what
            // stops the fold taking an earned place back, now that the places
            // are named verses rather than a count.
            next.sungVerseZones = new List<string>(state.sungVerseZones);
            // And the floor under them, or a save carried over from the old
            // tally ladder would lose its places on its very next fold — which
            // is the one moment the ladder is meant to be safest.
            next.grandfatheredKithSlots = state.grandfatheredKithSlots;
            next.purchasedKithSlots = state.purchasedKithSlots;
            next.starterBundleAmberGranted = state.starterBundleAmberGranted;
            next.droversHalterOwned = state.droversHalterOwned;

            // The Wayfarer's Plate needs no more than its flag here: the page
            // itself rides insectSketches across the fold with the rest of the
            // Folio, and the flag only keeps a re-delivery from re-announcing.
            next.wayfarersPlateOwned = state.wayfarersPlateOwned;

            // Amber earn/claim cooldowns are cross-run: they gate premium
            // currency, so a fold must not re-arm them (migrating is the one
            // repeatable act a player controls). The weekly cache especially —
            // without this a player could claim, fold, and claim again at once.
            next.weeklyCacheClaimedUnixMs = state.weeklyCacheClaimedUnixMs;
            next.adDripClaimedUnixMs = state.adDripClaimedUnixMs;
            next.timeSkipClaimedUnixMs = state.timeSkipClaimedUnixMs;
            next.timeSkipBudgetHours = state.timeSkipBudgetHours;
            next.timeSkipBudgetStampUnixMs = state.timeSkipBudgetStampUnixMs;

            // The clock ratchet crosses with them, and for the same reason: a
            // fold that reset the mark would hand back every wind-forward the
            // guard had just made the player pay for.
            next.clockHighWaterUnixMs = state.clockHighWaterUnixMs;

            // Play time is lifetime, not per-run — it only ever grows, so a fold
            // carries it (a migrated save is further along, not reset to zero).
            next.playedMs = state.playedMs;

            next.verdurePoints = VerdureAfterMigration(state, data);
            next.renown = state.renown;
            next.migrationCount = state.migrationCount + 1;
            next.rngState = state.rngState;

            // "You keep … Amber" — the premium currency never resets.
            next.amber = state.amber;

            // The warden's name crosses with it, and for the same reason: it was
            // bought, and it names the player rather than the run. A fold that
            // dropped it would charge 50 Amber again for a name already given.
            // The CAMP's name is deliberately absent here: it names the run,
            // and it folds with the camp — naming the next camp is the next
            // region's own ritual (design §9's sink slate).
            next.wardenName = state.wardenName;

            // The Wheel is the warden's, not the run's (design §15): the
            // hemisphere is the warden's reckoning and the claims are sabbats
            // already kept — a fold that dropped either would flip a chosen
            // calendar back to the locale guess and let a kept sabbat be kept
            // twice. The clock cursor deliberately does NOT cross: it is the
            // host's runtime stamp, re-taken every frame.
            next.hemisphere = state.hemisphere;
            next.sabbatClaims.AddRange(state.sabbatClaims);

            // The open tide's keeping crosses too: it is calendar-keyed, not
            // run-keyed (design §15) — answered slots stay answered, and
            // Keeping.Current redraws the rest against the new run's country.
            next.keeping = state.keeping;

            // Lore stays read: run 2 re-unlocks the zones without re-showing
            // every stone the warden has already stood before. (NewGame marks
            // the starting stone itself, so add without duplicating.)
            foreach (var zoneId in state.seenWaystoneZoneIds)
            {
                Narrative.MarkWaystoneRead(next, zoneId);
            }

            // The final waystones cross with the rest of the lore, and the fold
            // stamp crosses with them to keep the pair coherent — a stamp reset
            // to "never" beside a count of three would claim no stone had ever
            // been read. Folding is what earns the next one: migrationCount has
            // just moved past the stamp, so the new run opens with one waiting.
            next.finalWaystonesRead = state.finalWaystonesRead;
            next.finalWaystoneLastFold = state.finalWaystoneLastFold;

            // The Almanac is the permanent tree — bought once, kept forever,
            // and the repeatable line's levels cross with the rest of it.
            next.almanacNodeIds.AddRange(state.almanacNodeIds);
            foreach (var pair in state.almanacLevels)
            {
                next.almanacLevels[pair.Key] = pair.Value;
            }

            // The exotic lines act here, at the fold (design §8): granted
            // rungs — starting tools, known trails — are on the new run from
            // its first morning. Re-derived against the NEW fold count, so a
            // granted trail the fold gate still holds back arrives on the run
            // that earns it.
            Almanac.SyncGrantedUpgrades(next, data);

            // The Fire Remembers: stations carry their standing orders across
            // the fold — the assignment only, never the batch (the old camp's
            // in-flight inputs fold with it). Each station stalls quietly
            // until the new run re-earns its recipe's skill and heat, then
            // takes the order up again — Advance re-checks workability every
            // tick, so no bookkeeping is owed here.
            if (Upgrades.HasActiveEffect(state, data, EffectType.KeepCraftOrders))
            {
                // One order per station id: the second queue is bought per run
                // (design §9's sink slate) and lapses at the fold, so a second
                // slot's order folds with the queue that held it — carrying it
                // would hand the new run a capacity it hasn't bought.
                var carried = new HashSet<string>();
                foreach (var station in state.stations)
                {
                    if (station.recipeId != null && carried.Add(station.stationId))
                    {
                        next.stations.Add(new StationState
                        {
                            stationId = station.stationId,
                            recipeId = station.recipeId,
                        });
                    }
                }
            }

            // "You keep … the Folio" — fixed specimens and their spread bonuses too.
            next.fixedResources.AddRange(state.fixedResources);

            // "You keep … every plate" — recorded plates and the sketches
            // still being drawn both survive; the record spans migrations.
            foreach (var pair in state.insectSketches)
            {
                next.insectSketches[pair.Key] = pair.Value;
            }

            // The deep amber crosses too — journal content, like the plates.
            // (The pity clock doesn't: a fresh run starts a fresh watch.)
            next.deepAmberFound = state.deepAmberFound;

            // The kith and the camp are rebuilt every run, so these two records
            // are the only memory that a species was ever befriended or a
            // station ever worked. They cross with the rest of the Compendium.
            next.speciesEverBefriended = new List<string>(state.speciesEverBefriended);
            next.stationsEverWorked = new List<string>(state.stationsEverWorked);

            // "You keep … the Compendium" — the lifetime record crosses whole.
            foreach (var pair in state.lifetimeGathered)
            {
                next.lifetimeGathered[pair.Key] = pair.Value;
            }

            foreach (var pair in state.lifetimeCrafted)
            {
                next.lifetimeCrafted[pair.Key] = pair.Value;
            }

            foreach (var pair in state.lifetimeChoice)
            {
                next.lifetimeChoice[pair.Key] = pair.Value;
            }

            // A bond whose source is a kept permanent (Museum set / Almanac node)
            // stays earned — make sure its companion is present in the carried
            // roster (idempotent by bondId).
            Roster.SyncBonded(next, data);

            // A redemption is a redemption — the pony crosses the fold and is
            // standing in her lane in the new run (§11).
            Roster.SyncDroversHalter(next, data);

            // Recorded plates' effects fold into the fresh run's multipliers at once.
            Upgrades.RecomputeYieldMultipliers(next, data);
            return next;
        }
    }
}
