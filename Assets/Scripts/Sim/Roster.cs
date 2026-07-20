using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Building and naming the crew (design §4): recruitment events mint named
    /// familiars, the player renames them, and stationing sets their post.
    /// Bonded familiars (source-earned) are materialised idempotently here so
    /// they're present from minute one of every run.
    /// </summary>
    public static class Roster
    {
        /// <summary>
        /// Recruit a familiar of a species to a station (null = wandering), with a
        /// suggested default name the player can accept or change. Returns the new
        /// roster entry (for the arrival naming sheet).
        /// </summary>
        public static Familiar Recruit(GameState state, GameDataAsset data, string speciesId, string stationId, string name = null)
        {
            var familiar = new Familiar
            {
                id = state.NextFamiliarId(),
                speciesId = speciesId,
                name = string.IsNullOrEmpty(name) ? SuggestName(state, data, speciesId) : name,
                stationId = string.IsNullOrEmpty(stationId) ? null : stationId
            };

            state.roster.Add(familiar);
            return familiar;
        }

        /// <summary>A species-appropriate default name not already in use, or "&lt;Species&gt; N" when the pool is exhausted.</summary>
        public static string SuggestName(GameState state, GameDataAsset data, string speciesId)
        {
            var used = new HashSet<string>();
            foreach (var familiar in state.roster)
            {
                if (!string.IsNullOrEmpty(familiar.name))
                {
                    used.Add(familiar.name);
                }
            }

            if (data?.SpeciesById != null && data.SpeciesById.TryGetValue(speciesId ?? string.Empty, out var species))
            {
                foreach (var suggestion in species.suggestedNames)
                {
                    if (!used.Contains(suggestion))
                    {
                        return suggestion;
                    }
                }

                return species.displayName + " " + (state.roster.Count + 1);
            }

            return "Familiar " + (state.roster.Count + 1);
        }

        /// <summary>Rename a familiar (trimmed; ignored when blank). Returns false when nothing changed.</summary>
        public static bool Rename(Familiar familiar, string name)
        {
            if (familiar == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            familiar.name = name.Trim();
            return true;
        }

        /// <summary>
        /// Station a familiar at a post — a node id, <see cref="Familiar.TrailStation"/>,
        /// a <see cref="Familiar.DigStationPrefix"/> site, or null to wander (design §2:
        /// reassignment is always allowed and never costs goods). Bumps the modifier
        /// snapshot so any cached read refreshes.
        /// </summary>
        public static void Station(GameState state, GameDataAsset data, Familiar familiar, string stationId)
        {
            if (familiar == null)
            {
                return;
            }

            familiar.stationId = string.IsNullOrEmpty(stationId) ? null : stationId;
            state.BumpModifiers();
        }

        /// <summary>
        /// Materialise a roster entry for every earned bond not yet present
        /// (idempotent by bondId) — a bonded familiar is present from minute one
        /// of every run (design §4). Call at new game, on load, after Migration,
        /// and after any action that can complete a bond's source.
        /// </summary>
        public static void SyncBonded(GameState state, GameDataAsset data)
        {
            var present = new HashSet<string>();
            foreach (var familiar in state.roster)
            {
                if (!string.IsNullOrEmpty(familiar.bondId))
                {
                    present.Add(familiar.bondId);
                }
            }

            foreach (var bond in Bonds.Earned(state, data))
            {
                if (present.Contains(bond.id))
                {
                    continue;
                }

                state.roster.Add(new Familiar
                {
                    id = state.NextFamiliarId(),
                    bondId = bond.id,
                    bonded = true,
                    speciesId = bond.species,
                    name = string.IsNullOrEmpty(bond.displayName) ? SuggestName(state, data, bond.species) : bond.displayName,
                    stationId = null
                });
            }
        }
    }
}
