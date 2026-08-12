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
        public string species;
        public string upgrade;
        public double value;
    }

    [Serializable]
    public sealed class ResourceData
    {
        public string id;

        /// <summary>Base trade value per unit (raw gatherables only) — the field keeps its old name; see Economy.TradeValuePerUnit.</summary>
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

        /// <summary>Folds that must be behind the warden before this trail exists (design §8); 0 = from the first run.</summary>
        public int minMigration;

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

        /// <summary>Folds that must be behind the warden before this rung is on the ladder (design §8); 0 = from the first run.</summary>
        public int minMigration;

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

        /// <summary>One batch's craft time before speed multipliers divide it. 0 = economy.crafting.baseCraftSeconds.</summary>
        public double craftSeconds;
    }

    /// <summary>
    /// One camp building line (design §9) — the repeatable materials sink. Named
    /// §9 upgrades are milestone levels; bought levels each grant perLevel.
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
        /// <summary>"stationSpeedBonus" | "offlineCapBonusHours" | "comfort".</summary>
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
    public sealed class InsectData
    {
        public string id;
        public string displayName;
        public int sketches;
        public List<string> habitats = new List<string>();
        public double rarity;

        /// <summary>Awarded, never drawn (design §11) — excluded from the observation roll; see Observation.EligibleInsectsInto.</summary>
        public bool rewarded;

        public List<EffectData> effects = new List<EffectData>();
    }

    /// <summary>One Folio spread (design §6): fix a Choice specimen of each entry into the journal; completion grants a permanent effect surviving Migration.</summary>
    [Serializable]
    public sealed class FolioSpreadData
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

        /// <summary>Bought over and over (level L costs costVerdure · costGrowth.almanac^L, effects scaled by levels held) rather than learned once.</summary>
        public bool repeatable;

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
        /// <summary>"folioSpread" or "almanacNode".</summary>
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

        /// <summary>Species id (into species.json) — the companion the bond honours (the existing one, or a new arrival).</summary>
        public string species;

        /// <summary>Legacy role hint ("gatherer"/"carrier") — flavour only now that carrying is a post.</summary>
        public string role;

        public BondSourceData source;
    }

    /// <summary>A species' single fixed trait (design §4) — what makes it the specialist of one post. Interpreted by the sim by <see cref="kind"/>.</summary>
    [Serializable]
    public sealed class TraitData
    {
        public string displayName;
        public string description;

        /// <summary>nodeYieldBonus / choiceBonus / digSpeedBonus / bubbleRewardBonus / wardenYieldBonus.</summary>
        public string kind;

        public double value;

        /// <summary>
        /// The related pair of resources a nodeYieldBonus trait covers (e.g.
        /// copper-scree + tin-seam) — the familiar works either node. Empty for
        /// trail/watch/choice traits, which apply wherever the familiar is posted.
        /// </summary>
        public List<string> resources = new List<string>();

        /// <summary>True when this trait's pair covers <paramref name="resourceId"/>.</summary>
        public bool CoversResource(string resourceId)
        {
            return resources != null && resources.Contains(resourceId);
        }
    }

    /// <summary>The Exchange's barter constants (design §9) — rates derive from the trade-value table.</summary>
    [Serializable]
    public sealed class ExchangeData
    {
        public double spread;
        public double offerMinutes;
    }

    /// <summary>
    /// A familiar species (design §4): a role lean, suggested names for the
    /// player to accept or edit on arrival, and a single fixed trait. At most
    /// one familiar of each species ever joins the kith.
    /// </summary>
    [Serializable]
    public sealed class SpeciesData
    {
        public string id;
        public string displayName;

        /// <summary>"gatherer" or "carrier" — the natural lean, never a hard restriction on stationing.</summary>
        public string roleLean;

        public List<string> suggestedNames = new List<string>();
        public TraitData trait;

        /// <summary>Folds that must be behind the warden before this species answers a gift pile (design §8); 0 = from the first run.</summary>
        public int minMigration;

        /// <summary>
        /// Plate inscription lines (design §7) — one earned per signature
        /// milestone passed, in the warden's hand. Unauthored lines simply
        /// never show, so authoring can land species by species.
        /// </summary>
        public List<string> inscriptions = new List<string>();
    }

    /// <summary>
    /// A tincture (design §5, Apothecary): a buff consumable whose id matches
    /// the recipe output that brews it. While drunk (Tinctures.cs), its
    /// effects join the run's active-effect union for durationSec of sim time.
    /// </summary>
    [Serializable]
    public sealed class TinctureData
    {
        public string id;
        public string displayName;

        /// <summary>The bottle's own line in the HUD.</summary>
        public string description;

        public double durationSec;
        public List<EffectData> effects = new List<EffectData>();
    }

    /// <summary>
    /// The deep amber (design §6/§7): authored pieces surfaced in order at one
    /// zone's observation site (DeepAmber.cs). The completed set is a plate —
    /// its effects join the active-effect union and survive Migration.
    /// </summary>
    [Serializable]
    public sealed class DeepAmberData
    {
        /// <summary>The zone whose observation site surfaces the pieces.</summary>
        public string zoneId;

        /// <summary>Piece find rate per watcher-hour, before digSpeedMult modifiers.</summary>
        public double findsPerHour;

        /// <summary>Watched hours without a piece that guarantee the next one.</summary>
        public double pityHoursWatched;

        public string plateName;

        /// <summary>The finished plate's field note.</summary>
        public string completedLore;

        public List<EffectData> effects = new List<EffectData>();

        /// <summary>The pieces, in the order they surface.</summary>
        public List<AmberPieceData> pieces = new List<AmberPieceData>();
    }

    /// <summary>One authored piece of the deep amber and its lore line.</summary>
    [Serializable]
    public sealed class AmberPieceData
    {
        public string id;
        public string displayName;
        public string lore;
    }

    /// <summary>
    /// The Wheel (design §15): the eight sabbats and the tide-open lead time.
    /// The world's one ambient lean since the drawn region season retired
    /// (design §8, 2026-08-08). Read live by the sim's Wheel from the sim
    /// clock cursor — its touch effects never join the cached effect union.
    /// </summary>
    [Serializable]
    public sealed class WheelData
    {
        /// <summary>Days before the sabbat night that its tide opens.</summary>
        public int openDaysBefore = 30;

        /// <summary>The keeping's shape (design §15) — a zeroed section reads as "no verse"; Keeping.Configured is the liveness check.</summary>
        public ObservanceData observance;

        public List<SabbatData> sabbats = new List<SabbatData>();
    }

    /// <summary>The keeping: the tide's own offering verse and tier rewards (design §15).</summary>
    [Serializable]
    public sealed class ObservanceData
    {
        public int slotCount = 5;

        /// <summary>Slots answered per tier: kept the eve / the day / the wheel.</summary>
        public List<int> tierSlots = new List<int>();

        /// <summary>Amber granted as each tier is crossed.</summary>
        public List<double> tierAmber = new List<double>();

        /// <summary>Each goods slot's value against the candidates' average worth.</summary>
        public double slotValueMult = 2.0;

        /// <summary>The specimen slot's fixed Renown grant.</summary>
        public long specimenRenown;
    }

    /// <summary>One sabbat: a real-world festival day, hemisphere-mirrored, and its tide's ambient touch.</summary>
    [Serializable]
    public sealed class SabbatData
    {
        public string id;

        /// <summary>The real name, kept (design §15) — "Beltane". The tide span reads "{name}-tide".</summary>
        public string displayName;

        /// <summary>"fire" (cross-quarter) or "quarter" (solstice/equinox).</summary>
        public string kind;

        /// <summary>The warden's margin line — the calendar is the warden's, never the land's.</summary>
        public string sign;

        /// <summary>The ambient touch: one narrow lean, live while the tide is open.</summary>
        public List<EffectData> touch = new List<EffectData>();

        /// <summary>Goods the keeping's generator favours — the tide's theme.</summary>
        public List<string> verseLean = new List<string>();

        /// <summary>Sabbat nights as days-since-Unix-epoch (date-only), parsed from the authored "yyyy-MM-dd" at import.</summary>
        public List<int> northNightDays = new List<int>();
        public List<int> southNightDays = new List<int>();
    }

    /// <summary>
    /// A planter (design §3): a built structure improving one gather node or dig
    /// site, paid in another zone's goods. Interpreted by <see cref="kind"/>.
    /// </summary>
    [Serializable]
    public sealed class PlanterData
    {
        public string id;
        public string displayName;

        /// <summary>"nodeYieldMult" | "digSpeedMult".</summary>
        public string kind;

        /// <summary>The fractional bonus added to the target (0.5 = +50%).</summary>
        public double value;

        /// <summary>"node" (a gather node) or "digSite".</summary>
        public string target;

        public List<ItemAmount> materials = new List<ItemAmount>();
    }

    [Serializable]
    public sealed class RiteGeneratorConfigData
    {
        public double demandGrowth;

        /// <summary>
        /// A specimen slot's ask as a fraction of its verse's goods asks.
        /// Zero or less means no derivation — the authored counts hold.
        /// </summary>
        public double specimenSlotFraction;

        public double spotlightDiscount;
        public double offSpotlightPremium;

        /// <summary>
        /// Migrations per extra required slot — the fold gate's breadth step.
        /// Zero or less means no ramp (chooseCount stays where the data put it).
        /// </summary>
        public int chooseCountPerMigrations;

        /// <summary>Ceiling on the ramped chooseCount; zero or less leaves the ramp bounded only by each verse's slot count.</summary>
        public int chooseCountMax;

        /// <summary>
        /// How hard a good's own worth divides its slot's value into a unit
        /// count: amount = target / (unit^spread · pivot^(1-spread)). One is a
        /// pure value split — dear goods asked in tiny counts, cheap ones in
        /// tens of thousands. Below one those counts pull towards each other.
        /// Zero or less (or above one) reads as absent and leaves the pure
        /// split.
        /// </summary>
        public double valueSpread;
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
        public List<StringEntry> insectPlates = new List<StringEntry>();

        /// <summary>The last zone's ordered chain of stones (§7). Zone-less or stone-less reads as no chain.</summary>
        public FinalWaystonesData finalWaystones = new FinalWaystonesData();
    }

    [Serializable]
    public sealed class FinalWaystonesData
    {
        public string zoneId;
        public List<StringEntry> stones = new List<StringEntry>();
    }

    [Serializable]
    public sealed class EconomyData
    {
        public CostGrowthData costGrowth;
        public GiftsData gifts;
        public DeliveryData delivery;
        public KithData kith;
        public CraftingData crafting;
        public ToolsData tools;
        public MasteryData mastery;
        public VerdureData verdure;
        public XpData xp;
        public OfflineData offline;
        public QualityData quality;
        public ObservationData observation;
        public TendingData tending;
        public WardenData warden;

        /// <summary>Windfall bubbles (the tap-to-tend replacement). spawnIntervalSec &lt;= 0 = unconfigured (the Configured pattern — Unity can't serialize a null section).</summary>
        public BubblesData bubbles;
        public FamiliarXpData familiarXp;
        public ReplantData replant;

        /// <summary>Unity can't serialize a null section — treat timeSkipCostAmber &lt;= 0 as "no amber system" (the Configured pattern).</summary>
        public AmberData amber;

        /// <summary>Real-money catalogue constants (starter bundle Amber). starterBundleAmber &lt;= 0 = unconfigured (fixtures).</summary>
        public StoreData store;

        [Serializable]
        public sealed class CostGrowthData
        {
            public double building;

            /// <summary>Geometric step on a repeatable Almanac line. Zero or less = unconfigured (fixtures) — a repeatable line then prices every level flat.</summary>
            public double almanac;
        }

        /// <summary>The gift event (design §4): one pile of a node's own resource, one deterministic arrival.</summary>
        [Serializable]
        public sealed class GiftsData
        {
            public BigDouble pileGoods;
        }

        /// <summary>
        /// The delivery cadence (design §5): each node's pooled pickings land
        /// at camp as one quality-rolled batch every batchSeconds. A cadence,
        /// not a throughput cap — nothing gathered is ever lost.
        /// </summary>
        [Serializable]
        public sealed class DeliveryData
        {
            public double batchSeconds;
        }

        [Serializable]
        public sealed class KithData
        {
            public int slotsBase;
            public int slotsMax;

            /// <summary>
            /// Zone ids whose VERSE opens an earned slot the first time it is
            /// sung, ever (design §4 ladder) — one entry per earned place, in
            /// the order they are meant to land. Not a count: the ladder was a
            /// lifetime tally until 2026-08-11, which rewarded folding early
            /// and often rather than walking a trail to its end.
            /// </summary>
            public List<string> slotVerseZones = new List<string>();

            /// <summary>Gather posts the Rite generator assumes a plausible kith holds at once (run-2+ reachability).</summary>
            public int generatorGatherPosts;

            /// <summary>A familiar's base gather rate at its post, units/second. Absent or non-positive (hand-built fixtures) reads as the historical 1.0.</summary>
            public double gatherPerSecond;
        }

        [Serializable]
        public sealed class CraftingData
        {
            public double baseCraftSeconds;
        }

        [Serializable]
        public sealed class ToolsData
        {
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

            /// <summary>The away cap's ceiling. Non-positive means unbounded — the shape a hand-built test fixture leaves it in.</summary>
            public double maxCapHours;

            public double rateMultiplier;
        }

        [Serializable]
        public sealed class QualityData
        {
            public double decentChance;
            public double decentValueMult;
            public double choiceBaseChance;
            public double choiceValueMult;
        }

        [Serializable]
        public sealed class AmberData
        {
            public double digFindsPerHour;
            public double perFind;
            public double timeSkipHours;
            public double timeSkipCostAmber;
            public double timeSkipDailyCapHours;
            public double adDripAmber;
            public double weeklyCacheAmber;
            public double renameCostAmber;
            public double wardenRenameCostAmber;
            public double callingGiftAmber;
            public double campNameCostAmber;
            public double considerationCostAmber;
            public double secondQueueCostAmber;
        }

        /// <summary>Real-money catalogue constants: the starter bundle's one-time Amber grant and the consumable amber-pack piles.</summary>
        [Serializable]
        public sealed class StoreData
        {
            public double starterBundleAmber;
            public double amberPackSmall;
            public double amberPackLarge;
        }

        [Serializable]
        public sealed class ObservationData
        {
            public double pityTimerHoursWatched;
            public double baseSketchesPerHour;
            public string skill;
            public double watchXpPerHour;
        }

        [Serializable]
        public sealed class TendingData
        {
            public double burstYieldMult;
            public double burstDurationSec;
            public double choiceBonusDurationSec;
            public double choiceChanceBonus;
        }

        [Serializable]
        public sealed class WardenData
        {
            /// <summary>The warden's own gather rate at their post, straight to camp — the bare-node replant bootstrap.</summary>
            public double gatherPerSecond;
        }

        /// <summary>Windfall bubbles: a worked node drifts one up every spawnIntervalSec; catching it grants a flat rewardRatePerSecond x rewardSeconds of that node's resource and tends the node.</summary>
        [Serializable]
        public sealed class BubblesData
        {
            public double spawnIntervalSec;
            public double lifetimeSec;
            public int maxLive;
            public double rewardSeconds;

            /// <summary>The notional gatherer's hands behind a windfall — fixed, so every node pays the same haul.</summary>
            public double rewardRatePerSecond;
        }

        [Serializable]
        public sealed class FamiliarXpData
        {
            public double baseXp;
            public double growth;
            public int maxLevel;

            /// <summary>Base run XP a stationed familiar earns per second at its post.</summary>
            public double xpPerSecond;

            /// <summary>K_f in kinshipGain = floor(√(runXP / kinshipDivisor)) at Migration (design §8).</summary>
            public double kinshipDivisor;

            /// <summary>+XP rate per Kinship level (design §4).</summary>
            public double kinshipXpRatePerLevel;

            /// <summary>
            /// Kinship levels at which a familiar's signature deepens (design
            /// §4, ascending) — each one sharpens its species trait and earns
            /// its plate an inscription line (§7). Empty = signatures off.
            /// </summary>
            public List<int> signatureMilestones = new List<int>();

            /// <summary>Trait sharpening per milestone passed: the trait's value scales by 1 + signatureDeepening · milestonesPassed.</summary>
            public double signatureDeepening;
        }

        /// <summary>Replanting (design §3): raising a node's richness with its own resource.</summary>
        [Serializable]
        public sealed class ReplantData
        {
            /// <summary>Units of the node's own resource the first richness level costs; scales by growth^level.</summary>
            public BigDouble baseCost;
            public double growth;

            /// <summary>Yield added per richness level (richnessMult = 1 + richnessPerLevel·level).</summary>
            public double richnessPerLevel;
        }
    }
}
