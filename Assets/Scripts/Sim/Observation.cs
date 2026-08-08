using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The observation tick (design §6): the wanderer passes every zone's
    /// observation site as it roams (the watch is not a post of its own),
    /// watching what lives there and recording it — adding a field
    /// sketch at watchers · baseSketchesPerHour · digSpeedMult · the site's
    /// summed unrecorded rarity; a pity timer guarantees a sketch once
    /// pityTimerHoursWatched hours pass without one. Which insect the sketch
    /// belongs to is a rarity-weighted pick among the site's unrecorded plates
    /// — a recorded plate stops appearing, and a site with nothing left to
    /// record falls quiet. Nothing is taken: the insect is released. Rolls draw
    /// from the run's saved rng like quality does. (digSpeedMult is the shared
    /// "site speed" modifier — planters/gear/almanac all feed it.) Amber
    /// (design §10) is the exception: one flat roll per tick for the whole
    /// round, outside the site walk and untouched by digSpeedMult, because
    /// per-site × multiplicative-stack compounded into a login payout many
    /// times the design lean. Watching also
    /// trains the observation craft (economy.observation.skill), which is the
    /// only thing that earns that skill's XP — it is a watched-hours trickle, not
    /// a per-sketch award, so it keeps paying at a fully-recorded site.
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

            var hoursWatched = deltaSeconds / 3600.0;
            // Samhain's tide leans on the watch (design §15) — it joins the
            // sketch walk's stack below, and deliberately NOT the amber roll:
            // the 2026-08-02 flattening took every multiplier off the premium
            // faucet, and the tide does not reopen that door.
            var digMult = Upgrades.DigSpeedMultiplier(state, data) * Wheel.DigSpeedMult(state, data);

            // Amber (design §10) is the round's renewable find — old resin with
            // an ancient insect kept in it, the one thing takeable. Rolled ONCE
            // for the whole round rather than per site, and flat: the sketch
            // channel's dig-speed stack deliberately does not touch it. Both
            // used to, and both compounded — six sites against a multiplicative
            // ×10 stack put one login's catch-up near 70 amber where the design
            // lean is ~40 a week. Rolled before the sketch walk, so a
            // fully-recorded map keeps surfacing it. No draw when unconfigured
            // or before the first site opens: pre-amber rng sequences must not
            // shift.
            var amber = data.economy.amber;
            if (amber != null && amber.digFindsPerHour > 0.0 && state.digSites.Count > 0
                && Rng.NextDouble(ref state.rngState) < watchers * amber.digFindsPerHour * hoursWatched)
            {
                state.amber += amber.perFind;
                // Banked for GameLoop to report once per advance — the sim
                // holds no telemetry sink, and an offline catch-up would
                // otherwise fire thousands of per-substep events.
                state.amberFoundUnlogged += amber.perFind;
            }

            foreach (var site in state.digSites)
            {
                // Reed-screen planters (design §3) steady this site's sketching.
                var siteDigMult = digMult * Planters.DigSpeedMultiplier(state, data, site.zoneId);

                // The watching itself trains the craft (design §4: XP from every
                // action) — credited per watcher per site-hour, before this
                // site's find channels roll, so a site with every plate already
                // recorded still teaches. That ordering is the whole point: the
                // sketch pool is finite (25 portions across every plate) and
                // rides the fold in insectSketches while skillXp resets, so
                // paying XP per sketch instead would strand a fully-recorded run
                // at level 1 and put Brush Screens (entomology 8) out of reach
                // for good.
                if (observation.watchXpPerHour > 0.0)
                {
                    Skills.AddXp(state, data, observation.skill,
                        new BigDouble(watchers * observation.watchXpPerHour * siteDigMult * hoursWatched));
                }

                // The deep amber (design §6): the authored deep-past pieces,
                // surfaced only at their own zone's site. Stays per-site and
                // keeps its dig-speed multiplier where the ordinary amber
                // channel above gave both up — this one is lore pacing, not
                // currency, it can only ever pay out four times, and the watch
                // stack shortening that walk is the intent (see ambers.json).
                // It too keeps working after every plate here is recorded.
                DeepAmber.AdvanceSite(state, data, site.zoneId, watchers, siteDigMult, deltaSeconds);

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
                // An awarded plate holds no habitats, so it would fall out here
                // anyway — say so explicitly, because "no habitats" is a data
                // convention and this is the rule it stands for.
                if (insect.rewarded)
                {
                    continue;
                }

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
