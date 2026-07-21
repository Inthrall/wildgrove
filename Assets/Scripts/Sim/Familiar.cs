using System;
using System.Collections.Generic;

namespace Wildgrove.Sim
{
    /// <summary>
    /// One member of the warden's kith (design §4): an individual with a name,
    /// a species, a run-track level (derived from <see cref="xp"/>), a permanent
    /// Kinship track, chosen powerups, and a stationing post. Replaces the
    /// anonymous per-node/per-camp familiar counts — the flock is a roster of
    /// individuals now, and where each one stands is the moment-to-moment
    /// decision (§2).
    ///
    /// Levels never scale output — a familiar's level only paces its XP and
    /// Kinship; throughput and yield come from tools, hauling equipment,
    /// powerups and richness (§4).
    /// </summary>
    [Serializable]
    public sealed class Familiar
    {
        /// <summary>The trail-post station id — hauling is a post, not a species (§2).</summary>
        public const string TrailStation = "trail";

        /// <summary>Prefix for a dig-site station id: "dig:{zoneId}".</summary>
        public const string DigStationPrefix = "dig:";

        /// <summary>Stable per-run roster id (e.g. "fam-1"), minted by <see cref="GameState.NextFamiliarId"/>.</summary>
        public string id;

        /// <summary>Player-given name — a species-appropriate suggestion by default, renameable any time.</summary>
        public string name;

        /// <summary>Species id into <c>GameDataAsset.SpeciesById</c> — drives the powerup pool and name suggestions.</summary>
        public string speciesId;

        /// <summary>Run XP earned at its post (§4). Level derives from this via <see cref="XpCurve"/>; reset to 0 at Migration.</summary>
        public double xp;

        /// <summary>Permanent Kinship XP — the creature's memory of careful hands (§4). Survives Migration; run xp converts into it on the fold.</summary>
        public double kinshipXp;

        /// <summary>Powerup ids chosen this run (one per level-5 milestone), from the species' authored pool. Reset at Migration.</summary>
        public List<string> powerupIds = new List<string>();

        /// <summary>
        /// Where this familiar is stationed: a node id, <see cref="TrailStation"/>,
        /// a <see cref="DigStationPrefix"/> site, or null/empty when it wanders
        /// (§2: ×0.5 rate/XP, no powerups). The warden never wanders.
        /// </summary>
        public string stationId;

        /// <summary>True for a bonded familiar (§4): it crosses the fold and is present from minute one. Bonding is the rare honour; the roster entry keeps its name and Kinship.</summary>
        public bool bonded;

        /// <summary>True for the gift event's arrival (§4: one pile, one yes) — while it walks, no second pile tempts anyone.</summary>
        public bool gifted;

        /// <summary>The bond id this familiar was materialised from (design §4), or null for a normal recruit — keeps <see cref="Roster.SyncBonded"/> idempotent.</summary>
        public string bondId;

        /// <summary>An unassigned familiar wanders — half-hearted help, never zero (§2).</summary>
        public bool IsWandering => string.IsNullOrEmpty(stationId);

        /// <summary>True when stationed at the trail post (holding a haul lane, gathering nothing).</summary>
        public bool IsOnTrail => stationId == TrailStation;
    }
}
