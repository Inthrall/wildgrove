using UnityEngine;

namespace Wildgrove.Game.Input
{
    /// <summary>
    /// Whether the player is at a machine with a keyboard and a window, or
    /// holding one with a screen. The journal asks this for two things it would
    /// otherwise get wrong on Play Games on PC: what to call a press, and
    /// whether Back should close the game.
    /// <para>
    /// <c>Application.isMobilePlatform</c> alone can't answer it. A Play Games
    /// on PC build <em>is</em> an Android build, so it reports true — which
    /// hid the keyboard hint from the one player who has nothing else, and
    /// made Escape on the home page quit the app outright, a thing no desktop
    /// window does. Google's own answer is the
    /// <c>android.hardware.type.pc</c> system feature, the runtime twin of the
    /// manifest declaration (see <c>AndroidInputManifest</c>).
    /// </para>
    /// </summary>
    public static class DeviceForm
    {
        /// <summary>Google's feature name for "this is a PC", reported by Play Games on PC.</summary>
        private const string PcSystemFeature = "android.hardware.type.pc";

        /// <summary>Asked once: a machine does not change shape mid-run.</summary>
        private static bool? _desktopLike;

        /// <summary>
        /// True on desktop players, ChromeOS and Play Games on PC — anywhere the
        /// player has a keyboard and a window rather than a screen in their hand.
        /// </summary>
        public static bool IsDesktopLike
        {
            get
            {
                if (!_desktopLike.HasValue)
                {
                    _desktopLike = !Application.isMobilePlatform || HasPcSystemFeature();
                }

                return _desktopLike.Value;
            }
        }

        /// <summary>The verb for a press, in the player's own hardware: "tap" or "click".</summary>
        public static string PressVerb => IsDesktopLike ? "click" : "tap";

        /// <summary>
        /// How dense the screen is, for the one caller that needs to know how
        /// physically big it is (<c>JournalLayout.RoomFactor</c>, which hands a
        /// tablet more of the book than a phone).
        /// <para>
        /// <b>In the Editor, <c>Screen.dpi</c> is the DEVELOPER'S MONITOR</b> —
        /// nothing whatever to do with the game view, its resolution, or the
        /// device being previewed. A ~96dpi monitor makes every game view look
        /// like a twenty-inch screen, so the journal took the roomiest layout it
        /// has at every resolution, phone presets included, and the page was
        /// laid out for a tablet at a phone's size. Read straight, this reads
        /// the machine the game is being WRITTEN on.
        /// </para>
        /// <para>
        /// So the plain game view is answered with the density the reference
        /// resolution is measured at instead: the preview is then "this many
        /// pixels, at the density a handheld would have", which is both
        /// deterministic and the question the resolution dropdown is actually
        /// asking. A phone preset gets the phone's layout, every time, on every
        /// developer's monitor.
        /// </para>
        /// <para>
        /// The <b>Device Simulator</b> is the exception and the way to see a
        /// real tablet: it drives <c>UnityEngine.Device</c> with the chosen
        /// profile's own numbers, so the platform reads as mobile and the dpi
        /// is that device's. Everything here goes through <c>Device.Screen</c>
        /// rather than <c>Screen</c> for exactly that reason.
        /// </para>
        /// </summary>
        public static float ScreenDpi
        {
            get
            {
#if UNITY_EDITOR
                // Mobile only under the Simulator; the plain game view reports
                // the editor's own platform, and its dpi is the monitor's.
                if (!UnityEngine.Device.Application.isMobilePlatform)
                {
                    return JournalLayout.ReferenceDpi;
                }
#endif
                return UnityEngine.Device.Screen.dpi;
            }
        }

        private static bool HasPcSystemFeature()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packages = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    return packages.Call<bool>("hasSystemFeature", PcSystemFeature);
                }
            }
            catch (System.Exception error)
            {
                // A wrong guess costs a hint line and an Escape key. Taking the
                // HUD down over it would cost the run.
                Debug.LogWarning("DeviceForm: could not read the pc system feature, assuming handheld. " + error.Message);
                return false;
            }
#else
            return false;
#endif
        }
    }
}
