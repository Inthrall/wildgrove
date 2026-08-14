using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Game.Input;
using Wildgrove.Game.Services;
using Wildgrove.Game.World;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    // The events rail — the column of time-boxed things standing down the left
    // edge of the world strip, each a face, a countdown, and a tap into its own
    // popup. What goes on it is EventRail's decision; this is the furniture and
    // the cadence.
    //
    // It stands in the world gap for the same reason the slot counter does:
    // the chrome budget rule (see GameHud) says a pinned bar has to earn its
    // line of the page, and the rail earns nothing of the kind — it is read
    // twice a session. Standing in the band costs the page nothing at all, and
    // the band's left margin is empty room: the plates spread across the middle
    // of it, and a row of three leaves the rail's width clear at each end.
    //
    // The margin is the whole of what it takes. The strip is NOT told about a
    // narrower band (see ReportWorldStrip) — it was, and every plate moved over
    // and shrank to pay for a rail standing in space they were never using.
    //
    // It is also the one piece of chrome that renders in the CAMERA rather than
    // in the HUD's overlay canvas — see BuildEventRail. An overlay canvas draws
    // after the camera has finished, so nothing in the world can ever be in
    // front of one, and a windfall drifting up the band's left side went behind
    // the cells and came out the top.
    //
    // Standing under the windfalls settles the tap too: a press over a cell
    // with a windfall under the finger CATCHES, and the cell's own click is
    // swallowed on the release (_railTapCaught). The cell only opens when the
    // press caught nothing — which is the same rule the plates keep.
    public sealed partial class GameHud
    {
        /// <summary>The rail's own width in canvas units — cell, border and the breath either side.</summary>
        private const float RailWidth = 134f;

        // 120 units ≈ 48dp, Android's touch floor, and the cell is held at it
        // in BOTH axes rather than only in height. It was 138 when the cell
        // carried a wrapped NAME as well as its clock; the name is a face now,
        // so 120 seats "11d 16h" on its own with room either side — the caption
        // is set not to wrap (see BuildRailCell) so the space between the units
        // can never become a line break.
        private const float RailCellWidth = 120f;

        // 126 units ≈ 50dp at the 1080×1920 reference scale, just over Android's
        // 48dp touch floor. The caption is INSIDE the cell rather than under it
        // so the whole thing is one target: a 120-unit plate with a 26-unit
        // label beneath it needs 154 a cell, and the band's floor (0.14 of the
        // canvas — see JournalLayout) is 269, which would seat exactly one.
        private const float RailCellHeight = 126f;
        private const float RailCellGap = 8f;

        /// <summary>The rail's own breath at the top and bottom of the band, split between the two.</summary>
        private const float RailPadding = 8f;

        /// <summary>The countdown strip along the cell's bottom edge.</summary>
        private const float RailCaptionHeight = 26f;

        /// <summary>The icon's side. Larger than it was now that no name shares the plate with it.</summary>
        private const float RailGlyph = 72f;

        private RectTransform _eventRail;

        /// <summary>The rail's own canvas — camera-space, so the world can draw over it.</summary>
        private Canvas _railCanvas;

        /// <summary>The modal trap for a rail that no longer stands inside the page's own group — see HandleFocus.</summary>
        private CanvasGroup _railGroup;

        /// <summary>
        /// Set when a press over the rail caught a windfall, and consumed by
        /// the cell click that press turns into — the one-shot that lets the
        /// catch beat the cell without either side knowing about the other.
        /// <para>
        /// It works on the order the two arrive in, which is fixed:
        /// <see cref="HandleWorldTap"/> reads a pointer PRESS
        /// (<c>InputSystemGameInput.TendTriggered</c> is
        /// <c>wasPressedThisFrame</c>), and uGUI raises a Button's click on the
        /// RELEASE, at least a frame later. So the catch has always happened
        /// by the time the cell hears about the tap, and the flag is cleared at
        /// the head of every gesture rather than left to go stale.
        /// </para>
        /// </summary>
        private bool _railTapCaught;

        private readonly List<EventRailEntry> _railEntries = new List<EventRailEntry>();
        private readonly List<RailCell> _railCells = new List<RailCell>();

        /// <summary>The entry ids the cells were last built for — the rail rebuilds only when this changes.</summary>
        private string _railSignature;

        /// <summary>One built cell: the plate that takes the tap and the three things on it.</summary>
        private sealed class RailCell
        {
            internal Button button;
            internal Image plate;
            internal Image glyph;
            internal Text title;
            internal Text caption;
        }

        /// <summary>
        /// The rail's frame, built once with the rest of the chrome: a column
        /// standing at the band's left edge. The cells themselves come and go
        /// with what is running.
        /// <para>
        /// It gets a canvas of its own, in ScreenSpaceCamera, because that is
        /// the only way anything in the world can pass in FRONT of it: the
        /// HUD's canvas is an overlay, which is drawn after the camera is
        /// finished with the scene, so every sprite on the strip is behind it
        /// unconditionally. Here the rail is one more rung of
        /// <see cref="StripLayers"/> — over the plates and their captions,
        /// under every rung a windfall carries.
        /// </para>
        /// <para>
        /// The price of leaving the HUD canvas is that nothing the page does
        /// reaches the rail any more: it is laid out against the band by hand
        /// (<see cref="PlaceEventRail"/>) rather than by the gap's own rect,
        /// and it carries its own copy of the modal trap. Both are why the
        /// scaler below has to match the HUD's exactly — every measurement in
        /// this file is in the same canvas units as the rest of the chrome.
        /// </para>
        /// </summary>
        private void BuildEventRail()
        {
            var canvasGo = new GameObject("EventRailCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _railCanvas = canvasGo.GetComponent<Canvas>();
            _railCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            // Null until the world view makes one (it builds the camera on its
            // own first tick, after this) — PlaceEventRail keeps asking. A
            // camera-space canvas with no camera silently draws as an overlay,
            // which is the whole bug back again.
            _railCanvas.worldCamera = Camera.main;
            _railCanvas.sortingOrder = StripLayers.RailCell;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            _railScaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 0.5f;
            // The same answer the HUD's canvas was given, by the same rule —
            // see ApplyCanvasRoom, which re-asks it for both from here on.
            scaler.referenceResolution = JournalLayout.ReferenceResolution(Screen.width, Screen.height, DeviceForm.ScreenDpi);
            _railGroup = canvasGo.AddComponent<CanvasGroup>();

            _eventRail = MakeRect("EventRail", canvasGo.transform);
            // A point anchor at the canvas's centre: the band is reported in
            // screen pixels and lands here as a local offset, so an anchor with
            // any span of its own would only have to be undone.
            _eventRail.anchorMin = new Vector2(0.5f, 0.5f);
            _eventRail.anchorMax = new Vector2(0.5f, 0.5f);
            _eventRail.pivot = new Vector2(0f, 0.5f);
            // Cell and gutter together, which is also the rect the tap guard
            // tests — one width for what the rail covers, drawn and tapped.
            // The height arrives with the band.
            _eventRail.sizeDelta = new Vector2(RailWidth, 0f);
            _eventRail.anchoredPosition = Vector2.zero;

            var layout = _eventRail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // Top of the band: the strip's plates ride its middle (and its upper
            // row when they wrap), so a rail centred vertically would sit level
            // with them and read as a fourth plate in the row.
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = RailCellGap;
            layout.padding = new RectOffset(0, 0, (int)RailPadding / 2, (int)RailPadding / 2);
        }

        /// <summary>
        /// Stand the rail down the left edge of the band, given the band as the
        /// strip itself is given it — in screen pixels, once a frame from
        /// <see cref="ReportWorldStrip"/>. The gap's own rect can't do this work
        /// any more: it belongs to the HUD's canvas and the rail no longer does.
        /// </summary>
        private void PlaceEventRail(Rect band)
        {
            if (_railCanvas == null || _eventRail == null)
            {
                return;
            }

            if (_railCanvas.worldCamera == null)
            {
                _railCanvas.worldCamera = Camera.main;
            }

            var canvasRect = (RectTransform)_railCanvas.transform;
            var camera = _railCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, new Vector2(band.xMin, band.yMin), camera, out var foot)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, new Vector2(band.xMin, band.yMax), camera, out var head))
            {
                return;
            }

            var stand = new Vector2(foot.x, (foot.y + head.y) * 0.5f);
            var size = new Vector2(RailWidth, head.y - foot.y);
            // Only on a real change, the rule the world gap keeps: this runs
            // every frame, and either assignment dirties the layout group and
            // rebuilds every cell in the column.
            if (_eventRail.anchoredPosition != stand)
            {
                _eventRail.anchoredPosition = stand;
            }

            if (_eventRail.sizeDelta != size)
            {
                _eventRail.sizeDelta = size;
            }
        }

        /// <summary>
        /// Re-read what is running and put it on the rail. On the chrome
        /// cadence: the countdowns move in days and hours, and a per-frame
        /// re-read would allocate a duration string sixty times a second for a
        /// label that changes once an hour.
        /// </summary>
        private void RefreshEventRail()
        {
            if (_eventRail == null)
            {
                return;
            }

            EventRail.Collect(_loop.State, _loop.Data, _loop.NowUnixMs(),
                _loop.GameServices.IsSignedIn, _loop.RewardedReady(RewardedPlacement.TimeSkip), _railEntries);
            TrimRailToBand();

            var signature = RailSignature();
            if (signature != _railSignature)
            {
                _railSignature = signature;
                RebuildRailCells();
            }

            for (var index = 0; index < _railCells.Count && index < _railEntries.Count; index++)
            {
                PaintRailCell(_railCells[index], _railEntries[index]);
            }

            _eventRail.gameObject.SetActive(_railEntries.Count > 0);
        }

        /// <summary>
        /// Drop whatever the band cannot seat at a full fingertip. EventRail
        /// orders by urgency precisely so this can cut from the tail, and the
        /// alternative — shrinking the cells to fit — buys a fourth event by
        /// making all four too small to press.
        /// </summary>
        private void TrimRailToBand()
        {
            var band = _worldGap != null ? _worldGap.rect.height : 0f;
            if (band <= 0f)
            {
                return;
            }

            // n cells occupy n·height + (n−1)·gap + padding, so the seats the
            // band affords are (band − padding + gap) / (height + gap).
            var seats = Mathf.FloorToInt((band - RailPadding + RailCellGap) / (RailCellHeight + RailCellGap));
            if (seats < 1)
            {
                seats = 1;
            }

            while (_railEntries.Count > seats)
            {
                _railEntries.RemoveAt(_railEntries.Count - 1);
            }
        }

        private string RailSignature()
        {
            if (_railEntries.Count == 0)
            {
                return string.Empty;
            }

            var signature = _railEntries[0].id;
            for (var index = 1; index < _railEntries.Count; index++)
            {
                signature += "|" + _railEntries[index].id;
            }

            return signature;
        }

        private void RebuildRailCells()
        {
            foreach (var cell in _railCells)
            {
                if (cell.button == null)
                {
                    continue;
                }

                // Off before Destroy, the page rebuild's rule (see RebuildBody):
                // a destroyed child holds its place in the layout until end of
                // frame, so old and new cells would share the rail for one
                // rendered frame and the column would visibly jump.
                cell.button.gameObject.SetActive(false);
                Destroy(cell.button.gameObject);
            }

            _railCells.Clear();
            foreach (var entry in _railEntries)
            {
                _railCells.Add(BuildRailCell(entry));
            }
        }

        /// <summary>
        /// One cell. Hand-built rather than a <see cref="JournalWidgets"/>
        /// button because nothing in the factory stacks a face and a countdown
        /// inside one plate — the page's buttons are all a row of words — and a
        /// rail-shaped widget in the shared factory would be a widget with one
        /// caller.
        /// </summary>
        private RailCell BuildRailCell(EventRailEntry entry)
        {
            var go = new GameObject("EventCell", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_eventRail, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = RailCellWidth;
            element.minWidth = RailCellWidth;
            element.preferredHeight = RailCellHeight;
            element.minHeight = RailCellHeight;
            element.flexibleHeight = 0f;

            var plate = go.GetComponent<Image>();
            plate.color = DeepPaper;
            // Rounded, where the page's own panels are square. The rail's cells
            // are the one chrome that stands ON the world band rather than on
            // paper, and at a fingertip square they read as two cut tiles laid
            // over the strip — a card the warden tucked into the margin is what
            // they are meant to be.
            plate.sprite = JournalSprites.RoundedPlateSprite();
            plate.type = Image.Type.Sliced;
            AddBorder(go, Ink2, 0f, JournalSprites.RoundedBorderSprite());

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            var colours = button.colors;
            colours.highlightedColor = new Color(0.97f, 0.96f, 0.93f, 1f);
            colours.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            button.colors = colours;
            var id = entry.id;
            button.onClick.AddListener(() => OpenRailCell(id));
            // The rail is chrome, not page: it must not flush grey behind a sheet.
            NeverDim(button);

            // The face — a plate where the event has one, the words where it
            // doesn't. Only ever one of the two is showing, the same way
            // GlyphButton does it.
            var glyph = IconImage(go.transform, null, RailGlyph, Color.white).GetComponent<Image>();
            var glyphRect = (RectTransform)glyph.transform;
            glyphRect.anchorMin = new Vector2(0.5f, 1f);
            glyphRect.anchorMax = new Vector2(0.5f, 1f);
            glyphRect.pivot = new Vector2(0.5f, 1f);
            glyphRect.sizeDelta = new Vector2(RailGlyph, RailGlyph);
            // Centred in what the caption leaves, rather than hung off the top.
            glyphRect.anchoredPosition = new Vector2(0f, -(RailCellHeight - RailCaptionHeight - RailGlyph) * 0.5f);

            var title = MakeText(go.transform, string.Empty, 16, TextAnchor.MiddleCenter, Ink, _smallCaps);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(6f, RailCaptionHeight);
            titleRect.offsetMax = new Vector2(-6f, -4f);
            title.raycastTarget = false;

            var caption = MakeText(go.transform, string.Empty, 15, TextAnchor.MiddleCenter, Ink2, _smallCaps);
            var captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(1f, 0f);
            captionRect.pivot = new Vector2(0.5f, 0f);
            captionRect.sizeDelta = new Vector2(-8f, RailCaptionHeight);
            captionRect.anchoredPosition = new Vector2(0f, 4f);
            caption.raycastTarget = false;
            // Legacy Text breaks at any plain space, and the clock has one in
            // the middle of it: wrapped, "11d 16h" is two lines in a strip with
            // room for one, and the second is drawn outside the plate.
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;

            return new RailCell
            {
                button = button,
                plate = plate,
                glyph = glyph,
                title = title,
                caption = caption,
            };
        }

        /// <summary>
        /// What a cell's press turns into: its sheet, unless the same press
        /// already caught a windfall drawn over it. The windfalls draw in front
        /// of the rail (see <see cref="StripLayers.RailCell"/>), and a tap has
        /// to take what it looks like it takes — a cell that opened a popup
        /// while the picture under the finger was a windfall would be reading
        /// the player's aim off the thing they could not see.
        /// </summary>
        private void OpenRailCell(string id)
        {
            if (_railTapCaught)
            {
                _railTapCaught = false;
                return;
            }

            _sheets.OpenEventSheet(id);
        }

        private void PaintRailCell(RailCell cell, EventRailEntry entry)
        {
            var art = RailIcon(entry);
            cell.glyph.sprite = art;
            // An Image holding no sprite draws a plain white square.
            cell.glyph.enabled = art != null;
            cell.title.text = art != null ? string.Empty : entry.title;

            // Moss is the journal's invitation ink (see SetButtonTint) — a cell
            // with something to set down or take up wears it; a cell that is
            // only counting stays plain paper. Never ochre: that is the ink of
            // costs and halted work, and nothing here is either.
            cell.plate.color = entry.ready ? MossWash : DeepPaper;

            // The mark speaks over the clock where the entry has one: a tide
            // with an offering the stores can meet says so, and the cache says
            // what a signed-out player can do about it.
            if (entry.mark != null)
            {
                cell.caption.text = entry.ready
                    ? "<color=" + MossDeepHex + ">" + entry.mark + "</color>"
                    : entry.mark;
                return;
            }

            // Both units, the same clock every other surface reads (the Camp
            // row's "ready in", the sheets, the Trail's head). A cell showing
            // "1d" while the sheet behind it says "1d 3h" is the same wait told
            // two ways, and the coarse one is the one that can be acted on
            // wrongly — the tide a player means to catch tonight reads as a day
            // away all afternoon.
            cell.caption.text = entry.remainingSeconds > 0.0
                ? NumberFormat.Countdown(entry.remainingSeconds)
                : string.Empty;
        }

        /// <summary>
        /// The face a cell wears. Resolved here rather than named on the entry:
        /// which art library a picture comes out of is the HUD's business, and
        /// <see cref="EventRail"/> stays a pure statement of what is running.
        /// <para>
        /// The sabbat plates carry a coming tide as well as an open one. That
        /// was not always so — the plate is what a keeping EARNS (design §15),
        /// so the coming cell was deliberately faceless and wore its name in
        /// words instead. Two cells of wrapped small-caps is what the rail
        /// actually looked like, and the reveal it was protecting is the full
        /// plate on the Record's shelf and the keeping's card, which a
        /// 72-unit mark on a countdown does not spend.
        /// </para>
        /// <para>
        /// The cache wears a chest, not the Amber plate it wore first. Amber is
        /// what is INSIDE it, and a cell that shows the currency reads as a
        /// pile of amber standing on the strip — the ledger's own AMBER figure
        /// two rows above says that, and it says it about money the player
        /// holds rather than money Play is keeping for them.
        /// </para>
        /// <para>
        /// The time-skip wears a sand glass, drawn part run
        /// (tools/make-glass-plate.py): the warden times an observation with one,
        /// and what the cell offers is hours the land works through rather than
        /// an hour of the day, which a clock face would have said instead.
        /// </para>
        /// </summary>
        private Sprite RailIcon(EventRailEntry entry)
        {
            switch (entry.kind)
            {
                case EventRailKind.OpenTide:
                case EventRailKind.ComingSabbat:
                    return entry.sabbatId != null ? ArtLibrary.ForJournal("sabbat-" + entry.sabbatId) : null;
                case EventRailKind.WeeklyCache:
                    return ArtLibrary.ForJournal("chest");
                case EventRailKind.TimeSkip:
                    return ArtLibrary.ForJournal("glass");
                default:
                    return null;
            }
        }

        /// <summary>
        /// True while <paramref name="screenPoint"/> is over the rail. The world
        /// strip resolves taps itself, off a screen rect and its own hit
        /// circles, and knows nothing about uGUI — so this is how
        /// <see cref="HandleWorldTap"/> knows to stop and leave the press to the
        /// cell's own Button, which gets its click from the EventSystem
        /// regardless.
        /// <para>
        /// It used to stand the whole strip down over the rail, catch and all,
        /// so that a cell tap could not also spend a windfall. That was the
        /// right answer while the cells drew OVER the windfalls; now that they
        /// draw under, the catch is the thing the player aimed at, and it takes
        /// the press (<see cref="_railTapCaught"/> keeps the cell from opening
        /// on the release). The guard still holds for a press that caught
        /// nothing — the cell is what is under the finger then.
        /// </para>
        /// </summary>
        private bool PointerOverEventRail(Vector2 screenPoint)
        {
            if (_eventRail == null || !_eventRail.gameObject.activeSelf || _railEntries.Count == 0)
            {
                return false;
            }

            // The rail's own camera, not null: null is the overlay answer, and
            // it reads a camera-space rect as being wherever its world units
            // happen to land on screen.
            return RectTransformUtility.RectangleContainsScreenPoint(
                _eventRail, screenPoint, _railCanvas != null ? _railCanvas.worldCamera : null);
        }
    }
}
