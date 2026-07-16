using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Headless Android build entry point, for verifying the full IL2CPP +
    /// Gradle pipeline locally (editor upgrades, template changes) without a
    /// CI round-trip:
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android
    ///     -executeMethod Wildgrove.EditorTools.CiBuild.BuildAndroid
    ///
    /// Output path comes from the WILDGROVE_BUILD_PATH environment variable
    /// (default Builds/wildgrove.apk — git-ignored). Uses whatever signing the
    /// project is configured with (debug keystore when none) — release
    /// signing stays a CI concern.
    /// </summary>
    public static class CiBuild
    {
        public static void BuildAndroid()
        {
            var path = System.Environment.GetEnvironmentVariable("WILDGROVE_BUILD_PATH");
            if (string.IsNullOrEmpty(path))
            {
                path = "Builds/wildgrove.apk";
            }

            // An .apk exercises the same Gradle checks as the release .aab
            // (duplicate classes, template processing) without Play packaging.
            EditorUserBuildSettings.buildAppBundle = path.EndsWith(".aab");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            Debug.Log("[CiBuild] " + report.summary.result + " -> " + path
                      + " (" + report.summary.totalErrors + " errors)");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
