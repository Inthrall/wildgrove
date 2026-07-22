using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Batch texture-import tuning for the naturalist plates and UI glyphs under
    /// <c>Assets/Resources/Art</c>. They all imported at maxTextureSize 2048 with
    /// crunch off — ~2-4 MB of GPU texture each on Android, ~150-300 MB total —
    /// yet they only ever render as small HUD cards, portraits and glyphs.
    ///
    /// Per usage tier we drop the cap (Plates -> 512, UI glyphs -> 256), enable
    /// crunch, and force opaque plates onto the 4-bpp RGB path (alphaSource None).
    ///
    /// Rerunnable and idempotent — safe after adding new art. Menu items live
    /// under "Wildgrove/". Headless:
    ///   Unity.exe -batchmode -quit -buildTarget Android -projectPath . \
    ///     -executeMethod Wildgrove.EditorTools.ArtImportSettings.FixArtImportSettings
    ///   ... -executeMethod Wildgrove.EditorTools.ArtImportSettings.ReportArtTextureFootprint
    /// </summary>
    public static class ArtImportSettings
    {
        private const string ArtRoot = "Assets/Resources/Art";
        private const int PlateMaxSize = 512;   // cards / portraits
        private const int UiMaxSize = 256;      // glyphs / icons / journal furniture
        private const int CrunchQuality = 50;

        [MenuItem("Wildgrove/Fix Art Import Settings")]
        public static void FixArtImportSettings()
        {
            var paths = TexturePaths();
            var changed = 0;
            var log = new StringBuilder("[ArtImport] applying tiered import settings\n");

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in paths)
                {
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp)
                    {
                        continue;
                    }

                    var size = IsUi(path) ? UiMaxSize : PlateMaxSize;
                    var hasAlpha = imp.DoesSourceTextureHaveAlpha();

                    // Default platform (all non-overridden platforms inherit this).
                    imp.maxTextureSize = size;
                    imp.textureCompression = TextureImporterCompression.Compressed;
                    imp.crunchedCompression = true;
                    imp.compressionQuality = CrunchQuality;
                    imp.alphaSource = hasAlpha
                        ? TextureImporterAlphaSource.FromInput
                        : TextureImporterAlphaSource.None;

                    // Explicit Android override so the cap + crunch are unambiguous
                    // in the .meta and survive default-platform edits.
                    var android = imp.GetPlatformTextureSettings("Android");
                    android.overridden = true;
                    android.maxTextureSize = size;
                    android.format = TextureImporterFormat.Automatic;
                    android.textureCompression = TextureImporterCompression.Compressed;
                    android.crunchedCompression = true;
                    android.compressionQuality = CrunchQuality;
                    imp.SetPlatformTextureSettings(android);

                    EditorUtility.SetDirty(imp);
                    imp.SaveAndReimport();
                    changed++;
                    log.Append($"  {size,4}  alpha={(hasAlpha ? "yes" : "no ")}  {Rel(path)}\n");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            log.Append($"[ArtImport] done: {changed} textures updated");
            Debug.Log(log.ToString());
        }

        [MenuItem("Wildgrove/Report Art Texture Footprint")]
        public static void ReportArtTextureFootprint()
        {
            var paths = TexturePaths();
            long total = 0;
            var rows = new List<string>();

            foreach (var path in paths)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                {
                    continue;
                }

                var bytes = Profiler.GetRuntimeMemorySizeLong(tex);
                total += bytes;
                rows.Add($"{bytes / 1024.0,9:0.0} KB  {tex.width,5}x{tex.height,-5} {tex.format,-14} {Rel(path)}");
            }

            rows.Sort();
            var sb = new StringBuilder();
            sb.Append($"[ArtFootprint] platform={EditorUserBuildSettings.activeBuildTarget} " +
                      $"textures={rows.Count} TOTAL={total / (1024.0 * 1024.0):0.00} MB\n");
            foreach (var r in rows)
            {
                sb.Append("  ").Append(r).Append('\n');
            }

            Debug.Log(sb.ToString());
            // Machine-readable line for scripting the before/after diff.
            Debug.Log($"[ArtFootprint] TOTAL_BYTES={total} COUNT={rows.Count}");
        }

        private static string[] TexturePaths()
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToArray();
        }

        private static bool IsUi(string path) => path.Contains(ArtRoot + "/UI/");

        private static string Rel(string path) => path.Substring(ArtRoot.Length + 1);
    }
}
