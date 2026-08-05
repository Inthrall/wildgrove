using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using Wildgrove.Sim;
using Wildgrove.Data;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;

namespace Wildgrove.Game
{
    /// <summary>
    /// Journal labels that read live game state to make words — zone names,
    /// effect descriptions, station lines, bundle "have" lines. Distinct from
    /// <see cref="JournalFormat"/>, whose formatters depend only on their
    /// arguments; everything here needs the <see cref="GameLoop"/>.
    /// </summary>
    internal sealed class JournalText
    {
        private readonly GameLoop _loop;

        internal JournalText(GameLoop loop)
        {
            _loop = loop;
        }

        internal List<ZoneData> ZonesInOrder()
        {
            var unlocked = Upgrades.UnlockedZoneIds(_loop.State, _loop.Data);
            var zones = new List<ZoneData>();
            foreach (var zone in _loop.Data.zones.OrderBy(z => z.order))
            {
                if (unlocked.Contains(zone.id))
                {
                    zones.Add(zone);
                }
            }

            return zones;
        }

        internal ZoneData LatestZone()
        {
            var zones = ZonesInOrder();
            return zones.Count > 0 ? zones[zones.Count - 1] : null;
        }

        internal string ZoneName(string zoneId)
        {
            return _loop.Data.ZonesById.TryGetValue(zoneId ?? string.Empty, out var zone)
                ? zone.displayName
                : zoneId;
        }

        internal string UpgradeName(string upgradeId)
        {
            return _loop.Data.UpgradesById.TryGetValue(upgradeId ?? string.Empty, out var upgrade)
                ? upgrade.displayName
                : upgradeId;
        }

        /// <summary>The Ladder rung whose effects unlock <paramref name="skill"/>, or null when nothing grants it.</summary>
        internal UpgradeData SkillSource(string skill)
        {
            foreach (var upgrade in _loop.Data.upgrades)
            {
                foreach (var effect in upgrade.effects)
                {
                    if (effect.type == EffectType.UnlockSkill && effect.skill == skill)
                    {
                        return upgrade;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// What a rung or kit piece actually does, in journal ink — effects
        /// are machine-readable, so this is where they turn into words.
        /// </summary>
        internal string EffectsLabel(List<EffectData> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var effect in effects)
            {
                var label = EffectLabel(effect);
                if (!string.IsNullOrEmpty(label))
                {
                    parts.Add(label);
                }
            }

            return string.Join(" · ", parts);
        }

        private string EffectLabel(EffectData effect)
        {
            switch (effect.type)
            {
                case EffectType.YieldMult:
                    return YieldTarget(effect) + " ×" + PlainNumber(effect.value);
                case EffectType.YieldBonus:
                    return YieldTarget(effect) + " +" + Percent(effect.value);
                case EffectType.SellValueBonus:
                    return effect.resource + " sells +" + Percent(effect.value);
                case EffectType.CraftSpeedMult:
                    return (string.IsNullOrEmpty(effect.skill) ? "crafting" : effect.skill) + " ×" + PlainNumber(effect.value) + " faster";
                case EffectType.DigSpeedMult:
                    return "digs ×" + PlainNumber(effect.value) + " faster";
                case EffectType.ChoiceChanceBonus:
                    return "choice chance +" + Percent(effect.value);
                case EffectType.FolioSpreadBonusMult:
                    return "folio spreads ×" + PlainNumber(effect.value);
                case EffectType.OfflineCapHours:
                case EffectType.OfflineCapBonusHours:
                    return AwayCreditLabel(effect);
                case EffectType.TendingBurstBonus:
                    return "tending burst +" + Percent(effect.value);
                case EffectType.WardenYieldBonus:
                    return Warden.PossessiveName(_loop.State) + " own hands +" + Percent(effect.value);
                case EffectType.BubbleRewardBonus:
                    return "windfalls +" + Percent(effect.value);
                case EffectType.UnlockZone:
                    return "opens " + ZoneName(effect.zone);
                case EffectType.UnlockSkill:
                    return "unlocks " + effect.skill;
                case EffectType.UnlockRecipe:
                    return "teaches " + effect.recipe;
                case EffectType.UnlockDigSite:
                    return "opens the watch in " + ZoneName(effect.zone);
                case EffectType.RecruitSpecies:
                    return "a " + SpeciesName(effect.species) + " joins the kith";
                case EffectType.GrantUpgrade:
                    return "every fold begins with " + UpgradeName(effect.upgrade);
                case EffectType.KeepCraftOrders:
                    return "the stations keep their orders across the fold";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Away credit in ONE shape whatever the mechanism underneath: what this
        /// piece adds, then where the run stands with everything it already has.
        /// A rung reading "away credit up to 8h" beside a tarp reading "away
        /// credit +2h" looked like two different currencies, and neither said
        /// what the cap actually is — so the hours were never comparable.
        /// </summary>
        private string AwayCreditLabel(EffectData effect)
        {
            var gain = effect.type == EffectType.OfflineCapBonusHours
                ? effect.value
                : Upgrades.OfflineCapGainHours(_loop.State, _loop.Data, effect.value);

            return "away credit +" + PlainNumber(gain) + "h (now "
                   + PlainNumber(Upgrades.OfflineCapHours(_loop.State, _loop.Data)) + "h)";
        }

        private string YieldTarget(EffectData effect)
        {
            if (!string.IsNullOrEmpty(effect.skill))
            {
                return effect.skill + " yield";
            }

            if (!string.IsNullOrEmpty(effect.zone))
            {
                return ZoneName(effect.zone) + " yield";
            }

            if (!string.IsNullOrEmpty(effect.resource))
            {
                return effect.resource + " yield";
            }

            return "all yields";
        }

        internal string StationLabel(string stationId)
        {
            if (string.IsNullOrEmpty(stationId))
            {
                return "resting at camp";
            }

            if (stationId == Familiar.WanderStation)
            {
                return "wandering";
            }

            if (stationId == Familiar.PonyStation)
            {
                return "her own lane";
            }

            foreach (var node in _loop.State.nodes)
            {
                if (node.id == stationId)
                {
                    return node.resourceId;
                }
            }

            return stationId;
        }

        internal string SpeciesName(string speciesId)
        {
            return _loop.Data.SpeciesById != null && _loop.Data.SpeciesById.TryGetValue(speciesId ?? string.Empty, out var species)
                ? species.displayName
                : speciesId;
        }

        internal string UpgradeRequirement(UpgradeData upgrade)
        {
            var parts = new List<string>();

            // The fold gate speaks first and alone: a trail that does not exist
            // yet has no use for a shopping list, and reading "needs iron tools"
            // beside it would send the warden off earning something that changes
            // nothing this run.
            var folds = _loop.FoldsUntilUpgrade(upgrade);
            if (folds > 0)
            {
                return folds == 1 ? "not before the next fold" : "not for another " + folds + " folds";
            }

            if (!_loop.MeetsUpgradeSkillGate(upgrade) && !string.IsNullOrEmpty(upgrade.gateSkill))
            {
                parts.Add("needs " + upgrade.gateSkill + " " + upgrade.gateLevel);
            }

            var tool = _loop.MissingToolTier(upgrade);
            if (!string.IsNullOrEmpty(tool))
            {
                parts.Add("needs " + tool + " tools");
            }

            if (upgrade.materials != null && upgrade.materials.Count > 0)
            {
                parts.Add(BundleHaveLabel(upgrade.materials));
            }

            return parts.Count == 0 ? "ready" : string.Join(" · ", parts);
        }

        /// <summary>
        /// A bundle line that also says what the camp holds of each item —
        /// "4 berries (have 35.8K), 2 nuts (have 48)" — with any shortfall
        /// inked in ochre so the blocking item is the one that stands out.
        /// </summary>
        internal string BundleHaveLabel(IEnumerable<(string id, BigDouble amount)> bundle)
        {
            var parts = new List<string>();
            foreach (var (id, amount) in bundle)
            {
                var have = _loop.State.GetResource(id);
                var part = NumberFormat.Short(amount) + " " + id + " (have " + NumberFormat.Short(have) + ")";
                parts.Add(have < amount ? "<color=" + OchreInkHex + ">" + part + "</color>" : part);
            }

            return string.Join(", ", parts);
        }

        internal string BundleHaveLabel(List<ItemAmount> materials)
        {
            return BundleHaveLabel(Costs(materials));
        }

        internal string BundleHaveLabel(List<Buildings.MaterialCost> bundle)
        {
            return BundleHaveLabel(Costs(bundle));
        }

        /// <summary>
        /// A planter's name in the language of the node it serves. Foraging nodes
        /// keep the garden names (frame, trellis); mining, delving, logging,
        /// fishing, husbandry and watching get names that fit the work. Dig sites
        /// and anything unmapped fall back to the planter's own displayName.
        /// </summary>
        internal string PlanterDisplayName(PlanterData planter, string targetId)
        {
            var skill = NodeSkill(targetId);
            if (string.IsNullOrEmpty(skill))
            {
                return planter.displayName;
            }

            switch (planter.kind)
            {
                case "nodeYieldMult":
                    switch (skill)
                    {
                        case "mining": return "Ore Rig";
                        case "delving": return "Deep Hoist";
                        case "logging": return "Felling Rig";
                        case "fishing": return "Set Nets";
                        case "husbandry": return "Fenced Pens";
                        case "entomology": return "Light Traps";
                        default: return planter.displayName;
                    }
                default:
                    return planter.displayName;
            }
        }

        /// <summary>
        /// What raising a planter buys, in one short phrase. Nothing on the
        /// plate said: a Reed Screen asked for 20 reeds under a name alone, and
        /// the raised line only confirmed it stood there. The dig-site kind is
        /// sketching speed (Observation reads it as the site's own speed), not
        /// digging — there is no digging.
        /// </summary>
        internal string PlanterGives(PlanterData planter)
        {
            if (planter == null || planter.value <= 0.0)
            {
                return string.Empty;
            }

            var percent = UnityEngine.Mathf.RoundToInt((float)(planter.value * 100.0));
            switch (planter.kind)
            {
                case "nodeYieldMult":
                    return "+" + percent + "% yield";
                case "digSpeedMult":
                    return "+" + percent + "% sketching";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The gathering skill of the node with this id, or null when the target is a dig site.</summary>
        internal string NodeSkill(string targetId)
        {
            foreach (var node in _loop.State.nodes)
            {
                if (node.id == targetId)
                {
                    return _loop.Data.ResourcesById.TryGetValue(node.resourceId, out var res) ? res.skill : null;
                }
            }

            return null;
        }
    }
}
