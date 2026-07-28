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
    /// authentication, achievement unlocks, the Renown board, and cloud save via
    /// Snapshots. Android-only — GPGS's <c>PlayGamesPlatform</c> is itself
    /// compiled under <c>UNITY_ANDROID</c>, so this file is guarded to match (the
    /// editor and any non-Android target use <see cref="StubGameServices"/>,
    /// selected in GameLoop).
    /// <para>
    /// Every async call logs its outcome, and every JNI call that can throw is
    /// caught. Both are deliberate: this wiring's failures are all silent ones —
    /// a callback that never returns, a status code discarded at the call site, a
    /// JNI throw swallowed by a button handler — and none of them are visible on
    /// a device running a store build. Two long hunts came down to exactly that,
    /// so the lines stay.
    /// </para>
    /// </summary>
    public sealed class PlayGamesServices : IGameServices
    {
        private const string SnapshotName = "wildgrove_save";

        public bool IsSignedIn { get; private set; }

        public void SignIn(Action<bool> onComplete = null)
        {
            PlayGamesPlatform.DebugLogEnabled = Debug.isDebugBuild; // GPGS's own trace into logcat

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
                Log("sign-in FAILED building the platform — " + e.GetType().Name + ": " + e.Message);
                onComplete?.Invoke(false);
                return;
            }

            try
            {
                platform.Authenticate(status =>
                {
                    IsSignedIn = status == SignInStatus.Success;
                    Log("sign-in: " + status);
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
                Log("sign-in threw: " + e.GetType().Name + " — " + e.Message);
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
                    Log("manual sign-in: " + status);
                    onComplete?.Invoke(IsSignedIn);
                });
            }
            catch (Exception e)
            {
                // Same AndroidJavaProxy/R8 trap as SignIn — resolve as a
                // failure rather than leaving the caller waiting forever.
                Log("manual sign-in threw: " + e.GetType().Name + " — " + e.Message);
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
                success => Log("achievement " + achievementId + (success ? ": reported OK" : ": report FAILED")));
        }

        public void SubmitScore(string leaderboardId, long score)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                return;
            }

            // Keep the outcome. Discarding it once made "the board is empty"
            // impossible to tell apart from "the submit was rejected".
            PlayGamesPlatform.Instance.ReportScore(score, leaderboardId,
                success => Log("leaderboard " + leaderboardId + " score " + score
                    + (success ? ": accepted" : ": REJECTED")));
        }

        public void ShowLeaderboard(string leaderboardId, Action<bool> onClosed = null)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                onClosed?.Invoke(false);
                return;
            }

            try
            {
                // The callback overload, not ShowLeaderboardUI(id): the
                // one-argument version passes a null callback down, so every
                // reason the overlay might refuse was discarded. UserClosedUI
                // counts as a success — the overlay opened and was dismissed.
                PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId, LeaderboardTimeSpan.AllTime, status =>
                {
                    var opened = status == UIStatus.Valid || status == UIStatus.UserClosedUI;
                    Log("leaderboard " + leaderboardId + ": overlay " + status);
                    onClosed?.Invoke(opened);
                });
            }
            catch (Exception e)
            {
                // NOTHING CALLS THIS, and the catch is why: the overlay is the
                // only GPGS call routed through com.google.games.bridge.HelperFragment,
                // which extends the framework android.app.Fragment (deprecated
                // since API 28), so resolving it over JNI throws on a modern
                // target SDK. It throws SYNCHRONOUSLY, before any listener is
                // attached, which is exactly why it once looked like a hung
                // callback. The Standing is drawn from LoadLeaderboard instead;
                // this stays only for the day the bridge is fixed upstream.
                Log("leaderboard " + leaderboardId + ": overlay threw — "
                    + e.GetType().Name + ": " + e.Message);
                onClosed?.Invoke(false);
            }
        }

        public void LoadLeaderboard(string leaderboardId, int rowCount, Action<LeaderboardEntry[]> onLoaded)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(leaderboardId))
            {
                onLoaded?.Invoke(null);
                return;
            }

            PlayGamesPlatform.Instance.LoadScores(leaderboardId, LeaderboardStart.TopScores, rowCount,
                LeaderboardCollection.Public, LeaderboardTimeSpan.AllTime, data =>
                {
                    if (data == null || !data.Valid || data.Scores == null)
                    {
                        Log("leaderboard " + leaderboardId + ": read FAILED — "
                            + (data == null ? "no data" : data.Status.ToString()));
                        onLoaded?.Invoke(null);
                        return;
                    }

                    // The player's own row as well as the page size: an empty top
                    // page with the player ranked means the board holds the score
                    // and only the public page is withheld, which is a console
                    // state rather than anything to fix here.
                    Log("leaderboard " + leaderboardId + ": read " + data.Scores.Length + " rows, player "
                        + (data.PlayerScore == null ? "unranked" : "rank " + data.PlayerScore.rank));
                    ResolveNames(data, onLoaded);
                });
        }

        /// <summary>
        /// Turn a page of scores into journal lines, putting names to the ids.
        /// Play returns only player ids with the scores, so the names take a
        /// second call — and a board is still worth showing without them, so a
        /// failed lookup falls back to a stock name rather than dropping the row.
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
                // Play can rank them against a board whose public page it is
                // withholding. Their own line is the part they came to read.
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
                onLoaded?.Invoke(null);
                return;
            }

            OpenSnapshot((status, game) =>
            {
                if (status != SavedGameRequestStatus.Success || game == null)
                {
                    Log("cloud load: open FAILED — " + status);
                    onLoaded?.Invoke(null);
                    return;
                }

                PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(game, (readStatus, bytes) =>
                {
                    var length = bytes == null ? 0 : bytes.Length;
                    Log("cloud load: read " + readStatus + ", " + length + " bytes");
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
                    Log("cloud save: open FAILED — " + status);
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
                    Log("cloud save: commit " + commitStatus + ", " + bytes.Length + " bytes");
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

        private static void Log(string line)
        {
            Debug.Log("[play-games] " + line);
        }
    }
}
#endif
