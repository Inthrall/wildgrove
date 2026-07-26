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

        public void UnlockAchievement(string achievementId)
        {
            Debug.Log("[play-games] stub unlock achievement " + achievementId);
        }

        public void SubmitScore(string leaderboardId, long score)
        {
            Debug.Log("[play-games] stub submit score " + score + " to " + leaderboardId);
        }

        public void ShowLeaderboard(string leaderboardId)
        {
            Debug.Log("[play-games] stub show leaderboard " + leaderboardId);
        }

        public void LoadCloud(Action<string> onLoaded)
        {
            Debug.Log("[play-games] stub cloud load (none)");
            onLoaded?.Invoke(null);
        }

        public void SaveCloud(string data, long playedMs, Action onComplete = null)
        {
            Debug.Log("[play-games] stub cloud save");
            onComplete?.Invoke();
        }
    }
}
