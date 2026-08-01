using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wildgrove.Game.Telemetry;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins what a session tells telemetry. The awkward cases are all boundary
    /// ones: Android pauses before the run is up and quits straight after a
    /// pause, so session_end must neither fire unpaired nor twice — the gate
    /// metric is session length, and a double count would flatter it.
    /// </summary>
    public class SessionLogTests
    {
        private RecordingTelemetry _telemetry;
        private SessionLog _log;

        [SetUp]
        public void SetUp()
        {
            _telemetry = new RecordingTelemetry();
            _log = new SessionLog(_telemetry);
        }

        [Test]
        public void Start_ThenEnd_ReportsTheLengthItLasted()
        {
            _log.Start(10f);
            _log.End(70f);

            Assert.That(_telemetry.Names, Is.EqualTo(new[] { "session_start", "session_end" }));
            Assert.That(_telemetry.Value("session_end", "length_sec"), Is.EqualTo(60.0));
            Assert.That(_log.IsOpen, Is.False);
        }

        [Test]
        public void End_WithoutAStart_ReportsNothing()
        {
            // A pause that lands before the run is up — there is no session to end.
            _log.End(30f);

            Assert.That(_telemetry.Names, Is.Empty);
        }

        [Test]
        public void End_Twice_ReportsOnce()
        {
            // Quit-after-pause: both call End, and only the first is a boundary.
            _log.Start(0f);
            _log.End(30f);
            _log.End(45f);

            Assert.That(_telemetry.Count("session_end"), Is.EqualTo(1));
        }

        [Test]
        public void ReportWelcomeBack_BelowTheBar_StaysQuiet()
        {
            // A recompile or a quick restart isn't a return the player was greeted
            // for, so the metric mustn't count it as one.
            _log.ReportWelcomeBack(new OfflineSummary
            {
                realSeconds = 120.0,
                creditedSeconds = SessionLog.WelcomeBackMinSeconds - 1.0,
            });

            Assert.That(_telemetry.Names, Is.Empty);
        }

        [Test]
        public void ReportWelcomeBack_AtTheBar_ReportsBothClocks()
        {
            _log.ReportWelcomeBack(new OfflineSummary { realSeconds = 36000.4, creditedSeconds = 14400.0 });

            Assert.That(_telemetry.Value("welcome_back", "away_sec"), Is.EqualTo(36000.0));
            Assert.That(_telemetry.Value("welcome_back", "credited_sec"), Is.EqualTo(14400.0));
        }

        [Test]
        public void FlushAmberFinds_ReportsTheBatchAndClearsTheMarks()
        {
            var state = new GameState { amberFoundUnlogged = 3.0, deepAmberFoundUnlogged = 2, deepAmberFound = 5 };

            _log.FlushAmberFinds(state);

            Assert.That(_telemetry.Value("amber_found", "amount"), Is.EqualTo(3.0));
            Assert.That(_telemetry.Value("deep_amber_found", "pieces"), Is.EqualTo(2));
            Assert.That(_telemetry.Value("deep_amber_found", "total_found"), Is.EqualTo(5));
            Assert.That(state.amberFoundUnlogged, Is.Zero);
            Assert.That(state.deepAmberFoundUnlogged, Is.Zero);
        }

        [Test]
        public void FlushAmberFinds_WithNothingFound_StaysQuiet()
        {
            // Runs every tick — silence is the normal case.
            _log.FlushAmberFinds(new GameState());

            Assert.That(_telemetry.Names, Is.Empty);
        }

        private sealed class RecordingTelemetry : ITelemetry
        {
            private readonly List<(string name, (string key, object value)[] parameters)> _events =
                new List<(string, (string, object)[])>();

            public List<string> Names
            {
                get
                {
                    var names = new List<string>();
                    foreach (var logged in _events)
                    {
                        names.Add(logged.name);
                    }

                    return names;
                }
            }

            public int Count(string name)
            {
                var count = 0;
                foreach (var logged in _events)
                {
                    if (logged.name == name)
                    {
                        count++;
                    }
                }

                return count;
            }

            /// <summary>The value logged for a parameter of an event, or null when neither was recorded.</summary>
            public object Value(string name, string key)
            {
                foreach (var logged in _events)
                {
                    if (logged.name != name || logged.parameters == null)
                    {
                        continue;
                    }

                    foreach (var parameter in logged.parameters)
                    {
                        if (parameter.key == key)
                        {
                            return parameter.value;
                        }
                    }
                }

                return null;
            }

            public void LogEvent(string name, params (string key, object value)[] parameters)
            {
                _events.Add((name, parameters));
            }

            public void LogException(Exception exception)
            {
            }

            public void SetCollectionEnabled(bool enabled)
            {
            }

            public void SetConsent(bool granted)
            {
            }
        }
    }
}
