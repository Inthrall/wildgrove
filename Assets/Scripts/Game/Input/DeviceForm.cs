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
