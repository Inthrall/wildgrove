using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>Global economy constants (design doc §8).</summary>
    public sealed class EconomyConfig
    {
        public CostGrowthSection CostGrowth { get; set; }
        public GiftsSection Gifts { get; set; }
        public DeliverySection Delivery { get; set; }
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
        public StoreSection Store { get; set; }
        public TendingSection Tending { get; set; }
        public BubblesSection Bubbles { get; set; }
        public WardenSection Warden { get; set; }
        public FamiliarXpSection FamiliarXp { get; set; }
        public ReplantSection Replant { get; set; }

        public sealed class CostGrowthSection
        {
            public double Building { get; set; }

            /// <summary>Geometric step on a repeatable Almanac line (almanac.json's repeatable node).</summary>
            public double Almanac { get; set; }
        }

        /// <summary>Gift piles (design §4): one pile per verse sung; the arrival is the node resource's specialist.</summary>
        public sealed class GiftsSection
        {
            public long PileGoods { get; set; }
        }

        public sealed class DeliverySection
        {
            public double BatchSeconds { get; set; }
        }

        public sealed class KithSection
        {
            public int SlotsBase { get; set; }
            public int SlotsMax { get; set; }
            public List<int> VerseMilestones { get; set; }
            public int GeneratorGatherPosts { get; set; }
            public double GatherPerSecond { get; set; }
        }

        public sealed class CraftingSection
        {
            public double BaseCraftSeconds { get; set; }
        }

        public sealed class ToolsSection
        {
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

            /// <summary>The ceiling every away-cap source is measured against — no combination of raise-to rungs, gear, Almanac and Store levels credits more than this.</summary>
            public double MaxCapHours { get; set; }

            public double RateMultiplier { get; set; }
        }

        public sealed class QualitySection
        {
            public double DecentChance { get; set; }
            public double DecentValueMult { get; set; }
            public double ChoiceBaseChance { get; set; }
            public double ChoiceValueMult { get; set; }
        }

        public sealed class ObservationSection
        {
            public double PityTimerHoursWatched { get; set; }
            public double BaseSketchesPerHour { get; set; }

            /// <summary>The craft the watching trains — "Observation" in design §5, `entomology` in the data.</summary>
            public string Skill { get; set; }

            /// <summary>Skill XP per watcher per site-hour, scaled by the site's dig-speed multiplier.</summary>
            public double WatchXpPerHour { get; set; }
        }

        /// <summary>Amber (design §10): the free dig-find earn rate, the time-skip sink, the rewarded-ad drip, and the weekly cache. Optional — absent means the system is inert.</summary>
        public sealed class AmberSection
        {
            public double DigFindsPerHour { get; set; }
            public double PerFind { get; set; }
            public double TimeSkipHours { get; set; }
            public double TimeSkipCostAmber { get; set; }
            public double TimeSkipDailyCapHours { get; set; }
            public double AdDripAmber { get; set; }
            public double WeeklyCacheAmber { get; set; }
            public double RenameCostAmber { get; set; }
        }

        /// <summary>Real-money catalogue constants: the starter bundle's one-time Amber grant and the consumable amber-pack piles.</summary>
        public sealed class StoreSection
        {
            public double StarterBundleAmber { get; set; }
            public double AmberPackSmall { get; set; }
            public double AmberPackLarge { get; set; }
        }

        public sealed class TendingSection
        {
            public double BurstYieldMult { get; set; }
            public double BurstDurationSec { get; set; }
            public double ChoiceBonusDurationSec { get; set; }
            public double ChoiceChanceBonus { get; set; }
        }

        public sealed class WardenSection
        {
            public double GatherPerSecond { get; set; }
        }

        /// <summary>Windfall bubbles (the tap-to-tend replacement). Optional — absent means the system is inert.</summary>
        public sealed class BubblesSection
        {
            public double SpawnIntervalSec { get; set; }
            public double LifetimeSec { get; set; }
            public int MaxLive { get; set; }
            public double RewardSeconds { get; set; }

            /// <summary>The notional gatherer's hands behind a windfall — fixed, so every node pays the same haul.</summary>
            public double RewardRatePerSecond { get; set; }
        }

        public sealed class FamiliarXpSection
        {
            public double Base { get; set; }
            public double Growth { get; set; }
            public int MaxLevel { get; set; }
            public double XpPerSecond { get; set; }
            public double KinshipDivisor { get; set; }
            public double KinshipXpRatePerLevel { get; set; }

            /// <summary>Kinship levels at which a familiar's signature deepens (design §4, ascending). Empty = signatures off.</summary>
            public List<int> SignatureMilestones { get; set; }

            /// <summary>Trait sharpening per milestone passed (trait value × (1 + deepening · passed)).</summary>
            public double SignatureDeepening { get; set; }
        }

        public sealed class ReplantSection
        {
            public long BaseCost { get; set; }
            public double Growth { get; set; }
            public double RichnessPerLevel { get; set; }
        }
    }
}
