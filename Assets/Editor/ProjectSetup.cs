using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// One-shot project configuration, runnable headless:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod Wildgrove.EditorTools.ProjectSetup.Configure
    /// Idempotent — safe to re-run after editor upgrades.
    /// </summary>
    public static class ProjectSetup
    {
        public static void Configure()
        {
            PlayerSettings.companyName = "Inthrall";
            PlayerSettings.productName = "Wildgrove";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.inthrall.wildgrove");

            // Level Up requirement: Vulkan primary, GLES3 fallback only.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3
            });

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup.Configure complete: Vulkan-first Android, com.inthrall.wildgrove");
        }
    }
}
