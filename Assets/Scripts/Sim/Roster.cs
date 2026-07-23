using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Building and naming the kith (design §4): recruitment events mint named
    /// familiars, the player renames them, and stationing sets their post. The
    /// roster is the collection — at most one familiar of each species ever —
    /// and slots cap who holds a post, not who belongs. Bonds honour the
    /// companion of their species (or bring it, if it has never come).
    /// </summary>
    public static class Roster
    {
        /// <summary>The familiar of a species, or null when that species has never joined.</summary>
        public static Familiar OfSpecies(GameState state, string speciesId)
        {
            if (state?.roster == null || string.IsNullOrEmpty(speciesId))
            {
                return null;
            }

            foreach (var familiar in state.roster)
            {
                if (familiar.speciesId == speciesId)
                {
                    return familiar;
                }
            }

            return null;
        }

        /// <summary>
        /// Recruit a familiar of a species, with a suggested default name the
        /// player can accept or change. Each species joins once, ever — a
        /// duplicate recruit returns null and changes nothing. The arrival
        /// takes <paramref name="stationId"/> when a slot is open for it and
        /// the post stands empty (one body per post, §2 — an arrival never
        /// bumps anyone), otherwise it rests at camp (the collection is never
        /// slot-capped).
        /// </summary>
        public static Familiar Recruit(GameState state, GameDataAsset data, string speciesId, string stationId, string name = null)
        {
            if (OfSpecies(state, speciesId) != null)
            {
                return null;
            }

            var familiar = new Familiar
            {
                id = state.NextFamiliarId(),
                speciesId = speciesId,
                name = string.IsNullOrEmpty(name) ? SuggestName(state, data, speciesId) : name,
                stationId = null
            };

            state.roster.Add(familiar);

            if (!string.IsNullOrEmpty(stationId) && Kith.HasRoom(state, data)
                && Stationing.OccupantOf(state, stationId) == null
                && stationId != Warden.PostNodeId(state))
            {
                familiar.stationId = stationId;
            }

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
        /// <see cref="Familiar.WanderStation"/>, or null to rest at camp
        /// (design §2: reassignment is always allowed and never costs goods).
        /// One body per post: whoever holds the post steps back — a familiar
        /// rests at camp (its slot frees for the newcomer), the warden walks
        /// back to camp. Taking an EMPTY post needs an open slot when the
        /// familiar was resting (§4 ladder) — returns false and changes
        /// nothing without one. Bumps the modifier snapshot so any cached
        /// read refreshes.
        /// </summary>
        public static bool Station(GameState state, GameDataAsset data, Familiar familiar, string stationId)
        {
            if (familiar == null)
            {
                return false;
            }

            var wants = string.IsNullOrEmpty(stationId) ? null : stationId;
            if (wants == null)
            {
                familiar.stationId = null;
                state.BumpModifiers();
                return true;
            }

            var occupant = Stationing.OccupantOf(state, wants);
            if (occupant == familiar)
            {
                return true;
            }

            // Swapping in for the occupant frees their slot in the same move;
            // only an empty post asks the ladder for room.
            if (occupant == null && familiar.IsResting && !Kith.HasRoom(state, data))
            {
                return false;
            }

            if (occupant != null)
            {
                occupant.stationId = null;
            }

            if (wants == Warden.PostNodeId(state))
            {
                state.wardenPostNodeId = null;
            }

            familiar.stationId = wants;
            state.BumpModifiers();
            return true;
        }

        /// <summary>
        /// Honour every earned bond (idempotent by bondId): the companion of
        /// the bond's species is marked bonded — keeping its player-given name
        /// — and if that species has never joined, it arrives now, resting at
        /// camp (the collection is never slot-capped). Call at new game, on
        /// load, after Migration, and after any action that can complete a
        /// bond's source.
        /// </summary>
        public static void SyncBonded(GameState state, GameDataAsset data)
        {
            var honoured = new HashSet<string>();
            foreach (var familiar in state.roster)
            {
                if (!string.IsNullOrEmpty(familiar.bondId))
                {
                    honoured.Add(familiar.bondId);
                }
            }

            foreach (var bond in Bonds.Earned(state, data))
            {
                if (honoured.Contains(bond.id))
                {
                    continue;
                }

                var companion = OfSpecies(state, bond.species);
                if (companion != null)
                {
                    companion.bonded = true;
                    companion.bondId = bond.id;
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
