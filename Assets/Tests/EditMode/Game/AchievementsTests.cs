using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the achievement re-assert (GameLoop's sign-in callback): the milestone
    /// mapping runs against current state, so an achievement earned while signed
    /// out is granted the moment sign-in completes — the one-shot bond celebration
    /// that would otherwise fire it never shows again on a later launch. Any earned
    /// bond satisfies "First kith".
    /// </summary>
    public class AchievementsTests
    {
        private GameDataAsset _data;
        private RecordingGameServices _services;

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
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData { id = "old-friend", displayName = "The Old Friend", costVerdure = 12 },
            };
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "burr", displayName = "Burr", species = "meadow-vole", role = "gatherer",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
            _services = new RecordingGameServices();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void Reassert_WithAnEarnedBond_UnlocksFirstKith()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Unlocked, Does.Contain(AchievementIds.FirstKith));
        }

        [Test]
        public void Reassert_WithNoEarnedBond_UnlocksNothing()
        {
            var state = GameStateFactory.NewGame(_data);

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Unlocked, Is.Empty, "no bond earned — no achievement to re-assert");
        }

        [Test]
        public void Reassert_IsIdempotent_UnlocksFirstKithOncePerCall()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            Achievements.Reassert(_services, state, _data);
            Achievements.Reassert(_services, state, _data);

            // One unlock per call even with two earnable bonds' worth of state —
            // the loop stops at the first (Play Games itself dedupes the repeats).
            Assert.That(_services.Unlocked.FindAll(id => id == AchievementIds.FirstKith).Count, Is.EqualTo(2));
        }

        /// <summary>An <see cref="IGameServices"/> that records the achievements it was asked to unlock.</summary>
        private sealed class RecordingGameServices : IGameServices
        {
            public readonly List<string> Unlocked = new List<string>();

            public bool IsSignedIn => true;

            public void SignIn(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
            }

            public void UnlockAchievement(string achievementId)
            {
                Unlocked.Add(achievementId);
            }

            public void SubmitScore(string leaderboardId, long score)
            {
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
