using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// Authoring entry for one camp building line (design/data/buildings.json).
    /// Named §9 upgrades act as milestone levels of the line; further levels
    /// are bought on the §8 cost curve and each grants <see cref="PerLevel"/>.
    /// </summary>
    public sealed class BuildingDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>The base material bundle for the line's next level (design §9 money→XP: buildings are a goods sink). Scaled by costGrowth.building^level.</summary>
        public Dictionary<string, int> Materials { get; set; } = new Dictionary<string, int>();

        public List<string> MilestoneUpgradeIds { get; set; } = new List<string>();
        public BuildingPerLevelDef PerLevel { get; set; }
    }

    /// <summary>What each bought level of a building line grants.</summary>
    public sealed class BuildingPerLevelDef
    {
        /// <summary>"stationSpeedBonus" (needs Station + Value) | "basketCapacityBonus" (needs Value) | "familiarCaps" (the §8 roost formulas).</summary>
        public string Type { get; set; }

        public string Station { get; set; }
        public double Value { get; set; }
    }
}
