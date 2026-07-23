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

        internal List<string> TradeableResources()
        {
            var list = new List<string>();
            if (_loop.Data.resources != null)
            {
                foreach (var resource in _loop.Data.resources)
                {
                    list.Add(resource.id);
                }
            }

            if (_loop.Data.recipes != null)
            {
                foreach (var recipe in _loop.Data.recipes)
                {
                    if (recipe.kind == "trade" && recipe.output != null && !list.Contains(recipe.output))
                    {
                        list.Add(recipe.output);
                    }
                }
            }

            return list;
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
                case EffectType.HaulMult:
                    return "hauling ×" + PlainNumber(effect.value);
                case EffectType.SellValueBonus:
                    return effect.resource + " sells +" + Percent(effect.value);
                case EffectType.CraftSpeedMult:
                    return (string.IsNullOrEmpty(effect.skill) ? "crafting" : effect.skill) + " ×" + PlainNumber(effect.value) + " faster";
                case EffectType.DigSpeedMult:
                    return "digs ×" + PlainNumber(effect.value) + " faster";
                case EffectType.PristineChanceBonus:
                    return "pristine chance +" + Percent(effect.value);
                case EffectType.FolioSpreadBonusMult:
                    return "folio spreads ×" + PlainNumber(effect.value);
                case EffectType.OfflineCapHours:
                    return "away credit up to " + PlainNumber(effect.value) + "h";
                case EffectType.OfflineCapBonusHours:
                    return "away credit +" + PlainNumber(effect.value) + "h";
                case EffectType.OfflineNightFullRate:
                    return "full pace through the night away";
                case EffectType.TendingBurstBonus:
                    return "tending burst +" + Percent(effect.value);
                case EffectType.CarrierCapacityBonus:
                    return "carriers hold +" + Percent(effect.value);
                case EffectType.NoSpoilage:
                    return effect.resource + " never spoils";
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
                case EffectType.UnlockVerdureForecast:
                    return "reveals the verdure forecast";
                default:
                    return string.Empty;
            }
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

            if (stationId == Familiar.TrailStation)
            {
                return "the trail";
            }

            if (stationId == Familiar.WanderStation)
            {
                return "wandering";
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

        internal BigDouble NodeBasketCapacity(NodeState node)
        {
            var hauling = _loop.Data.economy?.hauling;
            if (hauling == null)
            {
                return BigDouble.Zero;
            }

            return new BigDouble(hauling.basketCapacity * Buildings.BasketCapacityMultiplier(_loop.State, _loop.Data))
                   * Planters.BasketCapacityMultiplier(_loop.State, _loop.Data, node);
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
                case "basketCapacityMult":
                    switch (skill)
                    {
                        case "mining": return "Mineshaft Beams";
                        case "delving": return "Pit Props";
                        case "logging": return "Log Cradle";
                        case "fishing": return "Fish Baskets";
                        case "husbandry": return "Feed Racks";
                        case "entomology": return "Netting Frames";
                        default: return planter.displayName;
                    }
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
