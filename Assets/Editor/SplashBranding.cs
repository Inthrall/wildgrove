using UnityEditor;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
using UnityEditor.Build;
#endif

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Wires the hand-drawn brand art (Assets/Art/Brand — generated from the
    /// briefs in design/art-prompts.md) into Player Settings: Android
    /// adaptive/round/legacy icons and the startup splash. The splash shows
    /// the ringed mark + wordmark over warm parchment, with the mandatory
    /// "Made with Unity" logo dark-on-light so it sits with the paper.
    /// Store-listing images (Play icon, feature graphic) live in
    /// design/store/ — uploaded via Play Console, not built into the app.
    ///
    /// Headless: Unity.exe -batchmode -quit -projectPath . -executeMethod Wildgrove.EditorTools.SplashBranding.Apply
    /// </summary>
    public static class SplashBranding
    {
        private const string ArtDir = "Assets/Art/Brand";
        private static readonly Color Parchment = new Color32(0xEE, 0xE4, 0xCC, 0xFF);

        [MenuItem("Wildgrove/Apply Splash + Icon Branding")]
        public static void Apply()
        {
            ConfigureImporters();
            AssignIcons();
            ConfigureSplash();

            AssetDatabase.SaveAssets();
            Debug.Log("SplashBranding.Apply complete: brand art wired into icons + splash.");
        }

        private static void ConfigureImporters()
        {
            foreach (var name in new[] { "icon-foreground.png", "icon-background.png", "icon-legacy.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(ArtDir + "/" + name);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var splash = (TextureImporter)AssetImporter.GetAtPath(ArtDir + "/splash-logo.png");
            splash.textureType = TextureImporterType.Sprite;
            splash.alphaIsTransparency = true;
            splash.mipmapEnabled = false;
            splash.textureCompression = TextureImporterCompression.Uncompressed;
            splash.SaveAndReimport();
        }

        // AndroidPlatformIconKind ships with the Android build-support module,
        // which the CI test image doesn't have — compile the icon wiring out
        // when the active target isn't Android.
#if UNITY_ANDROID
        private static void AssignIcons()
        {
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/icon-foreground.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/icon-background.png");
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/icon-legacy.png");

            var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive)
            {
                icon.SetTextures(background, foreground);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);

            var round = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Round);
            foreach (var icon in round)
            {
                icon.SetTextures(legacy);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Round, round);

            var legacyKind = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Legacy);
            foreach (var icon in legacyKind)
            {
                icon.SetTextures(legacy);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Legacy, legacyKind);
        }
#else
        private static void AssignIcons()
        {
            Debug.LogWarning("SplashBranding: active build target is not Android — icon assignment skipped.");
        }
#endif

        private static void ConfigureSplash()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/splash-logo.png");

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.backgroundColor = Parchment;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.logos = sprite == null
                ? new PlayerSettings.SplashScreenLogo[0]
                : new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, sprite) };
        }
    }
}
