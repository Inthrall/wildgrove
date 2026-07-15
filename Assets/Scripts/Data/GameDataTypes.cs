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
        public bool priced;
        public BigDouble mapCostCoin;
        public string scope;
    }

    [Serializable]
    public sealed class UpgradeData
    {
        public int order;
        public string id;
        public string displayName;
        public string track;
        public BigDouble costCoin;
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
        public List<ProvisionerEntry> provisioner = new List<ProvisionerEntry>();
        public List<string> migrationVignette = new List<string>();
        public List<StringEntry> fossilCards = new List<StringEntry>();
    }

    [Serializable]
    public sealed class EconomyData
    {
        public CostGrowthData costGrowth;
        public HiresData hires;
        public ToolsData tools;
        public MasteryData mastery;
        public VerdureData verdure;
        public XpData xp;
        public OfflineData offline;
        public QualityData quality;
        public ExcavationData excavation;
        public TendingData tending;

        [Serializable]
        public sealed class CostGrowthData
        {
            public double crewHire;
            public double porter;
            public double building;
        }

        [Serializable]
        public sealed class HiresData
        {
            public BigDouble crewBaseCoin;
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
        }

        [Serializable]
        public sealed class ExcavationData
        {
            public double pityTimerHoursDug;
        }

        [Serializable]
        public sealed class TendingData
        {
            public double burstYieldMult;
            public double burstDurationSec;
            public double pristineBonusDurationSec;
        }
    }
}
