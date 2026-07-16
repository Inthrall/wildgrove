using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>Global economy constants (design doc §8).</summary>
    public sealed class EconomyConfig
    {
        public CostGrowthSection CostGrowth { get; set; }
        public GiftsSection Gifts { get; set; }
        public HaulingSection Hauling { get; set; }
        public FamiliarCapsSection FamiliarCaps { get; set; }
        public CraftingSection Crafting { get; set; }
        public ToolsSection Tools { get; set; }
        public MasterySection Mastery { get; set; }
        public VerdureSection Verdure { get; set; }
        public XpSection Xp { get; set; }
        public OfflineSection Offline { get; set; }
        public QualitySection Quality { get; set; }
        public ExcavationSection Excavation { get; set; }
        public TendingSection Tending { get; set; }

        public sealed class CostGrowthSection
        {
            public double GathererGift { get; set; }
            public double CarrierGift { get; set; }
            public double Building { get; set; }
        }

        public sealed class GiftsSection
        {
            public long GathererBaseGoods { get; set; }
            public long CarrierBaseGoods { get; set; }
        }

        public sealed class HaulingSection
        {
            public double BaseCarryCapacity { get; set; }
            public double TripSeconds { get; set; }
            public double BasketCapacity { get; set; }
        }

        public sealed class FamiliarCapsSection
        {
            public int FlockCapBase { get; set; }
            public int FlockCapPerRoostLevel { get; set; }
            public int CarrierSlotsBase { get; set; }
            public int CarrierSlotsPerRoostLevel { get; set; }
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

        public sealed class ExcavationSection
        {
            public double PityTimerHoursDug { get; set; }
            public double BaseFragmentsPerHour { get; set; }
        }

        public sealed class TendingSection
        {
            public double BurstYieldMult { get; set; }
            public double BurstDurationSec { get; set; }
            public double PristineBonusDurationSec { get; set; }
            public double PristineChanceBonus { get; set; }
            public double HandGatherPerSecond { get; set; }
        }
    }
}
