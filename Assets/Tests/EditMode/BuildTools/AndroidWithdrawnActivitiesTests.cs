using System;
using System.Xml;
using NUnit.Framework;
using Wildgrove.BuildTools;

namespace Wildgrove.BuildTools.Tests
{
    /// <summary>
    /// The withdrawal is unverifiable by playing: what it removes is an activity
    /// the game never starts, from a file that only exists inside a build, and
    /// the proof it worked is a crash that stops arriving weeks later. So it is
    /// pinned here instead: the marker, where it sits, and that it does not
    /// disturb the launcher activity beside it.
    /// </summary>
    public sealed class AndroidWithdrawnActivitiesTests
    {
        /// <summary>
        /// What Unity generates for the launcher module, trimmed to the parts
        /// this transform navigates.
        /// </summary>
        private const string UnityLauncherManifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
            + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n"
            + "  <application android:label=\"@string/app_name\">\n"
            + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\">\n"
            + "      <intent-filter>\n"
            + "        <action android:name=\"android.intent.action.MAIN\" />\n"
            + "        <category android:name=\"android.intent.category.LAUNCHER\" />\n"
            + "      </intent-filter>\n"
            + "    </activity>\n"
            + "  </application>\n"
            + "</manifest>\n";

        private const string AndroidNs = AndroidInputManifest.AndroidNamespace;
        private const string ToolsNs = AndroidInputManifest.ToolsNamespace;

        private const string NativeBridge = "com.google.games.bridge.NativeBridgeActivity";

        [Test]
        public void Withdrawn_CarriesThePlayGamesNativeBridge()
        {
            // The one activity in the build that cannot survive being started:
            // its class initialiser loads libgpg.so, which no build has shipped.
            Assert.That(AndroidWithdrawnActivities.Withdrawn, Contains.Item(NativeBridge));
        }

        [Test]
        public void Withdrawn_LeavesTheFriendsResolutionActivityAlone()
        {
            // Live plugin code the game merely doesn't reach yet. Withdrawing it
            // would trade a crash nobody sees for an ActivityNotFoundException
            // the day a friends list is added.
            Assert.That(AndroidWithdrawnActivities.Withdrawn,
                Has.No.Member("com.google.games.bridge.GenericResolutionActivity"));
        }

        [Test]
        public void Withdraw_MarksEveryWithdrawnActivityForRemoval()
        {
            var withdrawn = AndroidWithdrawnActivities.Withdraw(UnityLauncherManifest);

            foreach (var name in AndroidWithdrawnActivities.Withdrawn)
            {
                var activity = ActivityNamed(withdrawn, name);
                Assert.That(activity, Is.Not.Null, name + " was not named in the manifest at all");
                Assert.That(activity.GetAttribute("node", ToolsNs), Is.EqualTo("remove"), name);
            }
        }

        [Test]
        public void Withdraw_PutsTheRemovalInsideApplication()
        {
            // tools:node="remove" only names an activity from within
            // <application>; at the top level the merger has nothing to match.
            var activity = ActivityNamed(AndroidWithdrawnActivities.Withdraw(UnityLauncherManifest), NativeBridge);

            Assert.That(activity, Is.Not.Null);
            Assert.That(activity.ParentNode.LocalName, Is.EqualTo("application"));
        }

        [Test]
        public void Withdraw_UsesTheConventionalPrefixes()
        {
            var withdrawn = AndroidWithdrawnActivities.Withdraw(UnityLauncherManifest);

            Assert.That(withdrawn, Does.Contain("android:name=\"" + NativeBridge + "\""));
            Assert.That(withdrawn, Does.Contain("tools:node=\"remove\""));
        }

        [Test]
        public void Withdraw_AppliedTwice_ChangesNothingTheSecondTime()
        {
            var once = AndroidWithdrawnActivities.Withdraw(UnityLauncherManifest);
            var twice = AndroidWithdrawnActivities.Withdraw(once);

            Assert.That(twice, Is.EqualTo(once));
            Assert.That(CountActivities(twice, NativeBridge), Is.EqualTo(1),
                "a second pass must amend the existing element, not add another");
        }

        [Test]
        public void Withdraw_AfterDeclare_KeepsOneToolsNamespace()
        {
            // Both transforms run over the same file on every build. Each
            // declaring its own xmlns:tools is valid XML that aapt2 rejects.
            var patched = AndroidWithdrawnActivities.Withdraw(AndroidInputManifest.Declare(UnityLauncherManifest));

            Assert.That(Occurrences(patched, "xmlns:tools="), Is.EqualTo(1));
            Assert.That(patched, Does.Contain("tools:replace=\"android:required\""), "the input declarations survive");
            Assert.That(patched, Does.Contain("tools:node=\"remove\""));
        }

        [Test]
        public void Withdraw_LeavesTheLauncherActivityIntact()
        {
            var withdrawn = AndroidWithdrawnActivities.Withdraw(UnityLauncherManifest);

            Assert.That(withdrawn, Does.Contain("com.unity3d.player.UnityPlayerGameActivity"));
            Assert.That(withdrawn, Does.Contain("android.intent.category.LAUNCHER"));
            Assert.That(ActivityNamed(withdrawn, "com.unity3d.player.UnityPlayerGameActivity")
                    .GetAttribute("node", ToolsNs),
                Is.Empty, "the launcher activity must not pick up the removal marker");
        }

        [Test]
        public void Withdraw_WhenTheManifestHasNoApplication_StillLandsTheRemoval()
        {
            // AndroidManifestSetup falls back to the library manifest when there
            // is no launcher module, and that one can arrive without one.
            var bare = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                       + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" />\n";

            var withdrawn = AndroidWithdrawnActivities.Withdraw(bare);

            Assert.That(ActivityNamed(withdrawn, NativeBridge), Is.Not.Null);
        }

        [Test]
        public void Withdraw_KeepsTheDeclarationUtf8()
        {
            var withdrawn = AndroidWithdrawnActivities.Withdraw(UnityLauncherManifest);

            Assert.That(withdrawn, Does.Contain("encoding=\"utf-8\""));
            Assert.That(withdrawn, Does.Not.Contain("utf-16"));
        }

        [Test]
        public void Withdraw_OnSomethingThatIsNotAManifest_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => AndroidWithdrawnActivities.Withdraw("<resources><string name=\"app_name\">Wildgrove</string></resources>"));
        }

        [Test]
        public void Withdraw_OnNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AndroidWithdrawnActivities.Withdraw(null));
        }

        private static XmlElement ActivityNamed(string manifestXml, string activityName)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);

            foreach (XmlNode node in document.GetElementsByTagName("activity"))
            {
                if (node is XmlElement element && element.GetAttribute("name", AndroidNs) == activityName)
                {
                    return element;
                }
            }

            return null;
        }

        private static int CountActivities(string manifestXml, string activityName)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);

            var found = 0;
            foreach (XmlNode node in document.GetElementsByTagName("activity"))
            {
                if (node is XmlElement element && element.GetAttribute("name", AndroidNs) == activityName)
                {
                    found++;
                }
            }

            return found;
        }

        private static int Occurrences(string text, string needle)
        {
            var found = 0;
            var at = text.IndexOf(needle, StringComparison.Ordinal);
            while (at >= 0)
            {
                found++;
                at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
            }

            return found;
        }
    }
}
