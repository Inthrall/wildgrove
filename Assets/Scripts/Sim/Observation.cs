using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The observation tick (design §6): the wanderer passes every zone's
    /// observation site as it roams (the watch is no longer a post of its
    /// own), watching what lives there and recording it — adding a field
    /// sketch at watchers · baseSketchesPerHour · digSpeedMult · the site's
    /// summed unrecorded rarity; a pity timer guarantees a sketch once
    /// pityTimerHoursWatched hours pass without one. Which insect the sketch
    /// belongs to is a rarity-weighted pick among the site's unrecorded plates
    /// — a recorded plate stops appearing, and a site with nothing left to
    /// record falls quiet. Nothing is taken: the insect is released. Rolls draw
    /// from the run's saved rng like quality does. (digSpeedMult is the shared
    /// "site speed" modifier — planters/gear/almanac all feed it.)
    /// </summary>
    public static class Observation
    {
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            var observation = data.economy?.observation;
            if (observation == null || observation.baseSketchesPerHour <= 0.0 || deltaSeconds <= 0.0)
            {
                return;
            }

            // The wander post supplies the watching (design §2) — one roaming
            // familiar covers every unlocked site, its dig-speed trait folded
            // in via Stationing.WanderAgents. No wanderer, no sketches (and no
            // rng drawn, so sequences match the idle-site behaviour).
            var watchers = Stationing.WanderAgents(state, data);
            if (watchers <= 0.0)
            {
                return;
            }

            var digMult = Upgrades.DigSpeedMultiplier(state, data);
            foreach (var site in state.digSites)
            {
                // Reed-screen planters (design §3) steady this site's sketching.
                var siteDigMult = digMult * Planters.DigSpeedMultiplier(state, data, site.zoneId);

                // Amber (design §10) is the site's renewable find — old resin
                // with an ancient insect kept in it, the one thing takeable. A
                // separate channel rolled before the sketch check, so a
                // fully-recorded site keeps surfacing it. No draw when
                // unconfigured: pre-amber rng sequences must not shift.
                var amber = data.economy.amber;
                if (amber != null && amber.digFindsPerHour > 0.0
                    && Rng.NextDouble(ref state.rngState)
                       < watchers * amber.digFindsPerHour * siteDigMult * (deltaSeconds / 3600.0))
                {
                    state.amber += amber.perFind;
                    // Banked for GameLoop to report once per advance — the sim
                    // holds no telemetry sink, and an offline catch-up would
                    // otherwise fire thousands of per-substep events.
                    state.amberFoundUnlogged += amber.perFind;
                }

                // Reused scratch: this runs per site per 1 s substep — a full
                // offline catch-up is tens of thousands of walks, so the list
                // must not be a fresh allocation each time.
                state.insectScratch = state.insectScratch ?? new List<InsectData>();
                var eligible = state.insectScratch;
                EligibleInsectsInto(state, data, site.zoneId, eligible);
                if (eligible.Count == 0)
                {
                    // Every plate this site holds is recorded — the site falls
                    // quiet (and doesn't bank pity toward nothing).
                    site.pityHours = 0.0;
                    continue;
                }

                var hoursWatched = deltaSeconds / 3600.0;
                site.pityHours += hoursWatched;

                var totalRarity = 0.0;
                foreach (var insect in eligible)
                {
                    totalRarity += insect.rarity;
                }

                var chance = watchers * observation.baseSketchesPerHour * siteDigMult * totalRarity * hoursWatched;
                var dropped = Rng.NextDouble(ref state.rngState) < chance;
                if (!dropped && observation.pityTimerHoursWatched > 0.0 && site.pityHours >= observation.pityTimerHoursWatched)
                {
                    dropped = true;
                }

                if (!dropped)
                {
                    continue;
                }

                site.pityHours = 0.0;
                var found = WeightedPick(state, eligible, totalRarity);
                state.insectSketches[found.id] = Insects.SketchCount(state, found.id) + 1;

                if (Insects.IsRecorded(state, found))
                {
                    // The plate's permanent effects go live the moment its
                    // last portion is sketched.
                    Upgrades.RecomputeYieldMultipliers(state, data);
                }
            }
        }

        /// <summary>The plates still being recorded whose habitats include this zone's observation site.</summary>
        public static List<InsectData> EligibleInsects(GameState state, GameDataAsset data, string zoneId)
        {
            var eligible = new List<InsectData>();
            EligibleInsectsInto(state, data, zoneId, eligible);
            return eligible;
        }

        private static void EligibleInsectsInto(GameState state, GameDataAsset data, string zoneId, List<InsectData> eligible)
        {
            eligible.Clear();
            if (data.insects == null)
            {
                return;
            }

            foreach (var insect in data.insects)
            {
                if (insect.habitats != null && insect.habitats.Contains(zoneId) && !Insects.IsRecorded(state, insect))
                {
                    eligible.Add(insect);
                }
            }
        }

        private static InsectData WeightedPick(GameState state, List<InsectData> eligible, double totalRarity)
        {
            var roll = Rng.NextDouble(ref state.rngState) * totalRarity;
            foreach (var insect in eligible)
            {
                roll -= insect.rarity;
                if (roll < 0.0)
                {
                    return insect;
                }
            }

            return eligible[eligible.Count - 1];
        }
    }
}
