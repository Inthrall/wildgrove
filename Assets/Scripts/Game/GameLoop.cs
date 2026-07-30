using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Game.Telemetry;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// The scene-side driver that turns the pure simulation into a running game:
    /// it owns the content asset and the live <see cref="GameState"/>, advances
    /// the tick every frame, and exposes the core-loop player actions (station
    /// the kith, name and level familiars, barter at the Exchange, buy upgrades)
    /// for the input/UI layer to call. All game logic lives in Wildgrove.Sim;
    /// this class is deliberately thin wiring.
    /// <para>
    /// THIS file holds the run itself — the Unity lifecycle, the services, the
    /// save and its cloud reconciliation. The player actions are grouped by
    /// design section in the partial files beside it (GameLoop.Kith.cs,
    /// .Trails.cs, .Camp.cs, .Amber.cs, .Fold.cs); each one is the same thin
    /// wiring, so a new action belongs in whichever section already owns its
    /// neighbours rather than here.
    /// </para>
    /// </summary>
    public sealed partial class GameLoop : MonoBehaviour
    {
        private const double AutosaveIntervalSeconds = 30.0;

        /// <summary>
        /// Below this much credited absence the welcome-back sheet stays quiet —
        /// the same bar <see cref="SessionLog"/> reports the metric on, so the
        /// sheet and the number agree about what players actually saw.
        /// </summary>
        public const double WelcomeBackMinSeconds = SessionLog.WelcomeBackMinSeconds;

        public GameDataAsset Data { get; private set; }
        public GameState State { get; private set; }

        /// <summary>
        /// What the last offline catch-up (cold launch or pause→resume)
        /// credited, held until the HUD collects it via
        /// <see cref="TakePendingOfflineSummary"/>. Null when there was
        /// nothing to credit (fresh run, or already shown).
        /// </summary>
        public OfflineSummary PendingOfflineSummary => _announce.PendingOfflineSummary;

        /// <summary>The analytics/crash-reporting sink (Debug.Log in editor, Firebase on device).</summary>
        public ITelemetry Telemetry { get; private set; }

        /// <summary>Rewarded-ads seam (stub until the AdMob implementation lands).</summary>
        public IAds Ads { get; private set; }

        /// <summary>In-app purchase seam (stub until the Unity IAP implementation lands).</summary>
        public IStore Store { get; private set; }

        /// <summary>Play Games seam — sign-in, achievements, cloud save (stub until the implementation lands).</summary>
        public IGameServices GameServices { get; private set; }

        /// <summary>The Game Stats recorder (Level Up): what the gamer profile is told, and when.</summary>
        public GameStats Stats { get; private set; }

        private double _autosaveCountdown = AutosaveIntervalSeconds;

        // The run's own bookkeeping, split out of this MonoBehaviour so each part
        // can be tested without an Awake: which save we woke from and when it was
        // last written, what the player is still owed a moment for, and what the
        // session has told telemetry.
        private readonly IClock _clock = SystemClock.Instance;
        private readonly Announcements _announce = new Announcements();
        private RunPersistence _persistence;
        private SessionLog _session;

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
            _session.FlushAmberFinds(State);
            _announce.NoticeArrivals(State.roster);
            NoticeKithSlots();

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
            else if (!_session.IsOpen && State != null)
            {
                // Resuming a still-alive process — Android's most common
                // return path. Update's next delta is clamped to a fraction of
                // a second, so the hours away must be credited here exactly
                // like a cold launch credits them (the pause branch saved on
                // the way out, making the last save the absence baseline).
                CreditAbsence((NowUnixMs() - _persistence.LastSavedUnixMs) / 1000.0);
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
            _session = new SessionLog(Telemetry);

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
            Stats = new GameStats(GameServices);
            _persistence = new RunPersistence(Data, new SaveFileStore(), GameServices, _clock);
            // Credit consumable purchases that resolved after their session ended
            // (fetched back and consumed on this launch, so no live callback is
            // waiting). The store's fetch is lazy — it runs no earlier than the
            // first purchase, by which point State is loaded.
            Store.ConsumablePurchased += OnConsumableRecovered;
            // Receive Play Games Rewards (design §11). Set before the billing
            // connection is asked for anything: a reward awarded while the game
            // was closed arrives on the very first purchase fetch, and the store
            // will not acknowledge it until this has granted it.
            Store.RewardRedeemed = OnRewardRedeemed;

            Ads.Initialise();
            // The billing connection must not run *at* startup — on some devices it
            // launches ProxyBillingActivity before Unity loads and crashes the app —
            // so it stays off the synchronous launch path. But we still resolve owned
            // entitlements a couple of seconds in (Unity fully up by then), so the
            // Remove Ads button can hide when the player already owns it instead of
            // only after a purchase attempt. A purchase before this fires still
            // lazy-inits the store itself.
            Invoke(nameof(ResolveEntitlements), StoreEntitlementResolveDelaySeconds);

            // A fresh run's seed kith (a vole and a raven, design §4) arrives to
            // be named; a loaded one has already been met, and its slot ladder
            // was earned in an earlier session — neither is news.
            var run = _persistence.Load();
            State = run.State;
            if (run.WasLoaded)
            {
                _announce.MarkArrivalsSeen(State.roster);
                _announce.MarkKithSlotsSeen();
                CreditAbsence(run.AwaySeconds);
            }

            _autosaveCountdown = AutosaveIntervalSeconds;
            // The stats baseline is the run as loaded: the lifetime totals it
            // arrives with were gathered in earlier sessions and mustn't be
            // reported as this one's.
            Stats.Rebase(State);

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
                    _persistence.Reconcile(AdoptCloudRun);
                }
            });

            StartSession();
        }

        // TEMP: delay before resolving owned entitlements — enough to clear the
        // launch frames so connecting billing can't race Unity's load.
        private const float StoreEntitlementResolveDelaySeconds = 2f;

        /// <summary>
        /// Connect the store and read owned products, off the synchronous launch
        /// path (see the Initialise note). Once it resolves, the camp-actions
        /// refresh hides the Remove Ads button for players who already own it,
        /// and the kith slots those entitlements paid for are folded into the
        /// run. Without that second half, ownership resolved outside a purchase
        /// — a reinstall, a second device, or a cloud save older than the
        /// purchase — hid the buy line (the store says owned) while the ladder
        /// stayed where it was: paid for, and no slot to show for it.
        /// </summary>
        private void ResolveEntitlements()
        {
            Store.Initialise(SyncStoreEntitlements);
        }

        /// <summary>
        /// Fold everything the store says this player holds into the run — the
        /// bought kith slots and the single-use Play Games Rewards alike. Both
        /// are durable entitlements that must survive a reinstall, so the store
        /// is asked rather than the save trusted.
        /// </summary>
        private void SyncStoreEntitlements()
        {
            SyncKithPurchases();
            SyncRewardEntitlements();
        }

        /// <summary>
        /// Take on a cloud save that beat the one we launched with (see
        /// <see cref="RunPersistence.Reconcile"/> for which one wins) — the run
        /// swaps under everything that was reading it, so each of those has to be
        /// told in the same breath.
        /// </summary>
        private void AdoptCloudRun(RunPersistence.Run adopted)
        {
            if (State == null)
            {
                // The pull outlived the run it was for (a teardown mid-flight);
                // nothing left to adopt into, and no save will follow.
                return;
            }

            State = adopted.State;
            // Re-baseline the stats on the adopted run, or the gap between two
            // runs' lifetime totals would post as this session's gathering.
            Stats.Rebase(State);
            // A cloud kith has already been met and named, like a local load.
            _announce.MarkArrivalsSeen(State.roster);
            _announce.MarkKithSlotsSeen();
            // The local load's summary credited the state we've just discarded;
            // drop it so the absence since the cloud save credits the adopted run.
            _announce.DropOfflineSummary();
            CreditAbsence(adopted.AwaySeconds);
            // The adopted save may predate a purchase or a reward this device
            // already owns — re-fold the entitlements rather than let the
            // cloud roll a paid slot or a redeemed pony back.
            SyncStoreEntitlements();
            // Converge the device and cloud on the adopted save now rather than
            // waiting for the autosave interval to write it back down locally.
            SaveNow();
            Telemetry.LogEvent("cloud_save_adopted",
                ("saved_at_ms", adopted.SavedAtUnixMs), ("played_ms", State.playedMs));
        }

        /// <summary>
        /// Run the offline catch-up for an absence and queue the welcome-back
        /// summary — shared by the cold-launch load and the pause→resume path.
        /// </summary>
        private void CreditAbsence(double awaySeconds)
        {
            var summary = Simulation.AdvanceOfflineWithSummary(State, Data, awaySeconds);
            _announce.OfferOfflineSummary(summary);
            _session.ReportWelcomeBack(summary);
        }

        private void StartSession()
        {
            _session.Start(Time.realtimeSinceStartup);
        }

        private void EndSession()
        {
            _session.End(Time.realtimeSinceStartup);
        }

        /// <summary>Persist the run now (also runs on the autosave interval, on pause, and on quit).</summary>
        public void SaveNow()
        {
            if (State == null)
            {
                return;
            }

            _persistence.Save(State);
            // Post the run's standing on the same cadence as the save (autosave,
            // pause, quit). Idempotent — Play Games keeps only the player's best.
            SubmitLeaderboards();
            // Same cadence for the Game Stats totals: hauls and crafts land every
            // tick, so they go as one figure per save rather than one event each.
            Stats.Flush(State);
            // And the achievements, for the same reason the leaderboards are
            // here: sign-in alone would leave a milestone crossed mid-session
            // waiting for the next launch to be granted.
            ReassertAchievements();
        }

        /// <summary>Collect (and clear) the load-time offline summary, so the welcome-back sheet shows once.</summary>
        public OfflineSummary TakePendingOfflineSummary()
        {
            return _announce.TakeOfflineSummary();
        }

        private long NowUnixMs()
        {
            return _clock.NowUnixMs();
        }

        /// <summary>
        /// Re-assert achievements the current state already satisfies, run when
        /// sign-in completes and again on each save — closing the sign-in race
        /// where a milestone reached while signed out never gets its one-shot
        /// celebration again, and granting mid-session ones without waiting for
        /// a relaunch. Idempotent either way: Play ignores an unlock it holds,
        /// and progress is set rather than added. The mapping
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
    }
}
