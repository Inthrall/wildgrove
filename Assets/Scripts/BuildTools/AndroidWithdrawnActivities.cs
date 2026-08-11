using System;
using System.Collections.Generic;
using System.Xml;

namespace Wildgrove.BuildTools
{
    /// <summary>
    /// Activities a bundled plugin declares in its library manifest that this
    /// game can never legitimately start, and the pure transform that withdraws
    /// them from the merged manifest. The build hook applies it
    /// (<c>AndroidManifestSetup</c>); the transform lives here so it can be
    /// pinned by tests, the same split <see cref="AndroidInputManifest"/> uses.
    /// <para>
    /// <b>A declared activity is a door, whether or not the game knocks on it.</b>
    /// Anything holding the package's own UID can start one, and something
    /// does: each entry below crashed at a rate of almost exactly one per
    /// release build, which is the fingerprint of an automated crawl (Play's
    /// pre-launch report walks the activities it finds declared) rather than of
    /// players. The crash lands in Crashlytics and against the crash-free rate
    /// all the same. Not declaring the door is the only way to shut it.
    /// </para>
    /// </summary>
    public static class AndroidWithdrawnActivities
    {
        /// <summary>
        /// The withdrawn set, by fully-qualified class name.
        /// <list type="bullet">
        /// <item><c>com.google.games.bridge.NativeBridgeActivity</c>. Its
        /// <c>&lt;clinit&gt;</c> is <c>System.loadLibrary("gpg")</c>, and
        /// <b>no build of this game has ever contained a libgpg.so</b>:
        /// <c>gpgs-plugin-support.aar</c> carries no <c>jni/</c> at all. It is
        /// left over from the v1 native C++ SDK, and nothing reaches it any
        /// more: v2 resolves everything through <c>HelperFragment</c>, which
        /// never names it, and neither does any of the plugin's C#. The 2.2.0
        /// manifest template emits the declaration all the same, so every build
        /// ships an activity that cannot survive being started. Starting it is
        /// an unconditional <c>UnsatisfiedLinkError</c>, so there is nothing
        /// here to preserve.
        /// </item>
        /// </list>
        /// <para>
        /// <c>GenericResolutionActivity</c> deliberately is <em>not</em> here.
        /// It NPEs the same way when started without its PendingIntent extra and
        /// the game never reaches it (its only caller is
        /// <c>HelperFragment.askForLoadFriendsResolution</c>, and Wildgrove asks
        /// for no friends data), but unlike the bridge it is live code that
        /// would work if it were ever wanted. Withdrawing it would trade a crash
        /// nobody sees for an <c>ActivityNotFoundException</c> the day someone
        /// adds a friends list. Add it here if that day is not coming.
        /// </para>
        /// </summary>
        public static readonly IReadOnlyList<string> Withdrawn = new[]
        {
            "com.google.games.bridge.NativeBridgeActivity"
        };

        /// <summary>
        /// Returns <paramref name="manifestXml"/> with every
        /// <see cref="Withdrawn"/> activity marked <c>tools:node="remove"</c>.
        /// Idempotent: applying it to its own output changes nothing.
        /// <para>
        /// It marks rather than deletes because at this point in the build there
        /// is nothing to delete: Unity exports the Gradle project and the
        /// <em>merger</em> pulls the plugins' library manifests in afterwards,
        /// so the declaration this is aimed at does not exist in the file yet.
        /// <c>tools:node="remove"</c> is the instruction that outlives the merge,
        /// and it is also what makes the fix survive the Play Games setup window
        /// regenerating <c>GooglePlayGamesManifest.androidlib</c> from its
        /// template.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="manifestXml"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The document is not an Android manifest.</exception>
        public static string Withdraw(string manifestXml)
        {
            if (manifestXml == null)
            {
                throw new ArgumentNullException(nameof(manifestXml));
            }

            var document = new XmlDocument();
            document.LoadXml(manifestXml);

            var root = document.DocumentElement;
            if (root == null || root.LocalName != "manifest")
            {
                throw new InvalidOperationException(
                    "Not an Android manifest: root element is <"
                    + (root == null ? "" : root.Name)
                    + ">, expected <manifest>.");
            }

            AndroidInputManifest.DeclareToolsNamespace(document, root);

            var application = FindApplication(root) ?? AppendApplication(document, root);
            foreach (var activityName in Withdrawn)
            {
                var element = FindActivity(application, activityName) ?? AppendActivity(document, application);
                AndroidInputManifest.SetPrefixed(
                    document, element, "android", "name", AndroidInputManifest.AndroidNamespace, activityName);
                AndroidInputManifest.SetPrefixed(
                    document, element, "tools", "node", AndroidInputManifest.ToolsNamespace, "remove");
            }

            return AndroidInputManifest.Serialise(document);
        }

        private static XmlElement FindApplication(XmlElement root)
        {
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement element && element.LocalName == "application")
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// A manifest with no <c>application</c> element still merges, and the
        /// removal has to live inside one to name an activity. Unity's launcher
        /// manifest always has it; the fallback exists so the transform is total
        /// rather than silently doing nothing on the library manifest
        /// <c>AndroidManifestSetup</c> drops to when there is no launcher module.
        /// </summary>
        private static XmlElement AppendApplication(XmlDocument document, XmlElement root)
        {
            var element = document.CreateElement("application");
            root.AppendChild(element);
            return element;
        }

        private static XmlElement FindActivity(XmlElement application, string activityName)
        {
            foreach (XmlNode node in application.ChildNodes)
            {
                if (node is XmlElement element
                    && element.LocalName == "activity"
                    && element.GetAttribute("name", AndroidInputManifest.AndroidNamespace) == activityName)
                {
                    return element;
                }
            }

            return null;
        }

        private static XmlElement AppendActivity(XmlDocument document, XmlElement application)
        {
            var element = document.CreateElement("activity");
            application.AppendChild(element);
            return element;
        }
    }
}
