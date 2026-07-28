using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// TEMP on-device diagnostics sink. Services append human-readable status
    /// lines (sign-in result, achievement and leaderboard report outcomes,
    /// Snapshot open/read/commit statuses) and the journal shows the collected
    /// lines in a sheet — so we can see, on the phone, where Play Games stops
    /// answering without needing logcat.
    ///
    /// This is the second time it has been needed. The first sink was retired
    /// as soon as the achievement unlocked, and the failure came back with no
    /// instrument left to read it — so this one is not startup-only: it keeps
    /// accepting lines for the whole session, and the Standing card can open it
    /// on demand. A sign-in that is now asked for by a button tap cannot be
    /// diagnosed by a popup that only fires at launch.
    ///
    /// Remove this class and its call sites once sign-in, the Renown board and
    /// cloud Snapshots are all confirmed working on device.
    /// </summary>
    public static class Diag
    {
        // Long enough to hold a launch plus a few deliberate taps; bounded so a
        // session left running can't grow the buffer without limit.
        private const int MaxLines = 60;

        // A Stopwatch, not Time.realtimeSinceStartup: the whole point of this
        // sink is to be called from Play Games' result callbacks, and the Unity
        // time API throws off the main thread. An instrument that can throw
        // inside the callback it exists to observe is worse than no instrument.
        private static readonly Stopwatch SinceLaunch = Stopwatch.StartNew();
        private static readonly object Gate = new object();

        /// <summary>The collected status lines, in the order they were logged.</summary>
        private static readonly List<string> Lines = new List<string>();

        /// <summary>Set once sign-in has resolved, so the HUD knows the lines are ready to show.</summary>
        public static bool Ready;

        /// <summary>
        /// Append a line, stamped with the seconds since launch (also mirrored
        /// to logcat). The stamp is the point: a request line with no result
        /// line after it is exactly the hang being hunted, and the gap says how
        /// long it has been waiting.
        /// </summary>
        public static void Log(string line)
        {
            var stamped = (SinceLaunch.ElapsedMilliseconds / 1000.0).ToString("0.0") + "s  " + line;
            lock (Gate)
            {
                if (Lines.Count >= MaxLines)
                {
                    Lines.RemoveAt(0);
                }

                Lines.Add(stamped);
            }

            Debug.Log("[diag] " + stamped);
        }

        /// <summary>
        /// A copy of the lines so far. A copy, not the list: the sheet builds on
        /// the main thread while a Play Games callback may still be appending,
        /// and enumerating the live list would throw mid-render.
        /// </summary>
        public static IReadOnlyList<string> Snapshot()
        {
            lock (Gate)
            {
                return Lines.ToArray();
            }
        }

        /// <summary>Clear the buffer at the start of a run so a launch begins fresh.</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                Lines.Clear();
            }

            Ready = false;
        }
    }
}
