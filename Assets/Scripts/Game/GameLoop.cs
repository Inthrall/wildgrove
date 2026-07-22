using System;
using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Game.Telemetry;
using Wildgrove.Sim;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game
{
    /// <summary>
    /// The scene-side driver that turns the pure simulation into a running game:
    /// it owns the content asset and the live <see cref="GameState"/>, advances
    /// the tick every frame, and exposes the core-loop player actions (station
    /// the kith, name and level familiars, barter at the Exchange, buy upgrades)
    /// for the input/UI layer to call. All game logic lives in Wildgrove.Sim;
    /// this class is deliberately thin wiring.
    /// </summary>
    public sealed class GameLoop : MonoBehaviour
    {
        private const double AutosaveIntervalSeconds = 30.0;

        /// <summary>
        /// Below this much credited absence the welcome-back sheet stays quiet
        /// (quick restarts and editor recompiles shouldn't greet the player).
        /// The welcome_back telemetry event uses the same bar so the metric
        /// counts what players actually saw.
        /// </summary>
        public const double WelcomeBackMinSeconds = 60.0;

        public GameDataAsset Data { get; private set; }
        public GameState State { get; private set; }

        /// <summary>
        /// What the last offline catch-up (cold launch or pause→resume)
        /// credited, held until the HUD collects it via
        /// <see cref="TakePendingOfflineSummary"/>. Null when there was
        /// nothing to credit (fresh run, or already shown).
        /// </summary>
        public OfflineSummary PendingOfflineSummary { get; private set; }

        /// <summary>The analytics/crash-reporting sink (Debug.Log in editor, Firebase on device).</summary>
        public ITelemetry Telemetry { get; private set; }

        /// <summary>Rewarded-ads seam (stub until the AdMob implementation lands).</summary>
        public IAds Ads { get; private set; }

        /// <summary>In-app purchase seam (stub until the Unity IAP implementation lands).</summary>
        public IStore Store { get; private set; }

        /// <summary>Play Games seam — sign-in, achievements, cloud save (stub until the implementation lands).</summary>
        public IGameServices GameServices { get; private set; }

        private double _autosaveCountdown = AutosaveIntervalSeconds;
        private float _sessionStartRealtime;
        private bool _sessionOpen;
        private long _lastSavedUnixMs;

        // Familiars the player has been introduced to (in-memory — a loaded kith
        // has already been met). A newly arrived, non-bonded familiar queues for
        // the naming sheet; bonded familiars have canonical names and their own
        // celebration (design §4).
        private readonly Queue<Familiar> _pendingArrivals = new Queue<Familiar>();
        private readonly HashSet<string> _announcedFamiliars = new HashSet<string>();

        private void Awake()
        {
            Initialise();
        }

        private void Update()
        {
            // A script recompile during Play reloads the app domain: non-serialised
            // fields reset and Awake does not re-run. Re-initialise rather than
            // ticking dead state — the run comes back from the last autosave.
            if (State == null)
            {
                Initialise();
            }

            Simulation.Advance(State, Data, Time.deltaTime);
            RefreshArrivals();

            _autosaveCountdown -= Time.deltaTime;
            if (_autosaveCountdown <= 0.0)
            {
                _autosaveCountdown = AutosaveIntervalSeconds;
                SaveNow();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Android's "the player switched away" signal — the last reliable
            // moment to persist before the OS may kill the process. It's also
            // the mobile session boundary: end on pause, start on resume.
            if (paused)
            {
                SaveNow();
                EndSession();
            }
            else if (!_sessionOpen && State != null)
            {
                // Resuming a still-alive process — Android's most common
                // return path. Update's next delta is clamped to a fraction of
                // a second, so the hours away must be credited here exactly
                // like a cold launch credits them (the pause branch saved on
                // the way out, making the last save the absence baseline).
                CreditAbsence((NowUnixMs() - _lastSavedUnixMs) / 1000.0);
                StartSession();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
            EndSession();
        }

        private void Initialise()
        {
            try
            {
                Data = GameDataAsset.LoadFromResources();
            }
            catch
            {
                // Missing/broken asset: fail loudly once, not once per frame.
                enabled = false;
                throw;
            }

            // In the editor the plain log sink keeps tests and Play mode free
            // of Firebase init churn; on device Firebase is the real sink,
            // with the log sink mirrored underneath for logcat.
#if UNITY_EDITOR
            Telemetry = new UnityLogTelemetry();
#else
            Telemetry = new FirebaseTelemetry(new UnityLogTelemetry());
#endif

            // The monetization/services seams. On device the SDK-backed impls
            // run (AdMob, Unity IAP, Play Games); the editor keeps the stubs so
            // Play mode and the EditMode suite need no SDK connection — the same
            // swap the Telemetry sink makes above.
#if UNITY_EDITOR
            Ads = new StubAds();
            Store = new StubStore();
            GameServices = new StubGameServices();
#elif UNITY_ANDROID
            Ads = new AdMobAds();
            Store = new UnityIapStore();
            GameServices = new PlayGamesServices();
#else
            Ads = new StubAds();
            Store = new StubStore();
            GameServices = new StubGameServices();
#endif
            Ads.Initialise();
            // Store is initialised lazily on the first purchase, not here: Unity
            // IAP's billing connection must not run at startup — on some devices
            // it launches ProxyBillingActivity before Unity loads, crashing the app.
            GameServices.SignIn();

            if (SaveFile.TryLoad(out var save))
            {
                State = SaveCodec.Restore(save, Data);
                // A loaded kith has already been met and named — don't re-prompt.
                MarkArrivalsSeen();
                CreditAbsence((NowUnixMs() - save.savedAtUnixMs) / 1000.0);
            }
            else
            {
                // A fresh run's seed kith (a vole and a raven, design §4) arrives
                // to be named — RefreshArrivals queues them on the first tick.
                State = GameStateFactory.NewGame(Data);
            }

            _lastSavedUnixMs = NowUnixMs();
            _autosaveCountdown = AutosaveIntervalSeconds;
            StartSession();
        }

        /// <summary>
        /// Run the offline catch-up for an absence and queue the welcome-back
        /// summary — shared by the cold-launch load and the pause→resume path.
        /// </summary>
        private void CreditAbsence(double awaySeconds)
        {
            var summary = Simulation.AdvanceOfflineWithSummary(State, Data, awaySeconds);

            // An unshown summary from a previous absence keeps priority — it
            // credited earlier, larger time; don't clobber it with a top-up.
            if (PendingOfflineSummary == null && summary.creditedSeconds > 0.0)
            {
                PendingOfflineSummary = summary;
            }

            if (summary.creditedSeconds >= WelcomeBackMinSeconds)
            {
                Telemetry.LogEvent("welcome_back",
                    ("away_sec", System.Math.Round(summary.realSeconds)),
                    ("credited_sec", System.Math.Round(summary.creditedSeconds)));
            }
        }

        private void StartSession()
        {
            _sessionStartRealtime = Time.realtimeSinceStartup;
            _sessionOpen = true;
            Telemetry.LogEvent("session_start");
        }

        private void EndSession()
        {
            // Guarded so quit-after-pause (or a pause before Awake) can't
            // double-count; the design's gate metric is session length.
            if (!_sessionOpen)
            {
                return;
            }

            _sessionOpen = false;
            Telemetry.LogEvent("session_end",
                ("length_sec", System.Math.Round(Time.realtimeSinceStartup - _sessionStartRealtime)));
        }

        /// <summary>
        /// Grant the OfflineBoost rewarded-ad reward: credit the welcome-back
        /// haul a second time. Called from the welcome sheet's "Double it" button
        /// once the rewarded ad reports the reward earned.
        /// </summary>
        public void GrantOfflineBonus(OfflineSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            Simulation.GrantHaul(State, summary.gains);
        }

        /// <summary>
        /// Grant the TimeSkip rewarded-ad reward: advance the run by
        /// <paramref name="hours"/> of gathering, exactly as an offline catch-up
        /// of that length would (subject to the same offline rate and cap).
        /// </summary>
        public void CreditTimeSkip(double hours)
        {
            if (State == null || hours <= 0.0)
            {
                return;
            }

            Simulation.AdvanceOffline(State, Data, hours * 3600.0);
        }

        /// <summary>Persist the run now (also runs on the autosave interval, on pause, and on quit).</summary>
        public void SaveNow()
        {
            if (State == null)
            {
                return;
            }

            _lastSavedUnixMs = NowUnixMs();
            var save = SaveCodec.Capture(State, _lastSavedUnixMs);
            SaveFile.Write(save);
            // Mirror to cloud (no-op until the Play Games Snapshots impl lands;
            // the load-side restore/merge arrives with it).
            GameServices.SaveCloud(SaveCodec.ToJson(save));
        }

        /// <summary>Collect (and clear) the load-time offline summary, so the welcome-back sheet shows once.</summary>
        public OfflineSummary TakePendingOfflineSummary()
        {
            var summary = PendingOfflineSummary;
            PendingOfflineSummary = null;
            return summary;
        }

        private static long NowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // ─────────────────────────── The kith (design §4) ────────────────────

        /// <summary>Queue any newly arrived, un-met, non-bonded familiar for the naming sheet.</summary>
        private void RefreshArrivals()
        {
            if (State == null)
            {
                return;
            }

            foreach (var familiar in State.roster)
            {
                if (_announcedFamiliars.Add(familiar.id) && !familiar.bonded)
                {
                    _pendingArrivals.Enqueue(familiar);
                }
            }
        }

        private void MarkArrivalsSeen()
        {
            foreach (var familiar in State.roster)
            {
                _announcedFamiliars.Add(familiar.id);
            }
        }

        /// <summary>The next familiar awaiting a name (without dequeuing), or null.</summary>
        public Familiar PeekPendingArrival()
        {
            return _pendingArrivals.Count > 0 ? _pendingArrivals.Peek() : null;
        }

        /// <summary>Claim the next arrival (the naming sheet dismisses it once named/accepted).</summary>
        public Familiar TakePendingArrival()
        {
            return _pendingArrivals.Count > 0 ? _pendingArrivals.Dequeue() : null;
        }

        /// <summary>Active kith slots on the ladder (design §4): four free, The Old Friend and the Warden's Gallery earn the rest.</summary>
        public int KithSlots()
        {
            return Kith.Slots(State, Data);
        }

        /// <summary>Familiars currently walking with the warden — the held slots.</summary>
        public int KithCount()
        {
            return Kith.Count(State);
        }

        /// <summary>Rename a familiar (design §4: name at arrival, rename any time). Returns false when the name is blank.</summary>
        public bool RenameFamiliar(Familiar familiar, string name)
        {
            return Roster.Rename(familiar, name);
        }

        /// <summary>Station a familiar at a post — a node id, "trail", a "dig:{zone}" site, or null to wander (design §2).</summary>
        public void StationFamiliar(Familiar familiar, string stationId)
        {
            Roster.Station(State, Data, familiar, stationId);
        }

        /// <summary>Walk the warden to a node without tending it — the post moves, the picking starts on arrival (design §13).</summary>
        public void PostWarden(NodeState node)
        {
            State.wardenPostNodeId = node.id;
        }

        /// <summary>A familiar's current run level (design §4).</summary>
        public int FamiliarLevel(Familiar familiar)
        {
            return Familiars.Level(familiar, Data);
        }

        /// <summary>Fraction of the way to the familiar's next level.</summary>
        public double FamiliarLevelProgress(Familiar familiar)
        {
            return Familiars.ProgressToNextLevel(familiar, Data);
        }

        /// <summary>The familiar's permanent Kinship level (design §4).</summary>
        public int FamiliarKinship(Familiar familiar)
        {
            return Kinship.Level(familiar);
        }

        /// <summary>True when a familiar has reached a level-5 milestone whose powerup it hasn't chosen — the pick prompt.</summary>
        public bool HasPendingPowerup(Familiar familiar)
        {
            return Familiars.HasPendingPowerup(familiar, Data);
        }

        /// <summary>The powerups still offerable to a familiar (species pool minus chosen) — for the pick sheet.</summary>
        public List<PowerupData> OfferablePowerups(Familiar familiar)
        {
            return Familiars.OfferablePowerups(familiar, Data);
        }

        /// <summary>Choose a powerup for a familiar with a pending pick. Returns false (no change) otherwise.</summary>
        public bool ChoosePowerup(Familiar familiar, string powerupId)
        {
            if (!Familiars.ChoosePowerup(State, Data, familiar, powerupId))
            {
                return false;
            }

            Telemetry.LogEvent("powerup_chosen", ("familiar", familiar.id), ("powerup", powerupId));
            return true;
        }

        // ─────────────────────────── The Exchange (design §9) ────────────────

        /// <summary>Units of <paramref name="to"/> per one unit of <paramref name="from"/> at the Exchange.</summary>
        public BigDouble ExchangeRate(string from, string to)
        {
            return Exchange.Rate(State, Data, from, to);
        }

        /// <summary>Units of <paramref name="to"/> received for spending <paramref name="amount"/> of <paramref name="from"/> (player-favourable rounding).</summary>
        public BigDouble ExchangeQuote(string from, string to, BigDouble amount)
        {
            return Exchange.Quote(State, Data, from, to, amount);
        }

        /// <summary>Barter goods for goods at the Exchange. Returns units received (0 = refused).</summary>
        public BigDouble TradeAtExchange(string from, string to, BigDouble amount)
        {
            var received = Exchange.TryTrade(State, Data, from, to, amount);
            if (received > BigDouble.Zero)
            {
                Telemetry.LogEvent("exchange_trade",
                    ("from", from), ("to", to),
                    ("spent", amount.ToDouble()), ("received", received.ToDouble()));
            }

            return received;
        }

        // ─────────────────────────── Tending & crafting ──────────────────────

        /// <summary>Tend a node — a burst of extra yield for a short while (the tap-to-tend action; also moves the warden's post and counts as a Rite deed).</summary>
        public void Tend(NodeState node)
        {
            Simulation.Tend(State, Data, node);
        }

        /// <summary>The node's next replant cost, in units of its own resource (design §3) — for the button label.</summary>
        public BigDouble ReplantCost(NodeState node)
        {
            return Replanting.ReplantCost(node, Data.economy);
        }

        /// <summary>True when camp stock covers the node's next replant — the button's enabled state.</summary>
        public bool CanReplant(NodeState node)
        {
            return Replanting.CanReplant(State, Data, node);
        }

        /// <summary>Replant a node's own resource to raise its richness (design §3 — the fourth lane). Returns false (no change) when stock is short.</summary>
        public bool Replant(NodeState node)
        {
            if (!Replanting.TryReplant(State, Data, node))
            {
                return false;
            }

            Telemetry.LogEvent("replanted", ("node", node.id), ("richness", node.richnessLevel));
            return true;
        }

        /// <summary>True while the gift event is live (design §4: a verse sung, a slot open, the pile unanswered) — shows the node plates' pile line.</summary>
        public bool GiftAvailable()
        {
            return Gifts.IsAvailable(State, Data);
        }

        /// <summary>Units of the node's own resource one pile costs — for the pile line's label.</summary>
        public BigDouble GiftPileCost()
        {
            return Gifts.PileCost(Data.economy);
        }

        /// <summary>True when camp stock covers a pile at this node.</summary>
        public bool CanLeaveGift(NodeState node)
        {
            return Gifts.CanLeavePile(State, Data, node);
        }

        /// <summary>
        /// Leave a pile of the node's own resource (design §4: one pile, one
        /// yes) — the arrival is stationed there and queues for the naming
        /// sheet like any recruit. Returns the newcomer, or null when the camp
        /// can't spare the pile.
        /// </summary>
        public Familiar LeaveGift(NodeState node)
        {
            var familiar = Gifts.LeavePile(State, Data, node);
            if (familiar != null)
            {
                Telemetry.LogEvent("gift_left", ("node", node.id), ("species", familiar.speciesId));
            }

            return familiar;
        }

        /// <summary>True once the Carving Bench has opened Bushcraft and its planter recipes (design §3) — gates the planter UI.</summary>
        public bool PlantersUnlocked()
        {
            return Planters.Unlocked(State, Data);
        }

        /// <summary>The planter types that attach to a gather node (design §3).</summary>
        public List<PlanterData> NodePlanters()
        {
            return PlantersForTarget("node");
        }

        /// <summary>The planter types that attach to a dig site (design §3).</summary>
        public List<PlanterData> DigSitePlanters()
        {
            return PlantersForTarget("digSite");
        }

        private List<PlanterData> PlantersForTarget(string target)
        {
            var matching = new List<PlanterData>();
            foreach (var planter in Data.planters)
            {
                if (planter.target == target)
                {
                    matching.Add(planter);
                }
            }

            return matching;
        }

        /// <summary>True when this planter is already built at the target.</summary>
        public bool PlanterBuilt(PlanterData planter, string targetId)
        {
            return State.HasPlanter(targetId, planter.id);
        }

        /// <summary>True when the planter can be built here (unlocked, absent, stock covers the bundle) — the build button's enabled state.</summary>
        public bool CanBuildPlanter(PlanterData planter, string targetId)
        {
            return Planters.CanBuild(State, Data, planter, targetId);
        }

        /// <summary>Build a planter at a node or dig site (design §3). Returns false (no change) when it can't be built.</summary>
        public bool BuildPlanter(PlanterData planter, string targetId)
        {
            if (!Planters.TryBuild(State, Data, planter, targetId))
            {
                return false;
            }

            Telemetry.LogEvent("planter-built", ("planter", planter.id), ("target", targetId));
            return true;
        }

        /// <summary>True once the run owns the one-off upgrade.</summary>
        public bool IsUpgradePurchased(UpgradeData upgrade)
        {
            return State.HasUpgrade(upgrade.id);
        }

        /// <summary>The tool tier blocking this upgrade (design §3 zone gate), or null when none — for the buy button's "needs … tools" line.</summary>
        public string MissingToolTier(UpgradeData upgrade)
        {
            return Upgrades.MissingToolTier(State, Data, upgrade);
        }

        /// <summary>True when the run holds the upgrade's materials (money→XP: no Coin) — for the buy button's enabled state.</summary>
        public bool CanAffordUpgrade(UpgradeData upgrade)
        {
            return Upgrades.CanAfford(State, upgrade);
        }

        /// <summary>True when the run's skill level clears the upgrade's gate (design §9).</summary>
        public bool MeetsUpgradeSkillGate(UpgradeData upgrade)
        {
            return Upgrades.MeetsSkillGate(State, Data, upgrade);
        }

        /// <summary>Buy a one-off upgrade. Returns false (no change) when owned, gated, or unaffordable.</summary>
        public bool PurchaseUpgrade(UpgradeData upgrade)
        {
            if (!Upgrades.TryPurchase(State, Data, upgrade))
            {
                return false;
            }

            Telemetry.LogEvent("upgrade_purchased", ("upgrade_id", upgrade.id));
            return true;
        }

        /// <summary>A building line's current level (bought + owned milestone upgrades) — for the buildings row.</summary>
        public int BuildingLevel(BuildingData building)
        {
            return Buildings.TotalLevel(State, building);
        }

        /// <summary>The material bundle for the line's next level (money→XP: buildings are a goods sink) — for the build button's label.</summary>
        public List<Buildings.MaterialCost> NextBuildingBundle(BuildingData building)
        {
            return Buildings.NextLevelBundle(State, Data, building);
        }

        /// <summary>True when camp stock covers the line's next-level bundle.</summary>
        public bool CanAffordBuilding(BuildingData building)
        {
            return Buildings.CanAfford(State, Data, building);
        }

        /// <summary>Buy the line's next level. Returns false (no change) when the bundle can't be covered.</summary>
        public bool BuyBuildingLevel(BuildingData building)
        {
            if (!Buildings.TryBuyLevel(State, Data, building))
            {
                return false;
            }

            Telemetry.LogEvent("building_level_bought",
                ("building", building.id),
                ("level", Buildings.TotalLevel(State, building)));
            return true;
        }

        /// <summary>The recipes the run can see — for the HUD's crafting section (level-locked ones included, as visible goals).</summary>
        public List<RecipeData> AvailableRecipes()
        {
            return Crafting.AvailableRecipes(State, Data);
        }

        /// <summary>True when the run's skill level covers the recipe's skillLevel — for the HUD's requirement hint.</summary>
        public bool IsRecipeLevelMet(RecipeData recipe)
        {
            return Crafting.SkillLevelMet(State, Data, recipe);
        }

        /// <summary>True when a station may actively work the recipe (every gate) — the assign/advance gate.</summary>
        public bool IsRecipeWorkable(RecipeData recipe)
        {
            return Crafting.IsWorkable(State, Data, recipe);
        }

        /// <summary>The skill's current level (1 when the XP system is unconfigured).</summary>
        public int SkillLevel(string skill)
        {
            return Skills.Level(State, Data, skill);
        }

        /// <summary>Fraction of the way to the skill's next level (0 once capped).</summary>
        public double SkillProgress(string skill)
        {
            return Skills.ProgressToNext(State, Data, skill);
        }

        /// <summary>The skills this run has opened, for the HUD's readout — stable alphabetical order.</summary>
        public List<string> UnlockedSkills()
        {
            var skills = new List<string>(Upgrades.UnlockedSkills(State, Data));
            skills.Sort(StringComparer.Ordinal);
            return skills;
        }

        /// <summary>True while this recipe's station is assigned to it.</summary>
        public bool IsCrafting(RecipeData recipe)
        {
            return Crafting.ActiveStationFor(State, recipe) != null;
        }

        /// <summary>True when camp stock covers one batch of the recipe's inputs.</summary>
        public bool CanCraft(RecipeData recipe)
        {
            return Crafting.HasInputs(State, recipe);
        }

        /// <summary>The in-flight batch's fraction complete (0 when idle or stalled).</summary>
        public double CraftProgress(RecipeData recipe)
        {
            return Crafting.Progress(State, Data, recipe);
        }

        /// <summary>Start the recipe on its station (displacing whatever it was working, in-flight inputs refunded), or stop it if it's already running.</summary>
        public void ToggleCraft(RecipeData recipe)
        {
            if (IsCrafting(recipe))
            {
                Crafting.Stop(State, Data, recipe);
                return;
            }

            if (!Crafting.IsWorkable(State, Data, recipe))
            {
                return;
            }

            Crafting.Assign(State, Data, recipe);
            Telemetry.LogEvent("craft_started",
                ("recipe", recipe.id),
                ("station", recipe.station));
        }

        /// <summary>Mark a zone's waystone inscription as read (design §6 — shown once on arrival, re-readable in the Compendium).</summary>
        public void MarkWaystoneRead(string zoneId)
        {
            Narrative.MarkWaystoneRead(State, zoneId);
            Telemetry.LogEvent("waystone_read", ("zone", zoneId));
        }

        /// <summary>Whether the time-skip is configured and affordable — the button's enabled state.</summary>
        public bool CanTimeSkip()
        {
            return Amber.CanTimeSkip(State, Data);
        }

        /// <summary>Spend Amber to instantly credit hours of full-rate production (design §10). Returns the hours credited (0 = refused).</summary>
        public double TimeSkip()
        {
            var cost = Data.economy?.amber?.timeSkipCostAmber ?? 0.0;
            var hours = Amber.TryTimeSkip(State, Data);
            if (hours > 0.0)
            {
                Telemetry.LogEvent("time_skip_used", ("hours", hours), ("amber_cost", cost));
            }

            return hours;
        }

        /// <summary>True when offering into this slot could land something now — the verse is open and the camp holds what it asks (the HUD's button gate).</summary>
        public bool CanOffer(RiteVerseData verse, int slotIndex)
        {
            return Rite.CanDeliver(State, Data, verse, slotIndex);
        }

        /// <summary>Offer camp stock into a resource slot of the Rite (design §7). Returns the units delivered.</summary>
        public BigDouble OfferResource(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var given = Rite.DeliverResource(State, Data, verse, slotIndex);
            if (given > BigDouble.Zero)
            {
                Telemetry.LogEvent("offering_made",
                    ("verse", verse.id), ("slot", slotIndex), ("amount", given.ToDouble()));
                AfterOffering(verse, wasComplete);
            }

            return given;
        }

        /// <summary>Offer one Fine/Pristine specimen into a specimen slot. Returns true when one was given.</summary>
        public bool OfferSpecimen(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var resourceId = Rite.DeliverSpecimen(State, Data, verse, slotIndex);
            if (resourceId == null)
            {
                return false;
            }

            Telemetry.LogEvent("offering_made",
                ("verse", verse.id), ("slot", slotIndex), ("specimen", resourceId));
            AfterOffering(verse, wasComplete);
            return true;
        }

        /// <summary>Offer one field sketch into a sketch slot — the page is torn out for the spirits, so that portion must be re-observed (design §6). Returns true when one was given.</summary>
        public bool OfferSketch(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var insectId = Rite.DeliverSketch(State, Data, verse, slotIndex);
            if (insectId == null)
            {
                return false;
            }

            Telemetry.LogEvent("offering_made",
                ("verse", verse.id), ("slot", slotIndex), ("sketch_from", insectId));
            AfterOffering(verse, wasComplete);
            return true;
        }

        /// <summary>Fix one Pristine specimen into the Folio (design §6 — permanence over the windfall). Returns false when no spread wants it or none is held.</summary>
        public bool FixSpecimen(string resourceId)
        {
            var bondsBefore = EarnedBondIds();
            if (!Folio.TryFix(State, Data, resourceId))
            {
                return false;
            }

            Telemetry.LogEvent("specimen_fixed", ("resource", resourceId));
            ReportNewBonds(bondsBefore);
            // A completed Gallery opens kith slot 6 — a bond that was waiting
            // for room steps in now (SyncBonded is idempotent).
            Roster.SyncBonded(State, Data);
            return true;
        }

        /// <summary>Craft and wear a piece of the kit (design §4) — spends its materials, fills its slot. Returns false when it can't be made.</summary>
        public bool CraftGear(GearData gear)
        {
            if (!Gear.TryCraft(State, Data, gear))
            {
                return false;
            }

            Telemetry.LogEvent("gear_crafted", ("gear", gear.id), ("slot", gear.slot));
            return true;
        }

        /// <summary>Verdure not yet allocated to an Almanac node — for the section header and buy buttons.</summary>
        public double AvailableVerdure()
        {
            return Almanac.AvailableVerdure(State, Data);
        }

        /// <summary>Buy an Almanac node with unallocated Verdure (permanent — survives Migration). Returns false when it can't be bought.</summary>
        public bool BuyAlmanacNode(AlmanacNodeData node)
        {
            var bondsBefore = EarnedBondIds();
            if (!Almanac.TryBuy(State, Data, node))
            {
                return false;
            }

            Telemetry.LogEvent("almanac_node_bought", ("node", node.id), ("verdure_cost", node.costVerdure));
            ReportNewBonds(bondsBefore);
            // The Old Friend opens kith slot 5 — a bond that was waiting for
            // room steps in now (SyncBonded is idempotent).
            Roster.SyncBonded(State, Data);
            return true;
        }

        private HashSet<string> EarnedBondIds()
        {
            var earned = new HashSet<string>();
            foreach (var bond in Bonds.Earned(State, Data))
            {
                earned.Add(bond.id);
            }

            return earned;
        }

        /// <summary>
        /// The most recently earned bond awaiting its HUD celebration — a
        /// companion is rare enough to deserve a moment. Null when none is pending.
        /// </summary>
        public BondData PendingBondCelebration { get; private set; }

        /// <summary>Claim the pending bond celebration (clears it), or null.</summary>
        public BondData TakePendingBondCelebration()
        {
            var bond = PendingBondCelebration;
            PendingBondCelebration = null;
            return bond;
        }

        /// <summary>
        /// A bond is earned the moment its source completes. Its companion is
        /// materialised into the roster by Folio/Almanac restore paths; here we
        /// just surface the celebration and telemetry for the newly earned ones.
        /// </summary>
        private void ReportNewBonds(HashSet<string> bondsBefore)
        {
            foreach (var bond in Bonds.Earned(State, Data))
            {
                if (!bondsBefore.Contains(bond.id))
                {
                    Telemetry.LogEvent("familiar_bonded", ("bond", bond.id));
                    PendingBondCelebration = bond;
                    // Materialise the companion now so it's present immediately.
                    Roster.SyncBonded(State, Data);
                    MarkArrivalsSeen();
                }
            }
        }

        /// <summary>True when the Rite has consented — the Migrate button's visibility.</summary>
        public bool CanMigrate()
        {
            return Migration.CanMigrate(State, Data);
        }

        /// <summary>The Verdure total a Migration right now would bank — for the confirm sheet.</summary>
        public double VerdureAfterMigration()
        {
            return Migration.VerdureAfterMigration(State, Data);
        }

        /// <summary>
        /// Fold the camp (design §7): swap in the next run's state, keeping the
        /// permanents (and the kith, with run XP banked into Kinship), and save
        /// at once so the old run can't be resumed by force-closing. Returns
        /// false when the Rite hasn't consented.
        /// </summary>
        public bool Migrate()
        {
            var next = Migration.Migrate(State, Data);
            if (next == null)
            {
                return false;
            }

            Telemetry.LogEvent("migration_completed",
                ("number", next.migrationCount),
                ("verdure", next.verdurePoints),
                ("renown", State.renown.ToDouble()));
            State = next;
            // The carried kith has already been met — don't re-prompt naming.
            MarkArrivalsSeen();
            SaveNow();
            return true;
        }

        private void AfterOffering(RiteVerseData verse, bool verseWasComplete)
        {
            if (!verseWasComplete && Rite.IsVerseComplete(State, Data, verse))
            {
                Telemetry.LogEvent("verse_completed", ("verse", verse.id));
                if (Rite.IsRiteComplete(State, Data))
                {
                    Telemetry.LogEvent("rite_completed", ("renown", State.renown.ToDouble()));
                }
            }
        }
    }
}
