using System;
using System.IO;
using System.Text;
using UnityEngine;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game
{
    /// <summary>
    /// The on-device save slot: one JSON file in persistentDataPath, written
    /// atomically (temp file, then swap) so a crash mid-write can't destroy
    /// the previous save. A file that no longer reads is set aside rather than
    /// deleted: *.corrupt for unreadable data, *.newer for a healthy save from
    /// a future build, *.legacy for one from below the migration floor (each its
    /// own slot, so one can't overwrite another).
    /// Cloud Saved Games layers on top of this in Phase 5.
    /// </summary>
    public static class SaveFile
    {
        /// <summary>
        /// The directory the slot lives in, or null for the app's persistent
        /// data path — which is what ships. Tests point it at a scratch
        /// directory: the awkward parts kept here are exactly the ones that
        /// can only be exercised against a real disk, and doing that in the
        /// editor's own persistent path would write over the developer's run.
        /// <para>
        /// Public rather than internal for the same reason as
        /// <see cref="JournalZones"/>: the tests are a separate assembly and
        /// there is no InternalsVisibleTo anywhere in the project.
        /// </para>
        /// </summary>
        public static string DirectoryOverride;

        public static string Path =>
            System.IO.Path.Combine(DirectoryOverride ?? Application.persistentDataPath, "save.json");

        /// <summary>Load and migrate the save. False when there is no usable save (missing, corrupt, from a future build, or older than <see cref="SaveCodec.EarliestReadableVersion"/>).</summary>
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
                // Three different failures, three slots — a save is only ever
                // called corrupt when it actually is, and none of them can
                // overwrite another.
                if (parsed != null && parsed.version > SaveCodec.CurrentVersion)
                {
                    // A future-build save (APK rollback, staged-rollout
                    // downgrade) is healthy data this build can't read. Park it
                    // so re-upgrading can recover it by hand.
                    SetAside(".newer", "Save is from a newer build (v" + parsed.version + ")");
                }
                else if (parsed != null && parsed.version < SaveCodec.EarliestReadableVersion)
                {
                    // Healthy data from below the migration floor: the ladder
                    // that would have carried it up has been retired. Keep it —
                    // it is the only copy of that run, and a build that still
                    // had the steps could read it.
                    SetAside(".legacy", "Save predates the oldest readable format (v" + parsed.version
                                        + ", need v" + SaveCodec.EarliestReadableVersion + ")");
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
            var temp = Path + ".tmp";
            try
            {
                // Flushed to the DEVICE, not just to the OS: without the true
                // flush the atomic replace only protects against a crash partway
                // through writing the slot, while a power loss could still swap
                // in a temp file whose bytes never reached storage — an empty
                // save that looks like a clean one. The point of the dance is
                // that the previous save survives anything, so the temp has to
                // be real before it becomes the save.
                var bytes = new UTF8Encoding(false).GetBytes(SaveCodec.ToJson(save));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

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

                // Don't leave the half-written temp behind: the next write opens
                // it with FileMode.Create and would truncate it anyway, but a
                // stray save.json.tmp beside a healthy save is the kind of thing
                // that gets mistaken for the real one during a support hunt.
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch (Exception cleanup)
                {
                    Debug.LogWarning("Save temp file left behind: " + cleanup.Message);
                }
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
