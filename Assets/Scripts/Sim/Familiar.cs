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
    /// Kinship; yield comes from tools, traits and richness (§4).
    /// </summary>
    [Serializable]
    public sealed class Familiar
    {
        /// <summary>Legacy trail-post station id — the haul lane retired when deliveries became automatic. Read only by save migration.</summary>
        public const string LegacyTrailStation = "trail";

        /// <summary>
        /// The fell pony's own station, at the warden's side (§11 — The
        /// Drover's Halter). Bijective with the pony: nothing else may stand
        /// here, and the pony may stand nowhere else — which is what makes its
        /// slot exemption safe (<see cref="Kith.Walking"/>), since it can never
        /// carry a free slot off to a node.
        /// </summary>
        public const string PonyStation = "pony-lane";

        /// <summary>The species that walks <see cref="PonyStation"/> — the reward grants an animal, not a post.</summary>
        public const string PonySpecies = "fell-pony";

        /// <summary>
        /// The wander-post station id: its holder walks the run watching the
        /// small lives at every observation site (the watch is not a post of
        /// its own). Watching is ALL the post does — a wanderer gathers
        /// nothing (a roamer who also gathered read as two jobs on one post,
        /// and players couldn't say what the post was for).
        /// </summary>
        public const string WanderStation = "wander";

        /// <summary>Prefix for a dig-site station id ("dig:{zoneId}"). Not a post the game hands out — the watch is the wanderer's — so a familiar carrying one is rested on load; see SaveCodec.StationValid.</summary>
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
        /// Where this familiar is stationed: a node id,
        /// <see cref="WanderStation"/>, or null/empty when it rests at camp.
        /// Every post holds at most ONE body — warden or familiar (§2). A
        /// stationed familiar holds one of the kith's slots (§4 ladder); a
        /// resting one works nothing and earns nothing, waiting to be called.
        /// </summary>
        public string stationId;

        /// <summary>True for a bonded familiar (§4): it crosses the fold and is present from minute one. Bonding is the rare honour; the roster entry keeps its name and Kinship.</summary>
        public bool bonded;

        /// <summary>True for a gift pile's arrival (§4) — each answered pile spends one of the piles the verses have earned.</summary>
        public bool gifted;

        /// <summary>The bond id this familiar was earned or honoured by (design §4), or null — keeps <see cref="Roster.SyncBonded"/> idempotent.</summary>
        public string bondId;

        /// <summary>
        /// A field-for-field copy. MemberwiseClone rather than an assignment
        /// list so a field added above can never be forgotten here; every field
        /// is a value or an immutable string, so the shallow copy is a whole
        /// one. Used where a familiar must be changed without the roster it
        /// came from changing under its holder — <see cref="Migration.Migrate"/>.
        /// </summary>
        public Familiar Copy()
        {
            return (Familiar)MemberwiseClone();
        }

        /// <summary>An unstationed familiar rests at camp — no post, no slot, no output (§4).</summary>
        public bool IsResting => string.IsNullOrEmpty(stationId);

        /// <summary>
        /// True for the fell pony (§11): it holds no slot, is always at its lane
        /// while owned, and can be neither moved nor rested. Keyed on the species
        /// rather than the station so the exemption travels with the animal.
        /// </summary>
        public bool IsPony => speciesId == PonySpecies;

        /// <summary>True when stationed at the wander post (walking the watch — every observation site, no gathering).</summary>
        public bool IsWandering => stationId == WanderStation;
    }
}
