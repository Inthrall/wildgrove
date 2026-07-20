using System.Linq;
using BreakInfinity;
using UnityEditor;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game;
using Wildgrove.Sim;
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

            var save = SaveCodec.Capture(StageShowcaseState(data),
                System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SaveFile.Path));
            System.IO.File.WriteAllText(SaveFile.Path, SaveCodec.ToJson(save));

            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1080, 2400, "Store Portrait");

            SessionState.SetBool(SessionFlag, true);
            SessionState.SetFloat(SessionFlag + ".start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
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
            else if (System.IO.File.Exists(SaveFile.Path))
            {
                // No prior save existed — don't leave the staged one behind.
                System.IO.File.Delete(SaveFile.Path);
            }

            if (System.IO.File.Exists(noneMarker))
            {
                System.IO.File.Delete(noneMarker);
            }
        }

        /// <summary>
        /// A camp worth photographing: three zones open, flocks working,
        /// specimens banked, a fossil half-dug, the Rite part-sung.
        /// </summary>
        private static GameState StageShowcaseState(GameDataAsset data)
        {
            var state = GameStateFactory.NewGame(data);

            // Skill XP up front so the §9 ladder's skill gates open (money→XP).
            if (data.economy?.xp != null)
            {
                Skills.AddGatherXp(state, data, "foraging", new BigDouble(200000));
                Skills.AddGatherXp(state, data, "mining", new BigDouble(80000));
                Skills.AddGatherXp(state, data, "logging", new BigDouble(60000));
                Skills.AddGatherXp(state, data, "firecraft", new BigDouble(60000));
                Skills.AddGatherXp(state, data, "forgecraft", new BigDouble(60000));
                Skills.AddGatherXp(state, data, "bushcraft", new BigDouble(60000));
            }

            // March up the ladder, granting the materials each rung asks for.
            foreach (var upgrade in data.upgrades.OrderBy(u => u.order).Take(16))
            {
                foreach (var material in upgrade.materials)
                {
                    state.AddResource(material.id, new BigDouble(material.amount));
                }

                Upgrades.TryPurchase(state, data, upgrade);
            }

            state.renown = new BigDouble(1250);
            state.amber = 12;

            // A crew worth photographing: gatherers across the nodes, three on the trail.
            var flockSizes = new[] { 5, 3, 6, 2, 4, 3, 5, 2, 3 };
            for (var i = 0; i < state.nodes.Count; i++)
            {
                var node = state.nodes[i];
                for (var f = 0; f < flockSizes[i % flockSizes.Length]; f++)
                {
                    Roster.Recruit(state, data, "meadow-vole", node.id);
                }

                node.masteryXp = 1800 + i * 700;
                node.basket = new BigDouble(12 + i * 7);
                state.AddResource(node.resourceId, new BigDouble(900 + i * 2100));
            }

            for (var t = 0; t < 3; t++)
            {
                Roster.Recruit(state, data, "pack-raven", Familiar.TrailStation);
            }

            state.AddResource("copper-ingot", new BigDouble(14));
            state.AddResource("charcoal", new BigDouble(22));
            state.AddPristine("berries", new BigDouble(3));
            state.AddPristine("wildflowers", new BigDouble(1));
            state.AddFine("nuts", new BigDouble(45));
            Folio.TryFix(state, data, "wildflowers");

            foreach (var site in state.digSites)
            {
                for (var d = 0; d < 2; d++)
                {
                    Roster.Recruit(state, data, "meadow-vole", Familiar.DigStationPrefix + site.zoneId);
                }
            }

            state.fossilFragments["antler-crown"] = 2;
            state.deedCounts["tend"] = 14;
            state.wardenPostNodeId = state.nodes.Count > 1 ? state.nodes[1].id : null;

            Upgrades.RecomputeYieldMultipliers(state, data);
            return state;
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
