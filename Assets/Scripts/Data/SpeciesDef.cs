using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// A species' single fixed trait (design doc §4): what makes it the
    /// specialist of one post. Interpreted by the sim: nodeYieldBonus (at a
    /// node of <see cref="Resource"/>), trailThroughputBonus, pristineBonus,
    /// digSpeedBonus.
    /// </summary>
    public sealed class TraitDef
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>What the trait does — interpreted by the sim.</summary>
        public string Kind { get; set; }

        public double Value { get; set; }

        /// <summary>
        /// The related pair of resources the trait is specialised to (e.g.
        /// copper-scree + tin-seam) — the familiar works either node. Empty
        /// means it applies wherever the familiar is posted (trail/watch/pristine).
        /// </summary>
        public List<string> Resources { get; set; } = new List<string>();

        /// <summary>
        /// Legacy single-resource form, still read when <see cref="Resources"/>
        /// is empty so older authoring files keep parsing.
        /// </summary>
        public string Resource { get; set; }
    }

    /// <summary>
    /// A familiar species (design doc §4): the kith is a collection of
    /// individuals — at most one familiar of each species ever walks with the
    /// warden, and each species carries a single fixed trait. Stationing is
    /// never locked to the lean — any familiar can hold any post.
    /// </summary>
    public sealed class SpeciesDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>"gatherer" or "carrier" — the species' natural lean (flavour + suggested first station), never a hard restriction.</summary>
        public string RoleLean { get; set; }

        public List<string> SuggestedNames { get; set; } = new List<string>();
        public TraitDef Trait { get; set; }
    }
}
