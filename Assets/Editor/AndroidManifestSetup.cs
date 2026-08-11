using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using Wildgrove.BuildTools;

namespace Wildgrove.EditorTools
{
    /// <summary>
    /// Writes Wildgrove's input declarations into the generated Android app
    /// manifest on every build (see <see cref="AndroidInputManifest"/> for what
    /// and why), and withdraws the plugin-declared activities the game can never
    /// start (see <see cref="AndroidWithdrawnActivities"/>). Unity owns that
    /// manifest — it composes it from Player Settings
    /// and the installed plugins' library manifests — so this patches the output
    /// rather than replacing it with a hand-kept copy in
    /// <c>Assets/Plugins/Android/</c>: a copy would silently stop tracking the
    /// activity, theme, orientation and splash that Player Settings decides.
    /// <para>
    /// A missing manifest fails the build rather than warning. The declarations
    /// are compliance, and a compliance line that quietly didn't get written is
    /// indistinguishable from one that was never asked for — the same reason
    /// <c>GameDataImporter</c> fails the build on invalid design data.
    /// </para>
    /// </summary>
    public sealed class AndroidManifestSetup : IPostGenerateGradleAndroidProject
    {
        /// <summary>
        /// Late, so the plugin resolvers (Firebase, AdMob, Play Games) have
        /// written their manifests first and this patch is what lands last.
        /// </summary>
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = ResolveAppManifest(path);
            if (manifestPath == null)
            {
                throw new BuildFailedException(
                    "AndroidManifestSetup: no generated AndroidManifest.xml found under "
                    + path + " — the gamepad and touchscreen declarations would have been "
                    + "dropped silently. Check the generated Gradle project layout.");
            }

            var before = File.ReadAllText(manifestPath);
            var after = AndroidWithdrawnActivities.Withdraw(AndroidInputManifest.Declare(before));
            if (after != before)
            {
                // No BOM: aapt2 reads the manifest as plain UTF-8.
                File.WriteAllText(manifestPath, after, new UTF8Encoding(false));
            }

            Debug.Log("AndroidManifestSetup: declared "
                      + string.Join(", ", AndroidInputManifest.OptionalFeatures)
                      + " as optional, and withdrew "
                      + string.Join(", ", AndroidWithdrawnActivities.Withdrawn)
                      + ", in " + manifestPath);
        }

        /// <summary>
        /// The manifest that ends up highest priority in the merge. Unity hands
        /// this callback the <c>unityLibrary</c> module, so the app manifest is
        /// the sibling <c>launcher</c> module's — but since a miss fails the
        /// build, the gradle project root is tried as well, and a project with
        /// no launcher at all (an exported library) falls back to
        /// unityLibrary's own manifest, where the entries still merge upward.
        /// They just can't out-rank a plugin that disagrees there, which is
        /// what <c>tools:replace</c> is for.
        /// </summary>
        private static string ResolveAppManifest(string generatedProjectPath)
        {
            var parent = Path.GetDirectoryName(generatedProjectPath);
            var candidates = new[]
            {
                // path is unityLibrary (what Unity documents), launcher alongside.
                parent == null ? null : Path.Combine(parent, "launcher", "src", "main", "AndroidManifest.xml"),
                // path is the gradle project root.
                Path.Combine(generatedProjectPath, "launcher", "src", "main", "AndroidManifest.xml"),
                // No launcher module: patch the library's own manifest instead.
                Path.Combine(generatedProjectPath, "src", "main", "AndroidManifest.xml"),
                Path.Combine(generatedProjectPath, "unityLibrary", "src", "main", "AndroidManifest.xml")
            };

            foreach (var candidate in candidates)
            {
                if (candidate != null && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
