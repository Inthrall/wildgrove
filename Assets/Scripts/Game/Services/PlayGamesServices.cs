#if UNITY_ANDROID
using System;
using System.Text;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The real <see cref="IGameServices"/>, backed by Play Games Services v2:
    /// authentication, achievement unlocks, and cloud save via Snapshots.
    /// Android-only — GPGS's <c>PlayGamesPlatform</c> is itself compiled under
    /// <c>UNITY_ANDROID</c>, so this file is guarded to match (the editor and any
    /// non-Android target use <see cref="StubGameServices"/>, selected in GameLoop).
    /// </summary>
    public sealed class PlayGamesServices : IGameServices
    {
        private const string SnapshotName = "wildgrove_save";

        public bool IsSignedIn { get; private set; }

        public void SignIn(Action<bool> onComplete = null)
        {
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                IsSignedIn = status == SignInStatus.Success;
                onComplete?.Invoke(IsSignedIn);
            });
        }

        public void UnlockAchievement(string achievementId)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(achievementId))
            {
                return;
            }

            PlayGamesPlatform.Instance.ReportProgress(achievementId, 100.0, _ => { });
        }

        public void LoadCloud(Action<string> onLoaded)
        {
            if (!IsSignedIn)
            {
                onLoaded?.Invoke(null);
                return;
            }

            OpenSnapshot((status, game) =>
            {
                if (status != SavedGameRequestStatus.Success || game == null)
                {
                    onLoaded?.Invoke(null);
                    return;
                }

                PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(game, (readStatus, bytes) =>
                {
                    onLoaded?.Invoke(readStatus == SavedGameRequestStatus.Success && bytes != null && bytes.Length > 0
                        ? Encoding.UTF8.GetString(bytes)
                        : null);
                });
            });
        }

        public void SaveCloud(string data, long playedMs, Action onComplete = null)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(data))
            {
                onComplete?.Invoke();
                return;
            }

            OpenSnapshot((status, game) =>
            {
                if (status != SavedGameRequestStatus.Success || game == null)
                {
                    onComplete?.Invoke();
                    return;
                }

                var bytes = Encoding.UTF8.GetBytes(data);
                // Record accumulated play time as played-time so OpenSnapshot's
                // UseLongestPlaytime resolves a cross-device conflict to the
                // further-along save — a monotonic value independent of the device
                // clock — rather than comparing zero to zero and picking arbitrarily.
                var update = new SavedGameMetadataUpdate.Builder()
                    .WithUpdatedPlayedTime(TimeSpan.FromMilliseconds(playedMs))
                    .Build();
                PlayGamesPlatform.Instance.SavedGame.CommitUpdate(game, update, bytes, (_, __) => onComplete?.Invoke());
            });
        }

        private static void OpenSnapshot(Action<SavedGameRequestStatus, ISavedGameMetadata> callback)
        {
            PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(
                SnapshotName,
                DataSource.ReadCacheOrNetwork,
                ConflictResolutionStrategy.UseLongestPlaytime,
                callback);
        }
    }
}
#endif
