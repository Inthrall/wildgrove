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
        /// <summary>Meta currency carried across migrations; drives the global yield bonus.</summary>
        public double verdurePoints;

        /// <summary>
        /// Lifetime Renown (design §9): all XP earned this run (warden skills +
        /// familiars) plus Rite offering credits. The ledger's single climbing
        /// number now that Coin is gone; Verdure derives from it at Migration
        /// and it is never spent.
        /// </summary>
        public BigDouble renown = BigDouble.Zero;

        /// <summary>Completed Migrations (design §7) — selects the run's Rite and drives achievements.</summary>
        public int migrationCount;

        /// <summary>Almanac nodes bought with Verdure (design §7) — permanent, surviving every Migration. Their costs stay allocated; see Almanac.AvailableVerdure.</summary>
        public List<string> almanacNodeIds = new List<string>();

        /// <summary>The warden's kit (design §4): worn gear id per slot (hands/pack/camp). Persists for the run; Migration resets it — the kit is rebuilt cheaply each run.</summary>
        public Dictionary<string, string> gearBySlot = new Dictionary<string, string>();

        /// <summary>Resources whose Pristine specimen has been fixed into the Folio (design §6) — permanent, surviving every Migration.</summary>
        public List<string> fixedResources = new List<string>();

        /// <summary>
        /// The warden's post: the node they are assigned to from the strip,
        /// like any familiar (one body per post, design §2). Null while the
        /// warden stands at camp, gathering nothing.
        /// </summary>
        public string wardenPostNodeId;

        /// <summary>Amber (design §10) — the premium currency; observation sites surface it free, and it survives Migration.</summary>
        public double amber;

        /// <summary>Verses sung in runs already folded (design §4 ladder) — the current run's completed verses add on top; see Kith.TotalVersesSung.</summary>
        public int foldedVersesSung;

        /// <summary>Kith slots owned through the store (0–2: the starter bundle, the plain slot). Synced from entitlements at startup and after purchase; survives Migration.</summary>
        public int purchasedKithSlots;

        /// <summary>True once the starter bundle's one-time Amber grant has been paid out — the entitlement re-resolves on every device, the pile arrives once.</summary>
        public bool starterBundleAmberGranted;

        /// <summary>
        /// Monotonic version of the effect sources (purchases, donations,
        /// insects, gear, Almanac, buildings) — bumping it invalidates the
        /// cached <see cref="modifierSnapshot"/>. Never saved.
        /// </summary>
        public int modifierVersion;

        /// <summary>Cached derived modifiers — see Modifiers.Of. Never saved.</summary>
        public ModifierSnapshot modifierSnapshot;

        /// <summary>Memoised generated rite for this run's migration — see Rite.CurrentRite. Never saved.</summary>
        public Wildgrove.Data.RiteData generatedRite;
        public int generatedRiteMigration = -1;
        public Wildgrove.Data.GameDataAsset generatedRiteFrom;

        /// <summary>Reusable scratch for the observation tick's eligible-insect walk — see Observation.Advance. Never saved.</summary>
        public List<Wildgrove.Data.InsectData> insectScratch;

        /// <summary>Invalidate the cached modifier snapshot after an effect-source mutation.</summary>
        public void BumpModifiers()
        {
            modifierVersion++;
        }

        /// <summary>Zones whose waystone inscription has been read (design §6) — lore stays read across Migration.</summary>
        public List<string> seenWaystoneZoneIds = new List<string>();

        /// <summary>Compendium lifetime counters (design §5) — never reset, never decremented; they survive Migration.</summary>
        public Dictionary<string, BigDouble> lifetimeGathered = new Dictionary<string, BigDouble>();
        public Dictionary<string, double> lifetimeCrafted = new Dictionary<string, double>();
        public Dictionary<string, BigDouble> lifetimePristine = new Dictionary<string, BigDouble>();

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

        /// <summary>
        /// The warden's kith (design §4): every familiar befriended this run,
        /// each an individual with a name, species, level (derived from xp),
        /// Kinship, powerups, and a stationing post. Replaces the anonymous
        /// per-node/per-camp counts — stationed roster members do the gathering
        /// and hauling now (see <see cref="Stationing"/>). Bonded familiars
        /// (design §4) live here too, materialised each run from their source.
        /// </summary>
        public List<Familiar> roster = new List<Familiar>();

        /// <summary>Sequence for minting stable per-run roster ids — see <see cref="NextFamiliarId"/>.</summary>
        public int nextFamiliarSeq = 1;

        /// <summary>
        /// Seconds accrued toward the fleet's next delivery (design §5: hauling
        /// lands in discrete batches, one every tripSeconds / carriers). Only
        /// accrues while a basket has goods waiting; reset when the trail runs
        /// out of work.
        /// </summary>
        public double haulTripProgress;

        /// <summary>Every gathering node the player has access to this run.</summary>
        public List<NodeState> nodes = new List<NodeState>();

        /// <summary>Dig sites opened this run (design §5: unlockDigSite upgrades on zones that hold one).</summary>
        public List<DigSiteState> digSites = new List<DigSiteState>();

        /// <summary>
        /// Planters built this run (design §3), each attached to one gather node
        /// or dig site by target id. Each improves that target's basket capacity,
        /// yield, or dig speed (see <see cref="Planters"/>). One planter of each
        /// type per target. Reset at Migration — cheap to rebuild each run.
        /// </summary>
        public List<BuiltPlanter> builtPlanters = new List<BuiltPlanter>();

        /// <summary>Field sketches recorded this run, keyed by insect id. A plate is recorded when its count reaches the data's sketches target — recording grants its permanent effects.</summary>
        public Dictionary<string, int> insectSketches = new Dictionary<string, int>();

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

        /// <summary>True when a planter of this type is already built at the given target.</summary>
        public bool HasPlanter(string targetId, string planterId)
        {
            foreach (var planter in builtPlanters)
            {
                if (planter.targetId == targetId && planter.planterId == planterId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Mint the next stable per-run roster id (e.g. "fam-1").</summary>
        public string NextFamiliarId()
        {
            return "fam-" + (nextFamiliarSeq++);
        }

        /// <summary>Total familiars in the kith this run — the whole roster (bonded and not).</summary>
        public int TotalFamiliars()
        {
            return roster.Count;
        }

        /// <summary>The roster familiar with this id, or null.</summary>
        public Familiar FamiliarById(string familiarId)
        {
            if (string.IsNullOrEmpty(familiarId))
            {
                return null;
            }

            foreach (var familiar in roster)
            {
                if (familiar.id == familiarId)
                {
                    return familiar;
                }
            }

            return null;
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
        /// <summary>Units (resource slots) or count (deed/specimen/sketch slots) delivered so far.</summary>
        public double delivered;

        /// <summary>True once a completion-granted slot (deeds) has credited its renownGrant — keeps the grant one-shot.</summary>
        public bool granted;
    }

    /// <summary>
    /// One zone's observation site (design §6: the wanderer passes it as it
    /// roams, watching what lives there and recording it as field sketches).
    /// Sketches land in GameState.insectSketches; the site itself only tracks
    /// how long since the last sketch. Not a post — the watching comes from
    /// the wander station (see <see cref="Stationing.WanderAgents"/>).
    /// </summary>
    [Serializable]
    public sealed class DigSiteState
    {
        public string zoneId;

        /// <summary>Hours watched since the last sketch — the pity timer (economy.observation.pityTimerHoursWatched guarantees a sketch).</summary>
        public double pityHours;
    }

    /// <summary>
    /// One built planter (design §3): a planter type attached to one gather node
    /// or dig site. The target id is a node id for node planters, or a zone id
    /// for dig-site planters (see <see cref="Planters"/>).
    /// </summary>
    [Serializable]
    public sealed class BuiltPlanter
    {
        /// <summary>Planter id from planters.json.</summary>
        public string planterId;

        /// <summary>Node id (node planters) or zone id (dig-site planters) this is attached to.</summary>
        public string targetId;
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

        /// <summary>
        /// Richness level (design §3): raised by replanting the node's own
        /// resource, each level adding economy.replant.richnessPerLevel to the
        /// node's yield. Per node, per run — reset at Migration.
        /// </summary>
        public int richnessLevel;

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
