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

        /// <summary>v2+: the camp-wide carrier pool (v1 saves predate carriers; migration grants the regional seed).</summary>
        public int carrierCount;

        public List<SavedResource> resources = new List<SavedResource>();
        public List<SavedNode> nodes = new List<SavedNode>();
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

        /// <summary>v2+: goods gathered but not yet hauled to camp (absent in v1 — defaults to zero).</summary>
        [JsonConverter(typeof(BigDoubleJsonConverter))]
        public BigDouble basket;
    }
}
