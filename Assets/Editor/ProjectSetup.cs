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

            // Pinned, not Auto: Auto follows whatever platform the installed
            // Android module happens to ship, so an editor or module update can
            // move the target out from under a release. 36 is the newest
            // platform this editor has (34/35/36) and what Auto already resolved
            // to in shipped builds — raise it deliberately when Play's required
            // level moves.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;

            // Play 64-bit requirement: Mono only emits ARMv7, so IL2CPP is
            // mandatory for any Play upload. ARM64 ONLY: the ARMv7 ABI
            // roughly doubled IL2CPP build time for a shrinking set of
            // 32-bit-only devices — re-add AndroidArchitecture.ARMv7 if
            // Play Console vitals ever show real 32-bit demand.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // ── Large screens, foldables and Play Games on PC ────────────────
            // Level Up asks for 4:3 / 16:10 / 21:9 without letterboxing, and PC
            // hands the game a resizable window rather than a screen.
            //
            // resizeableActivity was OFF (a fresh 6000.5.5f1 project has it on,
            // so this was flipped somewhere — most likely the 6000.5.3f1 upgrade
            // churn). Off means the OS runs the game in compatibility mode:
            // letterboxed on a large screen, and a foldable can prompt the
            // player to RESTART the app on unfold. It also made the wide journal
            // spread unreachable — that layout only appears if the OS actually
            // hands over a wide window.
            PlayerSettings.Android.resizeableActivity = true;

            // Supported Aspect Ratio is deliberately NOT set here. The mode is
            // an internal property with no public API, and setting the public
            // maxAspectRatio flips the mode as a side effect (1 -> 2) — on a
            // fresh 6000.5.5f1 project the mode is 1, so that flip reads as
            // moving off Native onto a Custom cap, which is the opposite of
            // what's wanted. It is moot anyway: android:maxAspectRatio only
            // applies to a NON-resizable activity, and the line above makes
            // this one resizable. Wildgrove's stored max is 2.1 against a
            // fresh project's 2.4 — unused in Native mode, worth an eyeball in
            // Player Settings if a 21:9 device ever shows bars (todo.md).

            // Not set here: chromeosInputEmulation. It looked like the paired
            // Player Setting for the android.hardware.type.pc declaration, but
            // in 6000.5 it is [Obsolete("ChromeOS is no longer supported.")]
            // and serialises nothing — the manifest declaration is the only
            // live lever, and Play Games on PC (not ChromeOS) is the target
            // that still reports the feature.

            // Default Android texture compression to ASTC. Textures were shipping
            // uncompressed (RGBA32) — the cause of the ~230 MB build — so make the
            // project-wide default a real compressed format; universal on the
            // Vulkan/GLES3 devices we target. Per-texture overrides still win.
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup.Configure complete: Vulkan-first Android, IL2CPP ARM64-only, ASTC textures, "
                      + "resizable + 2.4 max aspect + no ChromeOS input emulation, com.inthrall.wildgrove");
        }
    }
}
