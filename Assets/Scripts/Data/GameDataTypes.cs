using System;
using System.Collections.Generic;
using BreakInfinity;

namespace Wildgrove.Data
{
    // Unity-serializable runtime shapes generated from the authoring model by
    // GameDataMapper. Dictionaries become entry lists (Unity cannot serialize
    // dictionaries) and currency amounts are BigDouble per the locked
    // conventions in docs/dev-setup.md.

    [Serializable]
    public sealed class ItemAmount
    {
        public string id;
        public int amount;
    }

    [Serializable]
    public sealed class EffectData
    {
        public EffectType type;
        public string skill;
        public string zone;
        public string resource;
        public string recipe;
        public double value;
    }

    [Serializable]
    public sealed class ResourceData
    {
        public string id;

        /// <summary>Base Provisioner sell value in Coin per unit (raw gatherables only).</summary>
        public double sellValue;

        /// <summary>The gathering skill that works this resource — drives node upgrade targeting.</summary>
        public string skill;
    }

    [Serializable]
    public sealed class ZoneData
    {
        public string id;
        public int order;
        public string displayName;
        public List<string> resources = new List<string>();
        public List<string> unlocks = new List<string>();
        public string keystone;
        public bool digSite;
        public string verseSite;

        /// <summary>Tool tier the zone's trail map demands (design §3); null/empty = ungated.</summary>
        public string requiredTool;

        public string scope;
    }

    [Serializable]
    public sealed class UpgradeData
    {
        public int order;
        public string id;
        public string displayName;
        public string track;

        /// <summary>The tool tier owning this upgrade represents (economy.tools.tiers), for zone gating; null/empty for non-tier upgrades.</summary>
        public string toolTier;

        /// <summary>Skill gate (design §9 money→XP): the skill that must reach <see cref="gateLevel"/> to buy this. Null/empty = no skill gate.</summary>
        public string gateSkill;
        public int gateLevel;

        public List<ItemAmount> materials = new List<ItemAmount>();
        public List<EffectData> effects = new List<EffectData>();
    }

    [Serializable]
    public sealed class RecipeData
    {
        public string id;
        public string station;
        public string skill;
        public List<ItemAmount> inputs = new List<ItemAmount>();
        public string output;
        public double valueMult;
        public string kind;
        public bool defaultKnown;

        /// <summary>The station line level the recipe needs (design §9 heat: iron is forge 2). 1 = any built station.</summary>
        public int stationLevel = 1;

        /// <summary>The skill level the recipe needs (design §4: levels gate recipes). 1 = available from the start.</summary>
        public int skillLevel = 1;
    }

    /// <summary>
    /// One camp building line (design §9) — the repeatable Coin sink. Named §9
    /// upgrades are milestone levels; bought levels each grant perLevel.
    /// </summary>
    [Serializable]
    public sealed class BuildingData
    {
        public string id;
        public string displayName;

        /// <summary>Base material bundle for the next level (design §9 money→XP), scaled by costGrowth.building^level.</summary>
        public List<ItemAmount> materials = new List<ItemAmount>();

        public List<string> milestoneUpgradeIds = new List<string>();
        public BuildingPerLevelData perLevel;
    }

    [Serializable]
    public sealed class BuildingPerLevelData
    {
        /// <summary>"stationSpeedBonus" | "basketCapacityBonus" | "familiarCaps".</summary>
        public string type;
        public string station;
        public double value;
    }

    [Serializable]
    public sealed class GearData
    {
        public string id;
        public string displayName;
        public string slot;
        public string skill;
        public List<ItemAmount> materials = new List<ItemAmount>();
        public List<EffectData> effects = new List<EffectData>();
    }

    [Serializable]
    public sealed class FossilData
    {
        public string id;
        public string displayName;
        public int fragments;
        public List<string> digSites = new List<string>();
        public double strataRarity;
        public List<EffectData> effects = new List<EffectData>();
    }

    /// <summary>One Museum set (design §5): donate a Pristine specimen of each entry; completion grants a permanent effect surviving Migration.</summary>
    [Serializable]
    public sealed class MuseumSetData
    {
        public string id;
        public string displayName;
        public List<string> entries = new List<string>();
        public List<EffectData> effects = new List<EffectData>();
    }

    /// <summary>One node of the Almanac — the permanent Verdure tree (design §7). Ownership survives Migration.</summary>
    [Serializable]
    public sealed class AlmanacNodeData
    {
        public string id;
        public string displayName;
        public double costVerdure;

        /// <summary>Single prerequisite node id; null/empty for a root node.</summary>
        public string requires;

        public List<EffectData> effects = new List<EffectData>();
    }

    [Serializable]
    public sealed class RiteSlotData
    {
        public RiteSlotType type;
        public string resource;
        public long amount;
        public string deed;
        public int count;
        public string quality;
        public long renownGrant;
    }

    [Serializable]
    public sealed class RiteVerseData
    {
        public string id;
        public string zone;
        public List<string> spotlight = new List<string>();
        public List<RiteSlotData> slots = new List<RiteSlotData>();
    }

    [Serializable]
    public sealed class RiteData
    {
        public string id;
        public int migration;
        public List<RiteVerseData> verses = new List<RiteVerseData>();
    }

    [Serializable]
    public sealed class BondSourceData
    {
        /// <summary>"museumSet" or "almanacNode".</summary>
        public string type;
        public string id;
    }

    /// <summary>
    /// A bonded familiar (design §7): a permanent, role-locked companion
    /// earned from exactly one source, crossing every Migration. Earned
    /// state is derived from the source — never stored.
    /// </summary>
    [Serializable]
    public sealed class BondData
    {
        public string id;
        public string displayName;

        /// <summary>Species id (into species.json) — drives the bonded familiar's powerup pool.</summary>
        public string species;

        /// <summary>Legacy role hint ("gatherer"/"carrier") — flavour only now that carrying is a post.</summary>
        public string role;

        public BondSourceData source;
    }

    /// <summary>One powerup in a species' authored pool (design §4). Kept for the run once chosen; interpreted by the sim by <see cref="kind"/>.</summary>
    [Serializable]
    public sealed class PowerupData
    {
        public string id;
        public string displayName;
        public string description;

        /// <summary>nodeYieldBonus / trailThroughputBonus / pristineBonus / digSpeedBonus / offlineBonus.</summary>
        public string kind;

        public double value;

        /// <summary>Resource this powerup is specialised to, or null when it applies wherever the familiar is posted.</summary>
        public string resource;
    }

    /// <summary>The Exchange's barter constants (design §9) — rates derive from the trade-value table.</summary>
    [Serializable]
    public sealed class ExchangeData
    {
        public double spread;
    }

    /// <summary>
    /// A familiar species (design §4): a role lean, suggested names for the
    /// player to accept or edit on arrival, and a fixed powerup pool.
    /// </summary>
    [Serializable]
    public sealed class SpeciesData
    {
        public string id;
        public string displayName;

        /// <summary>"gatherer" or "carrier" — the natural lean, never a hard restriction on stationing.</summary>
        public string roleLean;

        public List<string> suggestedNames = new List<string>();
        public List<PowerupData> powerups = new List<PowerupData>();
    }

    [Serializable]
    public sealed class RiteGeneratorConfigData
    {
        public double demandGrowth;
        public double spotlightDiscount;
        public double offSpotlightPremium;
    }

    [Serializable]
    public sealed class RitesBundle
    {
        public int chooseCount;

        /// <summary>
        /// Run-2+ generator tuning. Unity serialization can't round-trip a
        /// null — treat demandGrowth &lt;= 0 as "no generator" (the Configured
        /// pattern), in which case later runs re-walk the authored Rite.
        /// </summary>
        public RiteGeneratorConfigData generator;

        public List<RiteData> rites = new List<RiteData>();
    }

    [Serializable]
    public sealed class StringEntry
    {
        public string key;
        public string text;
    }

    [Serializable]
    public sealed class ProvisionerEntry
    {
        public string id;
        public string trigger;
        public string line;
    }

    [Serializable]
    public sealed class DialogueBundle
    {
        public List<StringEntry> waystones = new List<StringEntry>();
        public List<StringEntry> verses = new List<StringEntry>();
        public List<ProvisionerEntry> provisioner = new List<ProvisionerEntry>();
        public List<string> migrationVignette = new List<string>();
        public List<StringEntry> fossilCards = new List<StringEntry>();
    }

    [Serializable]
    public sealed class EconomyData
    {
        public CostGrowthData costGrowth;
        public GiftsData gifts;
        public HaulingData hauling;
        public FamiliarCapsData familiarCaps;
        public CraftingData crafting;
        public ToolsData tools;
        public MasteryData mastery;
        public VerdureData verdure;
        public XpData xp;
        public OfflineData offline;
        public QualityData quality;
        public ExcavationData excavation;
        public TendingData tending;
        public WardenData warden;
        public FamiliarXpData familiarXp;

        /// <summary>Unity can't serialize a null section — treat timeSkipCostAmber &lt;= 0 as "no amber system" (the Configured pattern).</summary>
        public AmberData amber;

        [Serializable]
        public sealed class CostGrowthData
        {
            public double gathererGift;
            public double carrierGift;
            public double building;
        }

        [Serializable]
        public sealed class GiftsData
        {
            public BigDouble gathererBaseGoods;
            public BigDouble carrierBaseGoods;
        }

        [Serializable]
        public sealed class HaulingData
        {
            public double baseCarryCapacity;
            public double tripSeconds;
            public double basketCapacity;
        }

        [Serializable]
        public sealed class FamiliarCapsData
        {
            public int flockCapBase;
            public int flockCapPerRoostLevel;
            public int carrierSlotsBase;
            public int carrierSlotsPerRoostLevel;
        }

        [Serializable]
        public sealed class CraftingData
        {
            public double baseCraftSeconds;
        }

        [Serializable]
        public sealed class ToolsData
        {
            public BigDouble baseCostCoin;
            public double costMultPerTier;
            public double yieldMultPerTier;
            public List<string> tiers = new List<string>();
        }

        [Serializable]
        public sealed class MasteryData
        {
            public double yieldBonusPerLevel;
            public double baseXp;
            public double growth;
            public int maxLevel;
            public double xpPerUnit;
        }

        [Serializable]
        public sealed class VerdureData
        {
            public double renownDivisor;
            public double exponent;
            public double yieldBonusPerPoint;
        }

        [Serializable]
        public sealed class XpData
        {
            public double baseXp;
            public double growth;
            public int maxLevel;
            public double gatherPerUnit;
            public double craftPerBatch;
        }

        [Serializable]
        public sealed class OfflineData
        {
            public double baseCapHours;
            public double rateMultiplier;
        }

        [Serializable]
        public sealed class QualityData
        {
            public double fineChance;
            public double fineValueMult;
            public double pristineBaseChance;
            public double pristineValueMult;
        }

        [Serializable]
        public sealed class AmberData
        {
            public double digFindsPerHour;
            public double perFind;
            public double timeSkipHours;
            public double timeSkipCostAmber;
        }

        [Serializable]
        public sealed class ExcavationData
        {
            public double pityTimerHoursDug;
            public double baseFragmentsPerHour;
        }

        [Serializable]
        public sealed class TendingData
        {
            public double burstYieldMult;
            public double burstDurationSec;
            public double pristineBonusDurationSec;
            public double pristineChanceBonus;
        }

        [Serializable]
        public sealed class WardenData
        {
            /// <summary>The warden's own gather rate at their post, straight to camp — the bare-node replant bootstrap.</summary>
            public double gatherPerSecond;
        }

        [Serializable]
        public sealed class FamiliarXpData
        {
            public double baseXp;
            public double growth;
            public int maxLevel;

            /// <summary>Base run XP a stationed familiar earns per second at its post (wanderers ×0.5).</summary>
            public double xpPerSecond;

            /// <summary>K_f in kinshipGain = floor(√(runXP / kinshipDivisor)) at Migration (design §8).</summary>
            public double kinshipDivisor;

            /// <summary>+XP rate per Kinship level (design §4).</summary>
            public double kinshipXpRatePerLevel;
        }
    }
}
