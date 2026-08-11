using UnityEditor;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game;
using Wildgrove.Sim.Saves;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Store-screenshot harness. Launch with the GUI editor (not batchmode —
    /// screenshots need a real GameView):
    ///
    ///   WILDGROVE_STORE_CAPTURE=1 WILDGROVE_SHOT_DIR=&lt;dir&gt; Unity.exe
    ///     -projectPath . -executeMethod Wildgrove.EditorTools.StoreScreenshots.CaptureForCli
    ///
    /// Stages a rich mid-game save (the real one is set aside and restored),
    /// sizes the GameView to phone portrait, and enters Play — where
    /// <see cref="StoreCaptureRunner"/> walks the nav pages and captures one
    /// shot each. A poller watches for the runner's done-marker, exits Play,
    /// restores the save, and closes the editor. If the editor died mid-run,
    /// the poller's load-time sweep restores the set-aside save on the next
    /// launch instead.
    /// </summary>
    public static class StoreScreenshots
    {
        internal const string SessionFlag = "wildgrove.storeCapture";
        internal const string BackupSuffix = ".store-backup";
        // Marks "no real save existed before staging" — so a crash recovery
        // knows to delete the staged save rather than leave it playable.
        internal const string NoneMarkerSuffix = ".store-none";
        internal const string CaptureEnvVar = "WILDGROVE_STORE_CAPTURE";

        [MenuItem("Wildgrove/Capture Store Screenshots")]
        public static void CaptureForCli()
        {
            // The runner is gated on the same env var, and the poller
            // hard-exits the editor when it was set — a bare menu click would
            // capture nothing, then kill the session 240s later.
            if (System.Environment.GetEnvironmentVariable(CaptureEnvVar) == null)
            {
                Debug.LogError("[store-shots] Set " + CaptureEnvVar + "=1 (and WILDGROVE_SHOT_DIR) and launch via -executeMethod — this harness closes the editor when it finishes, so it must own the session.");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<GameDataAsset>("Assets/Resources/Data/GameData.asset");
            if (data == null)
            {
                Debug.LogError("[store-shots] GameData.asset not found");
                EditorApplication.Exit(1);
                return;
            }

            if (System.IO.File.Exists(SaveFile.Path))
            {
                System.IO.File.Copy(SaveFile.Path, SaveFile.Path + BackupSuffix, true);
            }
            else
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SaveFile.Path));
                System.IO.File.WriteAllText(SaveFile.Path + NoneMarkerSuffix, string.Empty);
            }

            var save = SaveCodec.Capture(ShowcaseState.Stage(data),
                System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SaveFile.Path));
            System.IO.File.WriteAllText(SaveFile.Path, SaveCodec.ToJson(save));

            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            var (shotWidth, shotHeight) = ShotSize();
            PlayModeWindow.SetCustomRenderingResolution((uint)shotWidth, (uint)shotHeight,
                shotWidth >= shotHeight ? "Store Landscape" : "Store Portrait");

            SessionState.SetBool(SessionFlag, true);
            SessionState.SetFloat(SessionFlag + ".start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// The GameView size to photograph: phone portrait by default (what the
        /// store listing asks for), or WILDGROVE_SHOT_SIZE=WIDTHxHEIGHT — which
        /// is how the journal's landscape shapes get looked at, the spread being
        /// the one layout no portrait shot can show (design §12 asks the game to
        /// hold up on 4:3 / 16:10 / 21:9).
        /// </summary>
        private static (int Width, int Height) ShotSize()
        {
            var requested = System.Environment.GetEnvironmentVariable("WILDGROVE_SHOT_SIZE");
            var parts = requested != null ? requested.Split('x') : null;
            if (parts != null && parts.Length == 2
                && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height)
                && width > 0 && height > 0)
            {
                return (width, height);
            }

            if (!string.IsNullOrEmpty(requested))
            {
                Debug.LogWarning("[store-shots] Ignoring WILDGROVE_SHOT_SIZE='" + requested + "' — expected WIDTHxHEIGHT, e.g. 2580x1459.");
            }

            return (1080, 2400);
        }

        /// <summary>Put the player's real save back (or remove the staged one when none existed).</summary>
        internal static void RestoreRealSave()
        {
            var backup = SaveFile.Path + BackupSuffix;
            var noneMarker = SaveFile.Path + NoneMarkerSuffix;
            if (System.IO.File.Exists(backup))
            {
                System.IO.File.Copy(backup, SaveFile.Path, true);
                System.IO.File.Delete(backup);
            }
            else if (System.IO.File.Exists(noneMarker) && System.IO.File.Exists(SaveFile.Path))
            {
                // No prior save existed — don't leave the staged one behind.
                // Gated on the marker, and the marker alone: this runs more than
                // once per capture (the poller calls it, and the load-time sweep
                // can too), and without the gate the second call found no backup,
                // decided the save in front of it was staged, and deleted a real
                // one. A save this harness did not create is never its to remove.
                System.IO.File.Delete(SaveFile.Path);
            }

            if (System.IO.File.Exists(noneMarker))
            {
                System.IO.File.Delete(noneMarker);
            }
        }

    }

    /// <summary>
    /// Watches for the runner's done-marker across the play-mode domain
    /// reload, and sweeps up after a crashed capture on the next editor
    /// launch (SessionState dies with the session, the set-aside files
    /// don't — their presence with no live capture flag IS the crash signal).
    /// </summary>
    [InitializeOnLoad]
    internal static class StoreScreenshotPoller
    {
        static StoreScreenshotPoller()
        {
            if (SessionState.GetBool(StoreScreenshots.SessionFlag, false))
            {
                EditorApplication.update += Poll;
                return;
            }

            if (System.IO.File.Exists(SaveFile.Path + StoreScreenshots.BackupSuffix)
                || System.IO.File.Exists(SaveFile.Path + StoreScreenshots.NoneMarkerSuffix))
            {
                Debug.LogWarning("[store-shots] Found a set-aside save from an interrupted capture — restoring it.");
                StoreScreenshots.RestoreRealSave();
            }
        }

        private static void Poll()
        {
            var dir = System.Environment.GetEnvironmentVariable("WILDGROVE_SHOT_DIR")
                      ?? Application.persistentDataPath;
            var done = System.IO.File.Exists(System.IO.Path.Combine(dir, "capture-done.marker"));
            var timedOut = EditorApplication.timeSinceStartup
                           - SessionState.GetFloat(StoreScreenshots.SessionFlag + ".start", 0f) > 240.0;

            if (EditorApplication.isPlaying && (done || timedOut))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode
                && (done || timedOut))
            {
                EditorApplication.update -= Poll;
                SessionState.SetBool(StoreScreenshots.SessionFlag, false);
                StoreScreenshots.RestoreRealSave();

                // Only the CLI launch owns the session — never hard-exit an
                // interactive editor (Exit skips the unsaved-scene prompt).
                if (System.Environment.GetEnvironmentVariable(StoreScreenshots.CaptureEnvVar) != null)
                {
                    EditorApplication.Exit(done ? 0 : 1);
                }
            }
        }
    }
}
