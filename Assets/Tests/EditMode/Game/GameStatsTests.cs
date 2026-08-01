using System;
using System.Collections.Generic;
using System.IO;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the Game Stats wiring (Level Up: five repetitive stats, one of them
    /// competitive, one progression stat). The two continuous stats are the
    /// delicate part — Play aggregates SUM over the events it receives while the
    /// game holds lifetime totals, so what goes out is a delta, and a delta must
    /// never be banked unless it was actually recorded.
    /// </summary>
    public class GameStatsTests
    {
        private RecordingGameServices _services;
        private GameStats _stats;

        [SetUp]
        public void SetUp()
        {
            _services = new RecordingGameServices();
            _stats = new GameStats(_services);
        }

        private static GameState StateWithGathered(double units)
        {
            var state = new GameState();
            state.lifetimeGathered["berries"] = new BigDouble(units);
            return state;
        }

        [Test]
        public void Flush_ReportsWhatWasGatheredSinceTheLastLook_NotTheLifetimeTotal()
        {
            var state = StateWithGathered(1000.0);
            _stats.Rebase(state); // the run as loaded — 1000 was gathered in earlier sessions

            state.lifetimeGathered["berries"] = new BigDouble(1250.0);
            _stats.Flush(state);

            Assert.That(_services.Amount(StatEventNames.HaulArrived), Is.EqualTo(250.0).Within(1e-9));
        }

        [Test]
        public void Flush_OnALoadedRunThatHasGatheredNothingYet_SaysNothing()
        {
            // The bug this pins: without the baseline the first flush of a launch
            // would post a whole lifetime total as one session's gathering.
            var state = StateWithGathered(1_000_000.0);
            _stats.Rebase(state);

            _stats.Flush(state);

            Assert.That(_services.Recorded(StatEventNames.HaulArrived), Is.False);
        }

        [Test]
        public void Flush_TwiceOver_ReportsEachStretchOnce()
        {
            var state = StateWithGathered(0.0);
            _stats.Rebase(state);

            state.lifetimeGathered["berries"] = new BigDouble(40.0);
            _stats.Flush(state);
            state.lifetimeGathered["berries"] = new BigDouble(100.0);
            _stats.Flush(state);

            Assert.That(_services.Amounts(StatEventNames.HaulArrived), Is.EqualTo(new List<double> { 40.0, 60.0 }));
        }

        [Test]
        public void Flush_WhenSignedOut_HoldsTheStretchForTheNextFlush()
        {
            // A signed-out session still gathers. Banking the delta anyway would
            // drop it on the floor, so the baseline only moves on a real record.
            var state = StateWithGathered(0.0);
            _stats.Rebase(state);
            _services.SignedIn = false;

            state.lifetimeGathered["berries"] = new BigDouble(75.0);
            _stats.Flush(state);
            Assert.That(_services.Recorded(StatEventNames.HaulArrived), Is.False, "nothing goes out signed out");

            _services.SignedIn = true;
            _stats.Flush(state);

            Assert.That(_services.Amount(StatEventNames.HaulArrived), Is.EqualTo(75.0).Within(1e-9),
                "the whole stretch, once there is a profile to hang it on");
        }

        [Test]
        public void Rebase_OnAnAdoptedCloudRun_DoesNotPostTheGapBetweenTwoRuns()
        {
            var local = StateWithGathered(500.0);
            _stats.Rebase(local);

            // Reconcile adopts a further-along cloud save: a different run, whose
            // lifetime total is not this session's work.
            var cloud = StateWithGathered(900_000.0);
            _stats.Rebase(cloud);
            _stats.Flush(cloud);

            Assert.That(_services.Recorded(StatEventNames.HaulArrived), Is.False);
        }

        [Test]
        public void Flush_AtAnUnrepresentableTotal_ReportsNothingRatherThanInfinity()
        {
            var state = new GameState();
            state.lifetimeGathered["berries"] = BigDouble.Pow10(400); // past double's reach
            _stats.Rebase(state);
            state.lifetimeGathered["berries"] = BigDouble.Pow10(500);

            _stats.Flush(state);

            Assert.That(_services.Recorded(StatEventNames.HaulArrived), Is.False);
        }

        [Test]
        public void Flush_AlsoAsksPlayToUpload()
        {
            var state = StateWithGathered(0.0);
            _stats.Rebase(state);

            _stats.Flush(state);

            Assert.That(_services.Flushes, Is.EqualTo(1));
        }

        [Test]
        public void ReportProgress_PostsTheWaystonesWalked()
        {
            var state = new GameState();
            state.seenWaystoneZoneIds.Add("sunfield-meadow");
            state.seenWaystoneZoneIds.Add("bramble-hollow");

            _stats.ReportProgress(state);

            Assert.That(_services.Property(StatEventNames.ProgressUpdate, StatPropertyNames.CurrentProgress),
                Is.EqualTo(2));
        }

        [Test]
        public void ReportProgress_UnchangedSinceTheLastPost_StaysQuiet()
        {
            var state = new GameState();
            state.seenWaystoneZoneIds.Add("sunfield-meadow");

            _stats.ReportProgress(state);
            _services.Clear();
            _stats.ReportProgress(state);

            Assert.That(_services.Recorded(StatEventNames.ProgressUpdate), Is.False);
        }

        [Test]
        public void ReportProgress_WhenSignedOut_IsPostedOnceSignedIn()
        {
            // Same rule as the deltas: a dropped post must not count as sent, or
            // the profile keeps a stale progression level all session.
            var state = new GameState();
            state.seenWaystoneZoneIds.Add("sunfield-meadow");
            _services.SignedIn = false;

            _stats.ReportProgress(state);
            _services.SignedIn = true;
            _stats.ReportProgress(state);

            Assert.That(_services.Property(StatEventNames.ProgressUpdate, StatPropertyNames.CurrentProgress),
                Is.EqualTo(1));
        }

        [Test]
        public void RecordWindfall_NamesTheResourceCaught()
        {
            _stats.RecordWindfall("wildflowers");

            Assert.That(_services.Property(StatEventNames.WindfallCaught, StatPropertyNames.Resource),
                Is.EqualTo("wildflowers"));
        }

        [Test]
        public void RecordVerseCompleted_NamesTheVerse()
        {
            _stats.RecordVerseCompleted("verse-1");

            Assert.That(_services.Property(StatEventNames.VerseCompleted, StatPropertyNames.Verse),
                Is.EqualTo("verse-1"));
        }

        [Test]
        public void RecordMigration_CarriesTheFoldNumber()
        {
            _stats.RecordMigration(3);

            Assert.That(_services.Property(StatEventNames.MigrationCompleted, StatPropertyNames.Number),
                Is.EqualTo(3));
        }

        [Test]
        public void TotalGathered_SumsEveryResource()
        {
            var state = new GameState();
            state.lifetimeGathered["berries"] = new BigDouble(10.0);
            state.lifetimeGathered["fibres"] = new BigDouble(2.5);

            Assert.That(GameStats.TotalGathered(state), Is.EqualTo(12.5).Within(1e-9));
        }

        [Test]
        public void EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema()
        {
            // Play silently drops events that aren't in the uploaded CSV schema,
            // so code and schema drifting apart is invisible on a device. This is
            // the only thing that notices.
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "store", "play-games",
                "gamestats", "PlayerGameEvent.csv"));
            Assert.That(File.Exists(path), Is.True, "the declared schema is missing: " + path);

            var declared = File.ReadAllText(path);
            var names = new[]
            {
                StatEventNames.HaulArrived, StatEventNames.CraftCompleted, StatEventNames.WindfallCaught,
                StatEventNames.SpecimenFixed, StatEventNames.VerseCompleted, StatEventNames.MigrationCompleted,
                StatEventNames.ProgressUpdate,
            };

            foreach (var name in names)
            {
                Assert.That(declared, Does.Contain(name), name + " is recorded but not declared in PlayerGameEvent.csv");
            }
        }

        /// <summary>Records what was handed to Play, so the wiring can be read back without a device.</summary>
        private sealed class RecordingGameServices : IGameServices
        {
            private readonly List<(string name, (string key, object value)[] properties)> _events =
                new List<(string, (string, object)[])>();

            public bool SignedIn = true;
            public int Flushes;

            public bool IsSignedIn => SignedIn;

            public void Clear()
            {
                _events.Clear();
                Flushes = 0;
            }

            public bool Recorded(string eventName)
            {
                return _events.Exists(e => e.name == eventName);
            }

            /// <summary>The named property of the first event of that name, or null.</summary>
            public object Property(string eventName, string key)
            {
                foreach (var recorded in _events)
                {
                    if (recorded.name != eventName)
                    {
                        continue;
                    }

                    foreach (var property in recorded.properties)
                    {
                        if (property.key == key)
                        {
                            return property.value;
                        }
                    }
                }

                return null;
            }

            public double Amount(string eventName)
            {
                var value = Property(eventName, StatPropertyNames.Amount);
                return value == null ? 0.0 : (double)value;
            }

            public List<double> Amounts(string eventName)
            {
                var amounts = new List<double>();
                foreach (var recorded in _events)
                {
                    if (recorded.name != eventName)
                    {
                        continue;
                    }

                    foreach (var property in recorded.properties)
                    {
                        if (property.key == StatPropertyNames.Amount)
                        {
                            amounts.Add((double)property.value);
                        }
                    }
                }

                return amounts;
            }

            public void RecordStat(string eventName, params (string key, object value)[] properties)
            {
                _events.Add((eventName, properties));
            }

            public void FlushStats()
            {
                Flushes++;
            }

            public void SignIn(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(SignedIn);
            }

            public void SignInInteractive(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(SignedIn);
            }

            public void UnlockAchievement(string achievementId)
            {
            }

            public void SetAchievementSteps(string achievementId, int steps)
            {
            }

            public void SubmitScore(string leaderboardId, long score)
            {
            }

            public void ShowLeaderboard(string leaderboardId, Action<bool> onClosed = null)
            {
                onClosed?.Invoke(true);
            }

            public void LoadLeaderboard(string leaderboardId, int rowCount, Action<LeaderboardEntry[]> onLoaded)
            {
                onLoaded?.Invoke(new LeaderboardEntry[0]);
            }

            public void LoadCloud(Action<string> onLoaded)
            {
                onLoaded?.Invoke(null);
            }

            public void SaveCloud(string data, long playedMs, Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
            }
        }
    }
}
