using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class UpgradeDef
    {
        public int Order { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Track { get; set; }

        /// <summary>The tool tier this upgrade represents owning (economy.tools.tiers), for zone gating; null for non-tier upgrades.</summary>
        public string ToolTier { get; set; }

        /// <summary>Skill gate (design §9 money→XP): the skill that must reach <see cref="GateLevel"/> before this can be bought. Null/empty = no skill gate.</summary>
        public string GateSkill { get; set; }
        public int GateLevel { get; set; }

        /// <summary>
        /// Folds that must be behind the warden before this rung is on the
        /// ladder (design doc §8); 0 (the default) = from the first run. A map
        /// rung also inherits its zone's gate, so a trail is gated in one place
        /// (the zone) rather than two.
        /// </summary>
        public int MinMigration { get; set; }

        public Dictionary<string, int> Materials { get; set; } = new Dictionary<string, int>();
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
