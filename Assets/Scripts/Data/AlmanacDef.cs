using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// One node of the Almanac — the permanent tree bought with Verdure
    /// (design doc §7). Ownership survives Migration; Verdure is allocated,
    /// never destroyed (see almanac.json's $comment).
    /// </summary>
    public sealed class AlmanacDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double CostVerdure { get; set; }

        /// <summary>Single prerequisite node id; null for a root node.</summary>
        public string Requires { get; set; }

        /// <summary>
        /// A line bought over and over rather than learned once: level L costs
        /// CostVerdure · costGrowth.almanac^L, and its effects scale with the
        /// levels held. The endless sink that keeps Verdure worth banking once
        /// the one-off tree is finished.
        /// </summary>
        public bool Repeatable { get; set; }

        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
