using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Game.Telemetry;
using Wildgrove.Sim;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the order the run is swapped out in. Both sequences replace the state
    /// under everything that was reading it, and every way of getting the order
    /// wrong is silent: a baseline taken before the swap posts the gap between
    /// two runs as this session's gathering, a summary dropped after the absence
    /// is credited throws away the new one instead of the old, a catch-up left
    /// running advances a run by an absence it never had.
    /// <para>
    /// <see cref="FakeRunHost.Steps"/> records the scene-side steps in order;
    /// the collaborators that are plain objects (<see cref="Announcements"/>,
    /// <see cref="GameStats"/>) are asserted on their effect instead, because
    /// their effect is what a wrong position would spoil.
    /// </para>
    /// </summary>
    public class RunSwapTests
    {
        private const long Now = 1_700_000_000_000L;

        private GameDataAsset _data;
        private FakeClock _clock;
        private FakeSaveStore _store;
        private FakeCloud _cloud;
        private RecordingTelemetry _telemetry;
        private Announcements _announce;
        private GameStats _stats;
        private RunPersistence _persistence;
        private FakeRunHost _host;
        private GameState _local;
        private RunSwap _sut;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { renownDivisor = 5000, exponent = 0.5, yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
            };

            _clock = new FakeClock { Now = Now };
            _store = new FakeSaveStore();
            _cloud = new FakeCloud();
            _telemetry = new RecordingTelemetry();
            _announce = new Announcements();
            _stats = new GameStats(_cloud, () => true);
            _persistence = new RunPersistence(_data, _store, _cloud, _clock);

            // The run this session launched with, and the state everything else
            // starts in as a result: its kith has been met, its ladder seen, and
            // its lifetime totals are the stats baseline (what GameLoop's
            // Initialise leaves behind).
            _local = Run(playedMs: 60_000L);
            _host = new FakeRunHost(_announce);
            _host.Hold(_local);
            _announce.MarkArrivalsSeen(_local.roster);
            _announce.NoticeKithSlots(1);
            _stats.Rebase(_local);

            _sut = new RunSwap(_host, _persistence, _announce, _stats, _telemetry);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        /// <summary>A run with a familiar in its kith — ids are minted per run, so both runs' first is "fam-1".</summary>
        private GameState Run(long playedMs)
        {
            var state = GameStateFactory.NewGame(_data);
            state.playedMs = playedMs;
            state.roster.Add(new Familiar { id = state.NextFamiliarId(), speciesId = "wood-vole", name = "Pip" });
            return state;
        }

        /// <summary>A further-along cloud run, with a second familiar this device has never met.</summary>
        private RunPersistence.Run Adopted(long playedMs = 900_000L, double awaySeconds = 3600.0)
        {
            var state = Run(playedMs);
            state.roster.Add(new Familiar { id = state.NextFamiliarId(), speciesId = "tawny-owl", name = "Ash" });
            return new RunPersistence.Run
            {
                State = state,
                WasLoaded = true,
                AwaySeconds = awaySeconds,
                SavedAtUnixMs = Now - (long)(awaySeconds * 1000.0),
            };
        }

        [Test]
        public void AdoptFromCloud_TakesTheStepsInOneOrder()
        {
            _sut.AdoptFromCloud(Adopted());

            Assert.That(_host.Steps, Is.EqualTo(new[]
            {
                // The catch-up goes first: it was crediting the book being set
                // aside, and a slice after the swap would advance the adopted run
                // by an absence it never had.
                FakeRunHost.CatchUpDropped,
                FakeRunHost.RunReplaced,
                // Then the absence since the cloud save, folded entitlements the
                // adopted save may predate, and only then the write that
                // converges both slots on all of it.
                FakeRunHost.AbsenceCredited,
                FakeRunHost.EntitlementsSynced,
                FakeRunHost.Saved,
            }));
        }

        [Test]
        public void AdoptFromCloud_PutsTheAdoptedRunInHand()
        {
            var adopted = Adopted();

            _sut.AdoptFromCloud(adopted);

            Assert.That(_host.State, Is.SameAs(adopted.State));
        }

        [Test]
        public void AdoptFromCloud_CreditsTheAbsenceSinceTheCloudSave()
        {
            _sut.AdoptFromCloud(Adopted(awaySeconds: 7200.0));

            Assert.That(_host.CreditedAwaySeconds, Is.EqualTo(7200.0));
        }

        [Test]
        public void AdoptFromCloud_TakesTheStatsBaselineFromTheAdoptedRun()
        {
            // The adopted run arrives with a night's gathering this session had no
            // part in. Rebased on it — after the swap, not before — the next flush
            // has nothing to report; missed, it posts the gap between two runs as
            // though this session had gathered it.
            var adopted = Adopted();
            adopted.State.lifetimeGathered["berries"] = 500.0;

            _sut.AdoptFromCloud(adopted);
            _stats.Flush(_host.State);

            Assert.That(_cloud.StatNames, Does.Not.Contain(StatEventNames.HaulArrived),
                "a lifetime total gathered in earlier sessions must not post as this one's");
        }

        [Test]
        public void AdoptFromCloud_TakesTheAdoptedKithAsAlreadyMet()
        {
            // A cloud kith has been met and named on the device it came from, so
            // none of it queues for the naming sheet here.
            var adopted = Adopted();

            _sut.AdoptFromCloud(adopted);
            _announce.NoticeArrivals(_host.State.roster);

            Assert.That(_announce.PeekArrival(), Is.Null);
        }

        [Test]
        public void AdoptFromCloud_ReSeedsTheKithLadderSilently()
        {
            // The adopted run's ladder was earned in an earlier session on another
            // device — it arrived rather than widened, and is not a celebration.
            _sut.AdoptFromCloud(Adopted());

            Assert.That(_announce.NoticeKithSlots(4), Is.Zero);
        }

        [Test]
        public void AdoptFromCloud_KeepsTheAdoptedRunsSummaryAndNotTheDiscardedRunsOne()
        {
            // The order the two summaries are handled in is invisible from the
            // outside and wrong either way round: drop after credit throws away
            // the summary just queued, and no drop at all leaves the old one
            // holding priority (Announcements gives an unshown summary priority
            // over a later offer, on purpose).
            _announce.OfferOfflineSummary(new OfflineSummary { realSeconds = 120.0, creditedSeconds = 120.0 });

            _sut.AdoptFromCloud(Adopted(awaySeconds: 3600.0));

            Assert.That(_announce.PendingOfflineSummary, Is.Not.Null,
                "the absence since the cloud save is still owed a welcome-back sheet");
            Assert.That(_announce.PendingOfflineSummary.creditedSeconds, Is.EqualTo(3600.0),
                "the sheet must quote the adopted run's absence, not the discarded run's");
        }

        [Test]
        public void AdoptFromCloud_SaysSoInTheMarginOnce()
        {
            _sut.AdoptFromCloud(Adopted());

            Assert.That(_sut.TakeNotice(), Is.Not.Empty, "the run changed under the player's hands");
            Assert.That(_sut.TakeNotice(), Is.Null, "and the journal says it the once");
        }

        [Test]
        public void AdoptFromCloud_LogsTheAdoptedRunsPlayTime()
        {
            var adopted = Adopted(playedMs: 900_000L, awaySeconds: 3600.0);

            _sut.AdoptFromCloud(adopted);

            Assert.That(_telemetry.Value("cloud_save_adopted", "played_ms"), Is.EqualTo(900_000L),
                "read after the swap — the discarded run's 60,000 would say nothing about what was adopted");
            Assert.That(_telemetry.Value("cloud_save_adopted", "saved_at_ms"), Is.EqualTo(adopted.SavedAtUnixMs));
        }

        [Test]
        public void AdoptFromCloud_WithNoRunInHand_DoesNothing()
        {
            // The pull outlived the run it was for (a teardown mid-flight). There
            // is nothing to adopt into, and a save here would write a run nothing
            // is holding.
            _host.Hold(null);

            _sut.AdoptFromCloud(Adopted());

            Assert.That(_host.Steps, Is.Empty);
            Assert.That(_sut.TakeNotice(), Is.Null);
            Assert.That(_telemetry.Names, Is.Empty);
        }

        [Test]
        public void AdoptFromCloud_WithNothingToAdopt_DoesNothing()
        {
            _sut.AdoptFromCloud(null);

            Assert.That(_host.Steps, Is.Empty);
            Assert.That(_host.State, Is.SameAs(_local));
        }

        [Test]
        public void StartAgain_TakesTheStepsInOneOrder()
        {
            _sut.StartAgain();

            Assert.That(_host.Steps, Is.EqualTo(new[]
            {
                FakeRunHost.CatchUpDropped,
                FakeRunHost.RunReplaced,
                // No save step: StartOver writes both slots itself, before
                // anything can beat a blank book back (see RunPersistence).
                FakeRunHost.EntitlementsSynced,
            }));
        }

        [Test]
        public void StartAgain_PutsABlankBookInHandAndWipesBothSlots()
        {
            _store.Saved = SaveCodec.Capture(_local, Now - 30_000L);

            _sut.StartAgain();

            Assert.That(_host.State, Is.Not.SameAs(_local));
            Assert.That(_host.State.playedMs, Is.Zero);
            Assert.That(_store.Saved.playedMs, Is.Zero, "the device slot still held the closed book");
            Assert.That(_cloud.SavedPlayedMs, Is.Zero, "and so did Play Games");
        }

        [Test]
        public void StartAgain_ForgetsWhoTheClosedBookHadMet()
        {
            // Familiar ids are minted per run, so the fresh seed kith's first
            // arrival is "fam-1" exactly like the closed book's — and would be
            // taken for someone already introduced, never asked for its name.
            _sut.StartAgain();
            _host.State.roster.Add(new Familiar { id = _host.State.NextFamiliarId(), speciesId = "wood-vole" });
            _announce.NoticeArrivals(_host.State.roster);

            Assert.That(_announce.PeekArrival(), Is.Not.Null);
        }

        [Test]
        public void StartAgain_DropsWhatTheClosedBookWasOwed()
        {
            _announce.OfferOfflineSummary(new OfflineSummary { realSeconds = 120.0, creditedSeconds = 120.0 });

            _sut.StartAgain();

            Assert.That(_announce.PendingOfflineSummary, Is.Null,
                "the haul that absence credited belongs to a book no longer in hand");
        }

        [Test]
        public void StartAgain_WithNoRunInHand_DoesNothing()
        {
            _host.Hold(null);

            _sut.StartAgain();

            Assert.That(_host.Steps, Is.Empty);
            Assert.That(_store.Saved, Is.Null, "there was no book to close, so no slot to overwrite");
            Assert.That(_telemetry.Names, Is.Empty);
        }

        /// <summary>
        /// The scene side of the run, recording the order it was asked for things.
        /// <see cref="CreditAbsence"/> also queues the summary a credited absence
        /// owes, as GameLoop's catch-up does — without that, the drop-then-credit
        /// order of the two summaries would be invisible here.
        /// </summary>
        private sealed class FakeRunHost : IRunHost
        {
            public const string RunReplaced = "run-replaced";
            public const string CatchUpDropped = "catch-up-dropped";
            public const string AbsenceCredited = "absence-credited";
            public const string EntitlementsSynced = "entitlements-synced";
            public const string Saved = "saved-and-synced";

            private readonly Announcements _announce;
            private GameState _state;

            public FakeRunHost(Announcements announce)
            {
                _announce = announce;
            }

            /// <summary>Every step the swap took, in order.</summary>
            public readonly List<string> Steps = new List<string>();

            /// <summary>The absence the swap asked to be credited, or 0.</summary>
            public double CreditedAwaySeconds { get; private set; }

            /// <summary>Put a run in hand without recording a step — the one the session launched with.</summary>
            public void Hold(GameState state)
            {
                _state = state;
            }

            public GameState State
            {
                get { return _state; }
                set
                {
                    _state = value;
                    Steps.Add(RunReplaced);
                }
            }

            public void DropCatchUp()
            {
                Steps.Add(CatchUpDropped);
            }

            public void CreditAbsence(double awaySeconds)
            {
                Steps.Add(AbsenceCredited);
                CreditedAwaySeconds = awaySeconds;
                _announce.OfferOfflineSummary(new OfflineSummary
                {
                    realSeconds = awaySeconds,
                    creditedSeconds = awaySeconds,
                });
            }

            public void SyncStoreEntitlements()
            {
                Steps.Add(EntitlementsSynced);
            }

            public void SaveAndSync()
            {
                Steps.Add(Saved);
            }
        }

        private sealed class FakeClock : IClock
        {
            public long Now;

            public long NowUnixMs()
            {
                return Now;
            }
        }

        private sealed class FakeSaveStore : ISaveStore
        {
            public SaveData Saved;

            public bool TryLoad(out SaveData save)
            {
                save = Saved;
                return Saved != null;
            }

            public void Write(SaveData save)
            {
                Saved = save;
            }
        }

        /// <summary>An <see cref="IGameServices"/> that is a cloud slot and a stats sink — the rest is unused here.</summary>
        private sealed class FakeCloud : IGameServices
        {
            public string SavedJson;
            public long SavedPlayedMs = -1L;

            /// <summary>The Game Stats events that reached Play, in order.</summary>
            public readonly List<string> StatNames = new List<string>();

            public bool IsSignedIn => true;

            public void LoadCloud(Action<string> onLoaded)
            {
                onLoaded?.Invoke(SavedJson);
            }

            public void SaveCloud(string data, long playedMs, Action<bool> onComplete = null)
            {
                SavedJson = data;
                SavedPlayedMs = playedMs;
                onComplete?.Invoke(true);
            }

            public void SignIn(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
            }

            public void SignInInteractive(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
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

            public void RecordStat(string eventName, params (string key, object value)[] properties)
            {
                StatNames.Add(eventName);
            }

            public void FlushStats()
            {
            }
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
