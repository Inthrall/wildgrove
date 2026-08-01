using System;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// One line of a leaderboard, flattened for the journal to draw. Play Games'
    /// own overlay is unreachable on a modern target SDK — its bridge extends the
    /// framework <c>android.app.Fragment</c> — so the Standing is read through
    /// <see cref="IGameServices.LoadLeaderboard"/> and set in the journal's own
    /// hand instead.
    /// </summary>
    public struct LeaderboardEntry
    {
        public int rank;
        public string name;
        public long score;

        /// <summary>True for the signed-in player's own line, so it can be marked.</summary>
        public bool isPlayer;
    }

    /// <summary>
    /// The Play Games Services seam — sign-in, achievements, and cloud save
    /// (Snapshots). Game code drives all three through this; the backend is
    /// swappable — <see cref="StubGameServices"/> until the Play Games plugin
    /// implementation exists, then the real service implements it without
    /// touching the call sites.
    /// </summary>
    public interface IGameServices
    {
        /// <summary>True once the player is authenticated with Play Games.</summary>
        bool IsSignedIn { get; }

        /// <summary>
        /// Attempt sign-in (silent, falling back to interactive on first run).
        /// <paramref name="onComplete"/> receives the resulting signed-in state.
        /// </summary>
        void SignIn(Action<bool> onComplete = null);

        /// <summary>
        /// Ask for sign-in on the player's own initiative, showing Play Games'
        /// UI. Distinct from <see cref="SignIn"/>: that one runs at launch and
        /// must stay silent, and once it has failed it keeps failing silently —
        /// so a sign-in-gated button needs this to have anything to offer.
        /// </summary>
        void SignInInteractive(Action<bool> onComplete = null);

        /// <summary>Unlock an achievement by its encoded ID (see <see cref="AchievementIds"/>). No-op if already unlocked.</summary>
        void UnlockAchievement(string achievementId);

        /// <summary>
        /// Report progress on an incremental achievement as a step count, which
        /// Play draws as a bar and unlocks on its own once the steps are met.
        /// <para>
        /// Deliberately set-to-at-least rather than increment-by. <see
        /// cref="Achievements.Reassert"/> re-derives every achievement from
        /// whole state on a cadence, so the same progress is reported over and
        /// over; an increment would add it every time and race to the unlock.
        /// Setting an absolute figure is idempotent, and Play keeps the highest
        /// it has seen — which also makes a per-run high-water mark safe to
        /// report after a Migration has reset the run's own counter.
        /// </para>
        /// </summary>
        void SetAchievementSteps(string achievementId, int steps);

        /// <summary>
        /// Submit a score to a leaderboard by its encoded ID (see <see cref="LeaderboardIds"/>).
        /// No-op when signed out. Play Games keeps only the player's best, so
        /// re-submitting an equal-or-lower score is harmless.
        /// </summary>
        void SubmitScore(string leaderboardId, long score);

        /// <summary>
        /// Open the native Play Games leaderboard overlay for the given ID.
        /// No-op when signed out. <paramref name="onClosed"/> reports whether the
        /// overlay actually opened — GPGS's one-argument overload passes a null
        /// callback, so a board that refuses to open (unpublished, Play Services
        /// out of date, another overlay already up) fails silently and the tap
        /// vanishes. Anything sign-in-gated needs to be able to say why not.
        /// </summary>
        void ShowLeaderboard(string leaderboardId, Action<bool> onClosed = null);

        /// <summary>
        /// Read the top of a leaderboard so the journal can draw it itself.
        /// <paramref name="onLoaded"/> receives null when the board can't be
        /// read (signed out, or Play declined). This is the path that actually
        /// works: it goes through the leaderboards client, not the fragment
        /// bridge that <see cref="ShowLeaderboard"/> is stranded on.
        /// </summary>
        void LoadLeaderboard(string leaderboardId, int rowCount, Action<LeaderboardEntry[]> onLoaded);

        /// <summary>
        /// Record one Game Stats player event (a Level Up guideline: five
        /// repetitive stats, one of them competitive, plus a progression stat).
        /// The event name and every property key must already be declared in the
        /// Play Console CSV schema — see <see cref="GameStats"/>, which owns the
        /// vocabulary, and <c>store/play-games/gamestats/</c>, which holds the
        /// CSVs uploaded to the console. Undeclared events are dropped by Play.
        /// No-op when signed out: the stat belongs to a gamer profile.
        /// </summary>
        void RecordStat(string eventName, params (string key, object value)[] properties);

        /// <summary>
        /// Ask for the recorded events to be uploaded now. Play batches on its
        /// own schedule, so this is a nudge at a natural boundary (the save
        /// cadence), not a delivery guarantee.
        /// </summary>
        void FlushStats();

        /// <summary>Read the cloud save blob (null when none exists or signed out).</summary>
        void LoadCloud(Action<string> onLoaded);

        /// <summary>
        /// Write the cloud save blob (no-op when signed out). <paramref name="playedMs"/>
        /// is the run's accumulated play time, recorded as the snapshot's
        /// played-time so the Snapshots layer's UseLongestPlaytime conflict
        /// resolution picks the further-along save — matching GameLoop's own
        /// most-played-wins reconcile — on a device-clock-independent basis
        /// instead of comparing zeros.
        /// <para>
        /// <paramref name="onComplete"/> reports whether the blob actually
        /// reached the cloud. It has to answer rather than merely fire: the
        /// inside cover tells the player where their run is kept, and a write
        /// that failed silently is exactly the case that has to be sayable.
        /// Signed out counts as false — nothing was written.
        /// </para>
        /// </summary>
        void SaveCloud(string data, long playedMs, Action<bool> onComplete = null);
    }
}
