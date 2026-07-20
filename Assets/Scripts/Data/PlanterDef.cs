using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// Authoring entry for one planter (design/data/planters.json, design §3): a
    /// built structure that improves a single gather node or dig site, paid in
    /// another zone's goods (the backward flow — Zone 3 timber has a job in
    /// Zone 1). Bushcraft-gated; the Carving Bench unlocks the recipes. One of
    /// each planter per target. Planters reset at Migration.
    /// </summary>
    public sealed class PlanterDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// What the planter does — interpreted by the sim (Planters.cs):
        /// basketCapacityMult (a bigger basket at a node), nodeYieldMult (a
        /// second yield lane), digSpeedMult (steady a dig site's sketching).
        /// </summary>
        public string Kind { get; set; }

        /// <summary>The fractional bonus this planter adds to its target (0.5 = +50%).</summary>
        public double Value { get; set; }

        /// <summary>Where it can be built: "node" (a gather node) or "digSite".</summary>
        public string Target { get; set; }

        /// <summary>The material bundle to build it, paid from camp stock.</summary>
        public Dictionary<string, int> Materials { get; set; } = new Dictionary<string, int>();
    }
}
