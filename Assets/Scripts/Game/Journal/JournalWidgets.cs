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
        /// but the mock was proofed on a desktop monitor — at 1.5 the working
        /// text (ledger, buttons, card heads) landed at 9–13sp on a real phone,
        /// under Android's ~12sp legibility floor. 2.0 puts the small end of
        /// the scale on the right side of that floor; the big end (titles)
        /// compensates with smaller authored sizes. Applied in MakeText and
        /// SizeOpen so every glyph scales together.
        /// </summary>
        internal const float FontScale = 2f;

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

        /// <summary>
        /// The square grid a drawer of plates is laid out on, sized to whatever
        /// width the page has. Shared by the Stores drawer and the Record page's
        /// collections because they are the same object seen twice — a drawer of
        /// specimens — and a grid that fitted its cells differently on one of
        /// them would read as a different kind of thing.
        /// </summary>
        internal static RectTransform Grid(RectTransform card, float idealCell)
        {
            var go = MakeRect("Grid", card).gameObject;
            var grid = go.AddComponent<SquareCellGrid>();
            grid.idealCell = idealCell;
            grid.spacing = new Vector2(8f, 8f);
            grid.childAlignment = TextAnchor.UpperLeft;
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// The action strip along a plate's bottom edge — what can be bought
        /// here, pinned right. The plates size themselves to their words
        /// (childControlWidth, no force-expand), so the strip is as wide as
        /// whatever is showing. Pinned left, that left a ragged gutter down the
        /// right of every card; centred (until 2026-08-14) it left BOTH edges
        /// ragged, so a column of nodes had no straight line anywhere on it,
        /// each card's "Plant back" starting and ending at whatever width its
        /// own planter name and price happened to make. Right puts the last plate
        /// under the post plate above it, which is already flush right (Row
        /// gives the label the flexible width), so every card reads down one
        /// margin and only the interior is ragged.
        /// </summary>
        internal static Transform ActionRow(RectTransform card)
        {
            var go = MakeRect("Actions", card).gameObject;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 8;
            return go.transform;
        }

        /// <summary>The height a row holds when its contents ask for nothing more.</summary>
        private const float RowFloor = 76f;

        internal static GameObject Row(RectTransform parent)
        {
            return Row(parent, RowFloor);
        }

        /// <summary>
        /// A row that opens at a height of its own rather than the shared floor —
        /// for rows built from fixed furniture (a plate, a cost strip, a button)
        /// whose tallest piece is known at build time. Handed the height it will
        /// hold anyway, the row never has to grow into it while the player is
        /// reading, which is what growing looks like from the outside: a lurch.
        /// </summary>
        internal static GameObject Row(RectTransform parent, float floorHeight)
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
            fitter.minHeight = floorHeight;
            // Grow-only, so a label that rewrites itself four times a second
            // can't drag every card below it up and down the page. The
            // LayoutElement above stays as the fallback if this goes unwired.
            var settled = go.AddComponent<HeightSettledElement>();
            settled.source = layout;
            settled.floorHeight = floorHeight;
            return go;
        }

        /// <summary>
        /// A stack of lines inside a row — a name over the state it is in. The
        /// pieces are separate <see cref="Text"/> objects rather than one label
        /// with a newline in it so that a clause appearing or leaving changes
        /// only its own line, never where the line above it wraps.
        /// </summary>
        internal static GameObject Column(Transform parent)
        {
            var go = MakeRect("Column", (RectTransform)parent).gameObject;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 2;
            return go;
        }

        internal static GameObject MakeHairline(RectTransform parent)
        {
            var go = MakePanel("Rule", parent, RulePaper);
            FixedHeight(go, 2);
            go.GetComponent<Image>().raycastTarget = false;
            return go;
        }

        /// <summary>The width of a fold heading's left margin, and the chevron standing in it.</summary>
        private const float FoldArrowLane = 56f;
        private const float FoldArrowGlyph = 34f;

        /// <summary>
        /// Pin the fold chevron into a heading's left margin — down while the
        /// section is open, turned a quarter to the right while it is shut.
        /// <para>
        /// The mark ignores the layout and the name is inset by the same lane on
        /// BOTH sides, so a centred heading stays centred over its contents
        /// instead of shunting right by half an arrow.
        /// </para>
        /// <para>
        /// Shared by the Trail's grounds and the Record page's cards: which way
        /// a section is folded is legible only by inference otherwise — from
        /// whether anything follows the heading — and a section with nothing
        /// under it then reads as an unresponsive button rather than an empty
        /// open one.
        /// </para>
        /// </summary>
        internal static void AddFoldArrow(Button heading, bool open)
        {
            var label = heading.GetComponentInChildren<Text>();
            if (label != null)
            {
                var labelRect = (RectTransform)label.transform;
                labelRect.offsetMin = new Vector2(FoldArrowLane, labelRect.offsetMin.y);
                labelRect.offsetMax = new Vector2(-FoldArrowLane, labelRect.offsetMax.y);
            }

            var go = new GameObject("FoldArrow", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(heading.transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.sprite = FoldArrowSprite();
            image.preserveAspect = true;
            // The plate takes the tap; a chevron that swallowed it would leave a
            // dead spot in the middle of the control it describes.
            image.raycastTarget = false;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(FoldArrowGlyph, FoldArrowGlyph);
            rect.anchoredPosition = new Vector2(FoldArrowLane * 0.5f, 0f);
            // Drawn pointing down; a quarter turn anticlockwise points it at the
            // name, which is where a shut section's contents have gone.
            rect.localRotation = Quaternion.Euler(0f, 0f, open ? 0f : 90f);
        }

        /// <summary>The width of a fold heading's right margin, and the specimen mark standing in it.</summary>
        private const float HeadingMarkLane = 130f;
        private const float HeadingMarkWidth = 112f;
        private const float HeadingMarkHeight = 76f;

        /// <summary>
        /// Pin a specimen mark into a heading's right margin — the chevron's
        /// opposite number, so a folded section is named on the left and shown
        /// on the right.
        /// <para>
        /// Call this AFTER <see cref="AddFoldArrow"/>: both widen the name's
        /// inset and the later call wins, so the wider lane has to be the one
        /// set last. The inset goes on BOTH sides for the reason the chevron's
        /// does — a centred heading must stay centred over its contents rather
        /// than shunting left by half a mark.
        /// </para>
        /// <para>
        /// The mark is fitted by width rather than height: the plates are
        /// specimen drawings of wildly different proportion, a trout beside a
        /// standing reed, and letting each fill the lane's width keeps them one
        /// family of marks instead of a fish the size of a thumbnail next to a
        /// reed the height of the bar.
        /// </para>
        /// </summary>
        internal static void AddHeadingMark(Button heading, Sprite mark)
        {
            if (mark == null)
            {
                return;
            }

            var label = heading.GetComponentInChildren<Text>();
            if (label != null)
            {
                var labelRect = (RectTransform)label.transform;
                labelRect.offsetMin = new Vector2(HeadingMarkLane, labelRect.offsetMin.y);
                labelRect.offsetMax = new Vector2(-HeadingMarkLane, labelRect.offsetMax.y);
            }

            var go = new GameObject("HeadingMark", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(heading.transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.sprite = mark;
            image.preserveAspect = true;
            // The plate takes the tap, as with the chevron — a mark that
            // swallowed it would leave a dead spot in the control it describes.
            image.raycastTarget = false;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(HeadingMarkWidth, HeadingMarkHeight);
            rect.anchoredPosition = new Vector2(-HeadingMarkLane * 0.5f, 0f);
        }

        internal static Button Button(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
        {
            var button = ButtonPlate(parent, width, onClick);
            var label = MakeText(button.transform, text, 19, TextAnchor.MiddleCenter, Ink, SmallCapsFont);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            // Wrap inside the plate, not against its border — and let the plate
            // grow when the label needs a third line rather than spilling the
            // text over the cards above and below it (see LabelFittedElement).
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-10, 0);
            FitToLabel(button, label, 120);
            return button;
        }

        /// <summary>
        /// A button that carries pictures beside its words: a leading plate (the
        /// animal), the label, and a trailing plate (what it works at). Either
        /// sprite may be null — the plate is simply left off, which is how "rests
        /// at camp" reads as nothing at all rather than a word.
        ///
        /// The posting sheet is why this exists: eight candidate rows spelling
        /// out species and station in prose was a wall of text for a question
        /// ("who walks here?") that pictures answer at a glance.
        /// </summary>
        internal static Button PictureButton(Transform parent, Sprite lead, string text, Sprite trail,
            float width, float glyph, UnityEngine.Events.UnityAction onClick)
        {
            var button = ButtonPlate(parent, width, onClick);
            var label = PictureStrip(button.transform, lead, text, trail, glyph, TextAnchor.MiddleCenter);
            // The pictures set the floor here, not the touch minimum — a plate
            // shorter than its own glyph would clip the animal.
            FitToLabel(button, label, Mathf.Max(120f, glyph + 16f));
            return button;
        }

        /// <summary>
        /// A button whose whole face is one picture — the trail's post plates,
        /// which wear whoever stands on the ground instead of naming the act.
        /// A body has a portrait and an empty post has the moss (+), so the
        /// plate answers "who is here?" without the row having to spend its
        /// width on a verb that never changes meaning.
        /// <para>
        /// <paramref name="fallback"/> is the words the plate falls back on when
        /// there is no picture for what it carries — <see cref="ArtLibrary"/>
        /// returns null for art not yet drawn, and a button is the one place
        /// where showing nothing is not an option. Swap the picture live with
        /// <see cref="SetButtonGlyph"/>.
        /// </para>
        /// </summary>
        internal static Button GlyphButton(Transform parent, Sprite sprite, string fallback, float width, float glyph,
            UnityEngine.Events.UnityAction onClick)
        {
            var button = ButtonPlate(parent, width, onClick);

            // The words live on the plate whether or not they are showing, so a
            // picture that goes missing at refresh has something to fall back to.
            var label = MakeText(button.transform, string.Empty, 19, TextAnchor.MiddleCenter, Ink, SmallCapsFont);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-10, 0);
            // The picture sets the floor, not the touch minimum — a plate
            // shorter than its own glyph would clip the animal.
            FitToLabel(button, label, Mathf.Max(120f, glyph + 16f));

            // No layout group on the plate, so the picture is anchored by hand,
            // over the label's own rect — only ever one of the two is showing.
            var icon = IconImage(button.transform, sprite, glyph, Color.white);
            var rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(glyph, glyph);

            SetButtonGlyph(button, sprite, fallback);
            return button;
        }

        /// <summary>
        /// Re-picture a <see cref="GlyphButton"/> — the post plates do this on
        /// the refresh cadence, as bodies come and go from the ground.
        /// </summary>
        internal static void SetButtonGlyph(Button button, Sprite sprite, string fallback)
        {
            var glyph = button.transform.Find("Glyph");
            if (glyph != null)
            {
                var image = glyph.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    // An Image holding no sprite draws a plain white square, so
                    // the picture is switched off rather than emptied.
                    image.enabled = sprite != null;
                }
            }

            SetButtonLabel(button, sprite != null ? string.Empty : fallback);
        }

        /// <summary>
        /// <see cref="PictureButton"/>'s strip without the tap — the same
        /// pictures-either-side-of-words line for a heading that states what a
        /// row would choose. Returns the label so the caller can style it.
        /// </summary>
        internal static Text PictureRow(Transform parent, Sprite lead, string text, Sprite trail, float glyph,
            float width)
        {
            var go = MakeRect("PictureRow", (RectTransform)parent).gameObject;
            // The heading's own width, not the words' — a strip that hugged its
            // text would stand its plates somewhere other than the rows below,
            // and the point of the heading is that it matches them.
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            return PictureStrip(go.transform, lead, text, trail, glyph, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// Lay out plate · words · plate across a host. A missing plate leaves an
        /// EMPTY plate of the same size rather than nothing: the words then sit
        /// in the same place on every row, so a column of them reads as a column
        /// even where one side has no picture to show.
        /// </summary>
        private static Text PictureStrip(Transform host, Sprite lead, string text, Sprite trail, float glyph,
            TextAnchor anchor)
        {
            var layout = host.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 10;

            PictureSlot(host, lead, glyph);
            var label = MakeText(host, text, 19, anchor, Ink, SmallCapsFont);
            FlexibleWidth(label.gameObject, 1f);
            PictureSlot(host, trail, glyph);
            return label;
        }

        /// <summary>One picture's worth of room — the plate when there is one, blank space when there isn't.</summary>
        internal static void PictureSlot(Transform parent, Sprite sprite, float glyph)
        {
            if (sprite != null)
            {
                IconImage(parent, sprite, glyph, Color.white);
                return;
            }

            var go = MakeRect("Empty", (RectTransform)parent).gameObject;
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = glyph;
            element.preferredWidth = glyph;
        }

        /// <summary>
        /// A centred row of small discs — a short countable ladder said as
        /// marks rather than as "n of m" in words, which is how a player can
        /// see at a glance that there are places beyond the ones they hold.
        /// Built blank: the caller tints each disc to what it stands for, and
        /// keeps the array so it can repaint them as the ladder moves.
        /// </summary>
        internal static Image[] MarkRow(Transform parent, int count, float size)
        {
            var go = MakeRect("Marks", parent).gameObject;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10;

            var marks = new Image[count > 0 ? count : 0];
            for (var index = 0; index < marks.Length; index++)
            {
                marks[index] = IconImage(go.transform, DiscSprite(), size, Ink).GetComponent<Image>();
            }

            return marks;
        }

        // ── Cost strips ───────────────────────────────────────────────────

        /// <summary>The room one cost chip holds, plate and number together.</summary>
        internal const float CostChipGlyph = 44f;
        private const float CostChipWidth = CostChipGlyph + 12f;

        /// <summary>
        /// A strip of what something SPENDS, in the Stores drawer's language:
        /// plates with a number under each. Written out as prose ("needs 4
        /// berries (have 1.81K), 2 nuts (have 33)") the same fact is the longest
        /// thing on its row, it re-wraps every time a stock count gains a digit,
        /// and it buries the one number being decided on — how many a batch
        /// costs — inside a sentence. As plates it is read at a glance, and it
        /// is the same width in every state.
        /// <para>
        /// Tapped as a whole rather than chip by chip: a single chip is 56 units
        /// across, well under the 120 that makes a fingertip target, and it
        /// would sit inches from the row's real button. The strip is the target,
        /// and what it says is the caller's to write.
        /// </para>
        /// </summary>
        internal static GameObject CostStrip(Transform parent, UnityEngine.Events.UnityAction onTap)
        {
            var go = MakeRect("Costs", (RectTransform)parent).gameObject;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 4;

            if (onTap != null)
            {
                // Invisible paper: the strip has no plate of its own to look at
                // — chips on card stock, not a panel — but a Button still needs
                // a graphic to be hit.
                var image = go.AddComponent<Image>();
                image.color = Color.clear;
                var button = go.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(onTap);
            }

            return go;
        }

        /// <summary>
        /// One material in a <see cref="CostStrip"/>: the plate, with what it
        /// costs written under it. The caption is handed back rather than set,
        /// because the number holds still while its INK reports whether the
        /// stores can cover it — a chip that rewrote its number would be saying
        /// two different things with one mark.
        /// <para>
        /// <paramref name="fallback"/> stands in where <see cref="ArtLibrary"/>
        /// has no plate: the good's own name, in the picture's room, so the
        /// strip keeps its width and the chip still says what it is.
        /// </para>
        /// </summary>
        internal static GameObject CostChip(Transform parent, Sprite plate, string fallback, out Text caption)
        {
            var go = MakeRect("Cost", (RectTransform)parent).gameObject;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2;
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = CostChipWidth;
            element.preferredWidth = CostChipWidth;

            if (plate != null)
            {
                IconImage(go.transform, plate, CostChipGlyph, Color.white);
            }
            else
            {
                var word = MakeText(go.transform, fallback, 11, TextAnchor.MiddleCenter, Ink2);
                var wordElement = word.gameObject.AddComponent<LayoutElement>();
                wordElement.minWidth = CostChipWidth;
                wordElement.preferredWidth = CostChipWidth;
                wordElement.minHeight = CostChipGlyph;
                wordElement.preferredHeight = CostChipGlyph;
            }

            caption = MakeText(go.transform, string.Empty, 13, TextAnchor.MiddleCenter, Ink);
            return go;
        }

        // ── Plate tiles ───────────────────────────────────────────────────

        /// <summary>
        /// Deep enough for two lines of the caption's own 13: these captions are
        /// names (a ground's, a companion's), not the Stores drawer's counts, and
        /// a name clipped in half names nothing.
        /// </summary>
        private const float TileCaption = 56f;
        private const float TileInset = 6f;

        /// <summary>
        /// The corner mark's plate — small enough that it reads as a mark ON the
        /// tile rather than a second thing to tap.
        /// </summary>
        private const float TileCorner = 46f;

        /// <summary>
        /// One square of a drawer whose tiles are TAPPED: the plate filling it,
        /// its name on a strip along the bottom inside edge, and an optional
        /// corner mark for whoever or whatever is already there. The caption is
        /// INSIDE the square on purpose — a label hung under the tile would make
        /// the cell oblong and the drawer ragged (the Stores drawer's rule).
        /// <para>
        /// Shared by the strip's posting pickers and the Warden page's roster,
        /// which are the same question asked from two places: a drawer of plates
        /// standing for bodies and grounds. <see cref="StoresPage"/> keeps its
        /// own tile — that one's border is the grade, so it cannot be this one's
        /// hairline.
        /// </para>
        /// </summary>
        internal static Button PlateTile(RectTransform grid, Sprite plate, string caption, Sprite corner,
            UnityEngine.Events.UnityAction onPick)
        {
            return PlateTile(grid, plate, caption, corner, onPick, out _, out _, out _);
        }

        /// <summary>
        /// As <see cref="PlateTile(RectTransform, Sprite, string, Sprite, UnityEngine.Events.UnityAction)"/>,
        /// handing back the three pieces a tile that OUTLIVES its own contents
        /// has to be able to rewrite: the caption (a companion can be renamed),
        /// the corner mark (they can walk elsewhere — see
        /// <see cref="SetTileCorner"/>), and the rule (an honour can be earned).
        /// A picker builds its tiles for one open sheet and needs none of them.
        /// </summary>
        internal static Button PlateTile(RectTransform grid, Sprite plate, string caption, Sprite corner,
            UnityEngine.Events.UnityAction onPick, out Text label, out Image mark, out Image rule)
        {
            var go = MakePanel("Tile", grid, DeepPaper);
            var tile = (RectTransform)go.transform;
            rule = AddBorder(go, Ink2).GetComponent<Image>();

            if (plate != null)
            {
                var art = new GameObject("Plate", typeof(Image));
                art.transform.SetParent(tile, false);
                var image = art.GetComponent<Image>();
                image.sprite = plate;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var rect = (RectTransform)art.transform;
                Stretch(rect);
                // Clear of the border and of the caption strip below it — a
                // plate read through either is a plate read twice.
                rect.offsetMin = new Vector2(TileInset, TileCaption);
                rect.offsetMax = new Vector2(-TileInset, -TileInset);
            }

            var strip = MakePanel("Caption", tile, CardPaper);
            var stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = new Vector2(2f, 2f);
            stripRect.offsetMax = new Vector2(-2f, TileCaption - 2f);
            strip.GetComponent<Image>().raycastTarget = false;

            label = MakeText(strip.transform, caption, 13, TextAnchor.MiddleCenter, Ink, SmallCapsFont);
            Stretch((RectTransform)label.transform);
            label.raycastTarget = false;

            // The mark rides on a disc of paper, pinned in the corner: dropped
            // straight onto the plate, a crop glyph reads as something the
            // animal is holding rather than as a mark about it. Built whether or
            // not there is anything to show, so a tile that gains a mark later
            // has somewhere to put it.
            var backing = MakePanel("Mark", tile, CardPaper);
            var backingImage = backing.GetComponent<Image>();
            backingImage.sprite = DiscSprite();
            backingImage.raycastTarget = false;
            var backingRect = (RectTransform)backing.transform;
            backingRect.anchorMin = Vector2.one;
            backingRect.anchorMax = Vector2.one;
            backingRect.pivot = Vector2.one;
            backingRect.anchoredPosition = new Vector2(-TileInset, -TileInset);
            backingRect.sizeDelta = new Vector2(TileCorner, TileCorner);

            // Inside the disc, so the rim of paper reads as a mount all the way
            // round the glyph.
            mark = IconImage(backing.transform, corner, TileCorner - 10f, Color.white).GetComponent<Image>();
            var markRect = (RectTransform)mark.transform;
            markRect.anchorMin = new Vector2(0.5f, 0.5f);
            markRect.anchorMax = new Vector2(0.5f, 0.5f);
            markRect.pivot = new Vector2(0.5f, 0.5f);
            markRect.anchoredPosition = Vector2.zero;
            markRect.sizeDelta = new Vector2(TileCorner - 10f, TileCorner - 10f);
            SetTileCorner(mark, corner);

            var button = go.AddComponent<Button>();
            // Without a targetGraphic the ColorTint transition has nothing to
            // tint, and the tile acknowledges no press at all.
            button.targetGraphic = go.GetComponent<Image>();
            var colours = button.colors;
            colours.highlightedColor = new Color(0.97f, 0.96f, 0.93f, 1f);
            colours.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            // Disabled stays SetButtonTint's job — white here so the two
            // channels don't multiply into a blank plate.
            colours.disabledColor = Color.white;
            button.colors = colours;
            button.onClick.AddListener(onPick);
            return button;
        }

        /// <summary>
        /// Re-mark a tile's corner, or clear it — a companion walks somewhere
        /// else and the tile has to say so without being rebuilt under a finger.
        /// The paper disc is the mark's own parent, so the pair is switched off
        /// together: a bare disc is a blot with nothing in it, and an
        /// <see cref="Image"/> holding no sprite draws a plain white square
        /// (<see cref="SetButtonGlyph"/>'s rule, for the same reason).
        /// </summary>
        internal static void SetTileCorner(Image mark, Sprite sprite)
        {
            mark.sprite = sprite;
            mark.transform.parent.gameObject.SetActive(sprite != null);
        }

        /// <summary>The bare plate every button is drawn on — paper, border, press tint, touch-sized.</summary>
        private static Button ButtonPlate(Transform parent, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = DeepPaper;
            AddBorder(go, Ink2);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            // Intrinsic height so buttons don't collapse to zero in a vertical
            // layout that controls height without force-expanding it (the sheets);
            // in rows, force-expand already governs, so this is a harmless floor.
            // 120 units ≈ 48dp (Android's touch minimum) at the 1080×1920
            // reference scale — 76 was ~31dp, half a fingertip.
            element.preferredHeight = 120;
            element.minHeight = 120;
            var button = go.GetComponent<Button>();
            // Without a targetGraphic the default ColorTint transition has
            // nothing to tint — no button anywhere acknowledged a press.
            button.targetGraphic = image;
            var colours = button.colors;
            colours.highlightedColor = new Color(0.97f, 0.96f, 0.93f, 1f);
            colours.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            // Disabled stays SetButtonTint's job — white here so the two
            // channels don't multiply into a blank plate.
            colours.disabledColor = Color.white;
            button.colors = colours;
            button.onClick.AddListener(onClick);
            return button;
        }

        private static void FitToLabel(Button button, Text label, float floorHeight)
        {
            var fitted = button.gameObject.AddComponent<LabelFittedElement>();
            fitted.label = label;
            fitted.floorHeight = floorHeight;
        }

        /// <summary>
        /// An icon-only button: a small glyph centred in an invisible touch
        /// plate. The plate carries the 120-unit (48dp) floor so the target is a
        /// whole fingertip, while the glyph stays the size the line it sits on
        /// can afford — a bordered plate that big beside a title would read as
        /// the title's equal, which a rename is not.
        /// </summary>
        internal static Button IconButton(Transform parent, Sprite sprite, float glyph, float touch,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("IconButton", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            // Clear, but still a raycast target: the whole plate takes the tap,
            // not just the pixels of the glyph.
            go.GetComponent<Image>().color = Color.clear;
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = touch;
            element.preferredWidth = touch;
            element.minHeight = touch;
            element.preferredHeight = touch;

            // No layout group on the plate, so the glyph is anchored by hand.
            var icon = IconImage(go.transform, sprite, glyph, Ink);
            var rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(glyph, glyph);

            var button = go.GetComponent<Button>();
            // Tint the glyph, not the clear plate — a ColorTint on an invisible
            // graphic acknowledges nothing, which is how buttons here have gone
            // silent before.
            button.targetGraphic = icon.GetComponent<Image>();
            var colours = button.colors;
            colours.highlightedColor = new Color(0.97f, 0.96f, 0.93f, 1f);
            colours.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            colours.disabledColor = Color.white;
            button.colors = colours;
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// Style a button as the page's key action — the fold, the migrate, the
        /// confirm. Moss, not ochre: the rust wash read as a warning colour on
        /// exactly the taps the player is meant to take, and it's now reserved
        /// for costs and halted work.
        /// </summary>
        internal static void KeyAction(Button button)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = MossWash;
            }

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = MossDeep;
            }
        }

        /// <summary>
        /// The focus mark — a doubled ochre rule pencilled just outside a
        /// control while keyboard or controller navigation is driving the
        /// journal (design §13 Phase 2: "focus states"). Drawn as a ring rather
        /// than a tint because the page is paper: every plate here is already
        /// one of four parchment shades, and a fifth would be read as another
        /// kind of button instead of as "you are here". Ochre is the journal's
        /// attention ink. Lives INSIDE the control it marks, so it rides the
        /// layout, scrolls with the page, and is clipped by the viewport mask
        /// for nothing.
        /// </summary>
        internal static GameObject AddFocusRing(GameObject host)
        {
            var go = MakeRect("FocusRing", (RectTransform)host.transform).gameObject;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AddBorder(go, Ochre, 4f);
            AddBorder(go, Ochre, 7f);
            return go;
        }

        /// <summary>
        /// Keep a button's plate unchanged when it goes non-interactable. The
        /// modal trap (see GameHud) switches the whole page off while a sheet is
        /// open, and uGUI answers that by tinting every control to its disabled
        /// colour — which would flush the page grey behind the sheet. The
        /// journal's own disabled look is <see cref="SetButtonTint"/>'s job, on
        /// a separate channel, so this one stays a no-op.
        /// </summary>
        internal static void NeverDim(Button button)
        {
            var colours = button.colors;
            colours.disabledColor = Color.white;
            button.colors = colours;
        }

        /// <summary>
        /// Take a control out of directional navigation while leaving it
        /// clickable — for the scroll stitches, which are draggable furniture
        /// rather than a stop a player wants focus to land on.
        /// </summary>
        internal static void NoNavigation(Selectable selectable)
        {
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        /// <summary>
        /// Give a plate a band that fills as the work it started runs. The
        /// progress belongs ON the control because the control is the one thing
        /// on the row a player is already watching, and because a bar anywhere
        /// else in the row is another piece of furniture whose appearing and
        /// leaving moves the row. Behind the border and the label, never in
        /// front — this is the plate colouring in, not a second thing drawn on
        /// top of it. Drive it with <see cref="SetButtonFill"/>.
        /// </summary>
        internal static GameObject ButtonFill(Button button)
        {
            var go = MakePanel("Fill", (RectTransform)button.transform, MossFill);
            go.GetComponent<Image>().raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            // Inside the ruled edge, so the band never paints over the border
            // that carries the plate's live/dead state.
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(0f, -2f);
            go.transform.SetAsFirstSibling();
            go.SetActive(false);
            return go;
        }

        /// <summary>
        /// A gauge of its own: a ruled track with a moss band that fills across
        /// it, for a clock a player watches rather than a control they press.
        /// Returns the track (hide or show that to take the whole gauge off a
        /// card) and hands back the band as <paramref name="fill"/>, which
        /// <see cref="SetFill"/> drives.
        /// <para>
        /// The band is the same object a plate's own fill is, and is painted by
        /// the same call — a bar and a colouring-in plate are one mechanism, and
        /// two of them would drift apart on the day one learned to ease.
        /// </para>
        /// </summary>
        internal static GameObject Gauge(RectTransform parent, float height, out GameObject fill)
        {
            var track = MakePanel("Gauge", parent, RulePaper);
            track.GetComponent<Image>().raycastTarget = false;
            var element = track.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;

            fill = MakePanel("Fill", (RectTransform)track.transform, MossFill);
            fill.GetComponent<Image>().raycastTarget = false;
            var rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fill.SetActive(false);
            return track;
        }

        /// <summary>
        /// Paint the band to a fraction of the plate, or take it off the plate
        /// entirely at zero — an empty band still draws a two-unit seam down the
        /// left edge, which on an idle button reads as a rendering fault.
        /// </summary>
        internal static void SetButtonFill(GameObject fill, float fraction)
        {
            SetFill(fill, fraction);
        }

        /// <summary>Paint a band — a plate's fill or a gauge's — to a fraction of its track.</summary>
        internal static void SetFill(GameObject fill, float fraction)
        {
            if (fill == null)
            {
                return;
            }

            var showing = fraction > 0f;
            if (fill.activeSelf != showing)
            {
                fill.SetActive(showing);
            }

            // Driven per frame, and a band that isn't moving is the common case
            // — every idle row on a card runs through here. Writing the same
            // anchor back still dirties the layout, so an unchanged one is left
            // alone rather than re-laid out sixty times a second.
            var rect = (RectTransform)fill.transform;
            var edge = Mathf.Clamp01(fraction);
            if (!Mathf.Approximately(rect.anchorMax.x, edge))
            {
                rect.anchorMax = new Vector2(edge, 1f);
            }
        }

        internal static void SetButtonLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>
        /// Grey a button out, or restore it. <paramref name="keyAction"/> keeps a
        /// <see cref="KeyAction"/> button's moss wash on the way back — without it
        /// the first refresh repaints it as an ordinary plate.
        /// <para>
        /// Plate, border AND label all carry "inactive" — one channel alone was
        /// never enough to read (Take up, Claim and Watch all sat there looking
        /// live while their criteria went unmet). The label changes INK rather
        /// than alpha: fading it was the other failure — it compounded to
        /// ~2.25:1 and blanked the names of exactly the things a player is
        /// saving toward. Ink2 on the dead plate is ~3.4:1 at button size
        /// (large text), so it stays plainly readable while plainly secondary.
        /// </para>
        /// </summary>
        internal static void SetButtonTint(Button button, bool on, bool keyAction = false)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                // Opaque, never RulePaper at partial alpha: at 55% it composites
                // over card paper to within a hair of DeepPaper — the live plate
                // — so both states render the same colour and nothing looks
                // disabled.
                image.color = on ? (keyAction ? MossWash : DeepPaper) : RulePaper;
            }

            // The ruled outline is what reads first at arm's length, so it
            // carries the state too: ink while the tap is live, a faint rule
            // when it isn't.
            var border = button.transform.Find("Border");
            if (border != null)
            {
                var borderImage = border.GetComponent<Image>();
                if (borderImage != null)
                {
                    borderImage.color = on ? Ink2 : RulePaper;
                }
            }

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                // Labels built from rich text carry their own <color> spans and
                // are unaffected by this — those call sites pick the dead ink
                // themselves (the posting sheet's blocked rows do).
                label.color = on ? (keyAction ? MossDeep : Ink) : Ink2;
            }
        }

        /// <summary>
        /// Light one button of a set as the chosen one — the tab strip's own
        /// lit/unlit language (page paper and bold ink against deep paper and
        /// secondary), so a segmented choice inside a card reads the same way
        /// the open tab does. Distinct from <see cref="SetButtonTint"/>: every
        /// button here stays tappable, one of them is simply the answer.
        /// </summary>
        internal static void SetButtonChosen(Button button, bool chosen)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = chosen ? PagePaper : DeepPaper;
            }

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = chosen ? Ink : Ink2;
                label.fontStyle = chosen ? FontStyle.Bold : FontStyle.Normal;
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
            element.preferredHeight = 120;
            element.minHeight = 120;

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
            return AddBorder(host, color, inset, BorderSprite());
        }

        /// <summary>
        /// As <see cref="AddBorder(GameObject, Color, float)"/>, on a rule of
        /// your choosing — the Stores drawer's grade rule is the hairline eight
        /// times over, because that border has to mean something rather than
        /// merely separate two sheets of paper.
        /// </summary>
        internal static GameObject AddBorder(GameObject host, Color color, float inset, Sprite rule)
        {
            var go = new GameObject("Border", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(host.transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.sprite = rule;
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
