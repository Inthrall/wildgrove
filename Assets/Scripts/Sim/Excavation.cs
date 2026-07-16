using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The dig tick (design §5): familiars set to a zone's dig site slowly turn
    /// soil, surfacing fossil fragments. Each tick rolls a drop at
    /// familiars · baseFragmentsPerHour · digSpeedMult · the site's summed
    /// incomplete strataRarity; a pity timer guarantees a find once
    /// pityTimerHoursDug hours pass without one. Which fossil the fragment
    /// belongs to is a strataRarity-weighted pick among the site's incomplete
    /// sets — completed fossils stop dropping, and a site with nothing left to
    /// find falls quiet. Rolls draw from the run's saved rng like quality does.
    /// </summary>
    public static class Excavation
    {
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            var excavation = data.economy?.excavation;
            if (excavation == null || excavation.baseFragmentsPerHour <= 0.0 || deltaSeconds <= 0.0)
            {
                return;
            }

            var digMult = Upgrades.DigSpeedMultiplier(state, data);
            foreach (var site in state.digSites)
            {
                if (site.familiarCount <= 0)
                {
                    continue;
                }

                // Amber (design §10) is the dig's renewable find — a separate
                // channel rolled before the fossil check, so fully-dug ground
                // keeps surfacing it. No draw when unconfigured: pre-amber rng
                // sequences must not shift.
                var amber = data.economy.amber;
                if (amber != null && amber.digFindsPerHour > 0.0
                    && Rng.NextDouble(ref state.rngState)
                       < site.familiarCount * amber.digFindsPerHour * digMult * (deltaSeconds / 3600.0))
                {
                    state.amber += amber.perFind;
                }

                var eligible = EligibleFossils(state, data, site.zoneId);
                if (eligible.Count == 0)
                {
                    // Everything this ground holds has been found — the site
                    // falls quiet (and doesn't bank pity toward nothing).
                    site.pityHours = 0.0;
                    continue;
                }

                var hoursDug = deltaSeconds / 3600.0;
                site.pityHours += hoursDug;

                var totalRarity = 0.0;
                foreach (var fossil in eligible)
                {
                    totalRarity += fossil.strataRarity;
                }

                var chance = site.familiarCount * excavation.baseFragmentsPerHour * digMult * totalRarity * hoursDug;
                var dropped = Rng.NextDouble(ref state.rngState) < chance;
                if (!dropped && excavation.pityTimerHoursDug > 0.0 && site.pityHours >= excavation.pityTimerHoursDug)
                {
                    dropped = true;
                }

                if (!dropped)
                {
                    continue;
                }

                site.pityHours = 0.0;
                var found = WeightedPick(state, eligible, totalRarity);
                state.fossilFragments[found.id] = Fossils.FragmentCount(state, found.id) + 1;

                if (Fossils.IsComplete(state, found))
                {
                    // The fossil's permanent effects go live the moment its
                    // last fragment lands.
                    Upgrades.RecomputeYieldMultipliers(state, data);
                }
            }
        }

        /// <summary>The fossils still assembling whose strata include this zone's dig site.</summary>
        public static List<FossilData> EligibleFossils(GameState state, GameDataAsset data, string zoneId)
        {
            var eligible = new List<FossilData>();
            if (data.fossils == null)
            {
                return eligible;
            }

            foreach (var fossil in data.fossils)
            {
                if (fossil.digSites != null && fossil.digSites.Contains(zoneId) && !Fossils.IsComplete(state, fossil))
                {
                    eligible.Add(fossil);
                }
            }

            return eligible;
        }

        private static FossilData WeightedPick(GameState state, List<FossilData> eligible, double totalRarity)
        {
            var roll = Rng.NextDouble(ref state.rngState) * totalRarity;
            foreach (var fossil in eligible)
            {
                roll -= fossil.strataRarity;
                if (roll < 0.0)
                {
                    return fossil;
                }
            }

            return eligible[eligible.Count - 1];
        }
    }
}
