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

        /// <summary>v10+: lifetime Renown from Rite offerings (absent before the Rite runtime — nothing offered yet).</summary>
        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble renown;

        /// <summary>v11+: completed Migrations (absent before the prestige build — zero).</summary>
        public int migrationCount;

        /// <summary>v12+: Almanac nodes bought with Verdure (absent before the Almanac — none owned).</summary>
        public List<string> almanacNodeIds = new List<string>();

        /// <summary>v13+: the warden's worn kit, one gear id per slot (absent before the gear system — bare hands).</summary>
        public List<SavedGearSlot> gear = new List<SavedGearSlot>();

        /// <summary>v14–v20 legacy wire name for fixed specimens — read only by the v20→v21 migration.</summary>
        public List<string> donatedResources = new List<string>();

        /// <summary>v21+: resources whose Pristine specimen was fixed into the Folio (design §6). Renamed from donatedResources.</summary>
        public List<string> fixedResources = new List<string>();

        /// <summary>v15–v18 wire name for the warden's post — read only by the v18→v19 migration.</summary>
        public string bondedPostNodeId;

        /// <summary>v19+: the warden's post (null = the first node; the bonded gatherers work at the warden's side).</summary>
        public string wardenPostNodeId;

        /// <summary>v17+: Amber (absent before the amber system — none held).</summary>
        public double amber;

        /// <summary>v18+: zones whose waystone has been read (absent before — every unlocked stone shows once).</summary>
        public List<string> seenWaystoneZoneIds = new List<string>();

        /// <summary>v16+: Compendium lifetime counters (absent before — nothing recorded yet).</summary>
        public List<SavedResource> lifetimeGathered = new List<SavedResource>();
        public List<SavedTally> lifetimeCrafted = new List<SavedTally>();
        public List<SavedResource> lifetimePristine = new List<SavedResource>();

        /// <summary>v2–v19 legacy: the anonymous camp-wide carrier count. Read only by the v19→v20 migration, which rebuilds it into <see cref="roster"/>.</summary>
        public int carrierCount;

        /// <summary>v20+: the warden's crew — every familiar as an individual (design §4). Replaces the anonymous per-node/per-camp counts.</summary>
        public List<SavedFamiliar> roster = new List<SavedFamiliar>();

        /// <summary>v20+: sequence for minting roster ids.</summary>
        public int nextFamiliarSeq;

        /// <summary>v7+: seconds toward the fleet's next delivery (absent before discrete hauling — defaults to a fresh trip).</summary>
        public double haulTripProgress;

        public List<SavedResource> resources = new List<SavedResource>();

        /// <summary>v8+: Fine-quality finds per resource (absent before quality rolls — pools start empty).</summary>
        public List<SavedResource> fineResources = new List<SavedResource>();

        /// <summary>v8+: Pristine specimens per resource (absent before quality rolls — pools start empty).</summary>
        public List<SavedResource> pristineResources = new List<SavedResource>();

        /// <summary>v8+: xorshift64* state for the run's rolls (0 in older saves — restore reseeds).</summary>
        public ulong rngState;

        public List<SavedNode> nodes = new List<SavedNode>();

        /// <summary>v9+: observation sites and their pity timers (absent before the observation system — sites resync from owned upgrades).</summary>
        public List<SavedDigSite> digSites = new List<SavedDigSite>();

        /// <summary>v23+: planters built this run (design §3), each attached to a node or dig site (absent before — none built).</summary>
        public List<SavedPlanter> builtPlanters = new List<SavedPlanter>();

        /// <summary>v9+: field sketches recorded, per insect id (absent before the observation system — nothing recorded yet). Renamed from v23's fossilFragments — the collectible changed entirely (§6), so old fossil progress does not carry.</summary>
        public List<SavedInsectSketches> insectSketches = new List<SavedInsectSketches>();

        /// <summary>v10+: warden deed counts (absent before the Rite runtime).</summary>
        public List<SavedDeedCount> deedCounts = new List<SavedDeedCount>();

        /// <summary>v10+: offering progress per verse of the Rite (absent before the Rite runtime).</summary>
        public List<SavedVerseProgress> verseProgress = new List<SavedVerseProgress>();

        public List<string> purchasedUpgradeIds = new List<string>();

        /// <summary>v3+: crafting stations and their work in progress (absent before crafting existed).</summary>
        public List<SavedStation> stations = new List<SavedStation>();

        /// <summary>v4+: bought camp building levels per line (absent before buildings existed).</summary>
        public List<SavedBuildingLevel> buildingLevels = new List<SavedBuildingLevel>();

        /// <summary>v5+: total XP per skill (absent before the XP system existed; levels are derived, never stored).</summary>
        public List<SavedSkillXp> skillXp = new List<SavedSkillXp>();
    }

    /// <summary>One skill's earned XP.</summary>
    [Serializable]
    public sealed class SavedSkillXp
    {
        public string id;
        public double xp;
    }

    /// <summary>One crew familiar (design §4). Level derives from xp; Kinship, powerups, station, and bond marker persist.</summary>
    [Serializable]
    public sealed class SavedFamiliar
    {
        public string id;
        public string name;
        public string speciesId;
        public double xp;
        public double kinshipXp;
        public List<string> powerupIds = new List<string>();
        public string stationId;
        public bool bonded;
        public string bondId;
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
    }

    /// <summary>One dig site's diggers and pity progress (identity resyncs from owned unlockDigSite upgrades on restore).</summary>
    [Serializable]
    public sealed class SavedDigSite
    {
        public string zoneId;

        /// <summary>v9–v19 legacy anonymous digger count; the v19→v20 migration rebuilds it into the roster.</summary>
        public int familiarCount;

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

        /// <summary>v2–v19 legacy anonymous gatherer count; the v19→v20 migration rebuilds it into the roster.</summary>
        public int familiarCount;

        /// <summary>v6+: mastery XP (replaces v≤5's masteryLevel, which nothing ever granted — dropped on read).</summary>
        public double masteryXp;

        /// <summary>v22+: replanting richness level (design §3; absent before — defaults to 0).</summary>
        public int richnessLevel;

        public double tendBurstRemaining;

        /// <summary>v8+: seconds left on the post-tend Pristine window (absent before quality rolls — defaults to zero).</summary>
        public double pristineBonusRemaining;

        /// <summary>v2+: goods gathered but not yet hauled to camp (absent in v1 — defaults to zero).</summary>
        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble basket;
    }
}
