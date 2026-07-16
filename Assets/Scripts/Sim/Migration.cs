using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Migration — the prestige reset (design §7). Gated by the completed
    /// Rite; the camp folds and the run starts over (Coin, familiars, tools,
    /// gear, zone progress, skill levels — all wiped back to a fresh region),
    /// keeping what the design says the land remembers: Verdure (recomputed
    /// from lifetime Renown), the Renown itself, and every fossil. The
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
        /// every fossil fragment dug, the rng thread, and the migration
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
            next.verdurePoints = VerdureAfterMigration(state, data);
            next.renown = state.renown;
            next.migrationCount = state.migrationCount + 1;
            next.rngState = state.rngState;

            // "You keep … every fossil" — completed fossils and the fragments
            // still assembling both survive; the dig chase spans migrations.
            foreach (var pair in state.fossilFragments)
            {
                next.fossilFragments[pair.Key] = pair.Value;
            }

            // Fossil effects fold into the fresh run's multipliers at once.
            Upgrades.RecomputeYieldMultipliers(next, data);
            return next;
        }
    }
}
