using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The camp building lines (design §9) — the repeatable Coin sink. A
    /// line's level is its bought levels plus the §9 milestone upgrades the
    /// run owns; the next bought level always costs baseCostCoin ·
    /// costGrowth.building^level, forever. Bought levels each grant the
    /// line's perLevel effect: station craft speed, basket capacity, or the
    /// Roosts line's comfort (+familiar XP rate while stationed, design §4 —
    /// kith headcount is the §4 slot ladder, never a roost formula).
    /// Station lines (fire / forge / bench) also gate recipes: a recipe needs
    /// its station's line at ≥ its stationLevel.
    /// </summary>
    public static class Buildings
    {
        /// <summary>Levels of this line bought with Coin (milestone upgrades not included).</summary>
        public static int BoughtLevels(GameState state, string buildingId)
        {
            return state.buildingLevels.TryGetValue(buildingId, out var levels) ? levels : 0;
        }

        /// <summary>The line's current level: bought levels plus owned milestone upgrades.</summary>
        public static int TotalLevel(GameState state, BuildingData building)
        {
            var level = BoughtLevels(state, building.id);
            foreach (var upgradeId in building.milestoneUpgradeIds)
            {
                if (state.HasUpgrade(upgradeId))
                {
                    level++;
                }
            }

            return level;
        }

        /// <summary>One material cost in a building level's bundle (BigDouble so late-run scaling can't overflow).</summary>
        public struct MaterialCost
        {
            public string id;
            public BigDouble amount;
        }

        /// <summary>
        /// The material bundle for the line's next level (design §9 money→XP):
        /// each base material × costGrowth.building^currentLevel. Paid in goods,
        /// competing with the Exchange, offerings, kit, and replanting.
        /// </summary>
        public static List<MaterialCost> NextLevelBundle(GameState state, GameDataAsset data, BuildingData building)
        {
            var growth = BigDouble.Pow(data.economy.costGrowth.building, TotalLevel(state, building));
            var bundle = new List<MaterialCost>();
            foreach (var material in building.materials)
            {
                bundle.Add(new MaterialCost { id = material.id, amount = new BigDouble(material.amount) * growth });
            }

            return bundle;
        }

        /// <summary>True when camp stock covers the next level's whole bundle. A line with no bundle can't be levelled.</summary>
        public static bool CanAfford(GameState state, GameDataAsset data, BuildingData building)
        {
            if (state == null || building == null || building.materials.Count == 0)
            {
                return false;
            }

            foreach (var cost in NextLevelBundle(state, data, building))
            {
                if (state.GetResource(cost.id) < cost.amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Buy the line's next level if camp stock covers the bundle. Returns
        /// false (and changes nothing) otherwise, so the caller can leave the
        /// button disabled.
        /// </summary>
        public static bool TryBuyLevel(GameState state, GameDataAsset data, BuildingData building)
        {
            if (!CanAfford(state, data, building))
            {
                return false;
            }

            foreach (var cost in NextLevelBundle(state, data, building))
            {
                state.resources[cost.id] = state.GetResource(cost.id) - cost.amount;
            }

            state.buildingLevels[building.id] = BoughtLevels(state, building.id) + 1;
            // Basket capacity is snapshot-cached — a bought level changes it.
            state.BumpModifiers();
            return true;
        }

        /// <summary>
        /// Craft-speed multiplier for one station from its line's bought
        /// levels: 1 + perLevel value per level. Multiplies with the skill's
        /// craftSpeedMult upgrades.
        /// </summary>
        public static double StationSpeedMultiplier(GameState state, GameDataAsset data, string stationId)
        {
            var mult = 1.0;
            foreach (var building in data.buildings)
            {
                if (building.perLevel != null
                    && building.perLevel.type == "stationSpeedBonus"
                    && building.perLevel.station == stationId)
                {
                    mult += building.perLevel.value * BoughtLevels(state, building.id);
                }
            }

            return mult;
        }

        /// <summary>Basket-capacity multiplier from Store-style lines: 1 + perLevel value per bought level.</summary>
        public static double BasketCapacityMultiplier(GameState state, GameDataAsset data)
        {
            return Modifiers.Of(state, data).basketCapacityMultiplier;
        }

        /// <summary>The raw derivation — the snapshot builder's path.</summary>
        internal static double ComputeBasketCapacityMultiplier(GameState state, GameDataAsset data)
        {
            var mult = 1.0;
            foreach (var building in data.buildings)
            {
                if (building.perLevel != null && building.perLevel.type == "basketCapacityBonus")
                {
                    mult += building.perLevel.value * BoughtLevels(state, building.id);
                }
            }

            return mult;
        }

        /// <summary>
        /// Familiar-comfort XP multiplier from the Roosts line (design §4:
        /// "the building line levels familiar comfort — +XP rate for all
        /// stationed familiars per level"): 1 + perLevel value per bought
        /// level. Resting familiars earn nothing, so nothing to comfort.
        /// </summary>
        public static double ComfortXpMultiplier(GameState state, GameDataAsset data)
        {
            var mult = 1.0;
            if (state == null || data?.buildings == null)
            {
                return mult;
            }

            foreach (var building in data.buildings)
            {
                if (building.perLevel != null && building.perLevel.type == "comfort")
                {
                    mult += building.perLevel.value * BoughtLevels(state, building.id);
                }
            }

            return mult;
        }
    }
}
