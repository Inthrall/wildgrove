using System.Collections.Generic;
using UnityEngine;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// TEMP on-device diagnostics sink. Services append human-readable status
    /// lines (sign-in result, achievement report outcome) and the HUD shows the
    /// collected lines in a startup popup — so we can see, on the phone, why the
    /// "First kith" achievement does or doesn't unlock without needing logcat.
    /// Remove this class and its call sites once the wiring is confirmed working.
    /// </summary>
    public static class Diag
    {
        /// <summary>The collected status lines, in the order they were logged.</summary>
        public static readonly List<string> Lines = new List<string>();

        /// <summary>Set once sign-in has resolved, so the HUD knows the lines are ready to show.</summary>
        public static bool Ready;

        /// <summary>Append a line (also mirrored to logcat).</summary>
        public static void Log(string line)
        {
            Lines.Add(line);
            Debug.Log("[diag] " + line);
        }

        /// <summary>Clear the buffer at the start of a run so a launch begins fresh.</summary>
        public static void Reset()
        {
            Lines.Clear();
            Ready = false;
        }
    }
}
