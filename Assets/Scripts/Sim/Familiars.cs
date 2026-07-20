using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Per-familiar run mechanics (design §4): the run-track level derived from
    /// XP, the powerup pick every 5 levels, and crediting familiar XP into
    /// Renown (the money→XP spine, §9). Levels never scale output — they pace
    /// XP and the powerup cadence only. Kinship (the permanent track) lives in
    /// <see cref="Kinship"/>. No-ops when economy.familiarXp is absent (fixtures).
    /// </summary>
    public static class Familiars
    {
        public const int LevelsPerPowerup = 5;

        /// <summary>A familiar's current run level, derived from its xp via the §4 curve (60·1.12^L). 1 when unconfigured.</summary>
        public static int Level(Familiar familiar, GameDataAsset data)
        {
            var xp = data.economy?.familiarXp;
            if (xp == null || familiar == null)
            {
                return 1;
            }

            return 1 + XpCurve.RungsForXp(xp.baseXp, xp.growth, xp.maxLevel - 1, familiar.xp);
        }

        /// <summary>Fraction of the way to the next level (0 once capped).</summary>
        public static double ProgressToNextLevel(Familiar familiar, GameDataAsset data)
        {
            var xp = data.economy?.familiarXp;
            if (xp == null || familiar == null)
            {
                return 0.0;
            }

            return XpCurve.ProgressToNextRung(xp.baseXp, xp.growth, xp.maxLevel - 1, familiar.xp);
        }

        /// <summary>Powerup picks a familiar has earned by its level (one per <see cref="LevelsPerPowerup"/> levels).</summary>
        public static int EarnedPowerupPicks(Familiar familiar, GameDataAsset data)
        {
            return Level(familiar, data) / LevelsPerPowerup;
        }

        /// <summary>True when the familiar has reached a milestone whose powerup it hasn't chosen yet.</summary>
        public static bool HasPendingPowerup(Familiar familiar, GameDataAsset data)
        {
            return familiar != null && EarnedPowerupPicks(familiar, data) > familiar.powerupIds.Count;
        }

        /// <summary>The powerups still offerable to a familiar — its species pool minus what it already chose.</summary>
        public static List<PowerupData> OfferablePowerups(Familiar familiar, GameDataAsset data)
        {
            var offerable = new List<PowerupData>();
            if (familiar == null || data?.SpeciesById == null
                || !data.SpeciesById.TryGetValue(familiar.speciesId ?? string.Empty, out var species)
                || species.powerups == null)
            {
                return offerable;
            }

            foreach (var powerup in species.powerups)
            {
                if (!familiar.powerupIds.Contains(powerup.id))
                {
                    offerable.Add(powerup);
                }
            }

            return offerable;
        }

        /// <summary>
        /// Choose a powerup (from the species pool, not already taken) when a
        /// pick is pending, then recompute the modifier snapshot so a live
        /// build change takes hold. Returns false (no change) otherwise.
        /// </summary>
        public static bool ChoosePowerup(GameState state, GameDataAsset data, Familiar familiar, string powerupId)
        {
            if (state == null || data == null || familiar == null
                || !HasPendingPowerup(familiar, data) || familiar.powerupIds.Contains(powerupId))
            {
                return false;
            }

            var valid = false;
            foreach (var powerup in OfferablePowerups(familiar, data))
            {
                if (powerup.id == powerupId)
                {
                    valid = true;
                    break;
                }
            }

            if (!valid)
            {
                return false;
            }

            familiar.powerupIds.Add(powerupId);

            // Node yield reads live agent factors (Stationing/Powerups), but the
            // effect snapshot fingerprints on other sources — bump it so any
            // cached read refreshes alongside the new build.
            state.BumpModifiers();
            return true;
        }

        /// <summary>
        /// Credit run XP to a familiar at its post (design §4), clamped at the
        /// max level's total, and mirror the gain into Renown (§9 — money becomes
        /// XP). Wandering earns half (§2). Applied to every roster familiar each
        /// tick; <paramref name="perSecond"/> is economy.familiarXp.xpPerSecond.
        /// </summary>
        public static void AddPostXp(GameState state, GameDataAsset data, Familiar familiar, double perSecond, double seconds)
        {
            var xp = data.economy?.familiarXp;
            if (xp == null || familiar == null || perSecond <= 0.0 || seconds <= 0.0)
            {
                return;
            }

            var amount = perSecond * seconds
                         * (familiar.IsWandering ? Stationing.WanderMultiplier : 1.0)
                         * Kinship.XpRateMultiplier(familiar);
            if (amount <= 0.0)
            {
                return;
            }

            var cap = XpCurve.TotalForRungs(xp.baseXp, xp.growth, xp.maxLevel - 1);
            var headroom = cap - familiar.xp;
            if (headroom <= 0.0)
            {
                return;
            }

            var gained = System.Math.Min(amount, headroom);
            familiar.xp += gained;
            state.renown += new BigDouble(gained);
        }
    }
}
