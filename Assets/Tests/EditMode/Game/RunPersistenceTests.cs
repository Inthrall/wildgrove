using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins which run the player keeps. Cloud adoption is most-played-wins by
    /// accumulated play time — never wall-clock, because a device with a wrong
    /// clock is a real case — and the bar it must beat moves with every save, or
    /// a stale device could overwrite progress made minutes ago. An undecodable
    /// cloud blob is left where it is rather than allowed to disturb the run in
    /// hand.
    /// </summary>
    public class RunPersistenceTests
    {
        private const long Now = 1_700_000_000_000L;
        private const double Tolerance = 1e-6;

        private GameDataAsset _data;
        private FakeClock _clock;
        private FakeSaveStore _store;
        private FakeCloud _cloud;
        private RunPersistence _sut;

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
            _sut = new RunPersistence(_data, _store, _cloud, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        /// <summary>A save of a run that has been played for <paramref name="playedMs"/>, written at <paramref name="savedAtUnixMs"/>.</summary>
        private SaveData Saved(long playedMs, long savedAtUnixMs)
        {
            var state = GameStateFactory.NewGame(_data);
            state.playedMs = playedMs;
            return SaveCodec.Capture(state, savedAtUnixMs);
        }

        [Test]
        public void Load_WithNothingSaved_StartsAFreshRun()
        {
            var run = _sut.Load();

            Assert.That(run.WasLoaded, Is.False);
            Assert.That(run.AwaySeconds, Is.Zero);
            Assert.That(run.State, Is.Not.Null);
            // A fresh run has no play time, so any cloud save beats it —
            // that is what makes a reinstall recover.
            Assert.That(_sut.LoadedPlayedMs, Is.Zero);
        }

        [Test]
        public void Load_WithASavedRun_CreditsTheTimeSinceItWasWritten()
        {
            _store.Saved = Saved(playedMs: 60_000L, savedAtUnixMs: Now - 7_200_000L);

            var run = _sut.Load();

            Assert.That(run.WasLoaded, Is.True);
            Assert.That(run.AwaySeconds, Is.EqualTo(7200.0).Within(Tolerance));
            Assert.That(_sut.LoadedPlayedMs, Is.EqualTo(60_000L));
            Assert.That(_sut.LastSavedUnixMs, Is.EqualTo(Now));
        }

        [Test]
        public void Save_WritesTheSlotAndMirrorsItToTheCloud()
        {
            var run = _sut.Load();
            run.State.playedMs = 90_000L;
            _clock.Now = Now + 5_000L;

            _sut.Save(run.State);

            Assert.That(_store.Saved, Is.Not.Null);
            Assert.That(_store.Saved.savedAtUnixMs, Is.EqualTo(Now + 5_000L));
            Assert.That(_cloud.SavedJson, Is.Not.Null);
            Assert.That(_cloud.SavedPlayedMs, Is.EqualTo(90_000L));
            Assert.That(_sut.LastSavedUnixMs, Is.EqualTo(Now + 5_000L));
        }

        [Test]
        public void Reconcile_WhenTheCloudRunIsFurtherAlong_HandsItOverToAdopt()
        {
            _store.Saved = Saved(playedMs: 10_000L, savedAtUnixMs: Now);
            _sut.Load();
            _cloud.Stored = SaveCodec.ToJson(Saved(playedMs: 500_000L, savedAtUnixMs: Now - 3_600_000L));

            RunPersistence.Run adopted = null;
            _sut.Reconcile(run => adopted = run);

            Assert.That(adopted, Is.Not.Null);
            Assert.That(adopted.State.playedMs, Is.EqualTo(500_000L));
            Assert.That(adopted.AwaySeconds, Is.EqualTo(3600.0).Within(Tolerance));
            Assert.That(adopted.SavedAtUnixMs, Is.EqualTo(Now - 3_600_000L));
            // The adopted run is now what a later cloud pull has to beat.
            Assert.That(_sut.LoadedPlayedMs, Is.EqualTo(500_000L));
        }

        [Test]
        public void Reconcile_WithNoCloudSave_KeepsTheRunInHand()
        {
            _sut.Load();

            var adopted = false;
            _sut.Reconcile(_ => adopted = true);

            Assert.That(adopted, Is.False);
        }

        [Test]
        public void Reconcile_WhenTheCloudRunIsNoFurtherAlong_KeepsTheRunInHand()
        {
            // Equal play time is a tie, and a tie keeps the local run: the next
            // save pushes it back up rather than pulling a copy down.
            _store.Saved = Saved(playedMs: 500_000L, savedAtUnixMs: Now);
            _sut.Load();
            _cloud.Stored = SaveCodec.ToJson(Saved(playedMs: 500_000L, savedAtUnixMs: Now - 60_000L));

            var adopted = false;
            _sut.Reconcile(_ => adopted = true);

            Assert.That(adopted, Is.False);
        }

        [Test]
        public void Reconcile_AfterASave_ComparesAgainstTheSavedRunNotTheLoadedOne()
        {
            // The reinstall-recovery case turned against itself: a fresh run has
            // no play time, so without the save advancing the bar, a stale cloud
            // save would beat progress made in this very session.
            var run = _sut.Load();
            run.State.playedMs = 400_000L;
            _sut.Save(run.State);
            _cloud.Stored = SaveCodec.ToJson(Saved(playedMs: 300_000L, savedAtUnixMs: Now - 60_000L));

            var adopted = false;
            _sut.Reconcile(_ => adopted = true);

            Assert.That(adopted, Is.False);
        }

        [Test]
        public void Reconcile_WithACorruptCloudSave_KeepsTheRunInHandQuietly()
        {
            // SaveCodec.FromJson answers null for text that isn't a save rather
            // than throwing, so it's the null guard that protects the run here —
            // the decode-failed log is for a blob that parses and then breaks
            // deeper in (a converter, say). Either way the local run plays on and
            // the cloud slot is left alone for the next save to overwrite.
            _sut.Load();
            _cloud.Stored = "{ not a save";

            var adopted = false;
            _sut.Reconcile(_ => adopted = true);

            Assert.That(adopted, Is.False);
        }

        [Test]
        public void Reconcile_WithACloudSaveFromAFutureBuild_KeepsTheRunInHand()
        {
            // Never guess at a shape this build doesn't know (SaveCodec.TryMigrate
            // refuses it) — and never overwrite it either.
            _sut.Load();
            var future = Saved(playedMs: 900_000L, savedAtUnixMs: Now - 60_000L);
            future.version = SaveCodec.CurrentVersion + 1;
            _cloud.Stored = SaveCodec.ToJson(future);

            var adopted = false;
            _sut.Reconcile(_ => adopted = true);

            Assert.That(adopted, Is.False);
        }

        [Test]
        public void Save_WhenTheCloudTakesIt_ReportsNoFailure()
        {
            var run = _sut.Load();

            _sut.Save(run.State);

            Assert.That(_sut.LastCloudWriteFailed, Is.False);
        }

        [Test]
        public void Save_WhenTheCloudRefusesIt_SaysSoRatherThanNothing()
        {
            // The whole point of the inside cover's cloud line: a run that is
            // only on this device, while the player believes Play Games holds
            // it, used to be indistinguishable from one safely copied up.
            var run = _sut.Load();
            _cloud.RefuseWrites = true;

            _sut.Save(run.State);

            Assert.That(_sut.LastCloudWriteFailed, Is.True);
        }

        [Test]
        public void Reconcile_AfterAdopting_RemembersWhereTheRunCameFrom()
        {
            _sut.Load();
            _cloud.Stored = SaveCodec.ToJson(Saved(playedMs: 900_000L, savedAtUnixMs: Now - 60_000L));

            _sut.Reconcile(_ => { });

            Assert.That(_sut.AdoptedFromCloud, Is.True);
        }

        [Test]
        public void StartOver_WritesTheFreshRunDownBeforeAnythingCanBeatIt()
        {
            // A fresh run has 0 played time, which every other copy in existence
            // beats — so it has to reach both slots immediately, not at the next
            // autosave.
            _store.Saved = Saved(playedMs: 600_000L, savedAtUnixMs: Now - 30_000L);
            _sut.Load();

            var run = _sut.StartOver();

            Assert.That(run.State.playedMs, Is.Zero);
            Assert.That(_store.Saved.playedMs, Is.Zero, "the device slot still held the old run");
            Assert.That(_cloud.SavedPlayedMs, Is.Zero, "the cloud still held the old run");
        }

        [Test]
        public void Reconcile_AfterStartingOver_RefusesToHandTheOldRunBack()
        {
            // Sign-in resolves seconds after launch, so a player who starts again
            // in that window would otherwise have the cloud's further-along run
            // adopted straight over the blank book they just asked for.
            _sut.Load();
            _sut.StartOver();
            _cloud.Stored = SaveCodec.ToJson(Saved(playedMs: 900_000L, savedAtUnixMs: Now - 60_000L));

            var adopted = false;
            _sut.Reconcile(_ => adopted = true);

            Assert.That(adopted, Is.False);
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

        /// <summary>An <see cref="IGameServices"/> that is only a cloud slot — the rest is unused here.</summary>
        private sealed class FakeCloud : IGameServices
        {
            public string Stored;
            public string SavedJson;
            public long SavedPlayedMs;

            /// <summary>Set to have the cloud refuse the write, as a signed-out or failed Snapshot commit does.</summary>
            public bool RefuseWrites;

            public bool IsSignedIn => true;

            public void LoadCloud(Action<string> onLoaded)
            {
                onLoaded?.Invoke(Stored);
            }

            public void SaveCloud(string data, long playedMs, Action<bool> onComplete = null)
            {
                if (RefuseWrites)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                SavedJson = data;
                SavedPlayedMs = playedMs;
                Stored = data;
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
            }

            public void FlushStats()
            {
            }
        }
    }
}
