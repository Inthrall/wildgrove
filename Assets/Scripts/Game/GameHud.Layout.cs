using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wildgrove.Game.World;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;
using static Wildgrove.Game.JournalSprites;

namespace Wildgrove.Game
{
    // The journal's furniture and how it folds to the screen: building the
    // page chrome once (header, ledger, note, tracker, tab bar, scroll body)
    // and re-laying it out whenever the canvas changes shape — the phone's
    // single column, the tablet's two-page spread, and the gap the world strip
    // is given above the page. Construction and responsive layout only; what
    // the chrome SAYS is GameHud.Chrome.cs.
    public sealed partial class GameHud
    {
        // ─────────────────────────── Chrome ──────────────────────────────────

        private void BuildChrome()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var root = MakeRect("Root", canvasGo.transform);
            Stretch(root);
            _root = root;
            // The modal trap: a sheet switches this off, which makes every
            // control on the page report itself non-interactable — and uGUI's
            // own directional navigation skips exactly those. Without it a pad
            // walks straight out of an open sheet into the page behind it.
            // (Nothing dims visually: the button plates all disable to white.)
            _pageGroup = root.gameObject.AddComponent<CanvasGroup>();
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.padding = new RectOffset(PageMargin, PageMargin, 10, 6);
            rootLayout.spacing = 6;

            // Header — the page title, centred like the mock's page head. The
            // mock's eyebrow above it ("THE RECORD") only ever restated the lit
            // tab three rows below, in a second type style, for a line of
            // height the page needed more; the camp number it also carried now
            // reads on the Record page's Standing card. The header band is
            // transparent: the page paper shows through, but it must NOT
            // swallow pointer raycasts (an Image would).
            // A ROW, not a column: on a spread the ledger folds up into this
            // line beside the title (see ApplyHeadFold), and a row is what it
            // has to land in.
            var headerGo = MakeRect("Header", root).gameObject;
            _headRow = (RectTransform)headerGo.transform;
            var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.spacing = 24;
            // 27 authored ≈ the old 36 at the previous FontScale — the title
            // was already big enough; the scale bump is for the working text.
            _title = MakeText(headerGo.transform, string.Empty, 27, TextAnchor.MiddleCenter, Ink, _serif);
            FlexibleWidth(_title.gameObject, 1f);

            // Ledger — the three meta currencies, hairline-ruled like the mock.
            // Keep it to those three: a chrome row that lists held resources
            // grows with the save (two lines at Sunfield, four by the second
            // camp), and every one of them comes off the page. The stores read
            // in their own drawer, and a tap here goes there.
            MakeHairline(root);
            var ledgerGo = MakeRect("Ledger", root);
            _ledgerRow = ledgerGo;
            var ledgerLayout = ledgerGo.gameObject.AddComponent<HorizontalLayoutGroup>();
            ledgerLayout.childControlWidth = true;
            ledgerLayout.childControlHeight = true;
            ledgerLayout.childForceExpandWidth = true;
            ledgerLayout.childForceExpandHeight = false;
            ledgerLayout.childAlignment = TextAnchor.MiddleCenter;
            _ledger = MakeText(ledgerGo, string.Empty, 19, TextAnchor.MiddleCenter, Ink);
            FlexibleWidth(_ledger.gameObject, 1f);
            var ledgerButton = _ledger.gameObject.AddComponent<Button>();
            ledgerButton.targetGraphic = _ledger;
            ledgerButton.onClick.AddListener(() => OpenTab(TabStores));
            NeverDim(ledgerButton);
            _ledgerRule = MakeHairline(root);

            // Pinned tracker — verse progress before the Rite consents, the
            // Fold forecast (with its button) after. It sits in a row of its own
            // so a spread can stand the margin note beside it (ApplyNoteFold).
            var trackerRow = MakeRect("TrackerRow", root);
            _trackerRow = trackerRow;
            var trackerRowLayout = trackerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            trackerRowLayout.childControlWidth = true;
            trackerRowLayout.childControlHeight = true;
            trackerRowLayout.childForceExpandWidth = true;
            trackerRowLayout.childForceExpandHeight = false;
            trackerRowLayout.childAlignment = TextAnchor.MiddleLeft;
            trackerRowLayout.spacing = 24;
            var trackerGo = MakePanel("Tracker", trackerRow, CardPaper);
            _trackerPanel = trackerGo;
            var trackerLayout = trackerGo.AddComponent<HorizontalLayoutGroup>();
            trackerLayout.childControlWidth = true;
            trackerLayout.childControlHeight = true;
            trackerLayout.childForceExpandWidth = false;
            // Force-expanded children make the GROUP report flexibleHeight >= 1,
            // and the root then hands the tracker a full share of screen slack —
            // the compact strip becomes a giant half-empty box. Children centre
            // via childAlignment instead, and the explicit LayoutElement pins
            // the panel out of the flexible pool for good.
            trackerLayout.childForceExpandHeight = false;
            trackerLayout.childAlignment = TextAnchor.MiddleCenter;
            trackerLayout.padding = new RectOffset(12, 12, 10, 10);
            trackerLayout.spacing = 10;
            var trackerElement = trackerGo.AddComponent<LayoutElement>();
            trackerElement.flexibleHeight = 0;
            // Ignored while the tracker owns its row; on a spread it shares the
            // width with the margin note, and takes the lot when the note is
            // saying nothing.
            trackerElement.flexibleWidth = 1f;
            var trackerButton = trackerGo.AddComponent<Button>();
            // The row deep-links to whatever it is showing — the verse card
            // usually, the keeping's card when the tide holds the row alone.
            trackerButton.onClick.AddListener(() => ScrollToOnTrail(_trackerTarget));
            NeverDim(trackerButton);
            AddBorder(trackerGo, Ink2);
            _trackerText = MakeText(trackerGo.transform, string.Empty, 21, TextAnchor.MiddleCenter, Ink, _serif);
            FlexibleWidth(_trackerText.gameObject, 1f);
            _foldButton = Button(trackerGo.transform, "Fold the camp", 260, _sheets.OpenMigrationSheet);
            KeyAction(_foldButton);
            _foldButton.gameObject.SetActive(false);

            // Margin note — the handwritten aside, UNDER the tracker. Hidden
            // while it has nothing to say (ShowNote), and on a spread it stands
            // in the tracker's row rather than owning a line of its own.
            _note = MakeText(root, string.Empty, 24, TextAnchor.MiddleLeft, Ink2, _hand);
            FlexibleWidth(_note.gameObject, 1f);
            _note.gameObject.SetActive(false);

            // World gap — the WorldView strip draws here. Capped rather than
            // flexible: on tall screens the slack goes to the page (more cards
            // visible), not to empty paper around the node strip. The height
            // is set from the canvas size in FitLayoutToScreen.
            var gap = MakeRect("WorldGap", root);
            _worldGap = gap;
            _worldGapElement = gap.gameObject.AddComponent<LayoutElement>();

            // Slots-in-use counter, pinned inside the strip band's bottom-right
            // corner — it counts the badges beside it, so it belongs with them
            // rather than up in the page head. Never a raycast target: the
            // whole band is the posting/catching surface.
            _slotCounter = MakeText(gap, string.Empty, 17, TextAnchor.MiddleRight, Ink2, _smallCaps);
            _slotCounter.gameObject.name = "SlotCounter";
            _slotCounter.raycastTarget = false;
            var counterRect = (RectTransform)_slotCounter.transform;
            counterRect.anchorMin = new Vector2(1f, 0f);
            counterRect.anchorMax = new Vector2(1f, 0f);
            counterRect.pivot = new Vector2(1f, 0f);
            counterRect.sizeDelta = new Vector2(380f, 44f);
            counterRect.anchoredPosition = new Vector2(-6f, 4f);

            // Nothing else pins here, between the strip and the page: the
            // trail-home line and the camp actions each head their own page
            // instead. Both are page chrome wearing a global badge — the
            // carrier walking home is the Trail's business, the rewarded
            // time-skip the Camp's — and pinned here they cost every tab a
            // fifth of its height.

            // The open journal page — a scroll view the tab pages build into.
            _body = BuildScroll(root);
            JournalWidgets.Content = _body;

            // The journal tabs along the bottom edge.
            BuildTabsBar(root);

            // The page paper is the WORLD CAMERA's background (WorldView sets
            // it), not a canvas image — an overlay canvas draws above every
            // camera, so an opaque paper Image here would permanently hide the
            // world strip's node sprites in the gap.

            // Paper grain over the whole page (the mock's noise overlay sits
            // above content too, at low opacity).
            var grain = new GameObject("Grain", typeof(Image));
            grain.transform.SetParent(canvasGo.transform, false);
            var grainImage = grain.GetComponent<Image>();
            grainImage.sprite = GrainSprite();
            grainImage.type = Image.Type.Tiled;
            grainImage.raycastTarget = false;
            Stretch((RectTransform)grain.transform);

            // Transient action feedback (the "+ planted" flashes) draws above
            // the page but under any open sheet. No Graphic — taps fall through.
            var feedbackGo = MakeRect("Feedback", canvasGo.transform);
            Stretch(feedbackGo);
            _feedbackLayer = feedbackGo;

            // Modal layer, above everything, initially empty.
            var modalGo = MakeRect("Modals", canvasGo.transform);
            Stretch(modalGo);
            _modalLayer = modalGo;

            Canvas.ForceUpdateCanvases();
            FitLayoutToScreen();
        }

        /// <summary>
        /// Fit the chrome to the device: keep the page out of the display
        /// cutout and gesture areas (a tabs bar flush with the screen edge
        /// lands inside Android's home-swipe zone), and cap the world strip's
        /// share of the screen. Re-applied whenever the safe area or canvas
        /// size changes.
        /// </summary>
        private void FitLayoutToScreen()
        {
            var safe = Screen.safeArea;
            var canvasRect = ((RectTransform)_canvas.transform).rect;
            var canvasHeight = canvasRect.height;
            var canvasWidth = canvasRect.width;
            if (safe == _appliedSafeArea
                && Mathf.Approximately(canvasHeight, _appliedCanvasHeight)
                && Mathf.Approximately(canvasWidth, _appliedCanvasWidth))
            {
                return;
            }

            _appliedSafeArea = safe;
            _appliedCanvasHeight = canvasHeight;
            _appliedCanvasWidth = canvasWidth;
            ApplyWideLayout(JournalLayout.IsWide(canvasWidth, canvasHeight));
            ApplyPageMargins(canvasWidth);

            var scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            _root.offsetMin = new Vector2(safe.xMin / scale, safe.yMin / scale);
            _root.offsetMax = new Vector2((safe.xMax - Screen.width) / scale, (safe.yMax - Screen.height) / scale);

            Canvas.ForceUpdateCanvases();
            UpdateWorldGap();
        }

        /// <summary>
        /// On a spread, the title and the ledger share one line — the title out
        /// to the left margin, RENOWN in at the right — and the rule that used
        /// to separate them goes with them.
        /// <para>
        /// Landscape is short of exactly one thing, height, and the chrome costs
        /// the same absolute units there as it does on a phone: two centred
        /// lines and their rules were a card's worth of the open page spent
        /// saying two short things that read perfectly well side by side. The
        /// page head and the ledger are still the same two elements, still in
        /// the same order left-to-right as they were top-to-bottom.
        /// </para>
        /// </summary>
        private void ApplyHeadFold(bool wide)
        {
            if (_headRow == null || _ledgerRow == null || _ledger == null)
            {
                return;
            }

            // Reparent BEFORE hiding the row it came from, or the ledger goes
            // dark with its old host.
            var host = wide ? _headRow : _ledgerRow;
            if (_ledger.transform.parent != host)
            {
                _ledger.transform.SetParent(host, false);
            }

            _title.alignment = wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _ledger.alignment = wide ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            _ledgerRow.gameObject.SetActive(!wide);
            _ledgerRule.SetActive(!wide);
            ApplyNoteFold(wide);
        }

        /// <summary>
        /// Stand the margin note beside the tracker on a spread instead of over
        /// it. Two short lines become one — and because the row is the tracker's
        /// either way, a note arriving mid-play doesn't shove the whole page
        /// down a line to make room for itself.
        /// </summary>
        private void ApplyNoteFold(bool wide)
        {
            if (_trackerRow == null || _note == null || _root == null)
            {
                return;
            }

            if (wide)
            {
                if (_note.transform.parent != _trackerRow)
                {
                    _note.transform.SetParent(_trackerRow, false);
                    // Right of the tracker plate — the same order the column
                    // reads top-to-bottom, turned on its side.
                    _note.transform.SetAsLastSibling();
                }

                return;
            }

            if (_note.transform.parent != _root)
            {
                _note.transform.SetParent(_root, false);
                // Back to its own line, immediately below the tracker's row.
                _note.transform.SetSiblingIndex(_trackerRow.GetSiblingIndex() + 1);
            }
        }

        /// <summary>
        /// Widen the side margins until the book is no broader than
        /// <see cref="JournalLayout.MaxWidth"/>, so an ultrawide canvas gets
        /// paper margins rather than a page stretched past any reading measure.
        /// Every row is a child of the root, so the chrome's rules, the strip
        /// band and both columns narrow together — the book stays one object.
        /// </summary>
        private void ApplyPageMargins(float canvasWidth)
        {
            var layout = _root != null ? _root.GetComponent<VerticalLayoutGroup>() : null;
            if (layout == null)
            {
                return;
            }

            var side = JournalLayout.SideMargin(canvasWidth, PageMargin);
            if (layout.padding.left == side)
            {
                return;
            }

            // A fresh RectOffset: mutating the live one in place doesn't dirty
            // the layout, and the margins would only land on the next rebuild.
            layout.padding = new RectOffset(side, side, layout.padding.top, layout.padding.bottom);
        }

        /// <summary>
        /// Turn the book to a spread, or back to a single column (design §13
        /// Phase 2). The Trail loses its tab when wide because it is always on
        /// screen — an open tab you cannot close reads as broken — and a run
        /// sitting on the Trail is moved to the Camp so the left page still
        /// says something the right one doesn't (the mock does the same).
        /// </summary>
        private void ApplyWideLayout(bool wide)
        {
            // Idempotent on purpose: the first fit runs before the tab bar
            // exists (so the hide below is a no-op), and the one at the end of
            // BuildUi has to be able to reassert it without counting as a
            // change and forcing a second rebuild of a page just built.
            var changed = wide != _wide;
            _wide = wide;
            ApplyHeadFold(wide);

            if (_tabButtons.TryGetValue(TabTrail, out var trailTab))
            {
                trailTab.gameObject.SetActive(!wide);
            }

            if (wide && _tab == TabTrail)
            {
                // Straight to the field: OpenTab would bounce this back.
                _tab = TabCamp;
                if (_tabButtons.Count > 0)
                {
                    foreach (var id in Tabs)
                    {
                        StyleTab(id, id == _tab);
                    }
                }

                changed = true;
            }

            if (changed)
            {
                // The page count changed under the reader — rebuild both columns.
                _builtTab = null;
                _dirty = true;
            }
        }

        /// <summary>
        /// Point the page builders at <paramref name="column"/>. Both funnels
        /// move together: <see cref="Body"/> for the builders that take a
        /// parent, JournalWidgets.Content for the card helpers that don't.
        /// </summary>
        private void SetPageColumn(RectTransform column)
        {
            _pageColumn = column;
            JournalWidgets.Content = column != null ? column : _body;
        }

        /// <summary>
        /// Build the spread's frame inside the scroll content: two equal
        /// columns side by side, top-aligned, and return them. The columns are
        /// ordinary vertical layouts, so a page cannot tell it is one.
        /// </summary>
        private void BuildSpreadColumns(out RectTransform left, out RectTransform right)
        {
            _spread = MakeRect("Spread", _body);
            var row = _spread.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            // Top-aligned: the two pages are independent flows, and a short
            // Camp page must not stretch to the Trail's length.
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.UpperLeft;
            row.spacing = JournalLayout.SpreadGap;

            left = MakeColumn("PageLeft", _spread);
            right = MakeColumn("PageRight", _spread);
        }

        private RectTransform MakeColumn(string name, RectTransform parent)
        {
            var column = MakeRect(name, parent);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10;
            // No ContentSizeFitter here, deliberately: the spread's row already
            // controls the column's height (childControlHeight), and a fitter on
            // the same axis fought it — it resized the rect about the centre
            // pivot AFTER the row had placed the top edge, so whichever page was
            // the shorter one floated down half the difference. On a spread that
            // read as the Camp starting a third of the way down the paper while
            // the Trail beside it began at the top. The row's own UpperLeft
            // alignment is the whole answer.
            //
            // Equal halves; without a flexible width the columns collapse to
            // their content and the spread drifts off-centre.
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = 1f;
            // And a preferred width of nothing, so the SPLIT is the layout's
            // decision rather than the pages'. A column reports the preferred
            // width of its widest card, the row honours that before it shares
            // anything out, and the Trail — whose rows are the longest in the
            // book — was helping itself to 70% of the paper while the Stores
            // grid crammed itself into what was left. Two pages of a book are
            // the same width whatever is printed on them.
            element.preferredWidth = 0f;
            return column;
        }

        /// <summary>
        /// Give the world strip whatever the chrome and the page don't need —
        /// the strip is the layout's shock absorber, not the page.
        /// <para>
        /// The page is the only row with flexible height, so before this every
        /// unit the chrome grew came straight out of it: the device-scale pass
        /// (bigger type, 48dp touch floors) halved the open page on a tall
        /// phone, and a fifth ledger line would take another bite. Now the
        /// pinned rows are measured, the page is guaranteed its floor, and the
        /// strip takes the remainder — clamped so it neither swells into empty
        /// paper on a tall screen nor collapses below a readable band on a
        /// short one. The shares differ between a column and a spread, which is
        /// why the layout is asked (<see cref="JournalLayout.StripHeight"/>)
        /// rather than clamped here.
        /// </para>
        /// </summary>
        private void UpdateWorldGap()
        {
            if (_root == null || _worldGapElement == null)
            {
                return;
            }

            var available = _root.rect.height;
            if (available <= 0f)
            {
                return;
            }

            var layout = _root.GetComponent<VerticalLayoutGroup>();
            var chrome = (float)(layout.padding.top + layout.padding.bottom);
            var rows = 0;
            for (var i = 0; i < _root.childCount; i++)
            {
                var child = (RectTransform)_root.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                rows++;
                if (child == _worldGap || child == _body)
                {
                    continue;
                }

                chrome += LayoutUtility.GetPreferredHeight(child);
            }

            chrome += layout.spacing * Mathf.Max(0, rows - 1);

            var target = JournalLayout.StripHeight(available, chrome, _wide);
            // Only on a real change — assigning every cadence dirties the whole
            // layout for nothing.
            if (Mathf.Abs(target - _worldGapElement.preferredHeight) > 1f)
            {
                _worldGapElement.preferredHeight = target;
            }
        }

        private void BuildTabsBar(RectTransform root)
        {
            var barGo = MakeRect("Tabs", root).gameObject;
            // ≥117 units ≈ Android's 48dp touch floor — 84 was ~32dp tabs.
            FixedHeight(barGo, 132);
            var layout = barGo.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 6;
            layout.padding = new RectOffset(0, 0, 6, 0);

            AddTab(barGo.transform, TabTrail, "Trail");
            AddTab(barGo.transform, TabCamp, "Camp");
            AddTab(barGo.transform, TabStores, "Stores");
            AddTab(barGo.transform, TabWarden, "Warden");
            AddTab(barGo.transform, TabRecord, "Record");
        }

        private void AddTab(Transform bar, string id, string label)
        {
            var go = new GameObject("Tab_" + id, typeof(Image), typeof(Button));
            go.transform.SetParent(bar, false);
            AddBorder(go, Ink2);
            var outer = AddBorder(go, RulePaper, 4f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => OpenTab(id));
            NeverDim(button);
            var text = MakeText(go.transform, label, 22, TextAnchor.MiddleCenter, Ink2, _smallCaps);
            Stretch((RectTransform)text.transform);

            // The merge strip — page paper drawn over the active tab's top
            // border and up across the gap to the page, so the raised tab
            // reads as PART of the page, not just a lighter plate.
            var merge = new GameObject("Merge", typeof(Image), typeof(LayoutElement));
            merge.transform.SetParent(go.transform, false);
            merge.GetComponent<LayoutElement>().ignoreLayout = true;
            var mergeImage = merge.GetComponent<Image>();
            mergeImage.color = PagePaper;
            mergeImage.raycastTarget = false;
            var mergeRect = (RectTransform)merge.transform;
            mergeRect.anchorMin = new Vector2(0f, 1f);
            mergeRect.anchorMax = Vector2.one;
            // Down 4 to cover the tab's own top border, up 14 across the bar
            // padding and root spacing; inset so the side rules still frame it.
            mergeRect.offsetMin = new Vector2(3f, -4f);
            mergeRect.offsetMax = new Vector2(-3f, 14f);

            _tabButtons[id] = button;
            _tabLabels[id] = text;
            _tabOuterRules[id] = outer;
            _tabMerges[id] = merge;
            StyleTab(id, id == _tab);
        }

        private RectTransform BuildScroll(RectTransform parent)
        {
            // No RectMask2D here — the viewport masks the page, and the
            // scrollbar hangs OUTSIDE this rect (in the right page gutter),
            // where a mask on the body would clip it away.
            var scrollGo = new GameObject("Body", typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            Flexible(scrollGo, 2f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll = scroll;

            var viewport = MakeRect("Viewport", scrollGo.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            scroll.viewport = viewport;

            var content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            // MakeRect's default sizeDelta is (100,100) — with stretched X
            // anchors that leaves the page 100 units WIDER than the viewport
            // (50 clipped off each edge). Zero it so content width == viewport.
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            // The scroll stitch — a slim ink scrollbar riding the right page
            // gutter, mirroring the spine. It lives outside the viewport so
            // the page keeps its full width, and auto-hides when the page fits.
            var barGo = new GameObject("Scrollbar", typeof(Image), typeof(Scrollbar));
            barGo.transform.SetParent(scrollGo.transform, false);
            var track = barGo.GetComponent<Image>();
            track.color = new Color(RulePaper.r, RulePaper.g, RulePaper.b, 0.45f);
            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = Vector2.one;
            barRect.offsetMin = new Vector2(5f, 2f);
            barRect.offsetMax = new Vector2(11f, -2f);

            var handleGo = new GameObject("Handle", typeof(Image));
            handleGo.transform.SetParent(barGo.transform, false);
            var handle = handleGo.GetComponent<Image>();
            handle.color = new Color(Ink2.r, Ink2.g, Ink2.b, 0.55f);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var scrollbar = barGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            // The stitch is draggable furniture, not a place to stand — focus
            // walking onto it would look like the mark had fallen off the page.
            NoNavigation(scrollbar);

            return content;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
