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

        // The accumulated play time of the currently-running save (0 for a fresh
        // run). Cloud reconciliation adopts a cloud save only when it has MORE
        // play time than this — a monotonic, device-clock-independent "which run
        // is further along" test, so wall-clock skew can't decide the winner.
        private long _loadedPlayedMs;

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

            // Accumulate real foreground time (unscaled, so a paused timescale or
            // a sim time-skip never inflates it) as the monotonic cloud-save metric.
            State.playedMs += (long)(Time.unscaledDeltaTime * 1000f);

            Simulation.Advance(State, Data, Time.deltaTime);
            FlushAmberFindTelemetry();
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
            // Credit consumable purchases that resolved after their session ended
            // (fetched back and consumed on this launch, so no live callback is
            // waiting). The store's fetch is lazy — it runs no earlier than the
            // first purchase, by which point State is loaded.
            Store.ConsumablePurchased += OnConsumableRecovered;

            Ads.Initialise();
            // Store is initialised lazily on the first purchase, not here: Unity
            // IAP's billing connection must not run at startup — on some devices
            // it launches ProxyBillingActivity before Unity loads, crashing the app.

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

            // The reconcile baseline: how far the run we just loaded has been
            // played (0 for a fresh run, so any cloud save is adopted — reinstall
            // recovery).
            _loadedPlayedMs = State.playedMs;
            _lastSavedUnixMs = NowUnixMs();
            _autosaveCountdown = AutosaveIntervalSeconds;

            // Sign in, then reconcile against the cloud once authenticated. The
            // local run above starts the game responsively; the cloud pull is
            // async and adopts a newer save (or the only save, after a reinstall)
            // when it lands. Signed-in state gates achievements and cloud writes.
            GameServices.SignIn(signedIn =>
            {
                if (signedIn)
                {
                    ReassertAchievements();
                    SubmitLeaderboards();
                    ReconcileCloudSave();
                }
            });

            StartSession();
        }

        /// <summary>
        /// Pull the Play Games cloud save and adopt it when it beats what we
        /// loaded locally. "Beats" is most-played-wins by accumulated play time: a
        /// further-along run from another device — or the only save left after a
        /// reinstall, where the local run had no play time (0) — replaces the
        /// running state and credits the absence since it was taken. A local run
        /// at least as far along is kept, and the next autosave pushes it back up.
        /// Play time (not wall-clock) is the criterion so a wrong device clock
        /// can't win; the cross-device Snapshots conflict is resolved on the same
        /// basis earlier, by UseLongestPlaytime in <see cref="Services.PlayGamesServices"/>.
        /// </summary>
        private void ReconcileCloudSave()
        {
            GameServices.LoadCloud(json =>
            {
                if (string.IsNullOrEmpty(json) || State == null)
                {
                    return;
                }

                SaveData cloud;
                try
                {
                    cloud = SaveCodec.FromJson(json);
                }
                catch (Exception e)
                {
                    // A cloud blob this build can't decode shouldn't disrupt the
                    // running local run — leave it, the next save overwrites it.
                    Debug.LogError("Cloud save decode failed: " + e.Message);
                    return;
                }

                // A null/corrupt or future-build cloud save is left untouched (and
                // not overwritten — the local save only uploads on top once it is
                // genuinely further along), mirroring SaveFile's set-aside policy.
                if (cloud == null || !SaveCodec.TryMigrate(cloud) || cloud.playedMs <= _loadedPlayedMs)
                {
                    return;
                }

                State = SaveCodec.Restore(cloud, Data);
                _loadedPlayedMs = State.playedMs;
                // A cloud kith has already been met and named, like a local load.
                MarkArrivalsSeen();
                // The local load's summary credited the state we've just discarded;
                // drop it so the absence since the cloud save credits the adopted run.
                PendingOfflineSummary = null;
                CreditAbsence((NowUnixMs() - cloud.savedAtUnixMs) / 1000.0);
                // Converge the device and cloud on the adopted save now rather than
                // waiting for the autosave interval to write it back down locally.
                SaveNow();
                Telemetry.LogEvent("cloud_save_adopted", ("saved_at_ms", cloud.savedAtUnixMs), ("played_ms", cloud.playedMs));
            });
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
        /// Refused (returns false) while the reward is still cooling down, so it
        /// can't be tapped without limit once Remove Ads drops the ad.
        /// </summary>
        public bool CreditTimeSkip(double hours)
        {
            if (State == null || hours <= 0.0 || !Amber.CanRewardedTimeSkip(State, NowUnixMs()))
            {
                return false;
            }

            Simulation.AdvanceOffline(State, Data, hours * 3600.0);
            Amber.StampRewardedTimeSkip(State, NowUnixMs());
            return true;
        }

        /// <summary>
        /// Whether a rewarded reward can be taken right now — a loaded ad, or the
        /// Remove Ads entitlement (which grants without one). Every "watch an ad"
        /// button gates its shown/enabled state on this so the reward stays
        /// reachable once ads are removed.
        /// </summary>
        public bool RewardedReady(RewardedPlacement placement) => Store.RemoveAdsOwned || Ads.IsRewardedReady(placement);

        /// <summary>The tail a reward button's label carries — dropped once Remove Ads is owned, since no ad plays.</summary>
        public string RewardedActionSuffix => Store.RemoveAdsOwned ? string.Empty : " — watch a short ad";

        /// <summary>
        /// Take a rewarded reward for <paramref name="placement"/>. Normally shows
        /// the ad; once Remove Ads is owned the reward is granted immediately with
        /// no ad — the whole point of the purchase. Every rewarded placement routes
        /// through here so "no more ads" stays true for all of them.
        /// </summary>
        public void WatchRewarded(RewardedPlacement placement, System.Action onReward, System.Action onClosed = null)
        {
            if (Store.RemoveAdsOwned)
            {
                onReward?.Invoke();
                onClosed?.Invoke();
                return;
            }

            Ads.ShowRewarded(placement, onReward, onClosed);
        }

        /// <summary>Persist the run now (also runs on the autosave interval, on pause, and on quit).</summary>
        public void SaveNow()
        {
            if (State == null)
            {
                return;
            }

            _lastSavedUnixMs = NowUnixMs();
            // Advance the reconcile baseline too: cloud adoption compares against
            // the play time of the newest local save we hold, not the run we
            // launched with. Without this a cloud save from a stale device could
            // wrongly beat freshly-autosaved progress and overwrite it.
            _loadedPlayedMs = State.playedMs;
            var save = SaveCodec.Capture(State, _lastSavedUnixMs);
            SaveFile.Write(save);
            // Mirror to cloud; ReconcileCloudSave pulls it back on the next signed-in
            // launch, adopting it when it is further along than the local slot. Play
            // time is also the snapshot's played-time for the Snapshots conflict tiebreak.
            GameServices.SaveCloud(SaveCodec.ToJson(save), State.playedMs);
            // Post the run's standing on the same cadence as the save (autosave,
            // pause, quit). Idempotent — Play Games keeps only the player's best.
            SubmitLeaderboards();
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

        /// <summary>Active kith slots on the ladder (design §4): one to start, verses sung earn three more, the store opens the last two.</summary>
        public int KithSlots()
        {
            return Kith.Slots(State, Data);
        }

        /// <summary>Companions in the collection — the whole roster, walking or resting.</summary>
        public int KithCount()
        {
            return Kith.Count(State);
        }

        /// <summary>Familiars currently holding a post — the held slots.</summary>
        public int KithWalking()
        {
            return Kith.Walking(State);
        }

        /// <summary>The next verse-milestone still ahead of the ladder, or 0 when every earned slot is open.</summary>
        public int NextKithVerseMilestone()
        {
            return Kith.NextVerseMilestone(State, Data);
        }

        /// <summary>Lifetime verses sung (design §4 ladder) — folded runs plus this one.</summary>
        public int TotalVersesSung()
        {
            return Kith.TotalVersesSung(State, Data);
        }

        /// <summary>A familiar's species trait (design §4) — null when the species is unknown.</summary>
        public TraitData FamiliarTrait(Familiar familiar)
        {
            return Traits.Of(Data, familiar);
        }

        /// <summary>Rename a familiar (design §4: name at arrival, rename any time). Returns false when the name is blank.</summary>
        public bool RenameFamiliar(Familiar familiar, string name)
        {
            return Roster.Rename(familiar, name);
        }

        /// <summary>
        /// Station a familiar at a post — a node id, "trail", a "dig:{zone}"
        /// site, or null to rest at camp (design §2). Returns false when a
        /// resting familiar wants a post and every slot is walked (§4 ladder).
        /// </summary>
        public bool StationFamiliar(Familiar familiar, string stationId)
        {
            return Roster.Station(State, Data, familiar, stationId);
        }

        /// <summary>Walk the warden to a node — one body per post, so a familiar holding it steps back to camp (design §2).</summary>
        public void PostWarden(NodeState node)
        {
            Warden.Post(State, node);
        }

        /// <summary>Send the warden to the wander post — roaming every node and watch site (design §2), evicting any familiar wandering there.</summary>
        public void WanderWarden()
        {
            Warden.Wander(State);
        }

        /// <summary>Send the warden back to camp — no post, no picking.</summary>
        public void RestWarden()
        {
            Warden.Rest(State);
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

        /// <summary>True while a verse-earned pile waits unanswered (design §4) — shows the node plates' pile lines.</summary>
        public bool GiftAvailable()
        {
            return Gifts.IsAvailable(State, Data);
        }

        /// <summary>Units of the node's own resource one pile costs — for the pile line's label.</summary>
        public BigDouble GiftPileCost()
        {
            return Gifts.PileCost(Data.economy);
        }

        /// <summary>The specialist a pile at this node would call (design §4), or null when no one new answers here.</summary>
        public SpeciesData GiftSpeciesFor(NodeState node)
        {
            return Gifts.NodeCanCall(State, Data, node) ? Gifts.SpecialistFor(Data, node) : null;
        }

        /// <summary>True when camp stock covers a pile at this node, someone new would answer, and a slot is open.</summary>
        public bool CanLeaveGift(NodeState node)
        {
            return Gifts.CanLeavePile(State, Data, node);
        }

        /// <summary>
        /// Leave a pile of the node's own resource (design §4) — the
        /// resource's specialist arrives, stationed there, and queues for the
        /// naming sheet like any recruit. Returns the newcomer, or null when
        /// the camp can't spare the pile (or no one new would answer).
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

        // ─────────────────────── The store's kith slots (design §4) ──────────

        /// <summary>
        /// Fold the store's entitlements into the run (design §4 ladder): the
        /// starter bundle and the plain slot each open a slot, the bundle pays
        /// its one-time Amber. Call sites: startup (saved values bridge until
        /// billing resolves) and every purchase result.
        /// </summary>
        public void SyncKithPurchases()
        {
            if (KithPurchases.Apply(State, Data,
                    Store.IsOwned(StoreProductIds.StarterBundle),
                    Store.IsOwned(StoreProductIds.KithSlot)))
            {
                SaveNow();
            }
        }

        /// <summary>
        /// Start a store purchase of a kith slot product and fold the
        /// entitlement in on success. The HUD owns the button copy; the result
        /// callback fires on the main thread like every IStore callback.
        /// </summary>
        public void PurchaseKithProduct(string productId, System.Action<StoreResult> onComplete)
        {
            Store.Purchase(productId, result =>
            {
                if (result == StoreResult.Purchased || result == StoreResult.AlreadyOwned)
                {
                    SyncKithPurchases();
                    Telemetry.LogEvent("iap_purchased", ("product", productId));
                }

                onComplete?.Invoke(result);
            });
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

        /// <summary>
        /// Credit the rewarded-ad Amber drip (design §10). The caller shows the
        /// ad and calls this only on the reward; returns the amount granted.
        /// </summary>
        public double GrantAmberDrip()
        {
            var amount = Amber.GrantDrip(State, Data, NowUnixMs());
            if (amount > 0.0)
            {
                Telemetry.LogEvent("amber_drip", ("amount", amount));
                SaveNow();
            }

            return amount;
        }

        /// <summary>Whether the rewarded Amber drip can be taken right now (configured, off cooldown) — a "Watch" button gates its enabled state on this and RewardedReady.</summary>
        public bool CanWatchAmberDrip => Amber.CanGrantDrip(State, Data, NowUnixMs());

        /// <summary>Whether the rewarded time-skip can be taken right now (off cooldown) — "Hasten a while" gates on this and RewardedReady.</summary>
        public bool CanTimeSkipReward => Amber.CanRewardedTimeSkip(State, NowUnixMs());

        /// <summary>Seconds until the rewarded time-skip re-arms, or 0 when it's ready now — the camp strip counts down from this.</summary>
        public double TimeSkipRewardCooldownRemaining => Amber.RewardedTimeSkipCooldownRemainingMs(State, NowUnixMs()) / 1000.0;

        /// <summary>Whether the weekly Amber cache is signed in, configured, and off cooldown — the button's shown/enabled state.</summary>
        public bool CanClaimWeeklyCache()
        {
            return GameServices.IsSignedIn && Amber.CanClaimWeeklyCache(State, Data, NowUnixMs());
        }

        /// <summary>Claim the weekly Amber cache (design §11). Returns the amount granted (0 = refused).</summary>
        public double ClaimWeeklyCache()
        {
            var amount = Amber.ClaimWeeklyCache(State, Data, NowUnixMs());
            if (amount > 0.0)
            {
                Telemetry.LogEvent("weekly_amber_cache", ("amount", amount));
                SaveNow();
            }

            return amount;
        }

        /// <summary>
        /// Buy a consumable Amber pack (design §10) and credit its pile on success.
        /// The result callback fires on the main thread like every IStore callback.
        /// </summary>
        public void PurchaseAmberPack(string productId, System.Action<StoreResult> onComplete)
        {
            Store.Purchase(productId, result =>
            {
                if (result == StoreResult.Purchased)
                {
                    var amount = Amber.GrantPack(State, AmberPackAmount(productId));
                    Telemetry.LogEvent("amber_pack", ("product", productId), ("amount", amount));
                    SaveNow();
                }

                onComplete?.Invoke(result);
            });
        }

        /// <summary>
        /// Credit a consumable pack that the store confirmed without a live
        /// callback — an interrupted purchase, consumed on this launch. The Play
        /// token is already spent, so this is the only place its pile is granted.
        /// </summary>
        private void OnConsumableRecovered(string productId)
        {
            if (State == null)
            {
                return;
            }

            var amount = Amber.GrantPack(State, AmberPackAmount(productId));
            if (amount > 0.0)
            {
                Telemetry.LogEvent("amber_pack_recovered", ("product", productId), ("amount", amount));
                SaveNow();
            }
        }

        /// <summary>The Amber pile a pack product grants, from the store catalogue (0 for an unknown id).</summary>
        public double AmberPackAmount(string productId)
        {
            var store = Data.economy?.store;
            if (store == null)
            {
                return 0.0;
            }

            if (productId == StoreProductIds.AmberPackSmall)
            {
                return store.amberPackSmall;
            }

            if (productId == StoreProductIds.AmberPackLarge)
            {
                return store.amberPackLarge;
            }

            return 0.0;
        }

        private void FlushAmberFindTelemetry()
        {
            if (State == null || State.amberFoundUnlogged <= 0.0)
            {
                return;
            }

            Telemetry.LogEvent("amber_found", ("amount", State.amberFoundUnlogged));
            State.amberFoundUnlogged = 0.0;
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
        /// Re-assert achievements the current state already satisfies, run when
        /// sign-in completes — closing the sign-in race where a milestone reached
        /// while signed out never gets its one-shot celebration again. The mapping
        /// lives in <see cref="Achievements.Reassert"/> so it can be tested with a
        /// fake service, without this MonoBehaviour's Awake/Initialise lifecycle.
        /// </summary>
        private void ReassertAchievements()
        {
            Achievements.Reassert(GameServices, State, Data);
        }

        /// <summary>
        /// Post the current best to the leaderboards, run on sign-in and each save.
        /// The mapping lives in <see cref="Leaderboards.SubmitAll"/> so it can be
        /// tested with a fake service, without this MonoBehaviour's lifecycle.
        /// </summary>
        private void SubmitLeaderboards()
        {
            Leaderboards.SubmitAll(GameServices, State, Data);
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
