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

        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble coin;

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

        /// <summary>v2+: the camp-wide carrier pool (v1 saves predate carriers; migration grants the regional seed).</summary>
        public int carrierCount;

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

        /// <summary>v9+: dig sites and their pity timers (absent before excavation — sites resync from owned upgrades).</summary>
        public List<SavedDigSite> digSites = new List<SavedDigSite>();

        /// <summary>v9+: fossil fragments surfaced, per fossil id (absent before excavation — nothing found yet).</summary>
        public List<SavedFossilFragments> fossilFragments = new List<SavedFossilFragments>();

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
        public int familiarCount;
        public double pityHours;
    }

    /// <summary>Fragments surfaced toward one fossil.</summary>
    [Serializable]
    public sealed class SavedFossilFragments
    {
        public string id;
        public int fragments;
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
        public int familiarCount;

        /// <summary>v6+: mastery XP (replaces v≤5's masteryLevel, which nothing ever granted — dropped on read).</summary>
        public double masteryXp;

        public double tendBurstRemaining;

        /// <summary>v8+: seconds left on the post-tend Pristine window (absent before quality rolls — defaults to zero).</summary>
        public double pristineBonusRemaining;

        /// <summary>v2+: goods gathered but not yet hauled to camp (absent in v1 — defaults to zero).</summary>
        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble basket;
    }
}
