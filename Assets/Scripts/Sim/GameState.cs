using System;
using System.Collections.Generic;
using BreakInfinity;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The mutable runtime state of a single run (one migration cycle). Plain
    /// C# so the simulation is testable without a scene and serialisable by
    /// <see cref="Saves.SaveCodec"/>. Content constants come from GameDataAsset;
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
        /// number — Verdure derives from it at Migration, and it is never spent.
        /// </summary>
        public BigDouble renown = BigDouble.Zero;

        /// <summary>Completed Migrations (design §7) — selects the run's Rite and drives achievements.</summary>
        public int migrationCount;

        /// <summary>Almanac nodes bought with Verdure (design §7) — permanent, surviving every Migration. Their costs stay allocated; see Almanac.AvailableVerdure.</summary>
        public List<string> almanacNodeIds = new List<string>();

        /// <summary>
        /// Levels held on each REPEATABLE Almanac line (design §7's endless
        /// sink) — permanent like the one-off nodes beside them. A repeatable
        /// line never appears in <see cref="almanacNodeIds"/>; its levels are
        /// the whole record of it.
        /// </summary>
        public Dictionary<string, int> almanacLevels = new Dictionary<string, int>();

        /// <summary>The warden's kit (design §4): worn gear id per slot (hands/pack/camp). Persists for the run; Migration resets it — the kit is rebuilt cheaply each run.</summary>
        public Dictionary<string, string> gearBySlot = new Dictionary<string, string>();

        /// <summary>
        /// The kit bag: every piece made this run, worn or not. A slot still
        /// holds one piece at a time, but the pieces it displaces keep in the
        /// bag and go back on for nothing — materials are spent once per piece,
        /// never again. Reset by Migration along with the kit itself.
        /// </summary>
        public List<string> gearCrafted = new List<string>();

        /// <summary>Resources whose Choice specimen has been fixed into the Folio (design §6) — permanent, surviving every Migration.</summary>
        public List<string> fixedResources = new List<string>();

        /// <summary>
        /// The warden's post: the node they are assigned to from the strip,
        /// like any familiar (one body per post, design §2). Null while the
        /// warden stands at camp, gathering nothing.
        /// </summary>
        public string wardenPostNodeId;

        /// <summary>
        /// What the warden is called, bought once for Amber (design §4's rename,
        /// extended to the player's own body). Empty until then, and empty is
        /// the shipped state: <see cref="Warden.DisplayName"/> reads it as "the
        /// warden", which is what every line said before a name could be given.
        /// Crosses the fold with the rest of what is the player's rather than
        /// the run's — a warden does not forget their name by migrating.
        /// </summary>
        public string wardenName;

        /// <summary>
        /// What this run's camp is called, bought once per run for Amber
        /// (design §9's sink slate, 2026-08-06). Null until named, and
        /// <see cref="Camp.DisplayName"/> reads that as "the camp". Unlike the
        /// warden's name it belongs to the run, not the player — the camp
        /// folds at Migration and the name folds with it, which is what makes
        /// naming it a ritual of each region rather than a purchase.
        /// </summary>
        public string campName;

        /// <summary>Amber (design §10) — the premium currency; observation sites surface it free, and it survives Migration.</summary>
        public double amber;

        /// <summary>Verses sung in runs already folded — the current run's completed verses add on top; see Kith.TotalVersesSung. Feeds the gift piles and the verse achievements; the kith ladder left this tally for named verses on 2026-08-11.</summary>
        public int foldedVersesSung;

        /// <summary>
        /// Zones whose verse this warden has EVER sung (design §4 ladder) —
        /// the kith's earned places open off this, one per
        /// economy.kith.slotVerseZones entry. A set, not a tally: a verse sung
        /// is never unsung, so it survives the fold and is never removed.
        /// </summary>
        public List<string> sungVerseZones = new List<string>();

        /// <summary>
        /// Earned kith places a save had already won under the old
        /// lifetime-tally ladder, kept as a floor so the change to named
        /// verses (2026-08-11, save rung 52) could never take a place back.
        /// Written once by the migration and never again — 0 on every save
        /// written since, and overtaken the moment the named verses are sung.
        /// </summary>
        public int grandfatheredKithSlots;

        /// <summary>Kith slots owned through the store (0–2: the starter bundle, the plain slot). Synced from entitlements at startup and after purchase; survives Migration.</summary>
        public int purchasedKithSlots;

        /// <summary>True once the starter bundle's one-time Amber grant has been paid out — the entitlement re-resolves on every device, the pile arrives once.</summary>
        public bool starterBundleAmberGranted;

        /// <summary>True once The Drover's Halter has been redeemed (design §11) — the fell pony's presence and lane derive from this; see Roster.SyncDroversHalter. Survives Migration: a redemption is a redemption.</summary>
        public bool droversHalterOwned;

        /// <summary>True once The Wayfarer's Plate has been redeemed (design §11). The plate itself lives in insectSketches like every other, so it crosses Migration on that alone — this only bridges sessions that start before billing resolves, and re-recording is idempotent.</summary>
        public bool wayfarersPlateOwned;

        /// <summary>UTC unix ms of the last weekly Amber cache claim (design §11) — 0 = never claimed; the cache re-arms a week after this.</summary>
        public long weeklyCacheClaimedUnixMs;

        /// <summary>UTC unix ms of the last rewarded Amber-drip claim — 0 = never; the drip re-arms after Amber.AdDripCooldownMs. Persisted so the cooldown can't be reset by relaunching, and (once Remove Ads drops the ad) so it still throttles the ad-free grant.</summary>
        public long adDripClaimedUnixMs;

        /// <summary>UTC unix ms of the last rewarded time-skip — 0 = never; re-arms after Amber.TimeSkipCooldownMs. Persisted for the same reason as the drip.</summary>
        public long timeSkipClaimedUnixMs;

        /// <summary>
        /// True once this run's second craft-queue slot is bought (design §9's
        /// sink slate): each station may then hold two standing orders. Per
        /// run on purpose — it lapses at the fold with the camp, which is what
        /// makes it a recurring sink rather than a one-time unlock.
        /// </summary>
        public bool secondQueueBought;

        /// <summary>The exchange window the considerations below were pressed in (design §9's sink slate) — a count stored under any other window is stale, and reads as none.</summary>
        public long exchangeConsiderationWindowIndex;

        /// <summary>Considerations pressed on the drover this window — mixed into the offer's seed (Exchange.OfferAt), and persisted so a reload cannot reroll the deal a bribe bought.</summary>
        public int exchangeConsiderationsThisWindow;

        /// <summary>Paid-skip budget hours left when it was last stamped (Amber.SkipBudgetHours refills from here at timeSkipDailyCapHours/24 per wall hour). Meaningful only alongside the stamp below.</summary>
        public double timeSkipBudgetHours;

        /// <summary>UTC unix ms the paid-skip budget was last settled — 0 = never spent, the budget reads full. Persisted so relaunching can't refill the day's hastening.</summary>
        public long timeSkipBudgetStampUnixMs;

        /// <summary>
        /// The latest UTC unix ms this run has ever been told it is — the
        /// ratchet every clock reading is held at or above, so winding the
        /// device clock forward and back can't farm the cooldowns above or the
        /// offline credit. See <see cref="ClockGuard"/>. 0 = never read.
        /// </summary>
        public long clockHighWaterUnixMs;

        /// <summary>
        /// Accumulated foreground play time in ms — a monotonic, clock-independent
        /// measure of how far a run has been carried. GameLoop adds each real
        /// frame delta; it survives Migration and is the basis cloud saves are
        /// compared on (most-played wins), so device wall-clock skew can't decide
        /// which save is adopted.
        /// </summary>
        public long playedMs;

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

        /// <summary>Reusable scratch for the observation tick's per-site watcher counts, parallel to digSites — see Observation.Advance. Never saved.</summary>
        public List<double> watcherScratch;

        /// <summary>Amber surfaced by observation sites but not yet reported to telemetry — GameLoop flushes it after each advance so an offline catch-up logs one aggregate find. Never saved.</summary>
        public double amberFoundUnlogged;

        /// <summary>Deep amber pieces surfaced (design §6), a count into the authored order — the journal keeps it across Migration. See <see cref="DeepAmber"/>.</summary>
        public int deepAmberFound;

        /// <summary>Hours watched at the deep site without a piece surfacing — the pity clock.</summary>
        public double deepAmberPityHours;

        /// <summary>Pieces surfaced but not yet reported to telemetry — GameLoop flushes after each advance. Never saved.</summary>
        public int deepAmberFoundUnlogged;

        /// <summary>Invalidate the cached modifier snapshot after an effect-source mutation.</summary>
        public void BumpModifiers()
        {
            modifierVersion++;
        }

        /// <summary>
        /// The sim clock cursor (UTC unix ms): where in wall-clock time the sim
        /// currently stands. The host stamps it from the ratchet before each
        /// live advance and back-dates it to leave-time before an offline
        /// catch-up; <see cref="Simulation"/> advances it per sub-step, which is
        /// what lets a tide edge (design §15) land mid-absence exactly where it
        /// would have landed live. 0 = never stamped (the Wheel reads it as "no
        /// tide"). Never saved — the ratchet's high water is the persisted clock.
        /// </summary>
        public long simNowUnixMs;

        /// <summary>Device UTC offset in minutes, stamped by the host alongside the cursor — the Wheel's windows close at warden-local midnight. Never saved.</summary>
        public int utcOffsetMinutes;

        /// <summary>
        /// The warden's hemisphere for the Wheel (design §15): 0 unset (the
        /// Wheel is inert until the host defaults it from locale), 1 north,
        /// 2 south. A warden property — it survives Migration and starting the
        /// book again in spirit, so it is saved.
        /// </summary>
        public int hemisphere;

        /// <summary>Sabbats kept, one claim per (sabbat, year, hemisphere) — the keeping's ledger (design §15). Saved.</summary>
        public List<SabbatClaim> sabbatClaims = new List<SabbatClaim>();

        /// <summary>
        /// The current tide's keeping (design §15): its generated slots and
        /// their progress, snapshotted as facts rather than re-derived — a reload
        /// never rerolls because nothing is re-derived. Survives the fold
        /// (answered slots are kept; the rest redraw against the new run) and
        /// stays behind as the record once its tide closes, until the next tide
        /// replaces it. Saved.
        /// </summary>
        public KeepingState keeping;

        /// <summary>Cached tide window — see Wheel. Never saved.</summary>
        public WheelCache wheelCache;

        /// <summary>Zones whose waystone inscription has been read (design §6) — lore stays read across Migration.</summary>
        public List<string> seenWaystoneZoneIds = new List<string>();

        /// <summary>Final waystones read (design §7), a count into the authored order — like the deep amber, the journal keeps it across Migration.</summary>
        public int finalWaystonesRead;

        /// <summary>
        /// The fold on which the last final waystone was read, so the chain can
        /// hand over at most one stone per fold no matter which fold the warden
        /// first arrives on. -1 is "none yet", which is why it is not a plain
        /// count of folds since arrival: a warden who climbs late must still get
        /// the reveal a stone at a time rather than all of it at once.
        /// </summary>
        public int finalWaystoneLastFold = -1;

        /// <summary>Compendium lifetime counters (design §5) — never reset, never decremented; they survive Migration.</summary>
        public Dictionary<string, BigDouble> lifetimeGathered = new Dictionary<string, BigDouble>();
        public Dictionary<string, double> lifetimeCrafted = new Dictionary<string, double>();
        public Dictionary<string, BigDouble> lifetimeChoice = new Dictionary<string, BigDouble>();

        /// <summary>
        /// Every species ever befriended, kept for good. The roster itself is
        /// rebuilt each run, so it can only ever say who walks with the warden
        /// now — and "befriend every species in the grove" is a question about
        /// the whole record, not about one camp.
        /// </summary>
        public List<string> speciesEverBefriended = new List<string>();

        /// <summary>
        /// Every station that has ever finished a batch. Stations are put to
        /// work afresh each run, so like the roster they cannot answer a
        /// question spanning Migrations on their own.
        /// </summary>
        public List<string> stationsEverWorked = new List<string>();

        /// <summary>Warden deeds performed this run, keyed by deed id (e.g. "tend") — deed slots of the Rite fill from these.</summary>
        public Dictionary<string, int> deedCounts = new Dictionary<string, int>();

        /// <summary>Offering progress per revealed verse of the Rite, created on first touch.</summary>
        public List<VerseProgressState> verseProgress = new List<VerseProgressState>();

        /// <summary>Raw and crafted materials at camp, keyed by resource id — the only stock that can be sold, gifted, or spent. Goods reach camp in periodic deliveries from the nodes.</summary>
        public Dictionary<string, BigDouble> resources = new Dictionary<string, BigDouble>();

        /// <summary>Decent-quality finds at camp, keyed by resource id (design §5: a Decent delivery batch, sold at the quality bonus alongside the plain stock).</summary>
        public Dictionary<string, BigDouble> decentResources = new Dictionary<string, BigDouble>();

        /// <summary>Choice specimens at camp, keyed by resource id (design §5). Never sold automatically — the windfall sale (and later donation or offering) is the player's explicit choice.</summary>
        public Dictionary<string, BigDouble> choiceResources = new Dictionary<string, BigDouble>();

        /// <summary>Xorshift64* state for the run's rolls (quality grades, observation sketches, deep amber). Seeded at run birth, saved with the run — the sim itself stays deterministic.</summary>
        public ulong rngState = 0x9E3779B97F4A7C15UL;

        /// <summary>
        /// The warden's kith (design §4): every familiar befriended this run,
        /// each an individual with a name, species, level (derived from xp),
        /// Kinship, powerups, and a stationing post. Replaces the anonymous
        /// per-node/per-camp counts — stationed roster members do the gathering
        /// now (see <see cref="Stationing"/>). Bonded familiars
        /// (design §4) live here too, materialised each run from their source.
        /// </summary>
        public List<Familiar> roster = new List<Familiar>();

        /// <summary>Sequence for minting stable per-run roster ids — see <see cref="NextFamiliarId"/>.</summary>
        public int nextFamiliarSeq = 1;

        /// <summary>
        /// Seconds accrued toward the next delivery (design §5: goods land at
        /// camp in discrete batches, one per node every
        /// economy.delivery.batchSeconds, so quality rolls stay per-batch).
        /// Only accrues while a node has pickings waiting.
        /// </summary>
        public double deliveryProgress;

        /// <summary>Every gathering node the player has access to this run.</summary>
        public List<NodeState> nodes = new List<NodeState>();

        /// <summary>Dig sites opened this run (design §5: unlockDigSite upgrades on zones that hold one).</summary>
        public List<DigSiteState> digSites = new List<DigSiteState>();

        /// <summary>
        /// Planters built this run (design §3), each attached to one gather node
        /// or dig site by target id. Each improves that target's yield or dig
        /// speed (see <see cref="Planters"/>). One planter of each
        /// type per target. Reset at Migration — cheap to rebuild each run.
        /// </summary>
        public List<BuiltPlanter> builtPlanters = new List<BuiltPlanter>();

        /// <summary>Field sketches recorded this run, keyed by insect id. A plate is recorded when its count reaches the data's sketches target — recording grants its permanent effects.</summary>
        public Dictionary<string, int> insectSketches = new Dictionary<string, int>();

        /// <summary>Ids of the one-off §9 upgrades bought this run (reset by Migration).</summary>
        public List<string> purchasedUpgradeIds = new List<string>();

        /// <summary>One entry per crafting station the run has put to work (fire / bench / forge).</summary>
        public List<StationState> stations = new List<StationState>();

        /// <summary>
        /// Tincture buffs currently live (design §5, Apothecary) — each ticks
        /// down in sim time and its effects join the active-effect union while
        /// positive (see <see cref="Tinctures"/>). Reset at Migration with the
        /// rest of the run's consumables.
        /// </summary>
        public List<ActiveTincture> activeTinctures = new List<ActiveTincture>();

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

        public BigDouble GetDecent(string resourceId)
        {
            return decentResources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddDecent(string resourceId, BigDouble amount)
        {
            decentResources[resourceId] = GetDecent(resourceId) + amount;
        }

        public BigDouble GetChoice(string resourceId)
        {
            return choiceResources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddChoice(string resourceId, BigDouble amount)
        {
            choiceResources[resourceId] = GetChoice(resourceId) + amount;
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
    /// One kept sabbat (design §15): the keeping's ledger entry, written when
    /// the first tier lands. Claims key on (sabbat, year, hemisphere) so clock
    /// or hemisphere games move hours, never rewards.
    /// </summary>
    [Serializable]
    public sealed class SabbatClaim
    {
        public string sabbatId;

        /// <summary>The calendar year of the sabbat night that was kept.</summary>
        public int year;

        /// <summary>The hemisphere the claim was made under (1 north, 2 south).</summary>
        public int hemisphere;
    }

    /// <summary>
    /// One tide's keeping (design §15): the generated offering slots and their
    /// progress. The slots are stored as FACTS (goods, target, delivered) —
    /// never re-derived from content, so a reload cannot reroll them and a data
    /// retune cannot orphan an answered slot. Deliberately NOT a Rite verse: it lives outside CurrentRite and
    /// verseProgress, so no verse milestone, gift pile, zone opening,
    /// achievement or stat can ever see it.
    /// </summary>
    [Serializable]
    public sealed class KeepingState
    {
        public string sabbatId;

        /// <summary>The calendar year of the tide's sabbat night.</summary>
        public int year;

        public int hemisphere;

        /// <summary>The fold the open slots were drawn against — a fold mid-tide redraws only what is unanswered.</summary>
        public int generatedForMigration = -1;

        /// <summary>Tiers already paid (0..tierSlots.Count) — grants are one-shot however the slots move.</summary>
        public int tierGranted;

        public List<KeepingSlotState> slots = new List<KeepingSlotState>();
    }

    /// <summary>One keeping slot: a whole-ask offering, answered in one act or not at all (the Rite's own rule).</summary>
    [Serializable]
    public sealed class KeepingSlotState
    {
        public const string ResourceKind = "resource";
        public const string SpecimenKind = "specimen";

        /// <summary>"resource" or "specimen".</summary>
        public string kind;

        /// <summary>The goods asked for — null for the specimen slot (any Decent find answers it).</summary>
        public string goodsId;

        public double target;
        public double delivered;

        /// <summary>Authored Renown for value-less materials (the Rite's GoodsSlot rule) — 0 means credit trade value on delivery.</summary>
        public long renownGrant;
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

        /// <summary>
        /// The run's lifetime count of this slot's deed at the moment its verse
        /// revealed. A deed slot counts only the work done since, so no verse
        /// arrives part-answered by the deeds its predecessors were paid for.
        /// </summary>
        public double deedBaseline;

        /// <summary>
        /// True once <see cref="deedBaseline"/> has been taken. A separate flag
        /// because zero is a real baseline — a verse revealed before its deed
        /// was ever done — and must not read as "not yet revealed".
        /// </summary>
        public bool deedBaselineSet;
    }

    /// <summary>One live tincture buff (design §5) — ticks down in sim time; see <see cref="Tinctures"/>.</summary>
    [Serializable]
    public sealed class ActiveTincture
    {
        /// <summary>Tincture id from tinctures.json (also the goods id its recipe brews).</summary>
        public string tinctureId;

        /// <summary>Sim seconds of buff left.</summary>
        public double remainingSeconds;
    }

    /// <summary>
    /// One zone's observation site (design §6: a body posted here watches what
    /// lives there and records it as field sketches). Sketches land in
    /// GameState.insectSketches; the site itself only tracks how long since the
    /// last sketch. Its post is <see cref="Familiar.WatchStation"/> for this
    /// zone (see <see cref="Stationing.WatchAgentsAt"/>).
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
        /// Seconds left on the post-tend Choice window (design §5: Tending
        /// "briefly raised Choice chance"). While positive, delivery batches from
        /// this node multiply their Choice chance by
        /// (1 + economy.tending.choiceChanceBonus). Refreshed by Tend
        /// alongside the yield burst, on its own (longer) duration.
        /// </summary>
        public double choiceBonusRemaining;

        /// <summary>
        /// The day's pickings pooled at the node, awaiting the next delivery —
        /// landed at camp as one quality-rolled batch every
        /// economy.delivery.batchSeconds. Uncapped: a pool is a cadence, not a
        /// bottleneck, and nothing gathered is ever lost.
        /// </summary>
        public BigDouble basket;

        /// <summary>
        /// Combined tool + gear + upgrade multiplier for this node. Defaults to
        /// 1 and is rebuilt by Upgrades.RecomputeYieldMultipliers on purchase;
        /// the tick treats it as an opaque multiplier so it stays
        /// balance-agnostic.
        /// </summary>
        public double yieldMultiplier = 1.0;
    }
}
