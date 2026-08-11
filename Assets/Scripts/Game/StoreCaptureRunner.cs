using System.Collections;
using UnityEngine;
using Wildgrove.Sim;

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
        private static readonly string[] Pages = { "trail", "camp", "stores", "warden", "record" };

        private IEnumerator Start()
        {
            var dir = System.Environment.GetEnvironmentVariable("WILDGROVE_SHOT_DIR")
                      ?? Application.persistentDataPath;

            // Let the HUD build against the staged save and settle its layout.
            GameHud hud = null;
            while (hud == null)
            {
                hud = FindAnyObjectByType<GameHud>();
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);

            WarnIfNotTheShowcase();

            foreach (var page in Pages)
            {
                hud.OpenTab(page);
                // Two label cadences so every row on the page is fresh.
                yield return new WaitForSeconds(0.6f);
                yield return ClearSheets(hud);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "store-" + page + ".png"));
                yield return new WaitForSeconds(0.4f);
            }

            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "capture-done.marker"), "done");
        }

        /// <summary>
        /// Say so when the session is about to photograph a run that isn't the
        /// showcase. The harness stages a save and enters Play, and nothing
        /// between the two ever checked that the save it staged is the save the
        /// session woke from — so a listing shot of an early camp (two
        /// companions on one slot, a welcome-back sheet the freshly-stamped
        /// showcase could never raise) looked exactly like a successful
        /// capture. The camp is staged a second time here purely to be counted;
        /// it is a few hundred objects, and the alternative is trusting the
        /// thing that was already wrong once.
        /// </summary>
        private static void WarnIfNotTheShowcase()
        {
            var loop = FindAnyObjectByType<GameLoop>();
            if (loop?.State == null || loop.Data == null)
            {
                return;
            }

            var showcase = ShowcaseState.Stage(loop.Data);
            var wanted = Kith.Count(showcase);
            var got = Kith.Count(loop.State);
            if (wanted == got)
            {
                return;
            }

            Debug.LogError("[store-shots] this is not the showcase — staged " + wanted
                           + " companions, the session woke with " + got
                           + ". The save that was staged is not the save that was loaded; the shots are of another run.");
        }

        /// <summary>
        /// Shut whatever sheet is standing before the shutter: the first thing
        /// every capture photographed was a welcome-back sheet, over all five
        /// pages, since the scrim outlives a tab change. Dismissing is the
        /// player's own way out of it (the scrim tap), and PumpSheets can raise
        /// a second one behind the first, so this drains rather than dismisses
        /// once. The reason first written here — that the staged save is
        /// stamped in the past — was wrong: it is stamped `UtcNow`, and 60 s of
        /// credited absence is the bar that sheet needs, so a welcome-back
        /// sheet in front of a capture means the session woke from ANOTHER
        /// save. That is what <see cref="WarnIfNotTheShowcase"/> now says out
        /// loud; draining is still right, because any sheet can stand here.
        /// </summary>
        private static IEnumerator ClearSheets(GameHud hud)
        {
            for (var attempt = 0; attempt < 6 && hud.Sheet != null; attempt++)
            {
                hud.Sheets.DismissSheet();
                yield return null;
            }

            // A dismissed sheet leaves a rebuild behind it; let the page settle
            // before it is photographed.
            yield return new WaitForSeconds(0.3f);
        }
    }
}
