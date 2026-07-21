using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>Global economy constants (design doc §8).</summary>
    public sealed class EconomyConfig
    {
        public CostGrowthSection CostGrowth { get; set; }
        public GiftsSection Gifts { get; set; }
        public HaulingSection Hauling { get; set; }
        public KithSection Kith { get; set; }
        public CraftingSection Crafting { get; set; }
        public ToolsSection Tools { get; set; }
        public MasterySection Mastery { get; set; }
        public VerdureSection Verdure { get; set; }
        public XpSection Xp { get; set; }
        public OfflineSection Offline { get; set; }
        public QualitySection Quality { get; set; }
        public ObservationSection Observation { get; set; }
        public AmberSection Amber { get; set; }
        public TendingSection Tending { get; set; }
        public WardenSection Warden { get; set; }
        public FamiliarXpSection FamiliarXp { get; set; }
        public ReplantSection Replant { get; set; }

        public sealed class CostGrowthSection
        {
            public double Building { get; set; }
        }

        /// <summary>The gift event (design §4): one pile of a node's own resource, one deterministic arrival.</summary>
        public sealed class GiftsSection
        {
            public long PileGoods { get; set; }
            public string Species { get; set; }
        }

        public sealed class HaulingSection
        {
            public double BaseCarryCapacity { get; set; }
            public double TripSeconds { get; set; }
            public double BasketCapacity { get; set; }
        }

        public sealed class KithSection
        {
            public int SlotsBase { get; set; }
            public int SlotsMax { get; set; }
        }

        public sealed class CraftingSection
        {
            public double BaseCraftSeconds { get; set; }
        }

        public sealed class ToolsSection
        {
            public long BaseCostCoin { get; set; }
            public double CostMultPerTier { get; set; }
            public double YieldMultPerTier { get; set; }
            public List<string> Tiers { get; set; }
        }

        public sealed class MasterySection
        {
            public double YieldBonusPerLevel { get; set; }
            public double Base { get; set; }
            public double Growth { get; set; }
            public int MaxLevel { get; set; }
            public double XpPerUnit { get; set; }
        }

        public sealed class VerdureSection
        {
            public double RenownDivisor { get; set; }
            public double Exponent { get; set; }
            public double YieldBonusPerPoint { get; set; }
        }

        public sealed class XpSection
        {
            public double Base { get; set; }
            public double Growth { get; set; }
            public int MaxLevel { get; set; }
            public double GatherPerUnit { get; set; }
            public double CraftPerBatch { get; set; }
        }

        public sealed class OfflineSection
        {
            public double BaseCapHours { get; set; }
            public double RateMultiplier { get; set; }
        }

        public sealed class QualitySection
        {
            public double FineChance { get; set; }
            public double FineValueMult { get; set; }
            public double PristineBaseChance { get; set; }
            public double PristineValueMult { get; set; }
        }

        public sealed class ObservationSection
        {
            public double PityTimerHoursWatched { get; set; }
            public double BaseSketchesPerHour { get; set; }
        }

        /// <summary>Amber (design §10): the free dig-find earn rate and the time-skip sink. Optional — absent means the system is inert.</summary>
        public sealed class AmberSection
        {
            public double DigFindsPerHour { get; set; }
            public double PerFind { get; set; }
            public double TimeSkipHours { get; set; }
            public double TimeSkipCostAmber { get; set; }
        }

        public sealed class TendingSection
        {
            public double BurstYieldMult { get; set; }
            public double BurstDurationSec { get; set; }
            public double PristineBonusDurationSec { get; set; }
            public double PristineChanceBonus { get; set; }
        }

        public sealed class WardenSection
        {
            public double GatherPerSecond { get; set; }
        }

        public sealed class FamiliarXpSection
        {
            public double Base { get; set; }
            public double Growth { get; set; }
            public int MaxLevel { get; set; }
            public double XpPerSecond { get; set; }
            public double KinshipDivisor { get; set; }
            public double KinshipXpRatePerLevel { get; set; }
        }

        public sealed class ReplantSection
        {
            public long BaseCost { get; set; }
            public double Growth { get; set; }
            public double RichnessPerLevel { get; set; }
        }
    }
}
