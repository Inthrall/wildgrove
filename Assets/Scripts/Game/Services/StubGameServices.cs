using System;
using UnityEngine;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Placeholder <see cref="IGameServices"/> until the Play Games Services
    /// implementation lands. Reports signed-in and logs achievement unlocks; the
    /// cloud-save calls are no-ops (the local SaveFile stays the source of truth
    /// until the real Snapshots impl arrives), so sign-in-gated UI and the
    /// achievement call sites work now.
    /// </summary>
    public sealed class StubGameServices : IGameServices
    {
        public bool IsSignedIn => true;

        public void SignIn(Action<bool> onComplete = null)
        {
            Debug.Log("[play-games] stub sign-in");
            onComplete?.Invoke(true);
        }

        public void SignInInteractive(Action<bool> onComplete = null)
        {
            Debug.Log("[play-games] stub manual sign-in");
            onComplete?.Invoke(true);
        }

        public void UnlockAchievement(string achievementId)
        {
            Debug.Log("[play-games] stub unlock achievement " + achievementId);
        }

        public void SetAchievementSteps(string achievementId, int steps)
        {
            Debug.Log("[play-games] stub achievement " + achievementId + " steps " + steps);
        }

        public void SubmitScore(string leaderboardId, long score)
        {
            Debug.Log("[play-games] stub submit score " + score + " to " + leaderboardId);
        }

        public void ShowLeaderboard(string leaderboardId, Action<bool> onClosed = null)
        {
            Debug.Log("[play-games] stub show leaderboard " + leaderboardId);
            onClosed?.Invoke(true);
        }

        public void LoadLeaderboard(string leaderboardId, int rowCount, Action<LeaderboardEntry[]> onLoaded)
        {
            Debug.Log("[play-games] stub load leaderboard " + leaderboardId);

            // A handful of invented wardens, so the Standing sheet can be laid
            // out and read in the editor without a device.
            var names = new[] { "Ashthorn", "Bramblewick", "Corvid", "Dunnock", "Elderfen" };
            var count = Mathf.Clamp(rowCount, 0, names.Length);
            var entries = new LeaderboardEntry[count];
            for (var i = 0; i < count; i++)
            {
                entries[i] = new LeaderboardEntry
                {
                    rank = i + 1,
                    name = names[i],
                    score = 10_000_000L - (i * 1_337_000L),
                    isPlayer = i == 2,
                };
            }

            onLoaded?.Invoke(entries);
        }

        public void RecordStat(string eventName, params (string key, object value)[] properties)
        {
            var line = "[game-stats] stub record " + eventName;
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    line += " " + property.key + "=" + property.value;
                }
            }

            Debug.Log(line);
        }

        public void FlushStats()
        {
            Debug.Log("[game-stats] stub flush");
        }

        public void LoadCloud(Action<string> onLoaded)
        {
            Debug.Log("[play-games] stub cloud load (none)");
            onLoaded?.Invoke(null);
        }

        public void SaveCloud(string data, long playedMs, Action<bool> onComplete = null)
        {
            Debug.Log("[play-games] stub cloud save");
            // Answers true to stay inside its own fiction: the stub reports
            // signed in, so a false here would have the editor's inside cover
            // claim Play Games refused a copy it was never asked for.
            onComplete?.Invoke(true);
        }
    }
}
