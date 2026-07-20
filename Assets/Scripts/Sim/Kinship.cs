using System;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The permanent familiar track (design §4): at Migration a familiar's run
    /// XP converts to Kinship (√, decelerating like Verdure — no second Renown
    /// grant), and Kinship gives small permanent perks: a higher starting level
    /// and a faster XP rate. Signature traits are the 1.1 depth lever, not MVP.
    /// Constants are first guesses pending a data section (todo.md).
    /// </summary>
    public static class Kinship
    {
        /// <summary>K_f in kinshipGain = floor(√(runXP / K_f)) — first guess, todo.md to data-drive.</summary>
        public const double Divisor = 1000.0;

        /// <summary>+XP rate per Kinship level (design §4 "+XP rate"). First guess.</summary>
        public const double XpRatePerLevel = 0.02;

        /// <summary>A familiar's Kinship level (kinshipXp stores it directly — each Migration adds the √ gain).</summary>
        public static int Level(Familiar familiar)
        {
            return familiar == null ? 0 : (int)familiar.kinshipXp;
        }

        /// <summary>Kinship gained at Migration from this run's familiar XP (design §8 √ conversion).</summary>
        public static double GainFrom(double runXp)
        {
            return runXp <= 0.0 ? 0.0 : Math.Floor(Math.Sqrt(runXp / Divisor));
        }

        /// <summary>The run XP a familiar begins the next run with — enough to start at level 1 + Kinship level (design §4 "higher starting level").</summary>
        public static double StartingXp(Familiar familiar, GameDataAsset data)
        {
            var xp = data.economy?.familiarXp;
            return xp == null ? 0.0 : XpCurve.TotalForRungs(xp.baseXp, xp.growth, Level(familiar));
        }

        /// <summary>XP-rate multiplier a familiar's Kinship grants (design §4 "+XP rate").</summary>
        public static double XpRateMultiplier(Familiar familiar)
        {
            return 1.0 + XpRatePerLevel * Level(familiar);
        }

        /// <summary>
        /// Fold a familiar across Migration: bank its run XP into permanent
        /// Kinship, drop the run build (level/powerups/station reset), and set the
        /// next run's starting XP from the new Kinship level.
        /// </summary>
        public static void Fold(Familiar familiar, GameDataAsset data)
        {
            if (familiar == null)
            {
                return;
            }

            familiar.kinshipXp += GainFrom(familiar.xp);
            familiar.powerupIds.Clear();
            familiar.stationId = null;
            familiar.xp = StartingXp(familiar, data);
        }
    }
}
