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
    /// Selected on device by GameLoop; the editor keeps
    /// <see cref="StubGameServices"/> (GPGS runs a dummy client there anyway).
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

        public void SaveCloud(string data, Action onComplete = null)
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
                var update = new SavedGameMetadataUpdate.Builder().Build();
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
