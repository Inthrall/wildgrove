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
                || state.HasUpgrade(upgrade.id) || !CanAfford(state, upgrade))
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
        /// upgrades: yieldMult effects multiply together, yieldBonus effects
        /// add a combined percentage on top. Call after any purchase (and on
        /// load, once the save system lands).
        /// </summary>
        public static void RecomputeYieldMultipliers(GameState state, GameDataAsset data)
        {
            foreach (var node in state.nodes)
            {
                var mult = 1.0;
                var bonus = 0.0;

                foreach (var effect in PurchasedEffects(state, data))
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
        /// Carry-capacity multiplier from owned haulMult upgrades (Waxed
        /// Satchel ×1.5, Handcart ×2, …) — they multiply together. Additive
        /// carrierCapacityBonus gear arrives with the gear system.
        /// </summary>
        public static double HaulCapacityMultiplier(GameState state, GameDataAsset data)
        {
            var mult = 1.0;
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.HaulMult)
                {
                    mult *= effect.value;
                }
            }

            return mult;
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
            foreach (var effect in PurchasedEffects(state, data))
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
        /// The run's offline cap: the base cap, raised (never lowered) to the
        /// best offlineCapHours upgrade owned (Root Cellar 6 h, Smokehouse 8 h).
        /// Additive offlineCapBonusHours gear arrives with the gear system.
        /// </summary>
        public static double OfflineCapHours(GameState state, GameDataAsset data)
        {
            var cap = data.economy.offline.baseCapHours;
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.OfflineCapHours && effect.value > cap)
                {
                    cap = effect.value;
                }
            }

            return cap;
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

            return effect.skill == AllGatheringSkill || effect.skill == node.skill;
        }
    }
}
