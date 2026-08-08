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
    public sealed partial class GameLoop : MonoBehaviour, IRunHost
    {
        private const double AutosaveIntervalSeconds = 30.0;

        /// <summary>
        /// How often the run reaches past this device: the Play Games Snapshots
        /// commit, the achievement sweep, the boards and the Game Stats figures.
        /// Deliberately not the autosave interval — the local write is a file
        /// and costs nothing, while each of these is a network round trip or
        /// forty-odd JNI calls, and Google asks that Snapshots not be committed
        /// on a short timer. The moments that matter (pause, quit, a purchase,
        /// an adopted run, a fresh book) mirror at once regardless; this is only
        /// the floor under a long uninterrupted session.
        /// </summary>
        private const double ServicesIntervalSeconds = 300.0;

        /// <summary>
        /// Wall-clock milliseconds a frame may spend crediting a deferred
        /// absence. Small enough to stay inside a 60 fps budget beside the
        /// frame's own work, so a twelve-hour catch-up costs frames rather than
        /// a stall. See <see cref="Wildgrove.Sim.OfflineCatchUp"/>.
        /// </summary>
        private const double CatchUpMillisecondsPerFrame = 6.0;

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

        /// <summary>Rewarded-ads seam — AdMob on device, <see cref="StubAds"/> in the editor.</summary>
        public IAds Ads { get; private set; }

        /// <summary>In-app purchase seam — Unity IAP on device, <see cref="StubStore"/> in the editor.</summary>
        public IStore Store { get; private set; }

        /// <summary>Play Games seam — sign-in, achievements, cloud save; stubbed in the editor.</summary>
        public IGameServices GameServices { get; private set; }

        /// <summary>The Game Stats recorder (Level Up): what the gamer profile is told, and when.</summary>
        public GameStats Stats { get; private set; }

        /// <summary>
        /// The player's own choices about the app (the inside cover), kept on the
        /// device rather than in the run so starting the book again doesn't
        /// silently re-consent them to anything.
        /// </summary>
        public PlayerPreferences Preferences { get; private set; }

        /// <summary>When the run was last written down — the inside cover's first line.</summary>
        public long LastSavedUnixMs => _persistence?.LastSavedUnixMs ?? 0L;

        /// <summary>True when the last mirror to Play Games did not land (see <see cref="RunPersistence"/>).</summary>
        public bool CloudWriteFailed => _persistence != null && _persistence.LastCloudWriteFailed;

        /// <summary>True when this session took up a further-along run from another device.</summary>
        public bool AdoptedFromCloud => _persistence != null && _persistence.AdoptedFromCloud;

        private double _autosaveCountdown = AutosaveIntervalSeconds;
        private double _servicesCountdown = ServicesIntervalSeconds;

        // The run's own bookkeeping, split out of this MonoBehaviour so each part
        // can be tested without an Awake: which save we woke from and when it was
        // last written, what the player is still owed a moment for, what the
        // session has told telemetry, and the order the run is swapped out in.
        private readonly IClock _clock = SystemClock.Instance;
        private readonly Announcements _announce = new Announcements();
        private readonly System.Diagnostics.Stopwatch _catchUpClock = new System.Diagnostics.Stopwatch();
        private RunPersistence _persistence;
        private RunSwap _swap;
        private SessionLog _session;

        // A long absence being credited a slice per frame; null when none is.
        private OfflineCatchUp _catchUp;
        private bool _rebaseStatsAfterCatchUp;

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

            // A deferred absence takes the frame's sim time instead of the live
            // tick: it is already advancing the grove, far faster than realtime,
            // and running both would fold the seconds since launch into the
            // welcome-back figure the sheet is about to quote.
            if (_catchUp != null)
            {
                // The catch-up owns the sim clock cursor while it runs — it was
                // back-dated to leave-time in CreditAbsence and walks forward a
                // sub-step at a time, so stamping "now" here would teleport the
                // Wheel (design §15) past the very tide edges the slicing exists
                // to honour.
                PumpCatchUp();
            }
            else
            {
                StampWheelClock(0.0);
                Simulation.Advance(State, Data, Time.deltaTime);
            }

            _session.FlushAmberFinds(State);
            _announce.NoticeArrivals(State.roster);
            NoticeKithSlots();

            // Unscaled on both: a time skip must not bring the autosave or the
            // cloud mirror forward with it.
            _servicesCountdown -= Time.unscaledDeltaTime;
            _autosaveCountdown -= Time.unscaledDeltaTime;
            if (_autosaveCountdown <= 0.0)
            {
                _autosaveCountdown = AutosaveIntervalSeconds;
                SaveNow();
            }
        }

        /// <summary>
        /// Credit as much of the deferred absence as this frame can afford. The
        /// budget is wall-clock, but the slices it buys are whole sim-seconds,
        /// so a slow device credits the same grove as a fast one — see
        /// <see cref="OfflineCatchUp"/>.
        /// </summary>
        private void PumpCatchUp()
        {
            _catchUpClock.Restart();
            while (!_catchUp.IsComplete && _catchUpClock.Elapsed.TotalMilliseconds < CatchUpMillisecondsPerFrame)
            {
                // A minute of grove per pass: long enough that the stopwatch
                // read isn't the expensive part, short enough to land inside
                // the budget rather than overshoot it.
                _catchUp.Advance(60.0);
            }

            _catchUpClock.Stop();
            if (_catchUp.IsComplete)
            {
                FinishCatchUp();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Android's "the player switched away" signal — the last reliable
            // moment to persist before the OS may kill the process. It's also
            // the mobile session boundary: end on pause, start on resume.
            if (paused)
            {
                // Sync, not just save: this is the last moment the process is
                // reliably alive, so the cloud mirror and the milestones owed
                // have to go now rather than on the next cadence.
                SaveAndSync();
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
            SaveAndSync();
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

            // Read and applied before the first event can fire: a player who
            // turned analytics off last session must not have this launch
            // reported before their answer is looked up.
            Preferences = new PlayerPreferences(new PlayerPrefsStore());
            Telemetry.SetCollectionEnabled(Preferences.ShareAnalytics);

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
            Stats = new GameStats(GameServices, () => Preferences.ShareAnalytics);
            _persistence = new RunPersistence(Data, new SaveFileStore(), GameServices, _clock);
            // The two sequences that replace the run wholesale. They read this
            // MonoBehaviour back through IRunHost, so their ordering — the thing
            // that fails silently — is pinned by RunSwapTests.
            _swap = new RunSwap(this, _persistence, _announce, Stats, Telemetry);
            // Credit consumable purchases that resolved after their session ended
            // (fetched back and consumed on this launch, so no live callback is
            // waiting). The store's fetch is lazy — it runs no earlier than the
            // first purchase, by which point State is loaded.
            Store.ConsumablePurchased += OnConsumableRecovered;
            // Fold entitlements in whenever the store resolves ownership — not
            // only on the one delayed attempt below. A launch with no signal
            // leaves that attempt with nothing queued, and the connection that
            // comes up later (a first purchase, a tap on Restore purchases) would
            // otherwise fill the store's owned set while the ladder stayed where
            // it was: paid for, with no slot to show for it until a relaunch.
            Store.EntitlementsResolved += SyncStoreEntitlements;
            // Receive Play Games Rewards (design §11). Set before the billing
            // connection is asked for anything: a reward awarded while the game
            // was closed arrives on the very first purchase fetch, and the store
            // will not acknowledge it until this has granted it.
            Store.RewardRedeemed = OnRewardRedeemed;

            // The regional consent answer binds the analytics sink as well as
            // the ads that ask for it. Only Google's form knows what an EEA
            // player said, and until this the sink never heard: a player who
            // chose "Do not consent" stopped seeing ads and went on being
            // counted. Subscribed before Initialise, which can resolve a cached
            // answer on the spot.
            Ads.ConsentResolved += Telemetry.SetConsent;

            // Ads are wanted unless the store said last time that they had been
            // bought away. Asked of the remembered flag rather than the store,
            // because billing cannot be asked anything this early — and a payer's
            // launch must not wake the ads SDK at all, which is what the purchase
            // is for. ResolveEntitlements corrects it either way, seconds later.
            Ads.Initialise(!Preferences.AdsRemoved);
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
                CreditAbsence(run.AwaySeconds, rebaseStatsWhenCredited: true);
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
                    _persistence.Reconcile(_swap.AdoptFromCloud);
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
            NoteAdsEntitlement(Store.RemoveAdsOwned);
        }

        /// <summary>
        /// Record what the store says about the ads, and hold this session to it.
        /// Only reached when ownership is actually known (the store raises its
        /// resolved event on a successful read, never on a failed one), so a
        /// refund can clear the flag here without a lost connection doing the
        /// same — and a cleared flag starts the ads this session rather than
        /// leaving the player without rewards until they relaunch.
        /// </summary>
        internal void NoteAdsEntitlement(bool removed)
        {
            Preferences.AdsRemoved = removed;
            Ads.SetAdsWanted(!removed);
        }

        // ─── The run, as RunSwap reaches it (IRunHost) ────────────────────────
        // Explicit implementations: each of these is already how the rest of this
        // class does the thing, and the seam must not widen any of them into the
        // public surface the HUD sees.

        GameState IRunHost.State
        {
            get { return State; }
            set { State = value; }
        }

        void IRunHost.DropCatchUp()
        {
            DropCatchUp();
        }

        void IRunHost.CreditAbsence(double awaySeconds)
        {
            CreditAbsence(awaySeconds);
        }

        void IRunHost.SyncStoreEntitlements()
        {
            SyncStoreEntitlements();
        }

        void IRunHost.SaveAndSync()
        {
            SaveAndSync();
        }

        /// <summary>
        /// Run the offline catch-up for an absence and queue the welcome-back
        /// summary — shared by the cold-launch load and the pause→resume path.
        /// <para>
        /// A short absence is credited here and now: five minutes is at most
        /// 300 sub-steps, and holding it over would cost more than running it.
        /// A longer one is handed to <see cref="OfflineCatchUp"/> and credited a
        /// slice per frame, because the away cap reaches twelve hours and that
        /// is 43,200 sub-steps — a stall on a cold launch, and a worse one on a
        /// pause→resume, where the grove is already on screen. Nothing is owed
        /// the player until it lands: the welcome-back sheet waits on
        /// <see cref="CatchingUp"/>, so the work happens behind the sheet that
        /// reports it rather than in front of the one frame that can't.
        /// </para>
        /// </summary>
        /// <param name="rebaseStatsWhenCredited">
        /// Re-take the Game Stats baseline once the absence has landed. The
        /// launch path wants this and the others don't: a run's lifetime totals
        /// as loaded — INCLUDING the night it was away — belong to earlier
        /// sessions, while a resume's or an adopted run's absence has always
        /// counted as this one's gathering. That difference predates the
        /// deferral; the flag is here so deferring can't quietly change it,
        /// because the baseline used to be taken after a catch-up that always
        /// finished before Initialise returned.
        /// </param>
        private void CreditAbsence(double awaySeconds, bool rebaseStatsWhenCredited = false)
        {
            // A catch-up still running belongs to this same absence chain (a
            // resume landing on a launch's). Finish it before starting another,
            // or its gains would be diffed against a moved baseline.
            FinishCatchUp();

            // Back-date the Wheel's cursor to leave-time: the catch-up walks
            // it forward per sub-step, so a tide that opened or closed while
            // the player was away pays exactly the seconds it was open. The
            // capped remainder of a very long absence is never ticked, and so
            // never paid — same as every other system.
            StampWheelClock(awaySeconds);

            var catchUp = OfflineCatchUp.Begin(State, Data, awaySeconds);
            if (catchUp.Summary.creditedSeconds < OfflineCatchUp.DeferThresholdSeconds)
            {
                catchUp.RunToCompletion();
                AnnounceCatchUp(catchUp);
                return;
            }

            _catchUp = catchUp;
            _rebaseStatsAfterCatchUp = rebaseStatsWhenCredited;
        }

        /// <summary>
        /// True while a long absence is still being credited. The sheet pump
        /// holds everything behind this — a welcome-back sheet quoting a figure
        /// the run is still adding to would be wrong twice over, and the
        /// "Double it" offer would double a haul that hadn't finished landing.
        /// </summary>
        public bool CatchingUp => _catchUp != null;

        /// <summary>How far a deferred catch-up has got, 0..1; 1 when none is running.</summary>
        public double CatchUpProgress => _catchUp?.Progress ?? 1.0;

        /// <summary>
        /// Credit whatever is left of a deferred absence at once and report it.
        /// The cold path — a save, a pause or a quit landing mid-catch-up, where
        /// the alternative is writing a run that has only half woken up.
        /// </summary>
        private void FinishCatchUp()
        {
            if (_catchUp == null)
            {
                return;
            }

            var catchUp = _catchUp;
            _catchUp = null;
            catchUp.RunToCompletion();
            AnnounceCatchUp(catchUp);
        }

        /// <summary>Hand a finished catch-up to the sheet queue, the stats baseline, and telemetry.</summary>
        private void AnnounceCatchUp(OfflineCatchUp catchUp)
        {
            if (_rebaseStatsAfterCatchUp)
            {
                _rebaseStatsAfterCatchUp = false;
                Stats.Rebase(State);
            }

            _announce.OfferOfflineSummary(catchUp.Summary);
            _session.ReportWelcomeBack(catchUp.Summary);
        }

        /// <summary>
        /// Throw away a catch-up in flight — the run it was crediting has been
        /// replaced (an adopted cloud save, a book started again), so its
        /// remaining seconds belong to nothing. Distinct from
        /// <see cref="FinishCatchUp"/>, which is for a run that is still ours.
        /// </summary>
        private void DropCatchUp()
        {
            _catchUp = null;
            _rebaseStatsAfterCatchUp = false;
        }

        private void StartSession()
        {
            _session.Start(Time.realtimeSinceStartup);
        }

        private void EndSession()
        {
            _session.End(Time.realtimeSinceStartup);
        }

        /// <summary>
        /// Write the run to the device now — the autosave, and what anything
        /// that changed the run should call. Cheap: a file, nothing else, unless
        /// the services cadence has come due (see
        /// <see cref="ServicesIntervalSeconds"/>).
        /// <para>
        /// Use <see cref="SaveAndSync"/> instead where the run must reach Play
        /// Games in the same breath — a purchase, a pause, a quit, a swapped
        /// book. Every other caller wants this one.
        /// </para>
        /// </summary>
        public void SaveNow()
        {
            Save(_servicesCountdown <= 0.0);
        }

        /// <summary>
        /// Write the run AND push everything that leaves the device: the cloud
        /// mirror, the boards, the achievements, the Game Stats figures. For the
        /// moments where waiting on a cadence would be wrong — the process may
        /// not be alive for the next one (pause, quit), or the thing that just
        /// happened is exactly what the cloud must not roll back (a purchase, a
        /// reward, an adopted run, a fresh book).
        /// </summary>
        public void SaveAndSync()
        {
            Save(true);
        }

        private void Save(bool syncServices)
        {
            if (State == null)
            {
                return;
            }

            // A half-credited absence must never be what gets written down: the
            // save stamp would move to now while the uncredited remainder still
            // measured from the old one, and those seconds would simply vanish.
            FinishCatchUp();

            _persistence.Save(State, syncServices);
            if (!syncServices)
            {
                return;
            }

            _servicesCountdown = ServicesIntervalSeconds;
            // Post the run's standing. Idempotent — Play Games keeps only the
            // player's best.
            SubmitLeaderboards();
            // The Game Stats totals: hauls and crafts land every tick, so they
            // go as one figure per sync rather than one event each.
            Stats.Flush(State);
            // And the achievements — sign-in alone would leave a milestone
            // crossed mid-session waiting for the next launch to be granted.
            ReassertAchievements();
        }

        /// <summary>Collect (and clear) the load-time offline summary, so the welcome-back sheet shows once.</summary>
        public OfflineSummary TakePendingOfflineSummary()
        {
            return _announce.TakeOfflineSummary();
        }

        /// <summary>
        /// Collect (and clear) the margin note owed for a cloud run taken up
        /// mid-session, or null. The HUD asks on its refresh cadence — the pull
        /// lands whenever sign-in resolves, which is no frame in particular.
        /// </summary>
        public string TakeCloudNotice()
        {
            return _swap?.TakeNotice();
        }

        /// <summary>
        /// Now, by the same clock the save stamps carry — what the inside cover
        /// measures its "ago" against, and what every cooldown in the game is
        /// read against. Through <see cref="ClockGuard"/>, so all of them see
        /// one ratcheted reading: the device clock is the player's to set, and
        /// this is the single place that is made not to matter.
        /// </summary>
        public long NowUnixMs()
        {
            return ClockGuard.Now(State, _clock.NowUnixMs());
        }

        /// <summary>
        /// Stamp the sim clock cursor the Wheel (design §15) reads: the
        /// ratcheted now, back-dated by <paramref name="backdateSeconds"/> for
        /// an offline catch-up so the cursor walks the absence from leave-time
        /// and crosses each tide edge where it truly fell. Also keeps the
        /// device's UTC offset current (windows close at warden-local
        /// midnight) and defaults the hemisphere from locale the first time —
        /// the journal's setting can overrule it later.
        /// </summary>
        private void StampWheelClock(double backdateSeconds)
        {
            State.simNowUnixMs = NowUnixMs() - (long)System.Math.Round(backdateSeconds * 1000.0);
            State.utcOffsetMinutes = (int)System.TimeZoneInfo.Local
                .GetUtcOffset(System.DateTimeOffset.FromUnixTimeMilliseconds(State.simNowUnixMs)).TotalMinutes;
            if (State.hemisphere == Wheel.HemisphereUnset)
            {
                State.hemisphere = HemisphereGuess.FromLocale();
            }
        }

        /// <summary>
        /// True when the device clock reads behind the run's own high water mark
        /// — the cooldowns are standing still until real time catches up. The
        /// inside cover says so rather than leaving a frozen countdown to look
        /// like a bug.
        /// </summary>
        public bool ClockIsBehind => ClockGuard.IsBehind(State, _clock.NowUnixMs());

        /// <summary>Seconds of real time before the clock catches its mark back up, or 0.</summary>
        public double ClockBehindBySeconds => ClockGuard.BehindByMs(State, _clock.NowUnixMs()) / 1000.0;

        /// <summary>
        /// Close this book and open a blank one: the run is wiped from the
        /// device and from Play Games, and a fresh camp takes its place without
        /// a relaunch. The sequence — everything reading the old run being told
        /// in the same breath — is <see cref="RunSwap.StartAgain"/>.
        /// </summary>
        public void StartAgain()
        {
            // Null only before Awake has run, where there is no book to close —
            // the same nothing-to-do the sequence's own State guard answers with.
            _swap?.StartAgain();
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
