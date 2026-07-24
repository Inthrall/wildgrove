using System;
using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the leaderboard submit (GameLoop's sign-in callback and each save):
    /// Renown is posted log-scaled so a BigDouble that outgrows a long can't
    /// overflow the score, and the scaling stays monotonic in Renown.
    /// </summary>
    public class LeaderboardsTests
    {
        private GameDataAsset _data;
        private RecordingGameServices _services;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _services = new RecordingGameServices();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void SubmitAll_PostsLogScaledRenownToTheRenownBoard()
        {
            var state = new GameState { renown = new BigDouble(1_000_000_000_000_000_000.0) }; // 1e18

            Leaderboards.SubmitAll(_services, state, _data);

            // log10(1e18) * 1e6 = 18_000_000.
            Assert.That(_services.Submitted, Does.ContainKey(LeaderboardIds.Renown));
            Assert.That(_services.Submitted[LeaderboardIds.Renown], Is.EqualTo(18_000_000));
        }

        [Test]
        public void RenownScore_AtZeroRenown_IsZeroNotNegativeInfinity()
        {
            // Guarded by Max(renown, One): log10(0) would be -infinity and cast to
            // long.MinValue, which would rank a fresh run above everyone.
            var state = new GameState { renown = BigDouble.Zero };

            Assert.That(Leaderboards.RenownScore(state), Is.EqualTo(0));
        }

        [Test]
        public void RenownScore_IsMonotonic_HigherRenownScoresHigher()
        {
            var lower = new GameState { renown = new BigDouble(1e30) };
            var higher = new GameState { renown = new BigDouble(1e60) };

            Assert.That(Leaderboards.RenownScore(higher), Is.GreaterThan(Leaderboards.RenownScore(lower)));
        }

        [Test]
        public void RenownScore_AtAstronomicalRenown_DoesNotOverflow()
        {
            // 1e300 Renown — far past long.MaxValue raw, but log-scaled it's ~3e8.
            var state = new GameState { renown = new BigDouble(1e300) };

            var score = Leaderboards.RenownScore(state);

            Assert.That(score, Is.EqualTo(300_000_000).Within(1_000));
        }

        /// <summary>An <see cref="IGameServices"/> that records the scores it was asked to submit.</summary>
        private sealed class RecordingGameServices : IGameServices
        {
            public readonly Dictionary<string, long> Submitted = new Dictionary<string, long>();

            public bool IsSignedIn => true;

            public void SignIn(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
            }

            public void UnlockAchievement(string achievementId)
            {
            }

            public void SubmitScore(string leaderboardId, long score)
            {
                Submitted[leaderboardId] = score;
            }

            public void ShowLeaderboard(string leaderboardId)
            {
            }

            public void LoadCloud(Action<string> onLoaded)
            {
                onLoaded?.Invoke(null);
            }

            public void SaveCloud(string data, long playedMs, Action onComplete = null)
            {
                onComplete?.Invoke();
            }
        }
    }
}
