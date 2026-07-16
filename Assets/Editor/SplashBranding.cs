using UnityEditor;
using UnityEngine;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// The one piece of branding that isn't waiting on real art: the startup
    /// splash shows the mandatory "Made with Unity" logo dark-on-light over
    /// warm parchment instead of the default black — so even the first frame
    /// belongs to the field-guide world. Icons and a proper splash logo come
    /// from the hand-drawn art pass (prompts in design/art-prompts.md).
    ///
    /// Headless: Unity.exe -batchmode -quit -projectPath . -executeMethod Wildgrove.EditorTools.SplashBranding.Apply
    /// </summary>
    public static class SplashBranding
    {
        private static readonly Color Parchment = new Color32(0xEE, 0xE4, 0xCC, 0xFF);

        [MenuItem("Wildgrove/Apply Splash Branding")]
        public static void Apply()
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.backgroundColor = Parchment;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];

            AssetDatabase.SaveAssets();
            Debug.Log("SplashBranding.Apply complete: parchment splash, dark-on-light Unity logo.");
        }
    }
}
