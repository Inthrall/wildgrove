using System;
using System.Collections.Generic;
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
        /// <summary>K_f used only when economy.familiarXp is absent (hand-built fixtures); real data drives it.</summary>
        private const double FallbackDivisor = 1000.0;

        /// <summary>A familiar's Kinship level (kinshipXp stores it directly — each Migration adds the √ gain).</summary>
        public static int Level(Familiar familiar)
        {
            return familiar == null ? 0 : (int)familiar.kinshipXp;
        }

        /// <summary>Kinship gained at Migration from this run's familiar XP (design §8 √ conversion).</summary>
        public static double GainFrom(double runXp, double divisor)
        {
            return runXp <= 0.0 || divisor <= 0.0 ? 0.0 : Math.Floor(Math.Sqrt(runXp / divisor));
        }

        /// <summary>The run XP a familiar begins the next run with — enough to start at level 1 + Kinship level (design §4 "higher starting level").</summary>
        public static double StartingXp(Familiar familiar, GameDataAsset data)
        {
            var xp = data.economy?.familiarXp;
            return xp == null ? 0.0 : XpCurve.TotalForRungs(xp.baseXp, xp.growth, Level(familiar));
        }

        /// <summary>XP-rate multiplier a familiar's Kinship grants (design §4 "+XP rate").</summary>
        public static double XpRateMultiplier(Familiar familiar, double ratePerLevel)
        {
            return 1.0 + ratePerLevel * Level(familiar);
        }

        /// <summary>
        /// Signature milestones a Kinship level has passed (design §4): each
        /// one deepens the species trait (<see cref="Traits.DeepeningFactor"/>)
        /// and earns the plate an inscription line (§7). Zero when signatures
        /// aren't configured.
        /// </summary>
        public static int MilestonesPassedAt(int kinshipLevel, GameDataAsset data)
        {
            var milestones = data?.economy?.familiarXp?.signatureMilestones;
            if (milestones == null)
            {
                return 0;
            }

            var passed = 0;
            foreach (var milestone in milestones)
            {
                if (milestone > 0 && kinshipLevel >= milestone)
                {
                    passed++;
                }
            }

            return passed;
        }

        /// <summary>Signature milestones this familiar has passed.</summary>
        public static int SignatureMilestonesPassed(Familiar familiar, GameDataAsset data)
        {
            return MilestonesPassedAt(Level(familiar), data);
        }

        /// <summary>
        /// The Kinship level this familiar would hold after a fold right now —
        /// current level plus this run's √ conversion. Lets the fold forecast
        /// say which signatures are about to sharpen without folding.
        /// </summary>
        public static int LevelAfterFold(Familiar familiar, GameDataAsset data)
        {
            if (familiar == null)
            {
                return 0;
            }

            var xp = data?.economy?.familiarXp;
            var divisor = xp != null && xp.kinshipDivisor > 0.0 ? xp.kinshipDivisor : FallbackDivisor;
            return Level(familiar) + (int)GainFrom(familiar.xp, divisor);
        }

        /// <summary>
        /// The plate inscription lines this familiar has earned (design §7):
        /// its species' authored lines, one per milestone passed, in order.
        /// Unauthored lines simply never show.
        /// </summary>
        public static List<string> InscriptionsEarned(Familiar familiar, GameDataAsset data)
        {
            var earned = new List<string>();
            if (familiar == null || data?.SpeciesById == null
                || !data.SpeciesById.TryGetValue(familiar.speciesId ?? string.Empty, out var species)
                || species.inscriptions == null)
            {
                return earned;
            }

            var passed = SignatureMilestonesPassed(familiar, data);
            for (var i = 0; i < passed && i < species.inscriptions.Count; i++)
            {
                earned.Add(species.inscriptions[i]);
            }

            return earned;
        }

        /// <summary>
        /// Fold a familiar across Migration: bank its run XP into permanent
        /// Kinship, drop the run build (level/station reset), and set the
        /// next run's starting XP from the new Kinship level.
        /// </summary>
        public static void Fold(Familiar familiar, GameDataAsset data)
        {
            if (familiar == null)
            {
                return;
            }

            var xp = data.economy?.familiarXp;
            var divisor = xp != null && xp.kinshipDivisor > 0.0 ? xp.kinshipDivisor : FallbackDivisor;
            familiar.kinshipXp += GainFrom(familiar.xp, divisor);
            familiar.stationId = null;
            familiar.xp = StartingXp(familiar, data);
        }
    }
}
