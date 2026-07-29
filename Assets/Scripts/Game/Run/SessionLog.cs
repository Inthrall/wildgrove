using Wildgrove.Game.Telemetry;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// What a session tells telemetry: its own boundaries, a welcome-back haul
    /// worth reporting, and the amber the sim found while nobody was looking.
    /// The realtime clock is passed in rather than read here, so the boundary
    /// rules — a pause before the run is up, a quit straight after a pause — can
    /// be tested without a running player loop.
    /// </summary>
    public sealed class SessionLog
    {
        /// <summary>
        /// Below this much credited absence the welcome-back sheet stays quiet
        /// (quick restarts and editor recompiles shouldn't greet the player).
        /// The welcome_back telemetry event uses the same bar so the metric
        /// counts what players actually saw.
        /// </summary>
        public const double WelcomeBackMinSeconds = 60.0;

        private readonly ITelemetry _telemetry;
        private bool _open;
        private float _startedAt;

        public SessionLog(ITelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        /// <summary>True between a <see cref="Start"/> and its <see cref="End"/> — a resume checks this before opening a second one.</summary>
        public bool IsOpen => _open;

        public void Start(float nowRealtime)
        {
            _startedAt = nowRealtime;
            _open = true;
            _telemetry.LogEvent("session_start");
        }

        public void End(float nowRealtime)
        {
            // Guarded so quit-after-pause (or a pause before Awake) can't
            // double-count; the design's gate metric is session length.
            if (!_open)
            {
                return;
            }

            _open = false;
            _telemetry.LogEvent("session_end", ("length_sec", System.Math.Round(nowRealtime - _startedAt)));
        }

        /// <summary>Report an absence the player will actually be greeted about.</summary>
        public void ReportWelcomeBack(OfflineSummary summary)
        {
            if (summary == null || summary.creditedSeconds < WelcomeBackMinSeconds)
            {
                return;
            }

            _telemetry.LogEvent("welcome_back",
                ("away_sec", System.Math.Round(summary.realSeconds)),
                ("credited_sec", System.Math.Round(summary.creditedSeconds)));
        }

        /// <summary>
        /// Report amber the sim has turned up since the last look and clear the
        /// marks. Finds land tick by tick, so the run counts them and this posts
        /// one event per batch rather than one per grain.
        /// </summary>
        public void FlushAmberFinds(GameState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.amberFoundUnlogged > 0.0)
            {
                _telemetry.LogEvent("amber_found", ("amount", state.amberFoundUnlogged));
                state.amberFoundUnlogged = 0.0;
            }

            if (state.deepAmberFoundUnlogged > 0)
            {
                _telemetry.LogEvent("deep_amber_found",
                    ("pieces", state.deepAmberFoundUnlogged),
                    ("total_found", state.deepAmberFound));
                state.deepAmberFoundUnlogged = 0;
            }
        }
    }
}
