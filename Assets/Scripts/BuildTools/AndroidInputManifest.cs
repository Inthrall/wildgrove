using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Wildgrove.BuildTools
{
    /// <summary>
    /// The hardware declarations Wildgrove's Android manifest must carry, and
    /// the pure transform that puts them there. A build hook writes them into
    /// Unity's generated manifest (<c>AndroidManifestSetup</c>); the transform
    /// lives here, apart from the hook, so it can be pinned by tests.
    /// <para>
    /// They all matter for one reason: the journal can be played end to end
    /// without a finger, and Wildgrove wants to be on a Chromebook, a TV and a
    /// PC — but <b>Play reads the manifest, not the build</b>. None of that
    /// work reaches a player who is never offered the game.
    /// </para>
    /// <para>
    /// Unity does have a Player Setting for gamepad support
    /// (<c>androidGamepadSupportLevel</c>), but the editor only applies it
    /// under <c>Android TV Compatibility</c>, which Wildgrove doesn't claim —
    /// so for a non-TV build it writes nothing, and this is the mechanism left.
    /// </para>
    /// </summary>
    public static class AndroidInputManifest
    {
        /// <summary>The Android manifest attribute namespace, conventionally the <c>android:</c> prefix.</summary>
        public const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        /// <summary>The manifest-merger tooling namespace, conventionally the <c>tools:</c> prefix.</summary>
        public const string ToolsNamespace = "http://schemas.android.com/tools";

        /// <summary>Where XML itself keeps namespace declarations (<c>xmlns:…</c> attributes).</summary>
        private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

        /// <summary>
        /// How the player works the game — declared so the store knows, never
        /// required so no device is turned away:
        /// <list type="bullet">
        /// <item><c>gamepad</c> — what Play reads to tell players the game takes
        /// a controller, and what lets it be served to a TV with a pad attached.
        /// It must never be required: plenty of devices that can pair a
        /// controller don't report the feature.</item>
        /// <item><c>touchscreen</c> — undeclared, Play <em>assumes</em> a
        /// touchscreen is required and withholds the game from every device
        /// without one. Saying it out loud is what makes the pad and keyboard
        /// work worth having.</item>
        /// <item><c>type.pc</c> — turns off the mouse-to-touch compatibility
        /// layer on Play Games on PC, so a click arrives as a click. Wildgrove
        /// already reads mice directly: everything pointer-borne goes through
        /// the Input System's <c>Pointer</c>, which a mouse is. This is the only
        /// live lever for it — Unity's <c>chromeosInputEmulation</c> looked like
        /// the paired Player Setting but is
        /// <c>[Obsolete("ChromeOS is no longer supported.")]</c> in 6000.5 and
        /// serialises nothing.</item>
        /// </list>
        /// </summary>
        public static readonly IReadOnlyList<string> InputFeatures = new[]
        {
            "android.hardware.gamepad",
            "android.hardware.touchscreen",
            "android.hardware.type.pc"
        };

        /// <summary>
        /// Google's published list of hardware that doesn't exist on a PC, which
        /// a game must not require if it wants to run on Play Games on PC.
        /// <para>
        /// Wildgrove asks for none of it — which is exactly why this list can't
        /// be skipped. <b>A permission implies a required feature:</b>
        /// <c>ACCESS_WIFI_STATE</c> implies <c>android.hardware.wifi</c>,
        /// <c>READ_PHONE_STATE</c> implies telephony, and so on. The permissions
        /// in the shipped manifest come from AdMob, Firebase, Play Games and
        /// androidx.work, whose library manifests change under us between
        /// versions. Declaring the whole set not-required means an ad SDK
        /// bumping a permission can never quietly cost the game its PC audience.
        /// </para>
        /// </summary>
        public static readonly IReadOnlyList<string> HardwareAbsentOnPc = new[]
        {
            "android.hardware.wifi",
            "android.hardware.bluetooth",
            "android.hardware.camera",
            "android.hardware.location",
            "android.hardware.audio.pro",
            "android.hardware.consumerir",
            "android.hardware.nfc",
            "android.hardware.sensor.light",
            "android.hardware.sensor.accelerometer",
            "android.hardware.sensor.barometer",
            "android.hardware.sensor.compass",
            "android.hardware.sensor.gyroscope",
            "android.hardware.sensor.proximity",
            "android.hardware.telephony",
            "android.hardware.usb.accessory",
            "android.hardware.usb.host",
            "android.software.midi"
        };

        /// <summary>
        /// Everything <see cref="Declare"/> writes, each as
        /// <c>android:required="false"</c>. Declared last on purpose: static
        /// field initialisers run in source order, so the two lists above must
        /// already exist.
        /// </summary>
        public static readonly IReadOnlyList<string> OptionalFeatures = Combine(InputFeatures, HardwareAbsentOnPc);

        private static string[] Combine(IReadOnlyList<string> first, IReadOnlyList<string> second)
        {
            var combined = new string[first.Count + second.Count];
            for (var i = 0; i < first.Count; i++)
            {
                combined[i] = first[i];
            }

            for (var i = 0; i < second.Count; i++)
            {
                combined[first.Count + i] = second[i];
            }

            return combined;
        }

        /// <summary>
        /// Returns <paramref name="manifestXml"/> with every
        /// <see cref="OptionalFeatures"/> entry declared and marked not-required.
        /// Idempotent: applying it to its own output changes nothing, so it is
        /// safe on a manifest a previous build already patched.
        /// <para>
        /// Each entry carries <c>tools:replace="android:required"</c> because the
        /// app manifest is only the highest-priority input to the merger — a
        /// library manifest (Firebase, AdMob, Play Games) that declared one of
        /// these as required would otherwise win, and a required touchscreen is
        /// invisible until Play quietly stops offering the game to a Chromebook.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="manifestXml"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The document is not an Android manifest.</exception>
        public static string Declare(string manifestXml)
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

            DeclareToolsNamespace(document, root);

            foreach (var feature in OptionalFeatures)
            {
                var element = FindFeature(root, feature) ?? InsertFeature(document, root);
                SetPrefixed(document, element, "android", "name", AndroidNamespace, feature);
                SetPrefixed(document, element, "android", "required", AndroidNamespace, "false");
                SetPrefixed(document, element, "tools", "replace", ToolsNamespace, "android:required");
            }

            return Serialise(document);
        }

        private static void DeclareToolsNamespace(XmlDocument document, XmlElement root)
        {
            // GetAttribute by qualified name returns "" when absent, which is the
            // whole idempotency check — re-declaring would append a second one.
            if (!string.IsNullOrEmpty(root.GetAttribute("xmlns:tools")))
            {
                return;
            }

            var declaration = document.CreateAttribute("xmlns", "tools", XmlnsNamespace);
            declaration.Value = ToolsNamespace;
            root.Attributes.Append(declaration);
        }

        private static XmlElement FindFeature(XmlElement root, string featureName)
        {
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement element
                    && element.LocalName == "uses-feature"
                    && element.GetAttribute("name", AndroidNamespace) == featureName)
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// A new <c>uses-feature</c> element, placed above <c>application</c>
        /// where a hand-written manifest would keep it. The merger doesn't care
        /// about order; the person reading a failed build does.
        /// </summary>
        private static XmlElement InsertFeature(XmlDocument document, XmlElement root)
        {
            var element = document.CreateElement("uses-feature");

            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement candidate && candidate.LocalName == "application")
                {
                    root.InsertBefore(element, candidate);
                    return element;
                }
            }

            root.AppendChild(element);
            return element;
        }

        /// <summary>
        /// Set an attribute with an explicit prefix. Going through
        /// <c>SetAttribute(localName, namespace, value)</c> instead leaves the
        /// prefix for the writer to invent, which can serialise as
        /// <c>p1:name</c> plus a redundant namespace declaration — valid XML
        /// that no one reviewing a manifest wants to read.
        /// </summary>
        private static void SetPrefixed(
            XmlDocument document,
            XmlElement element,
            string prefix,
            string localName,
            string namespaceUri,
            string value)
        {
            var attribute = element.Attributes[localName, namespaceUri];
            if (attribute == null)
            {
                attribute = document.CreateAttribute(prefix, localName, namespaceUri);
                element.Attributes.Append(attribute);
            }

            attribute.Value = value;
        }

        private static string Serialise(XmlDocument document)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                NewLineChars = "\n",
                Encoding = new UTF8Encoding(false)
            };

            using (var text = new Utf8StringWriter())
            {
                using (var writer = XmlWriter.Create(text, settings))
                {
                    document.Save(writer);
                }

                return text.ToString();
            }
        }

        /// <summary>
        /// A <see cref="StringWriter"/> that admits to being UTF-8. Without this
        /// the declaration comes out <c>encoding="utf-16"</c> — a StringWriter's
        /// encoding is what <see cref="XmlWriter"/> writes down, whatever bytes
        /// the file is eventually saved in.
        /// </summary>
        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => new UTF8Encoding(false);
        }
    }
}
