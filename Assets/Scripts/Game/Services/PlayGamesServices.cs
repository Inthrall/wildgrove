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
    ///
    /// Every async call reports its outcome into <see cref="Diag"/> as well as
    /// logcat. That is deliberate and TEMP: the failures this wiring keeps
    /// hitting are silent ones — a callback that never returns, or a status code
    /// that is thrown away at the call site — and neither is visible on a device
    /// running a store build. Retire the Diag lines with the sink itself.
    /// </summary>
    public sealed class PlayGamesServices : IGameServices
    {
        private const string SnapshotName = "wildgrove_save";

        public bool IsSignedIn { get; private set; }

        public void SignIn(Action<bool> onComplete = null)
        {
            // On unconditionally, not just in debug builds. Minified release
            // builds are the only place the sign-in hang has ever reproduced, so
            // gating GPGS's own trace behind Debug.isDebugBuild turned it off in
            // exactly the build that needed reading.
            PlayGamesPlatform.DebugLogEnabled = true;

            // Instance construction is a distinct failure stage: the getter
            // builds AndroidClient, which calls PlayGamesSdk.initialize over
            // JNI. Naming the stage separates "the SDK isn't in the APK" from
            // "the SDK is there and the auth task never finished".
            PlayGamesPlatform platform;
            try
            {
                platform = PlayGamesPlatform.Instance;
            }
            catch (Exception e)
            {
                Report("Sign-in: FAILED building the platform — " + e.GetType().Name + ": " + e.Message);
                onComplete?.Invoke(false);
                return;
            }

            Diag.Log("Sign-in: requested (silent)");

            try
            {
                platform.Authenticate(status =>
                {
                    IsSignedIn = status == SignInStatus.Success;
                    Report("Sign-in: " + status);
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
                Report("Sign-in: THREW — " + e.GetType().Name + ": " + e.Message);
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

            Diag.Log("Manual sign-in: requested");

            try
            {
                // ManuallyAuthenticate, not Authenticate: after the launch-time
                // silent attempt has failed, Authenticate just returns the same
                // failure without ever showing Play Games' own UI.
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
                {
                    IsSignedIn = status == SignInStatus.Success;
                    Report("Manual sign-in: " + status);
                    onComplete?.Invoke(IsSignedIn);
                });
            }
            catch (Exception e)
            {
                // Same AndroidJavaProxy/R8 trap as SignIn — resolve as a
                // failure rather than leaving the caller waiting forever.
                Report("Manual sign-in: THREW — " + e.GetType().Name + ": " + e.Message);
                onComplete?.Invoke(false);
            }
        }

        public void UnlockAchievement(string achievementId)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(achievementId))
            {
                Diag.Log("Achievement " + achievementId + ": skipped (signed out)");
                return;
            }

            // Log the report outcome — it distinguishes an accepted unlock from
            // one Play silently rejects (achievement still in draft, or the
            // account isn't on the testers list).
            PlayGamesPlatform.Instance.ReportProgress(achievementId, 100.0,
                success => Report("Achievement " + achievementId + (success ? ": reported OK" : ": report FAILED")));
        }

        public void SubmitScore(string leaderboardId, long score)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                // Deliberately silent: this rides the autosave cadence (~30 s),
                // so logging the signed-out skip would roll the sign-in lines
                // straight out of the buffer — losing the very thing being read.
                return;
            }

            // The outcome used to be discarded, which made "the board is empty"
            // impossible to tell apart from "the submit was rejected" — the same
            // blind spot the achievement report had before it started reporting.
            PlayGamesPlatform.Instance.ReportScore(score, leaderboardId,
                success => Report("Leaderboard " + leaderboardId + " score " + score
                    + (success ? ": accepted" : ": REJECTED")));
        }

        public void ShowLeaderboard(string leaderboardId, Action<bool> onClosed = null)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                Diag.Log("Leaderboard " + leaderboardId + ": overlay skipped (signed out)");
                onClosed?.Invoke(false);
                return;
            }

            Diag.Log("Leaderboard " + leaderboardId + ": opening overlay");

            // The callback overload, not ShowLeaderboardUI(id): the one-argument
            // version passes a null callback down, so every reason the overlay
            // might refuse — board still a draft, Play Services needing an
            // update, another overlay already up — was discarded and the tap
            // just did nothing. UserClosedUI counts as a success: the overlay
            // opened, and the player dismissed it.
            PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId, LeaderboardTimeSpan.AllTime, status =>
            {
                var opened = status == UIStatus.Valid || status == UIStatus.UserClosedUI;
                Report("Leaderboard " + leaderboardId + ": overlay " + status);
                onClosed?.Invoke(opened);
            });
        }

        public void LoadCloud(Action<string> onLoaded)
        {
            if (!IsSignedIn)
            {
                Diag.Log("Cloud load: skipped (signed out)");
                onLoaded?.Invoke(null);
                return;
            }

            Diag.Log("Cloud load: opening snapshot");
            OpenSnapshot((status, game) =>
            {
                if (status != SavedGameRequestStatus.Success || game == null)
                {
                    Report("Cloud load: open FAILED — " + status);
                    onLoaded?.Invoke(null);
                    return;
                }

                PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(game, (readStatus, bytes) =>
                {
                    var length = bytes == null ? 0 : bytes.Length;
                    Report("Cloud load: read " + readStatus + ", " + length + " bytes");
                    onLoaded?.Invoke(readStatus == SavedGameRequestStatus.Success && length > 0
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
                    Report("Cloud save: open FAILED — " + status);
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
                PlayGamesPlatform.Instance.SavedGame.CommitUpdate(game, update, bytes, (commitStatus, __) =>
                {
                    Report("Cloud save: commit " + commitStatus + ", " + bytes.Length + " bytes");
                    onComplete?.Invoke();
                });
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

        /// <summary>
        /// Mirror an outcome to both sinks — logcat for a tethered device, the
        /// Diag buffer for a phone with no cable. TEMP, with the sink.
        /// </summary>
        private static void Report(string line)
        {
            Debug.Log("[play-games] " + line);
            Diag.Log(line);
        }
    }
}
#endif
