using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// The catalogue itself: resources, zones, recipes, buildings, upgrades and
    /// gear. The checks that ask whether a thing names something that exists,
    /// costs something obtainable, and is reachable at all.
    /// <para>
    /// <see cref="ValidateZones"/> is the head of the gating chain: it runs the
    /// tool and fold checks in <c>GameDataValidator.Gating</c>, because what a
    /// zone opens is what decides whether anything past it can be reached.
    /// </para>
    /// </summary>
    public static partial class GameDataValidator
    {
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

            // Every zone's unlocks are checked, not only the starting zone's. The
            // starting zone's are live twice over — they seed UnlockedSkills — but
            // all of them are walked by RiteGenerator.SkillDebutOrder, which reads
            // the order of the zone that unlocks a skill to decide how early a
            // generated verse may ask for that skill's goods. So a typo in a late
            // zone's list moves the skill's debut, and the pacing of every run-2+
            // Rite with it, while the data still validates green.
            var startingZone = data.Zones.OrderBy(z => z.Order).FirstOrDefault();
            foreach (var zone in data.Zones)
            {
                foreach (var unlock in zone.Unlocks)
                {
                    if (KnownSkills.Contains(unlock))
                    {
                        continue;
                    }

                    // A later zone may name something that isn't a skill at all.
                    // The starting zone's list is read as skills, so nothing else
                    // belongs in it.
                    if (zone != startingZone && KnownNonSkillUnlocks.Contains(unlock))
                    {
                        continue;
                    }

                    issues.Add($"Zone '{zone.Id}' unlock '{unlock}' is not a known skill");
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
            ValidateFoldGating(data, issues);
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

                // Negative would run the batch backwards; the absent-field 0 is
                // the "inherit baseCraftSeconds" case and stays legal.
                if (recipe.CraftSeconds < 0)
                {
                    issues.Add($"Recipe '{recipe.Id}' has negative craftSeconds");
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
            "stationSpeedBonus", "offlineCapBonusHours", "comfort"
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

                // The XP clamp stops at maxLevel and MeetsSkillGate compares
                // against the clamped level, so a gate above the cap is an
                // upgrade that shows its price and can never be bought — the
                // same permanently-unbuyable shape the recipe rule above catches.
                if (data.Economy?.Xp != null && upgrade.GateLevel > data.Economy.Xp.MaxLevel)
                {
                    issues.Add($"Upgrade '{upgrade.Id}' gateLevel {upgrade.GateLevel} exceeds xp.maxLevel {data.Economy.Xp.MaxLevel} — unreachable forever");
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
    }
}
