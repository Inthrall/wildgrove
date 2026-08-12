using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// One effect, wherever it is authored. Upgrades, gear, insects, Almanac
    /// nodes, sabbat touches, tinctures, deep amber and Folio spreads all carry
    /// them, so the shape is checked in one place and the owner is passed in for
    /// the message.
    /// </summary>
    public static partial class GameDataValidator
    {
        private static void ValidateEffect(string owner, EffectDef effect, GameData data, HashSet<string> resourceIds, List<string> issues, bool almanacNode = false, bool sabbatTouch = false)
        {
            switch (effect.Type)
            {
                case EffectType.YieldMult:
                case EffectType.YieldBonus:
                case EffectType.CraftSpeedMult:
                    RequirePositiveValue(owner, effect, issues);
                    // A target-less craftSpeedMult is global — the sim applies
                    // it to every skill's recipes (Patient Hands). Yield
                    // effects still need a target — a skill, a zone, or a
                    // single resource (the sabbat touch's grain): the sim
                    // would silently apply a bare one to nothing.
                    if (effect.Skill == null && effect.Zone == null && effect.Resource == null
                        && effect.Type != EffectType.CraftSpeedMult)
                    {
                        issues.Add($"{owner} {effect.Type} effect targets neither a skill, a zone, nor a resource");
                    }

                    if (effect.Skill != null && !KnownSkills.Contains(effect.Skill) && !SkillWildcards.Contains(effect.Skill))
                    {
                        issues.Add($"{owner} references unknown skill '{effect.Skill}'");
                    }

                    if (effect.Zone != null && !data.ZonesById.ContainsKey(effect.Zone))
                    {
                        issues.Add($"{owner} references unknown zone '{effect.Zone}'");
                    }

                    if (effect.Resource != null && !resourceIds.Contains(effect.Resource))
                    {
                        issues.Add($"{owner} references unknown resource '{effect.Resource}'");
                    }

                    break;

                case EffectType.DigSpeedMult:
                case EffectType.FolioSpreadBonusMult:
                case EffectType.ChoiceChanceBonus:
                case EffectType.OfflineCapHours:
                case EffectType.OfflineCapBonusHours:
                case EffectType.TendingBurstBonus:
                case EffectType.WardenYieldBonus:
                case EffectType.BubbleRewardBonus:
                    RequirePositiveValue(owner, effect, issues);
                    break;

                case EffectType.SellValueBonus:
                    RequirePositiveValue(owner, effect, issues);
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

                case EffectType.RecruitSpecies:
                    if (effect.Species == null || !data.SpeciesById.ContainsKey(effect.Species))
                    {
                        issues.Add($"{owner} recruits unknown species '{effect.Species}'");
                    }

                    break;

                case EffectType.GrantUpgrade:
                    // Only the tree that survives the fold may hand out rungs;
                    // a rung granting a rung is a cycle waiting to happen, and
                    // gear or insects doing it would be a second ladder.
                    if (!almanacNode)
                    {
                        issues.Add($"{owner} carries a grantUpgrade effect — only the Almanac may grant ladder rungs");
                    }
                    else if (effect.Upgrade == null || !data.UpgradesById.TryGetValue(effect.Upgrade, out var grantedRung))
                    {
                        issues.Add($"{owner} grants unknown upgrade '{effect.Upgrade}'");
                    }
                    else if (grantedRung.Effects.Any(x => x.Type == EffectType.RecruitSpecies))
                    {
                        // Familiar permanence is Kinship's alone (design §4) —
                        // the Almanac never buys creatures, not even sideways.
                        issues.Add($"{owner} grants '{effect.Upgrade}', which recruits a familiar — the Almanac carries no familiar power (design §4)");
                    }

                    break;

                case EffectType.KeepCraftOrders:
                    if (!almanacNode)
                    {
                        issues.Add($"{owner} carries a keepCraftOrders effect — only the Almanac crosses the fold");
                    }

                    break;

                case EffectType.ReplantCostMult:
                case EffectType.ExchangeSpreadEase:
                    // Only the Wheel's live accessors read these — anywhere else
                    // they would silently do nothing (design §15).
                    RequirePositiveValue(owner, effect, issues);
                    if (!sabbatTouch)
                    {
                        issues.Add($"{owner} carries a {effect.Type} effect — only the Wheel's sabbat touch reads it (design §15)");
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
