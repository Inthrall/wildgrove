using System;
using System.Collections.Generic;
using BreakInfinity;
using Newtonsoft.Json;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// The versioned on-disk shape of a run (dev-setup: versioned JSON with
    /// migration hooks; cloud Saved Games layers on top in Phase 5). Field
    /// names are the wire format — renaming one is a save-format change and
    /// needs a version bump plus a migration step in <see cref="SaveCodec"/>.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version;

        /// <summary>UTC wall-clock of the save, unix milliseconds — the baseline for offline credit on the next load.</summary>
        public long savedAtUnixMs;

        public double verdurePoints;

        /// <summary>Lifetime Renown from Rite offerings.</summary>
        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble renown;

        /// <summary>Completed Migrations.</summary>
        public int migrationCount;

        /// <summary>Almanac nodes bought with Verdure.</summary>
        public List<string> almanacNodeIds = new List<string>();

        /// <summary>The warden's worn kit, one gear id per slot.</summary>
        public List<SavedGearSlot> gear = new List<SavedGearSlot>();

        /// <summary>The kit bag — every piece made this run, so a displaced piece can be worn again for nothing.</summary>
        public List<string> gearCrafted = new List<string>();

        /// <summary>Resources whose Choice specimen was fixed into the Folio (design §6).</summary>
        public List<string> fixedResources = new List<string>();

        /// <summary>The warden's post; null means the warden stands at camp.</summary>
        public string wardenPostNodeId;

        /// <summary>Amber held (design §10).</summary>
        public double amber;

        /// <summary>Verses sung in folded runs.</summary>
        public int foldedVersesSung;

        /// <summary>Kith slots owned through the store. Bridges sessions that start before billing resolves.</summary>
        public int purchasedKithSlots;

        /// <summary>Whether the starter bundle's one-time Amber grant has been paid out.</summary>
        public bool starterBundleAmberGranted;

        /// <summary>Whether The Drover's Halter has been redeemed.</summary>
        public bool droversHalterOwned;

        /// <summary>Whether The Wayfarer's Plate has been redeemed.</summary>
        public bool wayfarersPlateOwned;

        /// <summary>UTC unix ms of the last weekly Amber cache claim.</summary>
        public long weeklyCacheClaimedUnixMs;

        /// <summary>UTC unix ms of the last rewarded Amber-drip claim.</summary>
        public long adDripClaimedUnixMs;

        /// <summary>UTC unix ms of the last rewarded time-skip.</summary>
        public long timeSkipClaimedUnixMs;

        /// <summary>Paid-skip budget hours left at the stamp below.</summary>
        public double timeSkipBudgetHours;

        /// <summary>UTC unix ms the paid-skip budget was last settled.</summary>
        public long timeSkipBudgetStampUnixMs;

        /// <summary>The latest UTC unix ms the run has ever been told it is — the clock ratchet (v43). 0 = never read.</summary>
        public long clockHighWaterUnixMs;

        /// <summary>Accumulated foreground play time in ms. Monotonic; the basis cloud saves are compared on.</summary>
        public long playedMs;

        /// <summary>Zones whose waystone has been read.</summary>
        public List<string> seenWaystoneZoneIds = new List<string>();

        /// <summary>Final waystones read, a count into the authored order.</summary>
        public int finalWaystonesRead;

        /// <summary>The fold the last final waystone was read on; -1 is "none yet", and only meaningful when finalWaystonesRead > 0.</summary>
        public int finalWaystoneLastFold = -1;

        /// <summary>Compendium lifetime counters.</summary>
        public List<SavedResource> lifetimeGathered = new List<SavedResource>();
        public List<SavedTally> lifetimeCrafted = new List<SavedTally>();

        /// <summary>Choice specimens ever found, per resource.</summary>
        public List<SavedResource> lifetimeChoice = new List<SavedResource>();

        /// <summary>Every species ever befriended and every station that has ever finished a batch.</summary>
        public List<string> speciesEverBefriended = new List<string>();
        public List<string> stationsEverWorked = new List<string>();

        /// <summary>The warden's kith — every familiar as an individual (design §4).</summary>
        public List<SavedFamiliar> roster = new List<SavedFamiliar>();

        /// <summary>Sequence for minting roster ids.</summary>
        public int nextFamiliarSeq;

        /// <summary>Seconds toward the next delivery batch.</summary>
        public double deliveryProgress;

        public List<SavedResource> resources = new List<SavedResource>();

        /// <summary>Decent-quality finds per resource.</summary>
        public List<SavedResource> decentResources = new List<SavedResource>();

        /// <summary>Choice specimens per resource.</summary>
        public List<SavedResource> choiceResources = new List<SavedResource>();

        /// <summary>Xorshift64* state for the run's rolls; zero is not a usable state, so restore reseeds it.</summary>
        public ulong rngState;

        public List<SavedNode> nodes = new List<SavedNode>();

        /// <summary>Observation sites and their pity timers.</summary>
        public List<SavedDigSite> digSites = new List<SavedDigSite>();

        /// <summary>Planters built this run (design §3), each attached to a node or dig site.</summary>
        public List<SavedPlanter> builtPlanters = new List<SavedPlanter>();

        /// <summary>Field sketches recorded, per insect id (design §6).</summary>
        public List<SavedInsectSketches> insectSketches = new List<SavedInsectSketches>();

        /// <summary>Warden deed counts.</summary>
        public List<SavedDeedCount> deedCounts = new List<SavedDeedCount>();

        /// <summary>Offering progress per verse of the Rite.</summary>
        public List<SavedVerseProgress> verseProgress = new List<SavedVerseProgress>();

        public List<string> purchasedUpgradeIds = new List<string>();

        /// <summary>Crafting stations and their work in progress.</summary>
        public List<SavedStation> stations = new List<SavedStation>();

        /// <summary>Tincture buffs currently live.</summary>
        public List<SavedTincture> activeTinctures = new List<SavedTincture>();

        /// <summary>Deep amber pieces surfaced, a count into the authored order.</summary>
        public int deepAmberFound;

        /// <summary>Hours watched at the deep site without a piece surfacing — the pity clock.</summary>
        public double deepAmberPityHours;

        /// <summary>Bought camp building levels per line.</summary>
        public List<SavedBuildingLevel> buildingLevels = new List<SavedBuildingLevel>();

        /// <summary>Levels held on each repeatable Almanac line.</summary>
        public List<SavedAlmanacLevel> almanacLevels = new List<SavedAlmanacLevel>();

        /// <summary>Total XP per skill.</summary>
        public List<SavedSkillXp> skillXp = new List<SavedSkillXp>();
    }

    /// <summary>One skill's earned XP.</summary>
    [Serializable]
    public sealed class SavedSkillXp
    {
        public string id;
        public double xp;
    }

    /// <summary>One kith familiar (design §4). Level derives from xp; Kinship, station, and bond marker persist. No abilities are stored — a familiar's are its species' fixed trait.</summary>
    [Serializable]
    public sealed class SavedFamiliar
    {
        public string id;
        public string name;
        public string speciesId;
        public double xp;
        public double kinshipXp;
        public string stationId;
        public bool bonded;
        public string bondId;

        /// <summary>The gift event's arrival.</summary>
        public bool gifted;
    }

    /// <summary>One kit slot's worn gear.</summary>
    [Serializable]
    public sealed class SavedGearSlot
    {
        public string slot;
        public string gearId;
    }

    /// <summary>One warden deed's lifetime count this run.</summary>
    [Serializable]
    public sealed class SavedDeedCount
    {
        public string id;
        public int count;
    }

    /// <summary>One verse's offering progress; slots parallel the verse's data slots by index.</summary>
    [Serializable]
    public sealed class SavedVerseProgress
    {
        public string verseId;
        public List<SavedSlotProgress> slots = new List<SavedSlotProgress>();
    }

    /// <summary>One offering slot's progress.</summary>
    [Serializable]
    public sealed class SavedSlotProgress
    {
        public double delivered;
        public bool granted;

        /// <summary>The run's deed count when this slot's verse revealed — a deed slot counts only the work done since.</summary>
        public double deedBaseline;

        /// <summary>Whether <see cref="deedBaseline"/> has been taken (zero is a real baseline, so it needs its own flag).</summary>
        public bool deedBaselineSet;
    }

    /// <summary>One observation site's pity progress (identity resyncs from owned unlockDigSite upgrades on restore; the watching itself comes from the wanderer, not from anyone posted here).</summary>
    [Serializable]
    public sealed class SavedDigSite
    {
        public string zoneId;

        public double pityHours;
    }

    /// <summary>One built planter and the node or dig site it's attached to (design §3).</summary>
    [Serializable]
    public sealed class SavedPlanter
    {
        public string planterId;
        public string targetId;
    }

    /// <summary>Field sketches recorded of one insect (design §6).</summary>
    [Serializable]
    public sealed class SavedInsectSketches
    {
        public string id;
        public int sketches;
    }

    /// <summary>Bought levels of one camp building line (§9 milestone upgrades live in purchasedUpgradeIds).</summary>
    [Serializable]
    public sealed class SavedBuildingLevel
    {
        public string id;
        public int levels;
    }

    /// <summary>Levels held on one repeatable Almanac line.</summary>
    [Serializable]
    public sealed class SavedAlmanacLevel
    {
        public string id;
        public int levels;
    }

    /// <summary>One crafting station's assignment and in-flight batch.</summary>
    [Serializable]
    public sealed class SavedStation
    {
        public string stationId;
        public string recipeId;
        public bool inFlight;
        public double progressSeconds;
    }

    [Serializable]
    public sealed class SavedTincture
    {
        public string tinctureId;
        public double remainingSeconds;
    }

    [Serializable]
    public sealed class SavedResource
    {
        public string id;

        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble amount;
    }

    /// <summary>A plain-double tally keyed by id (Compendium crafted-batch counts).</summary>
    public sealed class SavedTally
    {
        public string id;
        public double count;
    }

    /// <summary>
    /// The player-earned fields of one gathering node. Identity and derived
    /// values (zone, resource, skill, yieldMultiplier) are rebuilt from the
    /// current content data on restore, so a save taken on older data
    /// self-corrects instead of trusting stale content.
    /// </summary>
    [Serializable]
    public sealed class SavedNode
    {
        public string id;

        /// <summary>Mastery XP.</summary>
        public double masteryXp;

        /// <summary>Replanting richness level (design §3).</summary>
        public int richnessLevel;

        public double tendBurstRemaining;

        /// <summary>Seconds left on the post-tend Choice window.</summary>
        public double choiceBonusRemaining;

        /// <summary>The pickings pooled at the node awaiting the next delivery.</summary>
        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble basket;
    }
}
