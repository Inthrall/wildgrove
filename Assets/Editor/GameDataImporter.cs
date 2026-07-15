using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Generates the runtime GameDataAsset ScriptableObject from the authoring
    /// JSON in design/data (the source of truth, editable outside Unity).
    /// Runs on editor load and before every build; also available from the
    /// Wildgrove menu. Refuses to write an asset from invalid data, so the
    /// last-good asset survives a bad edit.
    /// </summary>
    public static class GameDataImporter
    {
        public const string SourceDir = "design/data";
        public const string AssetPath = "Assets/Resources/Data/GameData.asset";

        [InitializeOnLoadMethod]
        private static void ImportOnEditorLoad()
        {
            foreach (var issue in Import())
            {
                Debug.LogError($"Design data: {issue}");
            }
        }

        [MenuItem("Wildgrove/Import Design Data")]
        public static void ImportFromMenu()
        {
            var issues = Import(force: true);
            foreach (var issue in issues)
            {
                Debug.LogError($"Design data: {issue}");
            }

            if (issues.Count == 0)
            {
                Debug.Log("GameDataImporter: design data valid, asset up to date.");
            }
        }

        /// <summary>Parses, validates, and (when valid and changed) regenerates the asset. Returns validation issues.</summary>
        public static IReadOnlyList<string> Import(bool force = false)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var sources = GameData.ReadSourcesFromFiles(Path.Combine(projectRoot, SourceDir));
            var hash = GameData.ComputeSourceHash(sources);

            var existing = AssetDatabase.LoadAssetAtPath<GameDataAsset>(AssetPath);
            if (!force && existing != null && existing.sourceHash == hash)
            {
                return new List<string>();
            }

            var data = GameData.Parse(sources);
            var issues = GameDataValidator.Validate(data);
            if (issues.Count > 0)
            {
                return issues;
            }

            var asset = existing != null ? existing : ScriptableObject.CreateInstance<GameDataAsset>();
            GameDataMapper.Populate(asset, data);
            asset.sourceHash = hash;

            if (existing == null)
            {
                Directory.CreateDirectory(Path.Combine(projectRoot, Path.GetDirectoryName(AssetPath)));
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"GameDataImporter: regenerated {AssetPath} from {SourceDir}");
            return issues;
        }
    }

    /// <summary>Fails the build if the design data is missing, malformed, or incoherent.</summary>
    public sealed class GameDataImportBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var issues = GameDataImporter.Import(force: true);
            if (issues.Count > 0)
            {
                throw new BuildFailedException("Design data validation failed:\n" + string.Join("\n", issues));
            }
        }
    }
}
