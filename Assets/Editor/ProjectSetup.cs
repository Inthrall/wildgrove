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

            // Play 64-bit requirement: Mono only emits ARMv7, so IL2CPP is
            // mandatory for any Play upload. ARM64 ONLY: the ARMv7 ABI
            // roughly doubled IL2CPP build time for a shrinking set of
            // 32-bit-only devices — re-add AndroidArchitecture.ARMv7 if
            // Play Console vitals ever show real 32-bit demand.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup.Configure complete: Vulkan-first Android, IL2CPP ARM64-only, com.inthrall.wildgrove");
        }
    }
}
