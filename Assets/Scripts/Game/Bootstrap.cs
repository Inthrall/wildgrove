using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// Spins up the game on Play without requiring anything wired into a scene:
    /// a single persistent object carrying the <see cref="GameLoop"/> and its
    /// <see cref="GameHud"/>. This keeps Phase 1 free of hand-authored scene YAML
    /// (fragile, unreviewable in diffs); a proper bootstrap scene can replace it
    /// once there's real content to lay out. Does nothing if a GameLoop already
    /// exists in the scene, so a manual setup wins.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Object.FindFirstObjectByType<GameLoop>() != null)
            {
                return;
            }

            var go = new GameObject("Wildgrove");
            Object.DontDestroyOnLoad(go);

            // GameLoop first so its Awake builds the run state before the HUD's
            // Awake reads it (GameHud also RequireComponents GameLoop as a guard).
            go.AddComponent<GameLoop>();
            go.AddComponent<World.WorldView>();
            go.AddComponent<GameHud>();
        }
    }
}
