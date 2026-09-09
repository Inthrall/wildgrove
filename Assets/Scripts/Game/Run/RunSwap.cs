using Wildgrove.Game.Services;
using Wildgrove.Game.Telemetry;

namespace Wildgrove.Game
{
    /// <summary>
    /// Putting a different run in the player's hands: a cloud save from another
    /// device that beat the one we launched with, and a book deliberately
    /// started again. Both replace the state under everything that was reading
    /// it, so both are a fixed sequence rather than a rule — the state goes
    /// down, then the stats baseline, the moments owed, the absence, the
    /// entitlements and the save, each step reading what the one before it left.
    /// <para>
    /// The order is the whole risk, and every way of getting it wrong is silent:
    /// a stats baseline taken before the swap posts the gap between two runs'
    /// lifetime totals as this session's gathering; an absence credited before
    /// the old summary is dropped has its own summary dropped instead; a
    /// catch-up left running advances the adopted run by an absence it never
    /// had; entitlements folded in after the save write a run that has just
    /// rolled a paid slot back. None of that throws. So the sequence lives here,
    /// reaching the scene through <see cref="IRunHost"/>, where a test can pin
    /// the order — the same reason <see cref="RunPersistence"/>,
    /// <see cref="Announcements"/> and <see cref="GameStats"/> were lifted out
    /// of the MonoBehaviour before it.
    /// </para>
    /// </summary>
    public sealed class RunSwap
    {
        /// <summary>
        /// The margin note owed when the run changes under the player's hands.
        /// The journal is where the game says something happened without
        /// stopping to say it.
        /// </summary>
        private const string AdoptedNotice = "another device walked further. the book opens there.";

        private readonly IRunHost _host;
        private readonly RunPersistence _persistence;
        private readonly Announcements _announce;
        private readonly GameStats _stats;
        private readonly ITelemetry _telemetry;

        private string _notice;

        public RunSwap(IRunHost host, RunPersistence persistence, Announcements announce, GameStats stats,
            ITelemetry telemetry)
        {
            _host = host;
            _persistence = persistence;
            _announce = announce;
            _stats = stats;
            _telemetry = telemetry;
        }

        /// <summary>
        /// Take on a cloud save that beat the one we launched with (see
        /// <see cref="RunPersistence.Reconcile"/> for which one wins) — the run
        /// swaps under everything that was reading it, so each of those has to be
        /// told in the same breath.
        /// </summary>
        public void AdoptFromCloud(RunPersistence.Run adopted)
        {
            if (_host.State == null || adopted == null)
            {
                // The pull outlived the run it was for (a teardown mid-flight);
                // nothing left to adopt into, and no save will follow.
                return;
            }

            // A catch-up in flight was crediting the run being set aside. Its
            // remaining seconds belong to a book no longer in hand — drop it
            // before the swap, or the next frame's slice would advance the
            // adopted run by an absence it never had.
            _host.DropCatchUp();

            _host.State = adopted.State;
            // Re-baseline the stats on the adopted run, or the gap between two
            // runs' lifetime totals would post as this session's gathering.
            _stats.Rebase(_host.State);
            // A cloud kith has already been met and named, like a local load.
            _announce.MarkArrivalsSeen(_host.State.roster);
            _announce.MarkKithSlotsSeen();
            // The local load's summary credited the state we've just discarded;
            // drop it so the absence since the cloud save credits the adopted run.
            _announce.DropOfflineSummary();
            // Same again for the confirmations owed: the queue standing was
            // built from the discarded run's list, and the adopted run carries
            // its own. It sits with the other announcement resets and ahead of
            // the absence, so every queue the HUD drains has been re-pointed at
            // the run in hand before anything is added to one.
            _host.QueueRewardsOwed();
            _host.CreditAbsence(adopted.AwaySeconds);
            // The adopted save may predate a purchase or a reward this device
            // already owns — re-fold the entitlements rather than let the
            // cloud roll a paid slot or a redeemed pony back.
            _host.SyncStoreEntitlements();
            // Converge the device and cloud on the adopted save now rather than
            // waiting for the autosave interval to write it back down locally.
            _host.SaveAndSync();
            // Say it. The run just changed under the player's hands — silently,
            // until now — and the margin note is where the journal tells them
            // something happened without stopping the game to do it.
            _notice = AdoptedNotice;
            _telemetry.LogEvent("cloud_save_adopted",
                ("saved_at_ms", adopted.SavedAtUnixMs), ("played_ms", _host.State.playedMs));
        }

        /// <summary>
        /// Close this book and open a blank one: the run is wiped from the
        /// device and from Play Games, and a fresh camp takes its place without
        /// a relaunch. Everything that was reading the old run is told in the
        /// same breath, exactly as <see cref="AdoptFromCloud"/> has to.
        /// </summary>
        public void StartAgain()
        {
            if (_host.State == null)
            {
                return;
            }

            // Same reason as AdoptFromCloud: a catch-up in flight was crediting
            // the book being closed.
            _host.DropCatchUp();

            // StartOver writes both slots itself, so the blank book cannot be
            // beaten back by the copy it just replaced.
            var run = _persistence.StartOver();
            _host.State = run.State;
            _stats.Rebase(_host.State);
            // Nothing owed by the old run belongs to this one — including who
            // has been met, so the new seed kith is asked for its names.
            _announce.Forget();
            // A wiped run has no bought slots in it. They were paid for, so they
            // are folded straight back rather than waiting for the next launch
            // to notice — the same reason an adopted cloud save re-syncs.
            _host.SyncStoreEntitlements();
            _telemetry.LogEvent("run_started_over");
        }

        /// <summary>
        /// Collect (and clear) the margin note owed for a cloud run taken up
        /// mid-session, or null. The HUD asks on its refresh cadence — the pull
        /// lands whenever sign-in resolves, which is no frame in particular.
        /// </summary>
        public string TakeNotice()
        {
            var notice = _notice;
            _notice = null;
            return notice;
        }
    }
}
