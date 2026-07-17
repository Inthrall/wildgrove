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
    /// restores the save, and closes the editor.
    /// </summary>
    public static class StoreScreenshots
    {
        private const string SessionFlag = "wildgrove.storeCapture";
        private const string BackupSuffix = ".store-backup";

        [MenuItem("Wildgrove/Capture Store Screenshots")]
        public static void CaptureForCli()
        {
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

        /// <summary>
        /// A camp worth photographing: three zones open, flocks working,
        /// specimens banked, a fossil half-dug, the Rite part-sung.
        /// </summary>
        private static GameState StageShowcaseState(GameDataAsset data)
        {
            var state = GameStateFactory.NewGame(data);

            // March up the §9 ladder far enough to open Bramble and Old-Growth —
            // granting the coin and materials each rung asks for.
            foreach (var upgrade in data.upgrades.OrderBy(u => u.order).Take(16))
            {
                state.coin = BigDouble.Max(state.coin, new BigDouble(upgrade.costCoin) * 2);
                foreach (var material in upgrade.materials)
                {
                    state.AddResource(material.id, new BigDouble(material.amount));
                }

                Upgrades.TryPurchase(state, data, upgrade);
            }

            state.coin = new BigDouble(2.4e6);
            state.carrierCount = 7;
            state.renown = new BigDouble(1250);
            state.amber = 12;

            var flockSizes = new[] { 5, 3, 6, 2, 4, 3, 5, 2, 3 };
            for (var i = 0; i < state.nodes.Count; i++)
            {
                var node = state.nodes[i];
                node.familiarCount = flockSizes[i % flockSizes.Length];
                node.masteryXp = 1800 + i * 700;
                node.basket = new BigDouble(12 + i * 7);
                state.AddResource(node.resourceId, new BigDouble(900 + i * 2100));
            }

            if (data.economy?.xp != null)
            {
                Skills.AddGatherXp(state, data, "foraging", new BigDouble(45000));
                Skills.AddGatherXp(state, data, "mining", new BigDouble(16000));
                Skills.AddGatherXp(state, data, "logging", new BigDouble(9000));
            }

            state.AddResource("copper-ingot", new BigDouble(14));
            state.AddResource("charcoal", new BigDouble(22));
            state.AddPristine("berries", new BigDouble(3));
            state.AddPristine("wildflowers", new BigDouble(1));
            state.AddFine("nuts", new BigDouble(45));
            Museum.TryDonate(state, data, "wildflowers");

            foreach (var site in state.digSites)
            {
                site.familiarCount = 2;
            }

            state.fossilFragments["antler-crown"] = 2;
            state.deedCounts["tend"] = 14;
            state.wardenPostNodeId = state.nodes.Count > 1 ? state.nodes[1].id : null;

            Upgrades.RecomputeYieldMultipliers(state, data);
            return state;
        }
    }

    /// <summary>Watches for the runner's done-marker across the play-mode domain reload.</summary>
    [InitializeOnLoad]
    internal static class StoreScreenshotPoller
    {
        private const string SessionFlag = "wildgrove.storeCapture";

        static StoreScreenshotPoller()
        {
            if (SessionState.GetBool(SessionFlag, false))
            {
                EditorApplication.update += Poll;
            }
        }

        private static void Poll()
        {
            var dir = System.Environment.GetEnvironmentVariable("WILDGROVE_SHOT_DIR")
                      ?? Application.persistentDataPath;
            var done = System.IO.File.Exists(System.IO.Path.Combine(dir, "capture-done.marker"));
            var timedOut = EditorApplication.timeSinceStartup
                           - SessionState.GetFloat(SessionFlag + ".start", 0f) > 240.0;

            if (EditorApplication.isPlaying && (done || timedOut))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode
                && (done || timedOut))
            {
                EditorApplication.update -= Poll;
                SessionState.SetBool(SessionFlag, false);

                var backup = SaveFile.Path + ".store-backup";
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

                EditorApplication.Exit(done ? 0 : 1);
            }
        }
    }
}
