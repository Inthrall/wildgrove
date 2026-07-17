using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Headless entry point for EDM4U's Android Resolver, via reflection so
    /// this compiles even if the Google packages are ever removed. Enables
    /// settings-template patching first: Unity 6 gradle repositories live in
    /// settingsTemplate.gradle (dependencyResolutionManagement), so without
    /// it the GeneratedLocalRepo maven repo never reaches the build and the
    /// firebase-*-unity artifacts are unresolvable.
    ///
    /// Headless: Unity.exe -batchmode -quit -projectPath . -executeMethod Wildgrove.EditorTools.AndroidResolverRunner.ForceResolve
    /// </summary>
    public static class AndroidResolverRunner
    {
        public static void ForceResolve()
        {
            var resolver = FindType("GooglePlayServices.PlayServicesResolver");
            if (resolver == null)
            {
                Debug.LogError("AndroidResolverRunner: EDM4U PlayServicesResolver not found.");
                EditorApplication.Exit(1);
                return;
            }

            EnableSettingsTemplatePatching();

            var resolveSync = resolver.GetMethod(
                "ResolveSync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            if (resolveSync == null)
            {
                Debug.LogError("AndroidResolverRunner: ResolveSync(bool) not found on PlayServicesResolver.");
                EditorApplication.Exit(1);
                return;
            }

            var succeeded = (bool)resolveSync.Invoke(null, new object[] { true });
            Debug.Log("AndroidResolverRunner: ResolveSync(force) " + (succeeded ? "succeeded" : "FAILED"));
            AssetDatabase.SaveAssets();
            if (!succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        private static void EnableSettingsTemplatePatching()
        {
            var settings = FindType("GooglePlayServices.SettingsDialog");
            if (settings == null)
            {
                Debug.LogWarning("AndroidResolverRunner: GooglePlayServices.SettingsDialog not found; cannot toggle template patching.");
                return;
            }

            foreach (var name in new[] { "PatchSettingsTemplateGradle", "PatchMainTemplateGradle" })
            {
                var property = settings.GetProperty(
                    name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(null, true);
                    Debug.Log("AndroidResolverRunner: " + name + " = true");
                }
                else
                {
                    Debug.LogWarning("AndroidResolverRunner: property " + name + " not settable on SettingsDialog.");
                }
            }
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }
    }
}
