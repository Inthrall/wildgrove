using System;
using System.IO;
using UnityEngine;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game
{
    /// <summary>
    /// The on-device save slot: one JSON file in persistentDataPath, written
    /// atomically (temp file, then swap) so a crash mid-write can't destroy
    /// the previous save. A file that no longer reads is set aside rather than
    /// deleted: *.corrupt for unreadable data, *.newer for a healthy save from
    /// a future build (each its own slot, so one can't overwrite the other).
    /// Cloud Saved Games layers on top of this in Phase 5.
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

            SaveData parsed;
            try
            {
                parsed = SaveCodec.FromJson(json);
            }
            catch (Exception e)
            {
                // FromJson absorbs JSON shape errors itself; anything that
                // still escapes is a decode failure the corrupt-file path must
                // own — the alternative is a crash loop on every launch.
                Debug.LogError("Save decode failed: " + e.Message);
                parsed = null;
            }

            if (parsed == null || !SaveCodec.TryMigrate(parsed))
            {
                // A future-build save (APK rollback, staged-rollout downgrade)
                // is healthy data this build can't read — park it in its own
                // slot so a later genuine corruption can't overwrite it, and
                // re-upgrading can recover it by hand.
                if (parsed != null && parsed.version > SaveCodec.CurrentVersion)
                {
                    SetAside(".newer", "Save is from a newer build (v" + parsed.version + ")");
                }
                else
                {
                    SetAside(".corrupt", "Save file was unreadable");
                }

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

        private static void SetAside(string suffix, string reason)
        {
            var target = Path + suffix;
            try
            {
                File.Copy(Path, target, true);
                File.Delete(Path);
                Debug.LogError(reason + " — set aside as " + target + ", starting fresh.");
            }
            catch (Exception e)
            {
                Debug.LogError(reason + " and could not be set aside: " + e.Message);
            }
        }
    }
}
