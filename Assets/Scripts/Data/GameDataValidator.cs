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
            CheckIds(data.Fossils.Select(f => f.Id), "fossil", issues);
            CheckIds(data.Almanac.Select(a => a.Id), "almanac node", issues);
            CheckIds(data.MuseumSets.Select(m => m.Id), "museum set", issues);

            // Everything obtainable: gathered from a zone or produced by a recipe.
            var resourceIds = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            resourceIds.UnionWith(data.Recipes.Select(r => r.Output).Where(o => o != null));

            ValidateResources(data, issues);
            ValidateZones(data, issues);
            ValidateRecipes(data, resourceIds, issues);
            ValidateRecipeObtainability(data, issues);
            ValidateBuildings(data, issues);
            ValidateUpgrades(data, resourceIds, issues);
            ValidateGear(data, resourceIds, issues);
            ValidateFossils(data, resourceIds, issues);
            ValidateAlmanac(data, resourceIds, issues);
            ValidateMuseum(data, resourceIds, issues);
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
            "stationSpeedBonus", "basketCapacityBonus", "familiarCaps"
        };

        private static void ValidateBuildings(GameData data, List<string> issues)
        {
            var upgradeIds = new HashSet<string>(data.Upgrades.Select(u => u.Id));
            var stations = new HashSet<string>(data.Recipes.Select(r => r.Station).Where(s => s != null));

            foreach (var building in data.Buildings)
            {
                if (building.BaseCostCoin <= 0)
                {
                    issues.Add($"Building '{building.Id}' has non-positive baseCostCoin");
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

                if (building.PerLevel.Type != "familiarCaps" && building.PerLevel.Value <= 0)
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
                if (upgrade.CostCoin <= 0)
                {
                    issues.Add($"Upgrade '{upgrade.Id}' has non-positive costCoin");
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

                    // Trail-map price must agree with the zone's own mapCostCoin.
                    if (effect.Type == EffectType.UnlockZone
                        && data.ZonesById.TryGetValue(effect.Zone ?? string.Empty, out var zone)
                        && zone.MapCostCoin.HasValue
                        && zone.MapCostCoin.Value != upgrade.CostCoin)
                    {
                        issues.Add($"Upgrade '{upgrade.Id}' costs {upgrade.CostCoin} but zone '{zone.Id}' mapCostCoin is {zone.MapCostCoin.Value}");
                    }
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

        private static void ValidateFossils(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var fossil in data.Fossils)
            {
                if (fossil.Fragments <= 0)
                {
                    issues.Add($"Fossil '{fossil.Id}' has non-positive fragment count");
                }

                if (fossil.StrataRarity <= 0 || fossil.StrataRarity > 1)
                {
                    issues.Add($"Fossil '{fossil.Id}' strataRarity {fossil.StrataRarity} is outside (0, 1]");
                }

                if (fossil.DigSites.Count == 0)
                {
                    issues.Add($"Fossil '{fossil.Id}' has no dig sites");
                }

                foreach (var site in fossil.DigSites)
                {
                    if (!data.ZonesById.TryGetValue(site, out var zone))
                    {
                        issues.Add($"Fossil '{fossil.Id}' references unknown zone '{site}'");
                    }
                    else if (!zone.DigSite)
                    {
                        issues.Add($"Fossil '{fossil.Id}' references zone '{site}' which is not a dig site");
                    }
                }

                foreach (var effect in fossil.Effects)
                {
                    ValidateEffect($"Fossil '{fossil.Id}'", effect, data, resourceIds, issues);
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

        private static void ValidateMuseum(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var gathered = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            foreach (var set in data.MuseumSets)
            {
                if (set.Entries.Count == 0)
                {
                    issues.Add($"Museum set '{set.Id}' has no entries");
                }

                foreach (var entry in set.Entries)
                {
                    // Pristine specimens only come from haul batches, so a set
                    // entry must be a GATHERED resource — a crafted good could
                    // never be donated.
                    if (!gathered.Contains(entry))
                    {
                        issues.Add($"Museum set '{set.Id}' entry '{entry}' is not a gathered resource");
                    }
                }

                foreach (var effect in set.Effects)
                {
                    ValidateEffect($"Museum set '{set.Id}'", effect, data, resourceIds, issues);
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

                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                case RiteSlotType.Specimen:
                    if (slot.Quality == null || !KnownSpecimenQualities.Contains(slot.Quality))
                    {
                        issues.Add($"Verse '{verseId}' specimen slot has unknown quality '{slot.Quality}'");
                    }

                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                case RiteSlotType.Fragment:
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

            foreach (var key in data.Dialogue.FossilCards.Keys.Where(k => !data.FossilsById.ContainsKey(k)))
            {
                issues.Add($"Fossil card text references unknown fossil '{key}'");
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
            RequireSection(economy.FamiliarCaps, "familiarCaps", issues);
            RequireSection(economy.Crafting, "crafting", issues);
            RequireSection(economy.Tools, "tools", issues);
            RequireSection(economy.Mastery, "mastery", issues);
            RequireSection(economy.Verdure, "verdure", issues);
            RequireSection(economy.Xp, "xp", issues);
            RequireSection(economy.Offline, "offline", issues);
            RequireSection(economy.Quality, "quality", issues);
            RequireSection(economy.Excavation, "excavation", issues);
            RequireSection(economy.Tending, "tending", issues);

            if (economy.CostGrowth != null
                && (economy.CostGrowth.GathererGift <= 1 || economy.CostGrowth.CarrierGift <= 1 || economy.CostGrowth.Building <= 1))
            {
                issues.Add("Economy costGrowth factors must all be > 1");
            }

            if (economy.Gifts != null && (economy.Gifts.GathererBaseGoods <= 0 || economy.Gifts.CarrierBaseGoods <= 0))
            {
                issues.Add("Economy gift base costs must be positive");
            }

            if (economy.Tending != null && economy.Tending.HandGatherPerSecond <= 0)
            {
                // Not merely a tuning value: gatherer gifts cost the node's own
                // resource, and a bare node's only source is the warden's hands.
                issues.Add("Economy tending.handGatherPerSecond must be positive or bare nodes can never afford their first gift");
            }

            if (economy.Hauling != null
                && (economy.Hauling.BaseCarryCapacity <= 0 || economy.Hauling.TripSeconds <= 0 || economy.Hauling.BasketCapacity <= 0))
            {
                issues.Add("Economy hauling values must all be positive");
            }

            if (economy.FamiliarCaps != null
                && (economy.FamiliarCaps.FlockCapBase <= 0 || economy.FamiliarCaps.FlockCapPerRoostLevel <= 0
                    || economy.FamiliarCaps.CarrierSlotsBase <= 0 || economy.FamiliarCaps.CarrierSlotsPerRoostLevel <= 0))
            {
                issues.Add("Economy familiarCaps values must all be positive");
            }

            if (economy.Crafting != null && economy.Crafting.BaseCraftSeconds <= 0)
            {
                issues.Add("Economy crafting.baseCraftSeconds must be positive");
            }

            if (economy.Tools != null && (economy.Tools.Tiers == null || economy.Tools.Tiers.Count == 0))
            {
                issues.Add("Economy tools.tiers is empty");
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

            if (economy.Mastery != null
                && (economy.Mastery.Base <= 0 || economy.Mastery.Growth <= 1
                    || economy.Mastery.MaxLevel < 1 || economy.Mastery.XpPerUnit <= 0))
            {
                issues.Add("Economy mastery progression is degenerate");
            }

            if (economy.Offline != null && economy.Offline.BaseCapHours <= 0)
            {
                issues.Add("Economy offline.baseCapHours must be positive");
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

            if (economy.Excavation != null
                && (economy.Excavation.PityTimerHoursDug <= 0 || economy.Excavation.BaseFragmentsPerHour <= 0))
            {
                // Zero rate AND zero pity means dig sites can never surface a
                // fragment — every fossil becomes unreachable.
                issues.Add("Economy excavation values must all be positive");
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
                case EffectType.MuseumSetBonusMult:
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
