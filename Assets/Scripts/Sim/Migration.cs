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

            // The crew crosses the fold (design §4): the roster and every
            // Kinship level persist — run XP banks into Kinship (√), and each
            // familiar returns to a clean run build (level, powerups, station
            // reset). This replaces the fresh run's seed crew with the carried
            // one. (Presence-lapse — benching non-bonded familiars to re-meet —
            // is a v1.1 refinement; at MVP the whole roster stays present.)
            foreach (var familiar in state.roster)
            {
                Kinship.Fold(familiar, data);
            }

            next.roster = state.roster;
            next.nextFamiliarSeq = state.nextFamiliarSeq;

            next.verdurePoints = VerdureAfterMigration(state, data);
            next.renown = state.renown;
            next.migrationCount = state.migrationCount + 1;
            next.rngState = state.rngState;

            // "You keep … Amber" — the premium currency never resets.
            next.amber = state.amber;

            // Lore stays read: run 2 re-unlocks the zones without re-showing
            // every stone the warden has already stood before.
            next.seenWaystoneZoneIds.AddRange(state.seenWaystoneZoneIds);

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
