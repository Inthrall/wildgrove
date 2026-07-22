using System;

namespace Wildgrove.Sim
{
    /// <summary>
    /// One member of the warden's kith (design §4): an individual with a name,
    /// a species (at most one familiar of each species ever), a run-track level
    /// (derived from <see cref="xp"/>), a permanent Kinship track, and a
    /// stationing post. Its abilities come from its species' single fixed
    /// trait (<see cref="Traits"/>) — there is no per-familiar build.
    ///
    /// Levels never scale output — a familiar's level only paces its XP and
    /// Kinship; throughput and yield come from tools, hauling equipment,
    /// traits and richness (§4).
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

        /// <summary>Species id into <c>GameDataAsset.SpeciesById</c> — drives the trait and name suggestions.</summary>
        public string speciesId;

        /// <summary>Run XP earned at its post (§4). Level derives from this via <see cref="XpCurve"/>; reset to 0 at Migration.</summary>
        public double xp;

        /// <summary>Permanent Kinship XP — the creature's memory of careful hands (§4). Survives Migration; run xp converts into it on the fold.</summary>
        public double kinshipXp;

        /// <summary>
        /// Where this familiar is stationed: a node id, <see cref="TrailStation"/>,
        /// a <see cref="DigStationPrefix"/> site, or null/empty when it rests at
        /// camp. A stationed familiar holds one of the kith's slots (§4 ladder);
        /// a resting one works nothing and earns nothing, waiting to be called.
        /// </summary>
        public string stationId;

        /// <summary>True for a bonded familiar (§4): it crosses the fold and is present from minute one. Bonding is the rare honour; the roster entry keeps its name and Kinship.</summary>
        public bool bonded;

        /// <summary>True for a gift pile's arrival (§4) — each answered pile spends one of the piles the verses have earned.</summary>
        public bool gifted;

        /// <summary>The bond id this familiar was earned or honoured by (design §4), or null — keeps <see cref="Roster.SyncBonded"/> idempotent.</summary>
        public string bondId;

        /// <summary>An unstationed familiar rests at camp — no post, no slot, no output (§4).</summary>
        public bool IsResting => string.IsNullOrEmpty(stationId);

        /// <summary>True when stationed at the trail post (holding a haul lane, gathering nothing).</summary>
        public bool IsOnTrail => stationId == TrailStation;
    }
}
