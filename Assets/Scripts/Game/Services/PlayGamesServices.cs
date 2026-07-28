#if UNITY_ANDROID
using System;
using System.Text;
using UnityEngine;
using UnityEngine.SocialPlatforms;
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

            try
            {
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
            catch (Exception e)
            {
                // This one throws SYNCHRONOUSLY, before any listener is attached:
                // the overlay is the only GPGS call routed through
                // com.google.games.bridge.HelperFragment, which extends the
                // framework android.app.Fragment (deprecated since API 28), and
                // resolving that class over JNI fails on a high target SDK. The
                // throw used to escape into the button handler, where Unity
                // logged it out of sight — so the tap died with a request line
                // and no answer, looking exactly like a callback that hung.
                Report("Leaderboard " + leaderboardId + ": overlay THREW — "
                    + e.GetType().Name + ": " + e.Message);
                onClosed?.Invoke(false);
            }
        }

        public void LoadLeaderboard(string leaderboardId, int rowCount, Action<LeaderboardEntry[]> onLoaded)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                Diag.Log("Leaderboard " + leaderboardId + ": read skipped (signed out)");
                onLoaded?.Invoke(null);
                return;
            }

            Diag.Log("Leaderboard " + leaderboardId + ": reading top " + rowCount);

            PlayGamesPlatform.Instance.LoadScores(leaderboardId, LeaderboardStart.TopScores, rowCount,
                LeaderboardCollection.Public, LeaderboardTimeSpan.AllTime, data =>
                {
                    if (data == null || !data.Valid || data.Scores == null)
                    {
                        Report("Leaderboard " + leaderboardId + ": read FAILED — "
                            + (data == null ? "no data" : data.Status.ToString()));
                        onLoaded?.Invoke(null);
                        return;
                    }

                    // Report the player's own row and Play's approximate total as
                    // well as the page size. An empty top page with the player
                    // ranked means the board exists and we asked wrongly; an
                    // empty page with the player unranked means Play is not
                    // publishing scores for this game yet, which is a console
                    // state and not something the client can fix.
                    var player = data.PlayerScore == null
                        ? "unranked"
                        : "rank " + data.PlayerScore.rank + " with " + data.PlayerScore.value;
                    Report("Leaderboard " + leaderboardId + ": read " + data.Scores.Length
                        + " rows, player " + player + ", approx total " + data.ApproximateCount);
                    ResolveNames(data, onLoaded);
                });
        }

        /// <summary>
        /// Turn a page of scores into journal lines, putting names to the ids.
        /// Play returns only player ids with the scores, so the names take a
        /// second call — and a board is still worth showing without them, so a
        /// failed lookup falls back to the rank rather than dropping the row.
        /// </summary>
        private static void ResolveNames(LeaderboardScoreData data, Action<LeaderboardEntry[]> onLoaded)
        {
            var scores = data.Scores;
            var playerId = data.PlayerScore == null ? null : data.PlayerScore.userID;
            var ids = new string[scores.Length];
            for (var i = 0; i < scores.Length; i++)
            {
                ids[i] = scores[i].userID;
            }

            if (ids.Length == 0)
            {
                // An empty top page does not mean the player has no standing:
                // Play withholds the public page for a game it is not yet
                // publishing scores for, while still ranking the player against
                // it. Their own line is worth showing on its own — it is the
                // part they actually came to read.
                if (data.PlayerScore != null)
                {
                    onLoaded?.Invoke(new[]
                    {
                        new LeaderboardEntry
                        {
                            rank = data.PlayerScore.rank,
                            name = PlayGamesPlatform.Instance.GetUserDisplayName(),
                            score = data.PlayerScore.value,
                            isPlayer = true,
                        },
                    });
                    return;
                }

                onLoaded?.Invoke(new LeaderboardEntry[0]);
                return;
            }

            PlayGamesPlatform.Instance.LoadUsers(ids, profiles =>
            {
                var entries = new LeaderboardEntry[scores.Length];
                for (var i = 0; i < scores.Length; i++)
                {
                    entries[i] = new LeaderboardEntry
                    {
                        rank = scores[i].rank,
                        name = NameFor(profiles, scores[i].userID),
                        score = scores[i].value,
                        isPlayer = playerId != null && scores[i].userID == playerId,
                    };
                }

                onLoaded?.Invoke(entries);
            });
        }

        private static string NameFor(IUserProfile[] profiles, string userId)
        {
            if (profiles != null)
            {
                for (var i = 0; i < profiles.Length; i++)
                {
                    if (profiles[i] != null && profiles[i].id == userId)
                    {
                        return profiles[i].userName;
                    }
                }
            }

            return "a warden";
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
