using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// Referential-integrity and sanity checks across the nine data files.
    /// Returns human-readable issues; empty list means the data is coherent.
    /// </summary>
    public static class GameDataValidator
    {
        private static readonly HashSet<string> KnownSkills = new HashSet<string>
        {
            "foraging", "mining", "logging", "fishing",
            "firecraft", "forgecraft", "bushcraft", "excavation",
            "entomology", "apothecary", "delving", "husbandry"
        };

        private static readonly HashSet<string> SkillWildcards = new HashSet<string> { "all", "all-gathering" };

        private static readonly HashSet<string> KnownScopes = new HashSet<string> { "mvp", "v1.1", "v1.2" };

        // The sim switches on the literal (Economy.cs sell pricing, rite slot
        // classification) — a typo'd kind silently makes a good unsellable.
        private static readonly HashSet<string> KnownRecipeKinds = new HashSet<string> { "material", "trade" };

        // Deeds a running sim can actually record (Simulation.RecordDeed call
        // sites) — a slot naming anything else could never fill.
        private static readonly HashSet<string> KnownDeeds = new HashSet<string> { "tend" };

        private static readonly HashSet<string> KnownSpecimenQualities = new HashSet<string> { "fine", "pristine" };

        public static IReadOnlyList<string> Validate(GameData data)
        {
            var issues = new List<string>();

            CheckIds(data.Resources.Select(r => r.Id), "resource", issues);
            CheckIds(data.Zones.Select(z => z.Id), "zone", issues);
            CheckIds(data.Upgrades.Select(u => u.Id), "upgrade", issues);
            CheckIds(data.Recipes.Select(r => r.Id), "recipe", issues);
            CheckIds(data.Buildings.Select(b => b.Id), "building", issues);
            CheckIds(data.Gear.Select(g => g.Id), "gear", issues);
            CheckIds(data.Insects.Select(f => f.Id), "insect", issues);
            CheckIds(data.Almanac.Select(a => a.Id), "almanac node", issues);
            CheckIds(data.Spreads.Select(s => s.Id), "folio spread", issues);

            // Everything obtainable: gathered from a zone or produced by a recipe.
            var resourceIds = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            resourceIds.UnionWith(data.Recipes.Select(r => r.Output).Where(o => o != null));

            ValidateResources(data, issues);
            ValidateZones(data, issues);
            ValidateRecipes(data, resourceIds, issues);
            ValidateRecipeObtainability(data, issues);
            ValidateBuildings(data, resourceIds, issues);
            ValidateUpgrades(data, resourceIds, issues);
            ValidateGear(data, resourceIds, issues);
            ValidateInsects(data, resourceIds, issues);
            ValidateAlmanac(data, resourceIds, issues);
            ValidateFolio(data, resourceIds, issues);
            ValidateBonds(data, issues);
            ValidateSpecies(data, issues);
            ValidatePlanters(data, resourceIds, issues);
            ValidateExchange(data, issues);
            ValidateRites(data, resourceIds, issues);
            ValidateDialogue(data, issues);
            ValidateEconomy(data.Economy, issues);

            return issues;
        }

        private static void CheckIds(IEnumerable<string> ids, string kind, List<string> issues)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add($"A {kind} entry has no id");
                }
                else if (!seen.Add(id))
                {
                    issues.Add($"Duplicate {kind} id '{id}'");
                }
            }
        }

        private static void ValidateResources(GameData data, List<string> issues)
        {
            // Every raw gatherable must be priced exactly once, and resources.json
            // must not price anything that isn't gathered (crafted trade goods
            // derive their value from their recipe; materials are not sold).
            var gathered = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            var priced = new HashSet<string>(data.Resources.Select(r => r.Id).Where(id => id != null));

            foreach (var id in gathered.Where(id => !priced.Contains(id)))
            {
                issues.Add($"Resource '{id}' is gathered from a zone but has no sell value in resources.json");
            }

            foreach (var id in priced.Where(id => !gathered.Contains(id)))
            {
                issues.Add($"resources.json prices '{id}' which is not gathered from any zone");
            }

            foreach (var resource in data.Resources.Where(r => r.SellValue <= 0))
            {
                issues.Add($"Resource '{resource.Id}' has non-positive sellValue");
            }

            // Every gatherable names its gathering skill — nodes take their
            // skill from the resource (a zone can mix skills), and upgrade
            // effects target skills, so a missing/unknown one silently breaks
            // yield targeting.
            foreach (var resource in data.Resources.Where(r => !KnownSkills.Contains(r.Skill)))
            {
                issues.Add($"Resource '{resource.Id}' has missing or unknown skill '{resource.Skill}'");
            }
        }

        private static void ValidateZones(GameData data, List<string> issues)
        {
            foreach (var duplicate in data.Zones.GroupBy(z => z.Order).Where(g => g.Count() > 1))
            {
                issues.Add($"Duplicate zone order {duplicate.Key}");
            }

            // The runtime seeds every fresh run from the fixed id, while this
            // validator proves reachability against the lowest-order zone —
            // the two must be the same zone or a rename/reorder ships green
            // and NewGame throws on first launch.
            var lowestOrder = data.Zones.OrderBy(z => z.Order).FirstOrDefault();
            if (!data.ZonesById.ContainsKey(GameData.StartingZoneId))
            {
                issues.Add($"Starting zone '{GameData.StartingZoneId}' does not exist in zones.json");
            }
            else if (lowestOrder != null && lowestOrder.Id != GameData.StartingZoneId)
            {
                issues.Add($"Starting zone '{GameData.StartingZoneId}' is not the lowest-order zone ('{lowestOrder.Id}' is)");
            }

            // Only the starting zone's unlocks are mechanically live (they
            // seed UnlockedSkills); a typo there silently severs everything
            // hanging off the skill. Later zones' unlocks are documentation —
            // a known divergence — so they stay unchecked.
            var startingZone = data.Zones.OrderBy(z => z.Order).FirstOrDefault();
            if (startingZone != null)
            {
                foreach (var skill in startingZone.Unlocks.Where(s => !KnownSkills.Contains(s)))
                {
                    issues.Add($"Starting zone '{startingZone.Id}' unlock '{skill}' is not a known skill");
                }
            }

            foreach (var zone in data.Zones)
            {
                if (zone.Resources.Count == 0)
                {
                    issues.Add($"Zone '{zone.Id}' has no resources");
                }

                if (!KnownScopes.Contains(zone.Scope))
                {
                    issues.Add($"Zone '{zone.Id}' has unknown scope '{zone.Scope}'");
                }

                // MVP zones host a verse of the Rite, so the site must be authored.
                if (zone.Scope == "mvp" && string.IsNullOrWhiteSpace(zone.VerseSite))
                {
                    issues.Add($"Zone '{zone.Id}' is mvp scope but has no verseSite");
                }
            }

            ValidateToolGating(data, issues);
        }

        /// <summary>
        /// The design §3 tool gate: zone requiredTool and upgrade toolTier must
        /// both name real tiers, and every demanded tier must be grantable by
        /// some upgrade — an ungrantable requirement walls off the zone (and
        /// everything behind it) forever.
        /// </summary>
        private static void ValidateToolGating(GameData data, List<string> issues)
        {
            var tiers = data.Economy?.Tools?.Tiers ?? new List<string>();
            var bestGrantable = -1;

            foreach (var upgrade in data.Upgrades)
            {
                if (string.IsNullOrEmpty(upgrade.ToolTier))
                {
                    continue;
                }

                var index = tiers.IndexOf(upgrade.ToolTier);
                if (index < 0)
                {
                    issues.Add($"Upgrade '{upgrade.Id}' toolTier '{upgrade.ToolTier}' is not in economy.tools.tiers");
                }
                else if (index > bestGrantable)
                {
                    bestGrantable = index;
                }
            }

            foreach (var zone in data.Zones)
            {
                if (string.IsNullOrEmpty(zone.RequiredTool))
                {
                    continue;
                }

                var index = tiers.IndexOf(zone.RequiredTool);
                if (index < 0)
                {
                    issues.Add($"Zone '{zone.Id}' requiredTool '{zone.RequiredTool}' is not in economy.tools.tiers");
                }
                else if (index > bestGrantable)
                {
                    issues.Add($"Zone '{zone.Id}' requiredTool '{zone.RequiredTool}' can never be met — no upgrade grants that tier");
                }
            }
        }

        private static void ValidateRecipes(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var upgradeUnlocked = new HashSet<string>(data.Upgrades
                .SelectMany(u => u.Effects)
                .Where(e => e.Type == EffectType.UnlockRecipe && e.Recipe != null)
                .Select(e => e.Recipe));

            // What a run can actually open: the starting (lowest-order) zone's
            // unlocks plus every upgrade-granted skill.
            var unlockableSkills = new HashSet<string>(
                data.Zones.OrderBy(z => z.Order).Select(z => z.Unlocks).FirstOrDefault() ?? new List<string>());
            unlockableSkills.UnionWith(data.Upgrades
                .SelectMany(u => u.Effects)
                .Where(e => e.Type == EffectType.UnlockSkill && e.Skill != null)
                .Select(e => e.Skill));

            foreach (var recipe in data.Recipes)
            {
                // Reachability: every recipe needs exactly one acquisition path.
                if (!recipe.DefaultKnown && !upgradeUnlocked.Contains(recipe.Id))
                {
                    issues.Add($"Recipe '{recipe.Id}' is neither defaultKnown nor unlocked by any upgrade");
                }
                else if (recipe.DefaultKnown && upgradeUnlocked.Contains(recipe.Id))
                {
                    issues.Add($"Recipe '{recipe.Id}' is defaultKnown but also unlocked by an upgrade — pick one");
                }

                if (string.IsNullOrWhiteSpace(recipe.Output))
                {
                    issues.Add($"Recipe '{recipe.Id}' has no output");
                }

                if (recipe.Inputs.Count == 0)
                {
                    issues.Add($"Recipe '{recipe.Id}' has no inputs");
                }

                if (!KnownSkills.Contains(recipe.Skill))
                {
                    issues.Add($"Recipe '{recipe.Id}' has unknown skill '{recipe.Skill}'");
                }

                if (!KnownRecipeKinds.Contains(recipe.Kind))
                {
                    issues.Add($"Recipe '{recipe.Id}' has unknown kind '{recipe.Kind}' — expected 'material' or 'trade'");
                }

                if (recipe.ValueMult <= 0)
                {
                    issues.Add($"Recipe '{recipe.Id}' has non-positive valueMult");
                }

                foreach (var input in recipe.Inputs.Keys.Where(i => !resourceIds.Contains(i)))
                {
                    issues.Add($"Recipe '{recipe.Id}' input '{input}' is not gathered from any zone or produced by any recipe");
                }

                foreach (var input in recipe.Inputs.Where(kv => kv.Value <= 0))
                {
                    // Zero would craft trade goods from nothing (free Coin via
                    // valueMult); negative would CREDIT stock at batch start.
                    issues.Add($"Recipe '{recipe.Id}' input '{input.Key}' amount must be positive");
                }

                if (recipe.StationLevel < 1)
                {
                    issues.Add($"Recipe '{recipe.Id}' has stationLevel below 1");
                }

                if (recipe.SkillLevel < 1)
                {
                    issues.Add($"Recipe '{recipe.Id}' has skillLevel below 1");
                }

                // The XP clamp stops at maxLevel, so a gate above it is a
                // "visible goal" that can never be reached.
                if (data.Economy?.Xp != null && recipe.SkillLevel > data.Economy.Xp.MaxLevel)
                {
                    issues.Add($"Recipe '{recipe.Id}' skillLevel {recipe.SkillLevel} exceeds xp.maxLevel {data.Economy.Xp.MaxLevel} — unreachable forever");
                }

                // Existence isn't enough: at runtime only the starting zone's
                // unlocks and upgrade unlockSkill effects open skills (other
                // zones' unlocks lists are documentation — known divergence).
                // A recipe keyed to a never-granted skill is invisible forever.
                if (KnownSkills.Contains(recipe.Skill) && !unlockableSkills.Contains(recipe.Skill))
                {
                    issues.Add($"Recipe '{recipe.Id}' skill '{recipe.Skill}' is never unlockable — not in the starting zone's unlocks and no upgrade grants it");
                }
            }
        }

        /// <summary>
        /// Every recipe must be reachable from gathered leaves: fixpoint over
        /// "all inputs obtainable → output obtainable". Catches circular
        /// chains (A needs B, B needs A — including self-inputs), which pass
        /// referential checks but deadlock stations at runtime.
        /// </summary>
        private static void ValidateRecipeObtainability(GameData data, List<string> issues)
        {
            var obtainable = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            var pending = new List<RecipeDef>(data.Recipes);
            var grew = true;
            while (grew)
            {
                grew = false;
                for (var i = pending.Count - 1; i >= 0; i--)
                {
                    var recipe = pending[i];
                    if (recipe.Inputs.Keys.All(input => obtainable.Contains(input)))
                    {
                        if (recipe.Output != null)
                        {
                            obtainable.Add(recipe.Output);
                        }

                        pending.RemoveAt(i);
                        grew = true;
                    }
                }
            }

            foreach (var recipe in pending)
            {
                issues.Add($"Recipe '{recipe.Id}' can never be crafted — its inputs are not reachable from gathered resources (cycle or missing source)");
            }
        }

        private static readonly HashSet<string> KnownBuildingPerLevelTypes = new HashSet<string>
        {
            "stationSpeedBonus", "basketCapacityBonus", "comfort"
        };

        private static void ValidateBuildings(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var upgradeIds = new HashSet<string>(data.Upgrades.Select(u => u.Id));
            var stations = new HashSet<string>(data.Recipes.Select(r => r.Station).Where(s => s != null));

            foreach (var building in data.Buildings)
            {
                // Money→XP (design §9): building levels cost a material bundle,
                // not Coin. A line with no bundle could never be levelled.
                if (building.Materials == null || building.Materials.Count == 0)
                {
                    issues.Add($"Building '{building.Id}' has no material cost bundle");
                }
                else
                {
                    foreach (var material in building.Materials.Keys.Where(m => !resourceIds.Contains(m)))
                    {
                        issues.Add($"Building '{building.Id}' material '{material}' is not gathered from any zone or produced by any recipe");
                    }

                    foreach (var material in building.Materials.Where(kv => kv.Value <= 0))
                    {
                        issues.Add($"Building '{building.Id}' material '{material.Key}' amount must be positive");
                    }
                }

                foreach (var milestone in building.MilestoneUpgradeIds.Where(m => !upgradeIds.Contains(m)))
                {
                    issues.Add($"Building '{building.Id}' milestone upgrade '{milestone}' does not exist");
                }

                if (building.PerLevel == null || !KnownBuildingPerLevelTypes.Contains(building.PerLevel.Type))
                {
                    issues.Add($"Building '{building.Id}' has missing or unknown perLevel type '{building.PerLevel?.Type}'");
                    continue;
                }

                if (building.PerLevel.Type == "stationSpeedBonus" && !stations.Contains(building.PerLevel.Station))
                {
                    issues.Add($"Building '{building.Id}' stationSpeedBonus targets unknown station '{building.PerLevel.Station}'");
                }

                if (building.PerLevel.Value <= 0)
                {
                    issues.Add($"Building '{building.Id}' perLevel value must be positive");
                }
            }

            // Every recipe station that gates on a building line must have that
            // line authored — a station line named after a station a recipe
            // uses is how the gate binds.
            foreach (var recipe in data.Recipes.Where(r => r.StationLevel > 1))
            {
                if (data.Buildings.All(b => b.Id != recipe.Station))
                {
                    issues.Add($"Recipe '{recipe.Id}' needs station '{recipe.Station}' level {recipe.StationLevel} but no building line has that id");
                }
            }

            // Once any building lines exist, EVERY recipe station must match
            // one: a typo'd station id doesn't break the recipe — it silently
            // deletes the station gate (StationLevelMet treats an unclaimed
            // station as ungated, a concession to hand-built test data).
            if (data.Buildings.Count > 0)
            {
                foreach (var recipe in data.Recipes.Where(r => r.Station != null && data.Buildings.All(b => b.Id != r.Station)))
                {
                    issues.Add($"Recipe '{recipe.Id}' station '{recipe.Station}' matches no building line — its station gate would silently vanish");
                }
            }
        }

        private static void ValidateUpgrades(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var upgrade in data.Upgrades)
            {
                // Money→XP (design §9): the cost is a skill gate + materials, no Coin.
                if (!string.IsNullOrEmpty(upgrade.GateSkill) && !KnownSkills.Contains(upgrade.GateSkill))
                {
                    issues.Add($"Upgrade '{upgrade.Id}' gateSkill '{upgrade.GateSkill}' is unknown");
                }

                if (upgrade.GateLevel < 0)
                {
                    issues.Add($"Upgrade '{upgrade.Id}' gateLevel must not be negative");
                }

                foreach (var material in upgrade.Materials.Keys.Where(m => !resourceIds.Contains(m)))
                {
                    issues.Add($"Upgrade '{upgrade.Id}' material '{material}' is not gathered from any zone or produced by any recipe");
                }

                foreach (var material in upgrade.Materials.Where(kv => kv.Value <= 0))
                {
                    issues.Add($"Upgrade '{upgrade.Id}' material '{material.Key}' amount must be positive");
                }

                foreach (var effect in upgrade.Effects)
                {
                    ValidateEffect($"Upgrade '{upgrade.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateGear(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var gear in data.Gear)
            {
                if (!KnownSkills.Contains(gear.Skill))
                {
                    issues.Add($"Gear '{gear.Id}' has unknown skill '{gear.Skill}'");
                }

                foreach (var material in gear.Materials.Keys.Where(m => !resourceIds.Contains(m)))
                {
                    issues.Add($"Gear '{gear.Id}' material '{material}' is not gathered from any zone or produced by any recipe");
                }

                foreach (var material in gear.Materials.Where(kv => kv.Value <= 0))
                {
                    issues.Add($"Gear '{gear.Id}' material '{material.Key}' amount must be positive");
                }

                foreach (var effect in gear.Effects)
                {
                    ValidateEffect($"Gear '{gear.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateInsects(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var insect in data.Insects)
            {
                if (insect.Sketches <= 0)
                {
                    issues.Add($"Insect '{insect.Id}' has non-positive sketch count");
                }

                if (insect.Rarity <= 0 || insect.Rarity > 1)
                {
                    issues.Add($"Insect '{insect.Id}' rarity {insect.Rarity} is outside (0, 1]");
                }

                if (insect.Habitats.Count == 0)
                {
                    issues.Add($"Insect '{insect.Id}' has no habitats");
                }

                foreach (var site in insect.Habitats)
                {
                    if (!data.ZonesById.TryGetValue(site, out var zone))
                    {
                        issues.Add($"Insect '{insect.Id}' references unknown zone '{site}'");
                    }
                    else if (!zone.DigSite)
                    {
                        issues.Add($"Insect '{insect.Id}' references zone '{site}' which has no observation site");
                    }
                }

                foreach (var effect in insect.Effects)
                {
                    ValidateEffect($"Insect '{insect.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateAlmanac(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var node in data.Almanac)
            {
                if (node.CostVerdure <= 0)
                {
                    issues.Add($"Almanac node '{node.Id}' must cost Verdure");
                }

                if (!string.IsNullOrEmpty(node.Requires) && !data.AlmanacById.ContainsKey(node.Requires))
                {
                    issues.Add($"Almanac node '{node.Id}' requires unknown node '{node.Requires}'");
                }

                foreach (var effect in node.Effects)
                {
                    ValidateEffect($"Almanac node '{node.Id}'", effect, data, resourceIds, issues);
                }
            }

            // The requires chain must ground out — a cycle makes every node in
            // it unbuyable forever.
            foreach (var node in data.Almanac)
            {
                var visited = new HashSet<string>();
                var current = node;
                while (current != null && !string.IsNullOrEmpty(current.Requires))
                {
                    if (!visited.Add(current.Id))
                    {
                        issues.Add($"Almanac node '{node.Id}' sits in a requires cycle");
                        break;
                    }

                    data.AlmanacById.TryGetValue(current.Requires, out current);
                }
            }
        }

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
            "nodeYieldBonus", "trailThroughputBonus", "pristineBonus", "digSpeedBonus"
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

            foreach (var species in data.Species)
            {
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

        // The planter kinds the sim knows how to apply (Planters.cs) — a typo'd
        // kind would build a planter that does nothing.
        private static readonly HashSet<string> KnownPlanterKinds = new HashSet<string>
        {
            "basketCapacityMult", "nodeYieldMult", "digSpeedMult"
        };

        private static readonly HashSet<string> KnownPlanterTargets = new HashSet<string> { "node", "digSite" };

        private static void ValidatePlanters(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            CheckIds(data.Planters.Select(p => p.Id), "planter", issues);

            foreach (var planter in data.Planters)
            {
                if (!KnownPlanterKinds.Contains(planter.Kind))
                {
                    issues.Add($"Planter '{planter.Id}' has unknown kind '{planter.Kind}'");
                }

                if (!KnownPlanterTargets.Contains(planter.Target))
                {
                    issues.Add($"Planter '{planter.Id}' has unknown target '{planter.Target}'");
                }

                // A dig site is sketched, not gathered into a basket — a
                // capacity/yield planter on a dig site would do nothing.
                if (planter.Target == "digSite" && planter.Kind != "digSpeedMult")
                {
                    issues.Add($"Planter '{planter.Id}' targets a dig site but its kind '{planter.Kind}' only applies to gather nodes");
                }

                if (planter.Target == "node" && planter.Kind == "digSpeedMult")
                {
                    issues.Add($"Planter '{planter.Id}' targets a gather node but digSpeedMult only applies to dig sites");
                }

                if (planter.Value <= 0.0)
                {
                    issues.Add($"Planter '{planter.Id}' must have a positive value");
                }

                if (planter.Materials == null || planter.Materials.Count == 0)
                {
                    issues.Add($"Planter '{planter.Id}' has no material cost bundle");
                }
                else
                {
                    foreach (var material in planter.Materials.Keys.Where(m => !resourceIds.Contains(m)))
                    {
                        issues.Add($"Planter '{planter.Id}' material '{material}' is not gathered from any zone or produced by any recipe");
                    }

                    foreach (var material in planter.Materials.Where(kv => kv.Value <= 0))
                    {
                        issues.Add($"Planter '{planter.Id}' material '{material.Key}' amount must be positive");
                    }
                }
            }
        }

        private static void ValidateExchange(GameData data, List<string> issues)
        {
            if (data.Exchange == null)
            {
                issues.Add("Exchange config is missing");
                return;
            }

            if (data.Exchange.Spread < 0.0 || data.Exchange.Spread >= 1.0)
            {
                // A spread ≥ 1 makes every trade return nothing; negative mints goods.
                issues.Add("Exchange spread must be in [0, 1)");
            }
        }

        private static void ValidateFolio(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var gathered = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            foreach (var spread in data.Spreads)
            {
                if (spread.Entries.Count == 0)
                {
                    issues.Add($"Folio spread '{spread.Id}' has no entries");
                }

                foreach (var entry in spread.Entries)
                {
                    // Pristine specimens only come from haul batches, so a spread
                    // entry must be a GATHERED resource — a crafted good could
                    // never be fixed into the Folio.
                    if (!gathered.Contains(entry))
                    {
                        issues.Add($"Folio spread '{spread.Id}' entry '{entry}' is not a gathered resource");
                    }
                }

                foreach (var effect in spread.Effects)
                {
                    ValidateEffect($"Folio spread '{spread.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateRites(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            if (data.Rites == null)
            {
                issues.Add("Rites data is missing");
                return;
            }

            CheckIds(data.Rites.Rites.Select(r => r.Id), "rite", issues);
            CheckIds(data.Rites.Rites.SelectMany(r => r.Verses).Select(v => v.Id), "verse", issues);

            // Resource offerings credit Renown at trade value (design §7) —
            // and only gathered raws and trade-kind recipe outputs have one.
            // A material offering must carry its own renownGrant instead.
            var tradeValued = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            tradeValued.UnionWith(data.Recipes
                .Where(r => r.Kind == "trade" && r.Output != null)
                .Select(r => r.Output));

            if (data.Rites.ChooseCount <= 0)
            {
                issues.Add("Rites chooseCount must be positive");
            }

            var generator = data.Rites.Generator;
            if (generator != null)
            {
                if (generator.DemandGrowth <= 1.0)
                {
                    issues.Add("Rites generator demandGrowth must exceed 1 — each Rite must ask more than the last");
                }

                if (generator.SpotlightDiscount <= 0.0 || generator.SpotlightDiscount > 1.0)
                {
                    issues.Add("Rites generator spotlightDiscount must be in (0, 1] — the spotlight is the cheap path");
                }

                if (generator.OffSpotlightPremium < 1.0)
                {
                    issues.Add("Rites generator offSpotlightPremium must be at least 1 — off-spotlight grinds at a premium");
                }
            }

            // CurrentRite takes the FIRST rite matching a migration index —
            // a duplicate silently shadows its twin forever.
            foreach (var duplicate in data.Rites.Rites.GroupBy(r => r.Migration).Where(g => g.Count() > 1))
            {
                issues.Add($"Duplicate rite migration index {duplicate.Key} — only the first is ever served");
            }

            // A verse only accepts offerings once its zone is unlocked, and
            // the Rite needs EVERY verse complete — a verse keyed to a zone
            // no trail map opens would block Migration permanently.
            var unlockableZones = new HashSet<string> { GameData.StartingZoneId };
            unlockableZones.UnionWith(data.Upgrades
                .SelectMany(u => u.Effects)
                .Where(e => e.Type == EffectType.UnlockZone && e.Zone != null)
                .Select(e => e.Zone));

            foreach (var rite in data.Rites.Rites)
            {
                if (rite.Migration < 0)
                {
                    issues.Add($"Rite '{rite.Id}' has negative migration index");
                }

                if (rite.Verses.Count == 0)
                {
                    issues.Add($"Rite '{rite.Id}' has no verses");
                }

                foreach (var verse in rite.Verses)
                {
                    if (verse.Zone == null || !data.ZonesById.ContainsKey(verse.Zone))
                    {
                        issues.Add($"Verse '{verse.Id}' references unknown zone '{verse.Zone}'");
                    }
                    else if (!unlockableZones.Contains(verse.Zone))
                    {
                        issues.Add($"Verse '{verse.Id}' zone '{verse.Zone}' is never unlockable — no upgrade grants it, so the Rite could never complete");
                    }

                    // The choose-N-of-M safety valve only exists if M > N.
                    if (verse.Slots.Count <= data.Rites.ChooseCount)
                    {
                        issues.Add($"Verse '{verse.Id}' has {verse.Slots.Count} slots but chooseCount is {data.Rites.ChooseCount} — no slot choice left");
                    }

                    foreach (var skill in verse.Spotlight.Where(s => !KnownSkills.Contains(s)))
                    {
                        issues.Add($"Verse '{verse.Id}' spotlights unknown skill '{skill}'");
                    }

                    foreach (var slot in verse.Slots)
                    {
                        ValidateRiteSlot(verse.Id, slot, resourceIds, tradeValued, issues);
                    }
                }
            }
        }

        private static void ValidateRiteSlot(string verseId, RiteSlotDef slot, HashSet<string> resourceIds,
            HashSet<string> tradeValued, List<string> issues)
        {
            switch (slot.Type)
            {
                case RiteSlotType.Resource:
                    if (slot.Resource == null || !resourceIds.Contains(slot.Resource))
                    {
                        issues.Add($"Verse '{verseId}' resource slot references '{slot.Resource}' which is not gathered from any zone or produced by any recipe");
                    }
                    else if (!tradeValued.Contains(slot.Resource) && slot.RenownGrant <= 0)
                    {
                        // Materials price at zero, so without an explicit grant
                        // the slot would credit no Renown — taxing prestige.
                        issues.Add($"Verse '{verseId}' resource slot '{slot.Resource}' is a material with no trade value — it needs an explicit renownGrant");
                    }

                    if (slot.Amount <= 0)
                    {
                        issues.Add($"Verse '{verseId}' resource slot needs a positive amount");
                    }

                    break;

                case RiteSlotType.Deed:
                    if (string.IsNullOrWhiteSpace(slot.Deed))
                    {
                        issues.Add($"Verse '{verseId}' deed slot names no deed");
                    }
                    else if (!KnownDeeds.Contains(slot.Deed))
                    {
                        issues.Add($"Verse '{verseId}' deed slot names '{slot.Deed}', which the sim never records — it could never fill");
                    }

                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                case RiteSlotType.Specimen:
                    if (slot.Quality == null || !KnownSpecimenQualities.Contains(slot.Quality))
                    {
                        issues.Add($"Verse '{verseId}' specimen slot has unknown quality '{slot.Quality}'");
                    }

                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                case RiteSlotType.Sketch:
                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                default:
                    issues.Add($"Verse '{verseId}' has slot type '{slot.Type}' with no validation rule");
                    break;
            }
        }

        private static void RequireCountAndGrant(string verseId, RiteSlotDef slot, List<string> issues)
        {
            if (slot.Count <= 0)
            {
                issues.Add($"Verse '{verseId}' {slot.Type} slot needs a positive count");
            }

            // Non-resource offerings have no trade value, so the fixed grant is
            // how they credit Renown (design doc §7) — zero would tax prestige.
            if (slot.RenownGrant <= 0)
            {
                issues.Add($"Verse '{verseId}' {slot.Type} slot needs a positive renownGrant");
            }
        }

        private static void ValidateDialogue(GameData data, List<string> issues)
        {
            if (data.Dialogue == null)
            {
                issues.Add("Dialogue data is missing");
                return;
            }

            foreach (var key in data.Dialogue.Waystones.Keys.Where(k => !data.ZonesById.ContainsKey(k)))
            {
                issues.Add($"Waystone text references unknown zone '{key}'");
            }

            foreach (var key in data.Dialogue.Verses.Keys.Where(k => !data.ZonesById.ContainsKey(k)))
            {
                issues.Add($"Verse text references unknown zone '{key}'");
            }

            foreach (var key in data.Dialogue.InsectPlates.Keys.Where(k => !data.InsectsById.ContainsKey(k)))
            {
                issues.Add($"Insect plate text references unknown insect '{key}'");
            }

            // The MVP zones ship with their words: a waystone that reveals
            // nothing on arrival, or a verse with no line at its site, is a
            // hole the player walks into. Later-scope zones may stay silent
            // until their content pass.
            foreach (var zone in data.Zones.Where(z => z.Scope == "mvp"))
            {
                if (!data.Dialogue.Waystones.TryGetValue(zone.Id, out var waystone)
                    || string.IsNullOrWhiteSpace(waystone))
                {
                    issues.Add($"MVP zone '{zone.Id}' has no waystone text");
                }

                if (!data.Dialogue.Verses.TryGetValue(zone.Id, out var verse)
                    || string.IsNullOrWhiteSpace(verse))
                {
                    issues.Add($"MVP zone '{zone.Id}' has no verse text");
                }
            }
        }

        private static void ValidateEconomy(EconomyConfig economy, List<string> issues)
        {
            if (economy == null)
            {
                issues.Add("Economy config is missing");
                return;
            }

            RequireSection(economy.CostGrowth, "costGrowth", issues);
            RequireSection(economy.Gifts, "gifts", issues);
            RequireSection(economy.Hauling, "hauling", issues);
            RequireSection(economy.Kith, "kith", issues);
            RequireSection(economy.Crafting, "crafting", issues);
            RequireSection(economy.Tools, "tools", issues);
            RequireSection(economy.Mastery, "mastery", issues);
            RequireSection(economy.Verdure, "verdure", issues);
            RequireSection(economy.Xp, "xp", issues);
            RequireSection(economy.Offline, "offline", issues);
            RequireSection(economy.Quality, "quality", issues);
            RequireSection(economy.Observation, "observation", issues);
            RequireSection(economy.Tending, "tending", issues);
            RequireSection(economy.Warden, "warden", issues);
            RequireSection(economy.FamiliarXp, "familiarXp", issues);
            RequireSection(economy.Replant, "replant", issues);

            if (economy.CostGrowth != null && economy.CostGrowth.Building <= 1)
            {
                issues.Add("Economy costGrowth factors must all be > 1");
            }

            if (economy.Gifts != null && economy.Gifts.PileGoods <= 0)
            {
                issues.Add("Economy gifts.pileGoods must be positive");
            }


            if (economy.Warden != null && economy.Warden.GatherPerSecond <= 0)
            {
                // Not merely a tuning value: the gift pile costs the node's own
                // resource, and a bare node's only source is the warden's hands.
                issues.Add("Economy warden.gatherPerSecond must be positive or bare nodes can never afford their first gift");
            }

            if (economy.Hauling != null
                && (economy.Hauling.BaseCarryCapacity <= 0 || economy.Hauling.TripSeconds <= 0 || economy.Hauling.BasketCapacity <= 0))
            {
                issues.Add("Economy hauling values must all be positive");
            }

            if (economy.Kith != null
                && (economy.Kith.SlotsBase <= 0 || economy.Kith.SlotsMax < economy.Kith.SlotsBase))
            {
                issues.Add("Economy kith needs a positive slotsBase and slotsMax >= slotsBase");
            }

            if (economy.Kith != null)
            {
                var milestones = economy.Kith.VerseMilestones;
                if (milestones == null || milestones.Count == 0)
                {
                    issues.Add("Economy kith.verseMilestones is empty — the earned slots need their verse counts");
                }
                else
                {
                    for (var i = 0; i < milestones.Count; i++)
                    {
                        if (milestones[i] <= 0 || (i > 0 && milestones[i] <= milestones[i - 1]))
                        {
                            issues.Add("Economy kith.verseMilestones must be positive and strictly ascending");
                            break;
                        }
                    }

                    // The ladder must land exactly on the ceiling: base + earned
                    // milestones + the two store purchases (§4).
                    if (economy.Kith.SlotsBase + milestones.Count + 2 != economy.Kith.SlotsMax)
                    {
                        issues.Add("Economy kith ladder is off: slotsBase + verseMilestones + 2 purchasable must equal slotsMax");
                    }
                }

                if (economy.Kith.GeneratorGatherPosts <= 0)
                {
                    issues.Add("Economy kith.generatorGatherPosts must be positive — the Rite generator's reachability proof needs it");
                }
            }

            if (economy.Crafting != null && economy.Crafting.BaseCraftSeconds <= 0)
            {
                issues.Add("Economy crafting.baseCraftSeconds must be positive");
            }

            if (economy.Tools != null && (economy.Tools.Tiers == null || economy.Tools.Tiers.Count == 0))
            {
                issues.Add("Economy tools.tiers is empty");
            }

            if (economy.Tools != null
                && (economy.Tools.BaseCostCoin <= 0 || economy.Tools.CostMultPerTier <= 1
                    || economy.Tools.YieldMultPerTier <= 1))
            {
                // Cost ≤ 0 makes tiers free; a mult ≤ 1 makes each tier cheaper
                // or weaker than the last — the ladder inverts.
                issues.Add("Economy tools progression is degenerate");
            }

            if (economy.Xp != null && (economy.Xp.Base <= 0 || economy.Xp.Growth <= 1 || economy.Xp.MaxLevel <= 1))
            {
                // Base ≤ 0 means every rung costs nothing — all skills read
                // max level at zero XP and every skillLevel gate falls open.
                issues.Add("Economy xp progression is degenerate");
            }

            if (economy.Xp != null && (economy.Xp.GatherPerUnit < 0 || economy.Xp.CraftPerBatch < 0))
            {
                issues.Add("Economy xp gains must not be negative");
            }

            if (economy.FamiliarXp != null
                && (economy.FamiliarXp.Base <= 0 || economy.FamiliarXp.Growth <= 1 || economy.FamiliarXp.MaxLevel <= 1))
            {
                issues.Add("Economy familiarXp progression is degenerate");
            }

            if (economy.FamiliarXp != null && economy.FamiliarXp.XpPerSecond < 0)
            {
                issues.Add("Economy familiarXp.xpPerSecond must not be negative");
            }

            if (economy.FamiliarXp != null && economy.FamiliarXp.KinshipDivisor <= 0)
            {
                // A non-positive divisor makes Kinship.Fold silently ignore the
                // authored tuning and fall back to a hardcoded constant.
                issues.Add("Economy familiarXp.kinshipDivisor must be positive");
            }

            if (economy.Replant != null
                && (economy.Replant.BaseCost <= 0 || economy.Replant.Growth <= 1 || economy.Replant.RichnessPerLevel <= 0))
            {
                // BaseCost ≤ 0 makes richness free; growth ≤ 1 never escalates; a
                // non-positive richnessPerLevel makes replanting do nothing.
                issues.Add("Economy replant is degenerate (baseCost > 0, growth > 1, richnessPerLevel > 0)");
            }

            if (economy.Mastery != null
                && (economy.Mastery.Base <= 0 || economy.Mastery.Growth <= 1
                    || economy.Mastery.MaxLevel < 1 || economy.Mastery.XpPerUnit <= 0))
            {
                issues.Add("Economy mastery progression is degenerate");
            }

            if (economy.Mastery != null && economy.Mastery.YieldBonusPerLevel < 0)
            {
                issues.Add("Economy mastery.yieldBonusPerLevel must not be negative");
            }

            if (economy.Verdure != null && (economy.Verdure.RenownDivisor <= 0 || economy.Verdure.Exponent <= 0))
            {
                // renownDivisor ≤ 0 makes Migration.VerdureAfterMigration bank
                // zero Verdure every fold — the permanent-progression currency
                // dies. exponent 0 makes Pow(x, 0) = 1, so every fold banks
                // exactly one point regardless of lifetime Renown.
                issues.Add("Economy verdure.renownDivisor and verdure.exponent must both be positive");
            }

            if (economy.Verdure != null && economy.Verdure.YieldBonusPerPoint < 0)
            {
                issues.Add("Economy verdure.yieldBonusPerPoint must not be negative");
            }

            if (economy.Offline != null && economy.Offline.BaseCapHours <= 0)
            {
                issues.Add("Economy offline.baseCapHours must be positive");
            }

            if (economy.Offline != null && economy.Offline.RateMultiplier <= 0)
            {
                // The sim multiplies every offline second by this — zero
                // silently voids all offline earnings.
                issues.Add("Economy offline.rateMultiplier must be positive");
            }

            if (economy.Quality != null
                && (!IsChance(economy.Quality.FineChance) || !IsChance(economy.Quality.PristineBaseChance)))
            {
                issues.Add("Economy quality chances must be within [0, 1]");
            }

            if (economy.Quality != null && economy.Quality.FineChance + economy.Quality.PristineBaseChance > 1)
            {
                // The two rolls share one [0,1) draw — together they must
                // leave room for Common.
                issues.Add("Economy quality chances must not sum above 1");
            }

            if (economy.Quality != null
                && (economy.Quality.FineValueMult <= 0 || economy.Quality.PristineValueMult <= 0))
            {
                issues.Add("Economy quality value multipliers must be positive");
            }

            if (economy.Tending != null && economy.Tending.PristineChanceBonus < 0)
            {
                issues.Add("Economy tending.pristineChanceBonus must not be negative");
            }

            if (economy.Tending != null && economy.Tending.BurstYieldMult <= 0)
            {
                // The sim multiplies a node's yield by this during the tend-burst
                // window — omitted/zero makes a freshly tended node yield nothing
                // for the burst, worse than leaving it alone.
                issues.Add("Economy tending.burstYieldMult must be positive");
            }

            if (economy.Tending != null && (economy.Tending.BurstDurationSec < 0 || economy.Tending.PristineBonusDurationSec < 0))
            {
                issues.Add("Economy tending burst/pristine durations must not be negative");
            }

            if (economy.Bubbles != null
                && (economy.Bubbles.SpawnIntervalSec <= 0 || economy.Bubbles.LifetimeSec <= 0
                    || economy.Bubbles.MaxLive <= 0 || economy.Bubbles.RewardSeconds <= 0))
            {
                // A present-but-zeroed section either never spawns a bubble or
                // spawns ones worth nothing — configure it whole or not at all.
                issues.Add("Economy bubbles values must all be positive");
            }

            if (economy.Observation != null
                && (economy.Observation.PityTimerHoursWatched <= 0 || economy.Observation.BaseSketchesPerHour <= 0))
            {
                // Zero rate AND zero pity means an observation site can never
                // surface a field sketch — every insect plate becomes unreachable.
                issues.Add("Economy observation values must all be positive");
            }

            if (economy.Amber != null
                && (economy.Amber.DigFindsPerHour <= 0 || economy.Amber.PerFind <= 0
                    || economy.Amber.TimeSkipHours <= 0 || economy.Amber.TimeSkipCostAmber <= 0
                    || economy.Amber.AdDripAmber <= 0 || economy.Amber.WeeklyCacheAmber <= 0
                    || economy.Amber.RenameCostAmber <= 0))
            {
                // A present-but-zeroed section would ship an earn with no sink
                // (or a sink no one can afford) — configure it whole or not at all.
                issues.Add("Economy amber values must all be positive");
            }

            if (economy.Store != null
                && (economy.Store.StarterBundleAmber <= 0
                    || economy.Store.AmberPackSmall <= 0 || economy.Store.AmberPackLarge <= 0))
            {
                issues.Add("Economy store amber values must all be positive — the bundle and packs promise piles, not pebbles");
            }
        }

        private static void RequireSection(object section, string name, List<string> issues)
        {
            if (section == null)
            {
                issues.Add($"Economy section '{name}' is missing");
            }
        }

        private static bool IsChance(double value)
        {
            return value >= 0 && value <= 1;
        }

        private static void ValidateEffect(string owner, EffectDef effect, GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            switch (effect.Type)
            {
                case EffectType.YieldMult:
                case EffectType.YieldBonus:
                case EffectType.CraftSpeedMult:
                    RequirePositiveValue(owner, effect, issues);
                    // A target-less craftSpeedMult is global — the sim applies
                    // it to every skill's recipes (Patient Hands). Yield
                    // effects still need a target: the sim would silently
                    // apply a bare one to nothing.
                    if (effect.Skill == null && effect.Zone == null && effect.Type != EffectType.CraftSpeedMult)
                    {
                        issues.Add($"{owner} {effect.Type} effect targets neither a skill nor a zone");
                    }

                    if (effect.Skill != null && !KnownSkills.Contains(effect.Skill) && !SkillWildcards.Contains(effect.Skill))
                    {
                        issues.Add($"{owner} references unknown skill '{effect.Skill}'");
                    }

                    if (effect.Zone != null && !data.ZonesById.ContainsKey(effect.Zone))
                    {
                        issues.Add($"{owner} references unknown zone '{effect.Zone}'");
                    }

                    break;

                case EffectType.HaulMult:
                case EffectType.DigSpeedMult:
                case EffectType.FolioSpreadBonusMult:
                case EffectType.PristineChanceBonus:
                case EffectType.OfflineCapHours:
                case EffectType.OfflineCapBonusHours:
                case EffectType.TendingBurstBonus:
                case EffectType.CarrierCapacityBonus:
                    RequirePositiveValue(owner, effect, issues);
                    break;

                case EffectType.SellValueBonus:
                    RequirePositiveValue(owner, effect, issues);
                    RequireKnownResource(owner, effect, resourceIds, issues);
                    break;

                case EffectType.NoSpoilage:
                    RequireKnownResource(owner, effect, resourceIds, issues);
                    break;

                case EffectType.UnlockZone:
                    if (effect.Zone == null || !data.ZonesById.ContainsKey(effect.Zone))
                    {
                        issues.Add($"{owner} unlocks unknown zone '{effect.Zone}'");
                    }

                    break;

                case EffectType.UnlockDigSite:
                    if (effect.Zone == null || !data.ZonesById.TryGetValue(effect.Zone, out var digZone))
                    {
                        issues.Add($"{owner} unlocks dig site in unknown zone '{effect.Zone}'");
                    }
                    else if (!digZone.DigSite)
                    {
                        issues.Add($"{owner} unlocks dig site in zone '{effect.Zone}' which has none");
                    }

                    break;

                case EffectType.UnlockSkill:
                    if (effect.Skill == null || !KnownSkills.Contains(effect.Skill))
                    {
                        issues.Add($"{owner} unlocks unknown skill '{effect.Skill}'");
                    }

                    break;

                case EffectType.UnlockRecipe:
                    if (effect.Recipe == null || !data.RecipesById.ContainsKey(effect.Recipe))
                    {
                        issues.Add($"{owner} unlocks unknown recipe '{effect.Recipe}'");
                    }

                    break;

                case EffectType.UnlockVerdureForecast:
                case EffectType.OfflineNightFullRate:
                    break;

                case EffectType.RecruitSpecies:
                    if (effect.Species == null || !data.SpeciesById.ContainsKey(effect.Species))
                    {
                        issues.Add($"{owner} recruits unknown species '{effect.Species}'");
                    }

                    break;

                default:
                    // A new EffectType was added to the enum but not given a rule here.
                    issues.Add($"{owner} has effect type '{effect.Type}' with no validation rule");
                    break;
            }
        }

        private static void RequirePositiveValue(string owner, EffectDef effect, List<string> issues)
        {
            if (!effect.Value.HasValue || effect.Value.Value <= 0)
            {
                issues.Add($"{owner} {effect.Type} effect needs a positive value");
            }
        }

        private static void RequireKnownResource(string owner, EffectDef effect, HashSet<string> resourceIds, List<string> issues)
        {
            if (effect.Resource == null || !resourceIds.Contains(effect.Resource))
            {
                issues.Add($"{owner} references unknown resource '{effect.Resource}'");
            }
        }
    }
}
