using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The one-off upgrade ladder (design doc §9): purchasing spends Coin plus
    /// any crafted materials, records the upgrade on the run, and recomputes
    /// the derived modifiers the tick and economy read. Pure and deterministic
    /// like the rest of the sim — the MonoBehaviour driver wires it to the UI,
    /// the tests pin the maths. Effect types the sim doesn't consume yet
    /// (unlockSkill / unlockRecipe / unlockDigSite, craft and dig speed, …)
    /// are recorded on the run but stay inert until their systems land.
    /// </summary>
    public static class Upgrades
    {
        /// <summary>The Whetstone-style wildcard: a skill target matching every gathering node.</summary>
        private const string AllGatheringSkill = "all-gathering";

        /// <summary>True when the run holds the Coin and every listed material.</summary>
        public static bool CanAfford(GameState state, UpgradeData upgrade)
        {
            if (state == null || upgrade == null || state.coin < upgrade.costCoin)
            {
                return false;
            }

            foreach (var material in upgrade.materials)
            {
                if (state.GetResource(material.id) < material.amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Buy <paramref name="upgrade"/> if it isn't already owned and the run
        /// can pay (Coin and materials both), then recompute the node
        /// multipliers its effects feed. Returns false (and changes nothing)
        /// otherwise, so the caller can leave the button disabled.
        /// </summary>
        public static bool TryPurchase(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            if (state == null || data == null || upgrade == null
                || state.HasUpgrade(upgrade.id) || !CanAfford(state, upgrade)
                || !MeetsToolRequirement(state, data, upgrade))
            {
                return false;
            }

            state.coin -= upgrade.costCoin;
            foreach (var material in upgrade.materials)
            {
                state.resources[material.id] = state.GetResource(material.id) - material.amount;
            }

            state.purchasedUpgradeIds.Add(upgrade.id);

            // A trail map's unlockZone effect takes hold immediately: the new
            // zone's nodes appear (with the design §2 regional seed) before the
            // multipliers are rebuilt so they're covered too.
            GameStateFactory.SyncUnlockedZones(state, data);
            RecomputeYieldMultipliers(state, data);
            return true;
        }

        /// <summary>
        /// The run's tool tier as an index into economy.tools.tiers: the best
        /// toolTier upgrade owned, −1 with none. Data without a tools section
        /// (hand-built fixtures) reads −1 but gates nothing — see
        /// <see cref="MeetsToolRequirement"/>.
        /// </summary>
        public static int ToolTierIndex(GameState state, GameDataAsset data)
        {
            var tiers = data.economy?.tools?.tiers;
            if (tiers == null)
            {
                return -1;
            }

            var best = -1;
            foreach (var upgradeId in state.purchasedUpgradeIds)
            {
                if (data.UpgradesById.TryGetValue(upgradeId, out var upgrade)
                    && !string.IsNullOrEmpty(upgrade.toolTier))
                {
                    var index = tiers.IndexOf(upgrade.toolTier);
                    if (index > best)
                    {
                        best = index;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The design §3 tool gate: a trail map can only be bought once the
        /// run's tool tier covers every zone it unlocks (Zone 2 flint …
        /// deeper steel+). Non-map upgrades, ungated zones, and data without
        /// a tools section all pass.
        /// </summary>
        public static bool MeetsToolRequirement(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            var missing = MissingToolTier(state, data, upgrade);
            return string.IsNullOrEmpty(missing);
        }

        /// <summary>
        /// The tier name blocking this upgrade's purchase (for the buy
        /// button's "needs … tools" line), or null when nothing is missing.
        /// </summary>
        public static string MissingToolTier(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            var tiers = data.economy?.tools?.tiers;
            if (tiers == null || upgrade == null)
            {
                return null;
            }

            string missing = null;
            var missingIndex = -1;
            var owned = ToolTierIndex(state, data);
            foreach (var effect in upgrade.effects)
            {
                if (effect.type != EffectType.UnlockZone || string.IsNullOrEmpty(effect.zone)
                    || !data.ZonesById.TryGetValue(effect.zone, out var zone)
                    || string.IsNullOrEmpty(zone.requiredTool))
                {
                    continue;
                }

                var required = tiers.IndexOf(zone.requiredTool);
                if (required > owned && required > missingIndex)
                {
                    missing = zone.requiredTool;
                    missingIndex = required;
                }
            }

            return missing;
        }

        /// <summary>
        /// The zones this run has opened: the starting zone plus every zone an
        /// owned upgrade's unlockZone effect grants.
        /// </summary>
        public static HashSet<string> UnlockedZoneIds(GameState state, GameDataAsset data)
        {
            var ids = new HashSet<string> { GameStateFactory.StartingZoneId };
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockZone && !string.IsNullOrEmpty(effect.zone))
                {
                    ids.Add(effect.zone);
                }
            }

            return ids;
        }

        /// <summary>
        /// Rebuild every node's tool/upgrade multiplier from the purchased
        /// upgrades and completed fossils: yieldMult effects multiply together,
        /// yieldBonus effects add a combined percentage on top. Call after any
        /// purchase, on restore, and when a fossil completes.
        /// </summary>
        public static void RecomputeYieldMultipliers(GameState state, GameDataAsset data)
        {
            foreach (var node in state.nodes)
            {
                var mult = 1.0;
                var bonus = 0.0;

                foreach (var effect in ActiveEffects(state, data))
                {
                    if (effect.type == EffectType.YieldMult && TargetsNode(effect, node))
                    {
                        mult *= effect.value;
                    }
                    else if (effect.type == EffectType.YieldBonus && TargetsNode(effect, node))
                    {
                        bonus += effect.value;
                    }
                }

                node.yieldMultiplier = mult * (1.0 + bonus);
            }
        }

        /// <summary>
        /// Carry-capacity multiplier: haulMult effects (Waxed Satchel ×1.5,
        /// Handcart ×2, Almanac Sure Paths) multiply together, then the
        /// additive carrierCapacityBonus band (the Birch Frame Pack's +25%)
        /// scales the product.
        /// </summary>
        public static double HaulCapacityMultiplier(GameState state, GameDataAsset data)
        {
            var mult = 1.0;
            var bonus = 0.0;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.HaulMult)
                {
                    mult *= effect.value;
                }
                else if (effect.type == EffectType.CarrierCapacityBonus)
                {
                    bonus += effect.value;
                }
            }

            return mult * (1.0 + bonus);
        }

        /// <summary>Extra Tending burst strength from worn gear (the Cordage Wraps' +50%), summed — multiplies the burst's yield multiplier.</summary>
        public static double TendingBurstBonus(GameState state, GameDataAsset data)
        {
            var bonus = 0.0;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.TendingBurstBonus)
                {
                    bonus += effect.value;
                }
            }

            return bonus;
        }

        /// <summary>
        /// The skills this run has opened: the starting zone's unlocks plus
        /// every skill an owned upgrade's unlockSkill effect grants. Gates
        /// which recipes can be crafted.
        /// </summary>
        public static HashSet<string> UnlockedSkills(GameState state, GameDataAsset data)
        {
            var skills = new HashSet<string>();
            if (data.ZonesById.TryGetValue(GameStateFactory.StartingZoneId, out var startingZone))
            {
                skills.UnionWith(startingZone.unlocks);
            }

            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockSkill && !string.IsNullOrEmpty(effect.skill))
                {
                    skills.Add(effect.skill);
                }
            }

            return skills;
        }

        /// <summary>Recipe ids granted by owned unlockRecipe effects (defaultKnown recipes don't need one).</summary>
        public static HashSet<string> UnlockedRecipeIds(GameState state, GameDataAsset data)
        {
            var recipes = new HashSet<string>();
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockRecipe && !string.IsNullOrEmpty(effect.recipe))
                {
                    recipes.Add(effect.recipe);
                }
            }

            return recipes;
        }

        /// <summary>
        /// Craft-speed multiplier for one skill's recipes: owned craftSpeedMult
        /// effects targeting that skill multiply together (Bellows Forge ×2 for
        /// forgecraft). Divides the per-batch craft time.
        /// </summary>
        public static double CraftSpeedMultiplier(GameState state, GameDataAsset data, string skill)
        {
            var mult = 1.0;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.CraftSpeedMult
                    && (string.IsNullOrEmpty(effect.skill) || effect.skill == skill))
                {
                    mult *= effect.value;
                }
            }

            return mult;
        }

        /// <summary>Sell-value multiplier for one resource: 1 + the summed sellValueBonus effects owned.</summary>
        public static double SellValueMultiplier(GameState state, GameDataAsset data, string resourceId)
        {
            var bonus = 0.0;
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.SellValueBonus && effect.resource == resourceId)
                {
                    bonus += effect.value;
                }
            }

            return 1.0 + bonus;
        }

        /// <summary>
        /// Flat Pristine-chance points from owned pristineChanceBonus effects
        /// (Field Press +0.01, the Sunken Jaw fossil +0.01), summed — design
        /// §8's additive band. Almanac bonuses join when that system lands.
        /// </summary>
        public static double PristineChanceBonus(GameState state, GameDataAsset data)
        {
            var bonus = 0.0;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.PristineChanceBonus)
                {
                    bonus += effect.value;
                }
            }

            return bonus;
        }

        /// <summary>Dig-speed multiplier from owned digSpeedMult upgrades (Brush Screens ×2) — they multiply together.</summary>
        public static double DigSpeedMultiplier(GameState state, GameDataAsset data)
        {
            var mult = 1.0;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.DigSpeedMult)
                {
                    mult *= effect.value;
                }
            }

            return mult;
        }

        /// <summary>Zones whose dig site an owned unlockDigSite effect has opened.</summary>
        public static HashSet<string> UnlockedDigSiteZones(GameState state, GameDataAsset data)
        {
            var zones = new HashSet<string>();
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockDigSite && !string.IsNullOrEmpty(effect.zone))
                {
                    zones.Add(effect.zone);
                }
            }

            return zones;
        }

        /// <summary>
        /// The run's offline cap: the base cap, raised (never lowered) to the
        /// best offlineCapHours effect active (Root Cellar 6 h, Smokehouse
        /// 8 h, the Almanac's Long Watch), plus the additive
        /// offlineCapBonusHours band (the Oilskin Tarp's +2 h).
        /// </summary>
        public static double OfflineCapHours(GameState state, GameDataAsset data)
        {
            var cap = data.economy.offline.baseCapHours;
            var bonus = 0.0;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.OfflineCapHours && effect.value > cap)
                {
                    cap = effect.value;
                }
                else if (effect.type == EffectType.OfflineCapBonusHours)
                {
                    bonus += effect.value;
                }
            }

            return cap + bonus;
        }

        /// <summary>Purchased upgrade effects, completed fossils', owned Almanac nodes', and worn gear's — everything currently modifying the run.</summary>
        private static IEnumerable<EffectData> ActiveEffects(GameState state, GameDataAsset data)
        {
            foreach (var effect in Gear.EquippedEffects(state, data))
            {
                yield return effect;
            }

            foreach (var effect in PurchasedEffects(state, data))
            {
                yield return effect;
            }

            foreach (var effect in Fossils.CompletedEffects(state, data))
            {
                yield return effect;
            }

            foreach (var nodeId in state.almanacNodeIds)
            {
                if (!data.AlmanacById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                foreach (var effect in node.effects)
                {
                    yield return effect;
                }
            }
        }

        private static IEnumerable<EffectData> PurchasedEffects(GameState state, GameDataAsset data)
        {
            foreach (var upgradeId in state.purchasedUpgradeIds)
            {
                // An id this data version doesn't know (saved on other data) is
                // skipped rather than crashing the run.
                if (!data.UpgradesById.TryGetValue(upgradeId, out var upgrade))
                {
                    continue;
                }

                foreach (var effect in upgrade.effects)
                {
                    yield return effect;
                }
            }
        }

        private static bool TargetsNode(EffectData effect, NodeState node)
        {
            if (!string.IsNullOrEmpty(effect.zone))
            {
                return effect.zone == node.zoneId;
            }

            // "all" is the fossil wildcard, "all-gathering" the upgrade one —
            // the validator accepts both spellings.
            return effect.skill == AllGatheringSkill || effect.skill == "all" || effect.skill == node.skill;
        }
    }
}
