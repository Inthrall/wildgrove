using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// One powerup a species can offer (design doc §4): a fixed, authored pick
    /// a familiar chooses at a level-5 milestone and keeps for the run. Pools
    /// are deterministic per species at MVP — the Rite generator can rely on
    /// what any kith can become.
    /// </summary>
    public sealed class PowerupDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// What the powerup does — interpreted by the sim: nodeYieldBonus,
        /// trailThroughputBonus, pristineBonus, digSpeedBonus, offlineBonus.
        /// </summary>
        public string Kind { get; set; }

        public double Value { get; set; }

        /// <summary>
        /// The resource the powerup is specialised to (e.g. berries) — null
        /// means it applies wherever the familiar is posted.
        /// </summary>
        public string Resource { get; set; }
    }

    /// <summary>
    /// A familiar species (design doc §4): the small flock is made of
    /// individuals, each with a name, a role lean, and a species powerup pool.
    /// Stationing is never locked to the lean — any familiar can hold any post.
    /// </summary>
    public sealed class SpeciesDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>"gatherer" or "carrier" — the species' natural lean (flavour + suggested first station), never a hard restriction.</summary>
        public string RoleLean { get; set; }

        public List<string> SuggestedNames { get; set; } = new List<string>();
        public List<PowerupDef> Powerups { get; set; } = new List<PowerupDef>();
    }
}
