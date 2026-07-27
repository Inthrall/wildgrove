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

            var banked = System.Math.Floor(
                System.Math.Pow((state.renown / verdure.renownDivisor).ToDouble(), verdure.exponent));

            // The land never forgets: an already-banked total can't shrink.
            return System.Math.Max(state.verdurePoints, banked);
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
            foreach (var familiar in state.roster)
            {
                Kinship.Fold(familiar, data);
            }

            next.roster = state.roster;
            next.nextFamiliarSeq = state.nextFamiliarSeq;

            // The ladder's currency and the store's slots both survive the
            // fold (§4): verses sung this run bank into the lifetime count,
            // and a purchase is a purchase.
            next.foldedVersesSung = state.foldedVersesSung + Rite.CompletedVerseCount(state, data);
            next.purchasedKithSlots = state.purchasedKithSlots;
            next.starterBundleAmberGranted = state.starterBundleAmberGranted;

            // Amber earn/claim cooldowns are cross-run: they gate premium
            // currency, so a fold must not re-arm them (migrating is the one
            // repeatable act a player controls). The weekly cache especially —
            // without this a player could claim, fold, and claim again at once.
            next.weeklyCacheClaimedUnixMs = state.weeklyCacheClaimedUnixMs;
            next.adDripClaimedUnixMs = state.adDripClaimedUnixMs;
            next.timeSkipClaimedUnixMs = state.timeSkipClaimedUnixMs;

            // Play time is lifetime, not per-run — it only ever grows, so a fold
            // carries it (a migrated save is further along, not reset to zero).
            next.playedMs = state.playedMs;

            next.verdurePoints = VerdureAfterMigration(state, data);
            next.renown = state.renown;
            next.migrationCount = state.migrationCount + 1;
            next.rngState = state.rngState;

            // "You keep … Amber" — the premium currency never resets.
            next.amber = state.amber;

            // Lore stays read: run 2 re-unlocks the zones without re-showing
            // every stone the warden has already stood before. (NewGame marks
            // the starting stone itself, so add without duplicating.)
            foreach (var zoneId in state.seenWaystoneZoneIds)
            {
                Narrative.MarkWaystoneRead(next, zoneId);
            }

            // The Almanac is the permanent tree — bought once, kept forever.
            next.almanacNodeIds.AddRange(state.almanacNodeIds);

            // "You keep … the Folio" — fixed specimens and their spread bonuses too.
            next.fixedResources.AddRange(state.fixedResources);

            // "You keep … every plate" — recorded plates and the sketches
            // still being drawn both survive; the record spans migrations.
            foreach (var pair in state.insectSketches)
            {
                next.insectSketches[pair.Key] = pair.Value;
            }

            // "You keep … the Compendium" — the lifetime record crosses whole.
            foreach (var pair in state.lifetimeGathered)
            {
                next.lifetimeGathered[pair.Key] = pair.Value;
            }

            foreach (var pair in state.lifetimeCrafted)
            {
                next.lifetimeCrafted[pair.Key] = pair.Value;
            }

            foreach (var pair in state.lifetimePristine)
            {
                next.lifetimePristine[pair.Key] = pair.Value;
            }

            // A bond whose source is a kept permanent (Museum set / Almanac node)
            // stays earned — make sure its companion is present in the carried
            // roster (idempotent by bondId).
            Roster.SyncBonded(next, data);

            // Recorded plates' effects fold into the fresh run's multipliers at once.
            Upgrades.RecomputeYieldMultipliers(next, data);
            return next;
        }
    }
}
