using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// Referential-integrity and sanity checks across the nine data files.
    /// Returns human-readable issues; empty list means the data is coherent.
    /// <para>
    /// <see cref="Validate"/> is the index: each call in it is one content area,
    /// and each area lives in its own partial beside this file (<c>Content</c>,
    /// <c>Gating</c>, <c>Kith</c>, <c>Collections</c>, <c>Wheel</c>, <c>Camp</c>,
    /// <c>Rites</c>, <c>Economy</c>, <c>Effects</c>). This file keeps the
    /// vocabulary they share, the id and duplicate checks, and the one check
    /// that spans every area at once: that a skill gate names a skill the run
    /// can actually earn.
    /// </para>
    /// </summary>
    public static partial class GameDataValidator
    {
        private static readonly HashSet<string> KnownSkills = new HashSet<string>
        {
            "foraging", "mining", "logging", "fishing",
            "firecraft", "forgecraft", "bushcraft", "excavation",
            "entomology", "apothecary", "delving", "husbandry"
        };

        private static readonly HashSet<string> SkillWildcards = new HashSet<string> { "all", "all-gathering" };

        // Tokens a zone's unlocks may carry that name something other than a
        // skill — the field doubles as documentation of what the zone opens, and
        // the last zone uses it for the final-waystones chain, which nothing
        // resolves as a skill. Whitelisted rather than waved through: an unknown
        // token is still a typo.
        private static readonly HashSet<string> KnownNonSkillUnlocks = new HashSet<string> { "final-waystones" };

        private static readonly HashSet<string> KnownScopes = new HashSet<string> { "mvp", "v1.1", "v1.2" };

        // The sim switches on the literal (Economy.cs sell pricing, rite slot
        // classification) — a typo'd kind silently makes a good unsellable.
        private static readonly HashSet<string> KnownRecipeKinds = new HashSet<string> { "material", "trade" };

        // Deeds a running sim can actually record (Simulation.RecordDeed call
        // sites) — a slot naming anything else could never fill.
        private static readonly HashSet<string> KnownDeeds = new HashSet<string> { "tend" };

        private static readonly HashSet<string> KnownSpecimenQualities = new HashSet<string> { "decent", "choice" };

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
            ValidateWheel(data, resourceIds, issues);
            ValidateTinctures(data, resourceIds, issues);
            ValidateDeepAmber(data, resourceIds, issues);
            ValidateExchange(data, issues);
            ValidateRites(data, resourceIds, issues);
            ValidateDialogue(data, issues);
            ValidateEconomy(data, issues);
            ValidateSkillGatesAreEarnable(data, issues);

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

        /// <summary>
        /// Every level gate must name a skill the run can actually earn XP in.
        /// Brush Screens shipped gated on entomology 8 while nothing in the game
        /// awarded entomology XP at all — the rung was simply unbuyable, and
        /// nothing said so: the ladder drew it, the gate label read sensibly, and
        /// the level behind it sat at 1 forever. XP has exactly three sources
        /// (Skills.AddGatherXp from a resource's skill, Skills.AddCraftXp from a
        /// recipe's, and the observation craft's watched hours), so the earnable
        /// set is computable here.
        /// </summary>
        private static void ValidateSkillGatesAreEarnable(GameData data, List<string> issues)
        {
            // Gathering and sketching are never level-gated, so those skills earn
            // from a standing start. A craft skill only does if it has at least
            // one recipe openable at level 1 — otherwise its first rung sits
            // behind a level only that rung could have paid for.
            var earnable = new HashSet<string>(data.Resources.Select(r => r.Skill).Where(s => s != null));
            earnable.UnionWith(data.Recipes.Where(r => r.SkillLevel <= 1 && r.Skill != null).Select(r => r.Skill));
            if (data.Economy?.Observation?.Skill != null && data.Economy.Observation.WatchXpPerHour > 0)
            {
                earnable.Add(data.Economy.Observation.Skill);
            }

            foreach (var upgrade in data.Upgrades
                .Where(u => u.GateLevel > 1 && !string.IsNullOrEmpty(u.GateSkill) && !earnable.Contains(u.GateSkill)))
            {
                issues.Add($"Upgrade '{upgrade.Id}' gates on {upgrade.GateSkill} {upgrade.GateLevel}, but nothing awards '{upgrade.GateSkill}' XP — the rung could never be bought");
            }

            foreach (var recipe in data.Recipes
                .Where(r => r.SkillLevel > 1 && r.Skill != null && !earnable.Contains(r.Skill)))
            {
                issues.Add($"Recipe '{recipe.Id}' needs {recipe.Skill} {recipe.SkillLevel}, but nothing awards '{recipe.Skill}' XP from level 1 — the recipe could never be crafted");
            }
        }
    }
}
