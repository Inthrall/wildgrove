using System;
using System.Collections.Generic;
using BreakInfinity;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The mutable runtime state of a single run (one migration cycle). Plain
    /// C# so the simulation is testable without a scene and serialisable by the
    /// save system when it lands. Content constants come from GameDataAsset;
    /// this holds only what changes as the player plays.
    /// </summary>
    [Serializable]
    public sealed class GameState
    {
        /// <summary>Per-run soft currency, spent on tools, buildings and maps (and, as a Phase-1 placeholder, familiar gifts).</summary>
        public BigDouble coin = BigDouble.Zero;

        /// <summary>Meta currency carried across migrations; drives the global yield bonus.</summary>
        public double verdurePoints;

        /// <summary>Lifetime Renown from Rite offerings (design §7) — Verdure derives from it at Migration and it is never spent.</summary>
        public BigDouble renown = BigDouble.Zero;

        /// <summary>Warden deeds performed this run, keyed by deed id (e.g. "tend") — deed slots of the Rite fill from these.</summary>
        public Dictionary<string, int> deedCounts = new Dictionary<string, int>();

        /// <summary>Offering progress per revealed verse of the Rite, created on first touch.</summary>
        public List<VerseProgressState> verseProgress = new List<VerseProgressState>();

        /// <summary>Raw and crafted materials at camp, keyed by resource id — the only stock that can be sold, gifted, or spent. Goods reach camp by carrier haul from the nodes' baskets.</summary>
        public Dictionary<string, BigDouble> resources = new Dictionary<string, BigDouble>();

        /// <summary>Fine-quality finds at camp, keyed by resource id (design §5: a Fine haul batch, sold at the quality bonus alongside the common stock).</summary>
        public Dictionary<string, BigDouble> fineResources = new Dictionary<string, BigDouble>();

        /// <summary>Pristine specimens at camp, keyed by resource id (design §5). Never sold automatically — the windfall sale (and later donation or offering) is the player's explicit choice.</summary>
        public Dictionary<string, BigDouble> pristineResources = new Dictionary<string, BigDouble>();

        /// <summary>Xorshift64* state for the run's rolls (quality, later loot). Seeded at run birth, saved with the run — the sim itself stays deterministic.</summary>
        public ulong rngState = 0x9E3779B97F4A7C15UL;

        /// <summary>Carrier familiars hauling for the camp (design §8: a camp-wide pool, not per-node).</summary>
        public int carrierCount;

        /// <summary>
        /// Seconds accrued toward the fleet's next delivery (design §5: hauling
        /// lands in discrete batches, one every tripSeconds / carrierCount).
        /// Only accrues while a basket has goods waiting; reset when the
        /// carriers run out of work.
        /// </summary>
        public double haulTripProgress;

        /// <summary>Every gathering node the player has access to this run.</summary>
        public List<NodeState> nodes = new List<NodeState>();

        /// <summary>Dig sites opened this run (design §5: unlockDigSite upgrades on zones that hold one).</summary>
        public List<DigSiteState> digSites = new List<DigSiteState>();

        /// <summary>Fossil fragments surfaced this run, keyed by fossil id. A fossil is complete when its count reaches the data's fragments target — completion grants its permanent effects.</summary>
        public Dictionary<string, int> fossilFragments = new Dictionary<string, int>();

        /// <summary>Ids of the one-off §9 upgrades bought this run (reset by Migration).</summary>
        public List<string> purchasedUpgradeIds = new List<string>();

        /// <summary>One entry per crafting station the run has put to work (fire / bench / forge).</summary>
        public List<StationState> stations = new List<StationState>();

        /// <summary>Bought levels per camp building line, keyed by building id (§9 milestone upgrades count separately).</summary>
        public Dictionary<string, int> buildingLevels = new Dictionary<string, int>();

        /// <summary>Total XP earned per skill this run (design §4; levels are derived by Skills.Level, never stored).</summary>
        public Dictionary<string, double> skillXp = new Dictionary<string, double>();

        public BigDouble GetResource(string resourceId)
        {
            return resources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddResource(string resourceId, BigDouble amount)
        {
            resources[resourceId] = GetResource(resourceId) + amount;
        }

        public BigDouble GetFine(string resourceId)
        {
            return fineResources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddFine(string resourceId, BigDouble amount)
        {
            fineResources[resourceId] = GetFine(resourceId) + amount;
        }

        public BigDouble GetPristine(string resourceId)
        {
            return pristineResources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddPristine(string resourceId, BigDouble amount)
        {
            pristineResources[resourceId] = GetPristine(resourceId) + amount;
        }

        public bool HasUpgrade(string upgradeId)
        {
            return purchasedUpgradeIds.Contains(upgradeId);
        }

        /// <summary>Total familiars befriended this run — every node's gatherers plus every dig site's diggers.</summary>
        public int TotalFamiliars()
        {
            var total = 0;
            foreach (var node in nodes)
            {
                total += node.familiarCount;
            }

            foreach (var site in digSites)
            {
                total += site.familiarCount;
            }

            return total;
        }
    }

    /// <summary>
    /// One crafting station's work in progress. A station auto-crafts its
    /// assigned recipe continuously (the Melvor-style bar): inputs are consumed
    /// when a craft starts, the output lands when it completes, and the station
    /// stalls quietly when camp stock can't cover the next batch.
    /// </summary>
    [Serializable]
    public sealed class StationState
    {
        /// <summary>Station id from the recipe data ("fire", "bench", "forge").</summary>
        public string stationId;

        /// <summary>The recipe this station is working, or null when idle.</summary>
        public string recipeId;

        /// <summary>True while a batch is mid-craft (its inputs are already spent).</summary>
        public bool inFlight;

        /// <summary>Seconds of progress into the in-flight batch.</summary>
        public double progressSeconds;
    }

    /// <summary>
    /// One verse's offering progress (design §7). Slots parallel the verse's
    /// data slots by index; identity and targets are always read from the
    /// current data, so a retuned verse self-corrects on load like nodes do.
    /// </summary>
    [Serializable]
    public sealed class VerseProgressState
    {
        public string verseId;
        public List<SlotProgressState> slots = new List<SlotProgressState>();
    }

    /// <summary>Progress on one offering slot.</summary>
    [Serializable]
    public sealed class SlotProgressState
    {
        /// <summary>Units (resource slots) or count (deed/specimen/fragment slots) delivered so far.</summary>
        public double delivered;

        /// <summary>True once a completion-granted slot (deeds) has credited its renownGrant — keeps the grant one-shot.</summary>
        public bool granted;
    }

    /// <summary>
    /// One zone's dig site (design §5: familiars set to Excavation slowly turn
    /// soil, surfacing fossil fragments). Fragments land in
    /// GameState.fossilFragments; the site itself only tracks who's digging
    /// and how long since the last find.
    /// </summary>
    [Serializable]
    public sealed class DigSiteState
    {
        public string zoneId;

        /// <summary>Familiars turning soil here. Count toward the zone's flock cap like gatherers.</summary>
        public int familiarCount;

        /// <summary>Hours dug since the last fragment — the pity timer (economy.excavation.pityTimerHoursDug guarantees a find).</summary>
        public double pityHours;
    }

    /// <summary>
    /// One worked resource node — a single resource within a zone, gathered by
    /// that zone's flock of familiars. Familiars accrue the resource
    /// automatically each tick.
    /// </summary>
    [Serializable]
    public sealed class NodeState
    {
        /// <summary>Stable node id, e.g. "sunfield-meadow:berries".</summary>
        public string id;
        public string zoneId;
        public string resourceId;

        /// <summary>The gathering skill working this node (e.g. "foraging").</summary>
        public string skill;

        /// <summary>Number of familiars working the node; the base gather rate is one unit per familiar per second.</summary>
        public int familiarCount;

        /// <summary>
        /// Mastery XP earned gathering this node's resource (design §4). Levels
        /// derive via Mastery.Level — each adds economy.mastery.yieldBonusPerLevel
        /// to the node's yield and the raw resource's sell value.
        /// </summary>
        public double masteryXp;

        /// <summary>
        /// Seconds left on an active Tending burst. While positive, the node's
        /// yield is multiplied by economy.tending.burstYieldMult for that slice
        /// of the tick; a fresh Tend refreshes it to the full burst duration.
        /// </summary>
        public double tendBurstRemaining;

        /// <summary>
        /// Seconds left on the post-tend Pristine window (design §5: Tending
        /// "briefly raised Pristine chance"). While positive, haul batches from
        /// this node multiply their Pristine chance by
        /// (1 + economy.tending.pristineChanceBonus). Refreshed by Tend
        /// alongside the yield burst, on its own (longer) duration.
        /// </summary>
        public double pristineBonusRemaining;

        /// <summary>
        /// Goods gathered but not yet hauled to camp — the basket at the node.
        /// Capped at economy.hauling.basketCapacity; gathering into a full
        /// basket is lost (design §2: "under-invest in carriers and baskets
        /// overflow at the node").
        /// </summary>
        public BigDouble basket;

        /// <summary>
        /// Combined tool + gear + upgrade multiplier for this node. Defaults to
        /// 1 and is rebuilt by Upgrades.RecomputeYieldMultipliers on purchase
        /// (gear folds in when its system lands); the tick treats it as an
        /// opaque multiplier so it stays balance-agnostic.
        /// </summary>
        public double yieldMultiplier = 1.0;
    }
}
