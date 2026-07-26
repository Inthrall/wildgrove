#if UNITY_ANDROID
using System;
using System.Text;
using UnityEngine;
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
            PlayGamesPlatform.DebugLogEnabled = Debug.isDebugBuild; // GPGS's own trace into logcat

            try
            {
                PlayGamesPlatform.Instance.Authenticate(status =>
                {
                    IsSignedIn = status == SignInStatus.Success;
                    Debug.Log("[play-games] sign-in: " + status);
                    onComplete?.Invoke(IsSignedIn);
                });
            }
            catch (Exception e)
            {
                // Authenticate attaches its result listeners as AndroidJavaProxy
                // implementations of com.google.android.gms.tasks.On*Listener,
                // resolved by name over JNI. If that name isn't there under R8,
                // the throw happens here, no listener is ever attached, and
                // sign-in hangs forever with nothing logged — so say it out loud
                // and resolve the callback as a failure rather than never.
                Debug.LogWarning("[play-games] sign-in threw: " + e.GetType().Name + " — " + e.Message);
                onComplete?.Invoke(false);
            }
        }

        public void SignInInteractive(Action<bool> onComplete = null)
        {
            if (IsSignedIn)
            {
                onComplete?.Invoke(true);
                return;
            }

            try
            {
                // ManuallyAuthenticate, not Authenticate: after the launch-time
                // silent attempt has failed, Authenticate just returns the same
                // failure without ever showing Play Games' own UI.
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
                {
                    IsSignedIn = status == SignInStatus.Success;
                    Debug.Log("[play-games] manual sign-in: " + status);
                    onComplete?.Invoke(IsSignedIn);
                });
            }
            catch (Exception e)
            {
                // Same AndroidJavaProxy/R8 trap as SignIn — resolve as a
                // failure rather than leaving the caller waiting forever.
                Debug.LogWarning("[play-games] manual sign-in threw: " + e.GetType().Name + " — " + e.Message);
                onComplete?.Invoke(false);
            }
        }

        public void UnlockAchievement(string achievementId)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(achievementId))
            {
                return;
            }

            // Log the report outcome — it distinguishes an accepted unlock from
            // one Play silently rejects (achievement still in draft, or the
            // account isn't on the testers list).
            PlayGamesPlatform.Instance.ReportProgress(achievementId, 100.0,
                success => Debug.Log("[play-games] achievement " + achievementId
                    + (success ? ": reported OK" : ": report FAILED")));
        }

        public void SubmitScore(string leaderboardId, long score)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                return;
            }

            PlayGamesPlatform.Instance.ReportScore(score, leaderboardId, _ => { });
        }

        public void ShowLeaderboard(string leaderboardId)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                return;
            }

            PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId);
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
