using System;
using System.IO;
using UnityEngine;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game
{
    /// <summary>
    /// The on-device save slot: one JSON file in persistentDataPath, written
    /// atomically (temp file, then swap) so a crash mid-write can't destroy
    /// the previous save. A file that no longer reads — corrupt, or written by
    /// a future build — is set aside as *.corrupt for post-mortem rather than
    /// deleted. Cloud Saved Games layers on top of this in Phase 5.
    /// </summary>
    public static class SaveFile
    {
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>Load and migrate the save. False when there is no usable save (missing, corrupt, or from a future build).</summary>
        public static bool TryLoad(out SaveData save)
        {
            save = null;

            string json;
            try
            {
                if (!File.Exists(Path))
                {
                    return false;
                }

                json = File.ReadAllText(Path);
            }
            catch (Exception e)
            {
                // An unreadable disk shouldn't stop the game launching.
                Debug.LogError("Save load failed, starting fresh: " + e.Message);
                return false;
            }

            var parsed = SaveCodec.FromJson(json);
            if (parsed == null || !SaveCodec.TryMigrate(parsed))
            {
                SetAsideCorrupt();
                return false;
            }

            save = parsed;
            return true;
        }

        public static void Write(SaveData save)
        {
            try
            {
                var temp = Path + ".tmp";
                File.WriteAllText(temp, SaveCodec.ToJson(save));
                if (File.Exists(Path))
                {
                    File.Replace(temp, Path, null);
                }
                else
                {
                    File.Move(temp, Path);
                }
            }
            catch (Exception e)
            {
                // A failed autosave shouldn't take the session down — the next
                // interval retries.
                Debug.LogError("Save write failed: " + e.Message);
            }
        }

        private static void SetAsideCorrupt()
        {
            var corrupt = Path + ".corrupt";
            try
            {
                File.Copy(Path, corrupt, true);
                File.Delete(Path);
                Debug.LogError("Save file was unreadable — set aside as " + corrupt + ", starting fresh.");
            }
            catch (Exception e)
            {
                Debug.LogError("Save file was unreadable and could not be set aside: " + e.Message);
            }
        }
    }
}
