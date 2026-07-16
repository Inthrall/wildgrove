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
    /// roosts line's familiar caps (§8 formulas via economy.familiarCaps).
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

        /// <summary>Coin cost of the line's next level: base · growth^currentLevel (design §8).</summary>
        public static BigDouble NextLevelCost(GameState state, GameDataAsset data, BuildingData building)
        {
            var growth = BigDouble.Pow(data.economy.costGrowth.building, TotalLevel(state, building));
            return building.baseCostCoin * growth;
        }

        /// <summary>
        /// Buy the line's next level if the purse covers it. Returns false
        /// (and changes nothing) otherwise, so the caller can leave the
        /// button disabled.
        /// </summary>
        public static bool TryBuyLevel(GameState state, GameDataAsset data, BuildingData building)
        {
            if (state == null || building == null)
            {
                return false;
            }

            var cost = NextLevelCost(state, data, building);
            if (state.coin < cost)
            {
                return false;
            }

            state.coin -= cost;
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

        /// <summary>The §8 roostLevel: bought levels of lines whose perLevel is familiarCaps.</summary>
        public static int RoostLevel(GameState state, GameDataAsset data)
        {
            var level = 0;
            foreach (var building in data.buildings)
            {
                if (building.perLevel != null && building.perLevel.type == "familiarCaps")
                {
                    level += BoughtLevels(state, building.id);
                }
            }

            return level;
        }

        /// <summary>
        /// Gatherer familiars allowed per zone (design §8: flockCap = base +
        /// perRoostLevel · roostLevel). Unlimited when the data has no caps —
        /// hand-built test data predating the cap system.
        /// </summary>
        public static int FlockCap(GameState state, GameDataAsset data)
        {
            var caps = data.economy?.familiarCaps;
            if (caps == null)
            {
                return int.MaxValue;
            }

            return caps.flockCapBase + caps.flockCapPerRoostLevel * RoostLevel(state, data);
        }

        /// <summary>Camp-wide carrier slots (design §8: base + perRoostLevel · roostLevel). Unlimited without cap data.</summary>
        public static int CarrierSlots(GameState state, GameDataAsset data)
        {
            var caps = data.economy?.familiarCaps;
            if (caps == null)
            {
                return int.MaxValue;
            }

            return caps.carrierSlotsBase + caps.carrierSlotsPerRoostLevel * RoostLevel(state, data);
        }
    }
}
