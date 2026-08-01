using System;
using System.Xml;
using NUnit.Framework;
using Wildgrove.BuildTools;

namespace Wildgrove.BuildTools.Tests
{
    /// <summary>
    /// The manifest patch is the only part of the pad/keyboard work that no
    /// amount of playing can verify — it lands in a generated file inside a
    /// build, and the thing it talks to is the Play Store. So it is pinned here
    /// instead: the two declarations, their not-required attribute, the merger
    /// override, and the shape of the file around them.
    /// </summary>
    public sealed class AndroidInputManifestTests
    {
        /// <summary>
        /// What Unity generates for the launcher module, trimmed to the parts
        /// this transform navigates.
        /// </summary>
        private const string UnityLauncherManifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
            + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" "
            + "android:installLocation=\"preferExternal\">\n"
            + "  <supports-screens android:smallScreens=\"true\" android:largeScreens=\"true\" />\n"
            + "  <application android:label=\"@string/app_name\" android:icon=\"@mipmap/app_icon\">\n"
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

        [Test]
        public void Declare_OnUnityLauncherManifest_MarksGamepadOptional()
        {
            var feature = FeatureNamed(AndroidInputManifest.Declare(UnityLauncherManifest), "android.hardware.gamepad");

            Assert.That(feature, Is.Not.Null, "the gamepad declaration is what Play reads to say the game takes a controller");
            Assert.That(feature.GetAttribute("required", AndroidNs), Is.EqualTo("false"));
        }

        [Test]
        public void Declare_OnUnityLauncherManifest_MarksTouchscreenOptional()
        {
            // Undeclared, Play assumes a touchscreen is required and withholds
            // the game from every device that hasn't got one.
            var feature = FeatureNamed(AndroidInputManifest.Declare(UnityLauncherManifest), "android.hardware.touchscreen");

            Assert.That(feature, Is.Not.Null);
            Assert.That(feature.GetAttribute("required", AndroidNs), Is.EqualTo("false"));
        }

        [Test]
        public void Declare_MarksThePcInputFeatureOptional()
        {
            // Turns off the mouse-to-touch compatibility layer on ChromeOS and
            // Play Games on PC. Required would strand the game on desktops.
            var feature = FeatureNamed(AndroidInputManifest.Declare(UnityLauncherManifest), "android.hardware.type.pc");

            Assert.That(feature, Is.Not.Null);
            Assert.That(feature.GetAttribute("required", AndroidNs), Is.EqualTo("false"));
        }

        [Test]
        public void Declare_EveryFeature_IsDeclaredNotRequired()
        {
            var patched = AndroidInputManifest.Declare(UnityLauncherManifest);

            foreach (var name in AndroidInputManifest.OptionalFeatures)
            {
                var feature = FeatureNamed(patched, name);
                Assert.That(feature, Is.Not.Null, name + " was not declared at all");
                Assert.That(feature.GetAttribute("required", AndroidNs), Is.EqualTo("false"), name);
            }
        }

        [Test]
        public void Declare_EachFeature_OutranksALibraryThatDisagrees()
        {
            var patched = AndroidInputManifest.Declare(UnityLauncherManifest);

            foreach (var name in AndroidInputManifest.OptionalFeatures)
            {
                var feature = FeatureNamed(patched, name);
                Assert.That(feature.GetAttribute("replace", ToolsNs), Is.EqualTo("android:required"),
                    name + " needs tools:replace, or a plugin's library manifest can harden it back to required");
            }
        }

        [Test]
        public void OptionalFeatures_IsTheInputSetPlusTheHardwareAbsentOnPc()
        {
            // OptionalFeatures is built from the other two at static-init time,
            // which is order-dependent — an accidental reorder would silently
            // leave it short.
            Assert.That(
                AndroidInputManifest.OptionalFeatures.Count,
                Is.EqualTo(AndroidInputManifest.InputFeatures.Count + AndroidInputManifest.HardwareAbsentOnPc.Count));
            Assert.That(AndroidInputManifest.OptionalFeatures, Is.Unique);
            Assert.That(AndroidInputManifest.OptionalFeatures, Has.No.Member(null));
        }

        [Test]
        public void HardwareAbsentOnPc_CoversWhatAPermissionCouldImply()
        {
            // Wildgrove asks for none of this hardware — the list exists because
            // a permission implies a required feature, and the permissions come
            // from ad and analytics libraries that change under us. These four
            // are the ones our dependency tree could plausibly imply.
            Assert.That(AndroidInputManifest.HardwareAbsentOnPc, Contains.Item("android.hardware.wifi"));
            Assert.That(AndroidInputManifest.HardwareAbsentOnPc, Contains.Item("android.hardware.telephony"));
            Assert.That(AndroidInputManifest.HardwareAbsentOnPc, Contains.Item("android.hardware.location"));
            Assert.That(AndroidInputManifest.HardwareAbsentOnPc, Contains.Item("android.hardware.bluetooth"));
        }

        [Test]
        public void Declare_UsesTheConventionalPrefixes()
        {
            // Letting the writer invent a prefix serialises as p1:required plus a
            // redundant xmlns — valid, unreadable, and unrecognisable to anyone
            // reviewing the manifest against Google's documentation.
            var patched = AndroidInputManifest.Declare(UnityLauncherManifest);

            Assert.That(patched, Does.Contain("android:name=\"android.hardware.gamepad\""));
            Assert.That(patched, Does.Contain("android:required=\"false\""));
            Assert.That(patched, Does.Contain("tools:replace=\"android:required\""));
        }

        [Test]
        public void Declare_AppliedTwice_ChangesNothingTheSecondTime()
        {
            // Every build patches the generated manifest, and a re-exported
            // Gradle project can hand back one this already ran on.
            var once = AndroidInputManifest.Declare(UnityLauncherManifest);
            var twice = AndroidInputManifest.Declare(once);

            Assert.That(twice, Is.EqualTo(once));
        }

        [Test]
        public void Declare_WhenAFeatureIsAlreadyRequired_ForcesItBackToOptional()
        {
            var hardened = UnityLauncherManifest.Replace(
                "  <supports-screens",
                "  <uses-feature android:name=\"android.hardware.touchscreen\" android:required=\"true\" />\n"
                + "  <supports-screens");
            Assert.That(hardened, Is.Not.EqualTo(UnityLauncherManifest), "the corruption must land");

            var patched = AndroidInputManifest.Declare(hardened);

            Assert.That(FeatureNamed(patched, "android.hardware.touchscreen").GetAttribute("required", AndroidNs),
                Is.EqualTo("false"));
            Assert.That(CountFeatures(patched, "android.hardware.touchscreen"), Is.EqualTo(1),
                "an existing declaration is amended, not duplicated");
        }

        [Test]
        public void Declare_DeclaresTheToolsNamespaceOnce()
        {
            var patched = AndroidInputManifest.Declare(AndroidInputManifest.Declare(UnityLauncherManifest));

            Assert.That(Occurrences(patched, "xmlns:tools="), Is.EqualTo(1));
        }

        [Test]
        public void Declare_LeavesTheLauncherActivityIntact()
        {
            // The reason this patches Unity's output instead of replacing it with
            // a hand-kept manifest: everything Player Settings decides has to
            // survive untouched.
            var patched = AndroidInputManifest.Declare(UnityLauncherManifest);

            Assert.That(patched, Does.Contain("com.unity3d.player.UnityPlayerGameActivity"));
            Assert.That(patched, Does.Contain("android.intent.category.LAUNCHER"));
            Assert.That(patched, Does.Contain("android:installLocation=\"preferExternal\""));
        }

        [Test]
        public void Declare_PlacesFeaturesAboveApplication()
        {
            var patched = AndroidInputManifest.Declare(UnityLauncherManifest);

            Assert.That(patched.IndexOf("uses-feature", StringComparison.Ordinal),
                Is.LessThan(patched.IndexOf("<application", StringComparison.Ordinal)));
        }

        [Test]
        public void Declare_KeepsTheDeclarationUtf8()
        {
            // A StringWriter's encoding is what XmlWriter writes down, so the
            // naive route stamps utf-16 onto a file saved as utf-8 bytes.
            var patched = AndroidInputManifest.Declare(UnityLauncherManifest);

            Assert.That(patched, Does.Contain("encoding=\"utf-8\""));
            Assert.That(patched, Does.Not.Contain("utf-16"));
        }

        [Test]
        public void Declare_OnSomethingThatIsNotAManifest_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => AndroidInputManifest.Declare("<resources><string name=\"app_name\">Wildgrove</string></resources>"));
        }

        [Test]
        public void Declare_OnNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AndroidInputManifest.Declare(null));
        }

        private static XmlElement FeatureNamed(string manifestXml, string featureName)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);

            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node is XmlElement element
                    && element.LocalName == "uses-feature"
                    && element.GetAttribute("name", AndroidNs) == featureName)
                {
                    return element;
                }
            }

            return null;
        }

        private static int CountFeatures(string manifestXml, string featureName)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);

            var found = 0;
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node is XmlElement element
                    && element.LocalName == "uses-feature"
                    && element.GetAttribute("name", AndroidNs) == featureName)
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
