using UnityEngine;
using UnityEngine.UI;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalSprites;

namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's uGUI construction kit — every card, row, button, panel,
    /// border and text object is built here so the pages read as layout, not
    /// GameObject plumbing. Fonts and the page-content parent are installed once
    /// by <see cref="GameHud"/> via <see cref="Init"/>; the game runs a single
    /// HUD, so holding them statically mirrors the generated-sprite caches.
    /// Shared by every journal view via <c>using static</c>.
    /// </summary>
    internal static class JournalWidgets
    {
        internal static Font BodyFont;      // body — Lora
        internal static Font SerifFont;     // titles, verses, lore — IM Fell English
        internal static Font SmallCapsFont; // chrome: eyebrows, card heads, tabs, buttons — IM Fell English SC
        internal static Font HandFont;      // margin notes, posted lines, flourishes — Caveat

        /// <summary>The open journal page — the scroll content that cards default into. Set by GameHud after the chrome is built.</summary>
        internal static RectTransform Content;

        /// <summary>Install the loaded fonts. Called once from GameHud.Awake.</summary>
        internal static void Init(Font body, Font serif, Font smallCaps, Font hand)
        {
            BodyFont = body;
            SerifFont = serif;
            SmallCapsFont = smallCaps;
            HandFont = hand;
        }

        internal static Font LoadFont(string name, Font fallback)
        {
            var font = Resources.Load<Font>("Fonts/" + name);
            return font != null ? font : fallback;
        }

        /// <summary>
        /// Global type scale. Authored sizes track the mock's rem values 1:1,
        /// but on a phone the mock's page is ~2.25x the CSS pixel size while
        /// the canvas reference gives ~1.5x — this closes the gap. Applied in
        /// MakeText and SizeOpen so every glyph scales together.
        /// </summary>
        internal const float FontScale = 1.5f;

        /// <summary>An inline rich-text size tag, scaled like MakeText sizes.</summary>
        internal static string SizeOpen(int size)
        {
            return "<size=" + Mathf.RoundToInt(size * FontScale) + ">";
        }

        internal static Text MakeText(Transform parent, string value, int size, TextAnchor anchor)
        {
            return MakeText(parent, value, size, anchor, Ink);
        }

        internal static Text MakeText(Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            return MakeText(parent, value, size, anchor, color, BodyFont);
        }

        internal static Text MakeText(Transform parent, string value, int size, TextAnchor anchor, Color color, Font font)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.RoundToInt(size * FontScale);
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
        }

        /// <summary>A journal card — the mock's bordered paper panel, with an optional small-caps head.</summary>
        internal static RectTransform Card(string head)
        {
            var go = MakePanel(head == null ? "Plate" : "Card_" + head, Content, CardPaper);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(14, 14, 10, 12);
            layout.spacing = 6;
            // The mock's double rule: an ink border on the card, a fainter
            // outline floated just outside it.
            AddBorder(go, Ink2);
            AddBorder(go, RulePaper, 5f);
            if (head != null)
            {
                var headText = MakeText(go.transform, head, 15, TextAnchor.MiddleCenter, Ink2, SmallCapsFont);
                headText.gameObject.name = "CardHead";
            }

            return (RectTransform)go.transform;
        }

        /// <summary>The action strip along a plate's bottom edge.</summary>
        internal static Transform ActionRow(RectTransform card)
        {
            var go = MakeRect("Actions", card).gameObject;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8;
            return go.transform;
        }

        internal static GameObject Row(RectTransform parent)
        {
            var go = MakeRect("Row", parent).gameObject;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(0, 0, 6, 6);
            layout.spacing = 8;
            var fitter = go.AddComponent<LayoutElement>();
            fitter.minHeight = 76;
            return go;
        }

        internal static void MakeHairline(RectTransform parent)
        {
            var go = MakePanel("Rule", parent, RulePaper);
            FixedHeight(go, 2);
            go.GetComponent<Image>().raycastTarget = false;
        }

        internal static Button Button(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = DeepPaper;
            AddBorder(go, Ink2);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            // Intrinsic height so buttons don't collapse to zero in a vertical
            // layout that controls height without force-expanding it (the sheets);
            // in rows, force-expand already governs, so this is a harmless floor.
            // 76 units ≈ a thumb-sized target on a 1080-wide phone.
            element.preferredHeight = 76;
            element.minHeight = 76;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            var label = MakeText(go.transform, text, 19, TextAnchor.MiddleCenter, Ink, SmallCapsFont);
            Stretch((RectTransform)label.transform);
            return button;
        }

        internal static void SetButtonLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
            }
        }

        internal static void SetButtonTint(Button button, bool on)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                var color = on ? DeepPaper : RulePaper;
                color.a = on ? 1f : 0.55f;
                image.color = color;
            }

            // A dead button with crisp ink still reads as live — fade the
            // label with the plate.
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                var labelColour = label.color;
                labelColour.a = on ? 1f : 0.45f;
                label.color = labelColour;
            }
        }

        internal static InputField MakeInputField(Transform parent, string value)
        {
            var go = new GameObject("Field", typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = CardPaper;
            AddBorder(go, Ink2);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 560;
            element.minWidth = 560;
            element.preferredHeight = 96;
            element.minHeight = 96;

            var text = MakeText(go.transform, value, 24, TextAnchor.MiddleLeft, Ink);
            var textRect = (RectTransform)text.transform;
            Stretch(textRect);
            textRect.offsetMin = new Vector2(14, 6);
            textRect.offsetMax = new Vector2(-14, -6);
            text.supportRichText = false;

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.text = value;
            field.characterLimit = 24;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>
        /// A dashed outline drawn just outside a control's bounds — the empty
        /// post: a pencilled box waiting for someone to stand in it. Toggle
        /// the returned object with the fallow/filled state.
        /// </summary>
        internal static GameObject AddDashedBorder(GameObject host)
        {
            var go = MakeRect("DashedBorder", (RectTransform)host.transform).gameObject;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // Breathe a little past the text so the first glyph never sits on the rule.
            rect.offsetMin = new Vector2(-10f, -4f);
            rect.offsetMax = new Vector2(10f, 4f);

            DashStrip(rect, DashSprite(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f));
            DashStrip(rect, DashSprite(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(3f, 0f));
            DashStrip(rect, DashAcrossSprite(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 3f));
            DashStrip(rect, DashAcrossSprite(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f));
            return go;
        }

        internal static void DashStrip(RectTransform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 thickness)
        {
            var go = new GameObject("Dash", typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Tiled;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            // The stretched axis follows the anchors; sizeDelta only widens the collapsed one.
            rect.sizeDelta = thickness;
        }

        /// <summary>Overlay a ruled outline on a panel. Positive inset draws it outside the bounds (the mock's offset outline).</summary>
        internal static GameObject AddBorder(GameObject host, Color color, float inset = 0f)
        {
            var go = new GameObject("Border", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(host.transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.sprite = BorderSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-inset, -inset);
            rect.offsetMax = new Vector2(inset, inset);
            return go;
        }

        internal static GameObject MakePanel(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>
        /// A naturalist plate pinned into a card: full card width, fixed height,
        /// aspect preserved (so a portrait plate letterboxes rather than
        /// stretches). Decorative — never a raycast target.
        /// </summary>
        internal static GameObject PlateImage(Transform parent, Sprite sprite, float height)
        {
            var go = new GameObject("Plate", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return go;
        }

        /// <summary>
        /// A small square glyph for a row's leading edge (craft mark, roster
        /// portrait). <paramref name="tint"/> recolours monochrome line-art —
        /// the craft glyphs are silhouettes, some white-on-transparent, so the
        /// crafts card tints them to ink; full-colour plates pass white.
        /// </summary>
        internal static GameObject IconImage(Transform parent, Sprite sprite, float size, Color tint)
        {
            var go = new GameObject("Glyph", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = size;
            element.preferredWidth = size;
            element.minHeight = size;
            element.preferredHeight = size;
            return go;
        }

        internal static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void FixedHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0;
        }

        internal static void Flexible(GameObject go, float weight)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleHeight = weight;
        }

        /// <summary>
        /// Take the row's horizontal slack (the mock's flex:1 label). Distinct
        /// from Flexible: a flexibleHeight on a row label bubbles up through
        /// the layout groups and lets the whole panel soak up screen slack.
        /// </summary>
        internal static void FlexibleWidth(GameObject go, float weight)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleWidth = weight;
        }
    }
}
