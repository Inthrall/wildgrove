using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// The creatures: bonds and the species that earn them, with their roles,
    /// leans and traits.
    /// </summary>
    public static partial class GameDataValidator
    {
        private static readonly HashSet<string> KnownBondRoles = new HashSet<string> { "gatherer", "carrier" };
        private static readonly HashSet<string> KnownBondSourceTypes = new HashSet<string> { "folioSpread", "almanacNode" };

        private static void ValidateBonds(GameData data, List<string> issues)
        {
            CheckIds(data.Bonds.Select(b => b.Id), "bond", issues);

            var seenSources = new HashSet<string>();
            var seenSpecies = new HashSet<string>();
            foreach (var bond in data.Bonds)
            {
                if (!KnownBondRoles.Contains(bond.Role))
                {
                    issues.Add($"Bond '{bond.Id}' has unknown role '{bond.Role}'");
                }

                if (string.IsNullOrEmpty(bond.Species) || !data.SpeciesById.ContainsKey(bond.Species))
                {
                    issues.Add($"Bond '{bond.Id}' references unknown species '{bond.Species}'");
                }
                else if (!seenSpecies.Add(bond.Species))
                {
                    // At most one familiar per species (design §4). Two bonds on
                    // the same species clobber each other's bondId on the shared
                    // roster entry, which can re-materialise a duplicate.
                    issues.Add($"Bond '{bond.Id}' targets species '{bond.Species}' already claimed by another bond — one bond per species");
                }

                if (bond.Source == null || !KnownBondSourceTypes.Contains(bond.Source.Type))
                {
                    issues.Add($"Bond '{bond.Id}' has an unknown source type '{bond.Source?.Type}'");
                    continue;
                }

                var exists = bond.Source.Type == "folioSpread"
                    ? data.SpreadsById.ContainsKey(bond.Source.Id ?? string.Empty)
                    : data.AlmanacById.ContainsKey(bond.Source.Id ?? string.Empty);
                if (!exists)
                {
                    issues.Add($"Bond '{bond.Id}' source references unknown {bond.Source.Type} '{bond.Source.Id}'");
                }

                // Each bond source grants exactly ONE permanent companion
                // (design §7) — two bonds on one source silently halves the
                // reward the second one promises.
                if (!seenSources.Add(bond.Source.Type + ":" + bond.Source.Id))
                {
                    issues.Add($"Bond '{bond.Id}' shares its source {bond.Source.Type} '{bond.Source.Id}' with another bond — one companion per source");
                }
            }
        }

        private static readonly HashSet<string> KnownRoleLeans = new HashSet<string> { "gatherer", "carrier" };

        // The trait kinds the sim knows how to apply (Traits.cs) — a typo'd
        // kind would sit on a species and do nothing.
        private static readonly HashSet<string> KnownTraitKinds = new HashSet<string>
        {
            "nodeYieldBonus", "choiceBonus", "digSpeedBonus", "bubbleRewardBonus", "wardenYieldBonus"
        };

        // The trait's authored resource pair, falling back to the legacy single
        // Resource — mirrors GameDataMapper.ResolveTraitResources.
        private static List<string> TraitResources(TraitDef trait)
        {
            if (trait.Resources != null && trait.Resources.Count > 0)
            {
                return trait.Resources;
            }

            return string.IsNullOrEmpty(trait.Resource) ? new List<string>() : new List<string> { trait.Resource };
        }

        private static void ValidateSpecies(GameData data, List<string> issues)
        {
            CheckIds(data.Species.Select(s => s.Id), "species", issues);

            // Gift piles resolve a node's arrival by its resource's specialist —
            // two species claiming the same resource would make that ambiguous.
            var specialistResources = new HashSet<string>();

            // Inscriptions unlock one per signature milestone — a line past the
            // last milestone is authored words no one can ever read.
            var signatureMilestones = data.Economy?.FamiliarXp?.SignatureMilestones?.Count ?? 0;

            foreach (var species in data.Species)
            {
                if (species.Inscriptions != null)
                {
                    if (species.Inscriptions.Count > signatureMilestones)
                    {
                        issues.Add($"Species '{species.Id}' has {species.Inscriptions.Count} inscriptions but only {signatureMilestones} signature milestones exist — the extra lines are unreachable");
                    }

                    if (species.Inscriptions.Any(string.IsNullOrWhiteSpace))
                    {
                        issues.Add($"Species '{species.Id}' has a blank inscription line");
                    }
                }

                if (!KnownRoleLeans.Contains(species.RoleLean))
                {
                    issues.Add($"Species '{species.Id}' has unknown roleLean '{species.RoleLean}'");
                }

                if (species.SuggestedNames == null || species.SuggestedNames.Count == 0)
                {
                    issues.Add($"Species '{species.Id}' has no suggested names — arrival naming needs at least one");
                }

                var trait = species.Trait;
                if (trait == null)
                {
                    issues.Add($"Species '{species.Id}' has no trait — every species is the specialist of something");
                    continue;
                }

                if (!KnownTraitKinds.Contains(trait.Kind))
                {
                    issues.Add($"Species '{species.Id}' trait has unknown kind '{trait.Kind}'");
                }

                if (trait.Value <= 0.0)
                {
                    issues.Add($"Species '{species.Id}' trait must have a positive value");
                }

                var traitResources = TraitResources(trait);
                if (traitResources.Count > 0)
                {
                    if (trait.Kind != "nodeYieldBonus")
                    {
                        issues.Add($"Species '{species.Id}' trait has resources but kind '{trait.Kind}' never reads them");
                    }

                    foreach (var resource in traitResources)
                    {
                        if (!data.ResourcesById.ContainsKey(resource))
                        {
                            issues.Add($"Species '{species.Id}' trait references unknown resource '{resource}'");
                        }

                        // Each node resolves its gift-pile arrival by resource,
                        // so no two species may claim the same one — even though
                        // a species now works a pair.
                        if (!specialistResources.Add(resource))
                        {
                            issues.Add($"Two species claim resource '{resource}' — gift piles need one specialist per resource");
                        }
                    }
                }
            }
        }
    }
}
