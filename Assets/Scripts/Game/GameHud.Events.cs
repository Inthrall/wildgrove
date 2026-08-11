using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    // The events rail — the column of time-boxed things standing down the left
    // edge of the world strip, each a face, a countdown, and a tap into its own
    // popup. What goes on it is EventRail's decision; this is the furniture and
    // the cadence.
    //
    // It lives INSIDE the world gap for the same reason the slot counter does:
    // the chrome budget rule (see GameHud) says a pinned bar has to earn its
    // line of the page, and the rail earns nothing of the kind — it is read
    // twice a session. Taking its space out of the strip's WIDTH instead costs
    // the page nothing at all, and the band is the one row on screen with width
    // to spare (the plates spread across the middle of it).
    public sealed partial class GameHud
    {
        /// <summary>What the rail takes out of the strip's width, in canvas units — cell, border and the breath either side.</summary>
        private const float RailWidth = 152f;

        private const float RailCellWidth = 138f;

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
        private const float RailCaptionHeight = 30f;

        private RectTransform _eventRail;
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
        /// pinned to the gap's left edge. The cells themselves come and go with
        /// what is running.
        /// </summary>
        private void BuildEventRail(RectTransform gap)
        {
            _eventRail = MakeRect("EventRail", gap);
            _eventRail.anchorMin = new Vector2(0f, 0f);
            _eventRail.anchorMax = new Vector2(0f, 1f);
            _eventRail.pivot = new Vector2(0f, 0.5f);
            // The rail's rect IS the width the strip gives up, cell and gutter
            // together — so the inset the strip reads is the rail's own right
            // edge rather than a second constant that could drift from it.
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
                _loop.GameServices.IsSignedIn, _railEntries);
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
            AddBorder(go, Ink2);

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            var colours = button.colors;
            colours.highlightedColor = new Color(0.97f, 0.96f, 0.93f, 1f);
            colours.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            button.colors = colours;
            var id = entry.id;
            button.onClick.AddListener(() => _sheets.OpenEventSheet(id));
            // The rail is chrome, not page: it must not flush grey behind a sheet.
            NeverDim(button);

            // The face — a plate where the event has one, the words where it
            // doesn't. Only ever one of the two is showing, the same way
            // GlyphButton does it.
            var glyph = IconImage(go.transform, null, 62f, Color.white).GetComponent<Image>();
            var glyphRect = (RectTransform)glyph.transform;
            glyphRect.anchorMin = new Vector2(0.5f, 1f);
            glyphRect.anchorMax = new Vector2(0.5f, 1f);
            glyphRect.pivot = new Vector2(0.5f, 1f);
            glyphRect.sizeDelta = new Vector2(62f, 62f);
            glyphRect.anchoredPosition = new Vector2(0f, -12f);

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

            return new RailCell
            {
                button = button,
                plate = plate,
                glyph = glyph,
                title = title,
                caption = caption,
            };
        }

        private void PaintRailCell(RailCell cell, EventRailEntry entry)
        {
            var art = entry.plateKey != null ? ArtLibrary.ForJournal(entry.plateKey) : null;
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

            cell.caption.text = entry.remainingSeconds > 0.0
                ? NumberFormat.Countdown(entry.remainingSeconds)
                : string.Empty;
        }

        /// <summary>
        /// True while <paramref name="screenPoint"/> is over the rail. The world
        /// strip resolves taps itself, off a screen rect and its own hit
        /// circles, and knows nothing about uGUI — so without this a tap on a
        /// cell would ALSO pop whatever windfall happened to be drifting over
        /// it. (The rail's own cells are ordinary Buttons and get their click
        /// from the EventSystem regardless.)
        /// </summary>
        private bool PointerOverEventRail(Vector2 screenPoint)
        {
            if (_eventRail == null || !_eventRail.gameObject.activeSelf || _railEntries.Count == 0)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(_eventRail, screenPoint, null);
        }

        /// <summary>True while the rail is standing on the strip's left edge and the strip must give way.</summary>
        private bool EventRailStanding()
        {
            return _eventRail != null && _eventRail.gameObject.activeSelf && _railEntries.Count > 0;
        }
    }
}
