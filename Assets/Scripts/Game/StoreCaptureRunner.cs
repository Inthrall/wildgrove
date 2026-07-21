using System.Collections;
using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// Store-screenshot chauffeur: when the WILDGROVE_STORE_CAPTURE environment
    /// variable is set (the editor harness sets it; it never exists on a
    /// device), waits for the HUD, walks the nav pages, and captures one
    /// screenshot per page into WILDGROVE_SHOT_DIR. Writes a "done" marker the
    /// editor harness polls to exit play mode. Completely inert otherwise.
    /// </summary>
    public static class StoreCaptureRunner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WILDGROVE_STORE_CAPTURE")))
            {
                return;
            }

            var go = new GameObject("StoreCaptureRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<StoreCaptureBehaviour>();
        }
    }

    internal sealed class StoreCaptureBehaviour : MonoBehaviour
    {
        private static readonly string[] Pages = { "trail", "camp", "warden", "record" };

        private IEnumerator Start()
        {
            var dir = System.Environment.GetEnvironmentVariable("WILDGROVE_SHOT_DIR")
                      ?? Application.persistentDataPath;

            // Let the HUD build against the staged save and settle its layout.
            GameHud hud = null;
            while (hud == null)
            {
                hud = FindFirstObjectByType<GameHud>();
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);

            foreach (var page in Pages)
            {
                hud.OpenTab(page);
                // Two label cadences so every row on the page is fresh.
                yield return new WaitForSeconds(0.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "store-" + page + ".png"));
                yield return new WaitForSeconds(0.4f);
            }

            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "capture-done.marker"), "done");
        }
    }
}
