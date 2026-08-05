using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// An absence being credited a slice at a time. The tick sub-steps at one
    /// second (<see cref="Simulation"/>), so a long absence is tens of
    /// thousands of steps — a twelve-hour away cap is 43,200 of them, and run
    /// in one call on the main thread that is a stall the player watches. This
    /// carries the same work across frames instead: the caller advances it as
    /// much as it can afford each frame and asks whether it has finished.
    /// <para>
    /// The result is identical to running it in one go, and that is a property
    /// rather than an accident: <see cref="Advance"/> only ever hands the tick
    /// WHOLE seconds until the last slice, so the sub-step boundaries fall
    /// exactly where an unsliced call would put them, whatever size the slices
    /// are. Slicing therefore cannot shift a quality roll or a craft batch, and
    /// a slow device does not play a different game from a fast one.
    /// </para>
    /// <para>
    /// Short absences are not worth the machinery — see
    /// <see cref="DeferThresholdSeconds"/>.
    /// </para>
    /// </summary>
    public sealed class OfflineCatchUp
    {
        /// <summary>
        /// Absences shorter than this are credited on the spot. Five minutes of
        /// away time is at most 300 sub-steps — cheaper to run than to hold, and
        /// short enough that the welcome-back sheet it feeds isn't waiting on
        /// anything. Above it the work is worth spreading, and there is a sheet
        /// to spread it behind (the bar the sheet itself appears at is
        /// SessionLog.WelcomeBackMinSeconds, deliberately lower: a two-minute
        /// absence is still greeted, just credited in one breath).
        /// </summary>
        public const double DeferThresholdSeconds = 300.0;

        private readonly GameState _state;
        private readonly GameDataAsset _data;
        private readonly Dictionary<string, BigDouble> _before;
        private readonly OfflineSummary _summary;
        private readonly double _totalSimSeconds;

        private double _remainingSimSeconds;

        private OfflineCatchUp(GameState state, GameDataAsset data, OfflineSummary summary, double simSeconds)
        {
            _state = state;
            _data = data;
            _summary = summary;
            _totalSimSeconds = simSeconds;
            _remainingSimSeconds = simSeconds;
            _before = state != null
                ? Simulation.SnapshotHoldings(state)
                : new Dictionary<string, BigDouble>();
        }

        /// <summary>
        /// Begin crediting <paramref name="realElapsedSeconds"/> of absence: the
        /// away cap and the offline rate are resolved here, so the work left to
        /// do is fixed at the start and can't move under a slice. Never null —
        /// an absence with nothing to credit comes back already complete, so
        /// callers have one shape to handle.
        /// </summary>
        public static OfflineCatchUp Begin(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            var summary = new OfflineSummary
            {
                realSeconds = System.Math.Max(0.0, realElapsedSeconds),
            };

            if (state == null || data == null || realElapsedSeconds <= 0.0)
            {
                return new OfflineCatchUp(state, data, summary, 0.0);
            }

            summary.creditedSeconds = Simulation.CreditedSeconds(state, data, realElapsedSeconds);
            return new OfflineCatchUp(state, data, summary, summary.creditedSeconds * Simulation.OfflineRateMultiplier(data));
        }

        /// <summary>True once every second of the absence has been credited and <see cref="Summary"/> is final.</summary>
        public bool IsComplete => _remainingSimSeconds <= 0.0;

        /// <summary>
        /// What the absence paid out. The gains are empty until
        /// <see cref="IsComplete"/> — a half-credited absence is not a smaller
        /// absence, and reporting it as one would have the welcome-back sheet
        /// quote a figure the run went on to beat.
        /// </summary>
        public OfflineSummary Summary => _summary;

        /// <summary>How far through the absence this is, 0..1 — 1 when there was nothing to credit.</summary>
        public double Progress =>
            _totalSimSeconds > 0.0 ? 1.0 - _remainingSimSeconds / _totalSimSeconds : 1.0;

        /// <summary>
        /// Credit up to <paramref name="maxSimSeconds"/> more of the absence.
        /// The slice is taken in whole seconds (see the class note) except for
        /// the last, which carries whatever fraction the total ended on — the
        /// same place an unsliced call would put it. A slice smaller than one
        /// second still advances a second: refusing would let a caller with a
        /// tight budget spin forever without finishing.
        /// </summary>
        public void Advance(double maxSimSeconds)
        {
            if (IsComplete || maxSimSeconds <= 0.0)
            {
                return;
            }

            var whole = System.Math.Floor(maxSimSeconds);
            var slice = System.Math.Min(_remainingSimSeconds, whole > 0.0 ? whole : 1.0);
            Simulation.Advance(_state, _data, slice);
            _remainingSimSeconds -= slice;

            if (IsComplete)
            {
                Settle();
            }
        }

        /// <summary>Credit the whole remaining absence now — the cold path, and what a short absence takes.</summary>
        public void RunToCompletion()
        {
            if (IsComplete)
            {
                return;
            }

            Simulation.Advance(_state, _data, _remainingSimSeconds);
            _remainingSimSeconds = 0.0;
            Settle();
        }

        /// <summary>
        /// Diff the holdings and fill in the summary's gains. Camp stock plus
        /// the quality pools plus what is still pooled at the nodes, so a batch
        /// that landed Decent or Choice offline still reads as a gain of its
        /// resource.
        /// </summary>
        private void Settle()
        {
            if (_state == null)
            {
                return;
            }

            var after = Simulation.SnapshotHoldings(_state);
            foreach (var pair in after)
            {
                _before.TryGetValue(pair.Key, out var had);
                var gained = pair.Value - had;
                if (gained > BigDouble.Zero)
                {
                    _summary.gains[pair.Key] = gained;
                }
            }
        }
    }
}
