using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wildgrove.Game.Input;
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
            _hudScaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 0.5f;
            // The reference is not the flat 1080x1920 it reads as: a screen
            // physically bigger than a phone is handed more units to work in
            // (JournalLayout.RoomFactor), and this is re-asked whenever the
            // screen changes shape — a foldable opening is a different device.
            ApplyCanvasRoom();

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
            trackerButton.onClick.AddListener(() => GoToTrail(_trackerTarget));
            NeverDim(trackerButton);
            AddBorder(trackerGo, Ink2);
            _trackerText = MakeText(trackerGo.transform, string.Empty, 21, TextAnchor.MiddleCenter, Ink, _serif);
            FlexibleWidth(_trackerText.gameObject, 1f);
            _foldButton = Button(trackerGo.transform, "Fold the camp", 260, _sheets.OpenMigrationSheet);
            KeyAction(_foldButton);
            _foldButton.gameObject.SetActive(false);

            // Margin note — the handwritten aside, UNDER the tracker. Its line
            // is held open whether or not there is anything to say (StandNoteLane),
            // and on a spread it stands in the tracker's row rather than owning
            // a line of its own.
            //
            // It lives in a lane of its own height rather than sizing the row
            // itself: a note is pinned ABOVE the page, so a sentence that wrapped
            // to two lines shoved the whole journal down and pulled it back up
            // again six seconds later. The lane is one line, always; the mask
            // clips what will not fit and MarqueeLine walks it across.
            var noteLane = MakeRect("NoteLane", root);
            _noteLane = noteLane;
            noteLane.gameObject.AddComponent<RectMask2D>();
            FlexibleWidth(noteLane.gameObject, 1f);
            _note = MakeText(noteLane, string.Empty, 24, TextAnchor.MiddleLeft, Ink2, _hand);
            // Overflow, not Wrap: the wrap is the thing being prevented, and the
            // mask is what stops the overflow reaching the page margins.
            _note.horizontalOverflow = HorizontalWrapMode.Overflow;
            var noteRect = (RectTransform)_note.transform;
            noteRect.anchorMin = Vector2.zero;
            noteRect.anchorMax = new Vector2(0f, 1f);
            noteRect.pivot = new Vector2(0f, 0.5f);
            noteRect.anchoredPosition = Vector2.zero;
            var marquee = noteLane.gameObject.AddComponent<MarqueeLine>();
            marquee.label = _note;
            // Standing and empty, which is how the column opens. The fit at the
            // end of this build takes it down again if the canvas turns out to
            // be a spread.
            StandNoteLane();

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

            // The events rail down the band's other edge — the strip gives it
            // width rather than the page giving it height (see GameHud.Events).
            // Not a child of the gap, though it stands in it: the rail has a
            // canvas of its own so the world can draw over it, and is laid
            // against the band by hand each frame.
            BuildEventRail();

            // Nothing else pins here, between the strip and the page: the
            // trail-home line and the camp actions each head their own page
            // instead. Both are page chrome wearing a global badge — the
            // carrier walking home is the Trail's business, the rewarded
            // time-skip the Camp's — and pinned here they cost every tab a
            // fifth of its height.

            // The open journal page. TWO scroll views side by side, not one
            // scroll holding two columns: a spread's pages each keep their own
            // place (see BuildPageArea).
            BuildPageArea(root);
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
        /// <summary>
        /// Hand every canvas the units this screen has earned — one reference
        /// resolution, opened out by <see cref="JournalLayout.RoomFactor"/>, so
        /// a tablet gets more of the book rather than a bigger copy of the
        /// phone's.
        /// <para>
        /// Asked off the SCREEN's pixels rather than the canvas's units, which
        /// is what keeps it from chasing its own tail: changing the reference
        /// changes the canvas, and a rule that read the canvas would answer
        /// differently every time it ran. The screen only changes when the
        /// device does — a rotation keeps the same pixel count and so the same
        /// answer, while a foldable opening is genuinely a larger screen and
        /// gets one.
        /// </para>
        /// </summary>
        private void ApplyCanvasRoom()
        {
            var width = Screen.width;
            var height = Screen.height;
            if (width == _appliedScreenWidth && height == _appliedScreenHeight)
            {
                return;
            }

            _appliedScreenWidth = width;
            _appliedScreenHeight = height;
            var reference = JournalLayout.ReferenceResolution(width, height, DeviceForm.ScreenDpi);
            if (_hudScaler != null)
            {
                _hudScaler.referenceResolution = reference;
            }

            // The rail keeps a canvas of its own so the world can draw over it,
            // and lays itself against the band in the HUD's units by hand — two
            // scalers that disagreed would stand it somewhere else entirely.
            // Null on the first call: the rail is built after the chrome, and
            // sets the same reference itself.
            if (_railScaler != null)
            {
                _railScaler.referenceResolution = reference;
            }

            // The canvas has a new size; the fit below has to measure the new
            // one, not the one this call just replaced.
            Canvas.ForceUpdateCanvases();
        }

        private void FitLayoutToScreen()
        {
            ApplyCanvasRoom();
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
            ApplyWideLayout(JournalLayout.IsWide(canvasWidth, canvasHeight), canvasHeight);
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

            // And the tracker with them (2026-08-14): title · banner · ledger,
            // one line. The banner is a single short sentence, and on a spread
            // it had a row of its own with 940 units of empty paper either side
            // of it while the page below was clipped by the tabs. It stands
            // between the two things already in this line because that is the
            // order the column reads top-to-bottom, turned on its side, and it
            // takes the row's slack (flexibleWidth) so the title and the ledger
            // keep their own widths at the margins.
            if (_trackerPanel != null)
            {
                var trackerHost = wide ? _headRow : (Transform)_trackerRow;
                if (_trackerPanel.transform.parent != trackerHost)
                {
                    _trackerPanel.transform.SetParent(trackerHost, false);
                }

                if (wide)
                {
                    _trackerPanel.transform.SetSiblingIndex(1);
                }
            }

            // Nothing left in it on a spread: the note it used to stand beside
            // goes back to a line of its own, which stands only when there is
            // something written on it (StandNoteLane).
            _trackerRow.gameObject.SetActive(!wide);
            ApplyNoteFold(wide);
        }

        /// <summary>
        /// Keep the margin note on a line of its own, immediately under the
        /// tracker's row.
        /// <para>
        /// It stood BESIDE the tracker on a spread until 2026-08-14, to make
        /// two short lines into one. The tracker has gone up into the title's
        /// line since (<see cref="ApplyHeadFold"/>), which buys the page the
        /// whole of that row rather than half of it, and leaves the note with
        /// nothing to share. It costs the spread nothing to be back on its own
        /// line: a silent lane doesn't stand there at all
        /// (<see cref="StandNoteLane"/>), and a note that arrives mid-play is
        /// paid for out of the world band rather than out of the page — the
        /// band is the layout's shock absorber (<see cref="UpdateWorldGap"/>),
        /// which is what stops a sentence shoving the journal under a thumb
        /// already reading it.
        /// </para>
        /// <para>
        /// Which way the page is folded is still what decides whether a silent
        /// lane stands at all, so a device turned mid-run asks that again here.
        /// </para>
        /// </summary>
        private void ApplyNoteFold(bool wide)
        {
            if (_trackerRow == null || _noteLane == null || _root == null)
            {
                return;
            }

            if (_noteLane.parent != _root)
            {
                _noteLane.SetParent(_root, false);
            }

            _noteLane.SetSiblingIndex(_trackerRow.GetSiblingIndex() + 1);
            StandNoteLane();
        }

        /// <summary>The rail's breath between itself and the page it opens.</summary>
        private const float TabRailGap = 20f;

        /// <summary>
        /// The tabs' second home: a rail down the page's fore-edge, standing
        /// only while the book is a spread.
        /// <para>
        /// Built empty. The tab plates themselves live in whichever home the
        /// fold has put them in (<see cref="ApplyTabFold"/>) — they are the same
        /// five objects either way, so which tab is lit, what it says and where
        /// its handler goes all survive the move without being told about it.
        /// </para>
        /// </summary>
        private void BuildTabRail(RectTransform parent)
        {
            var railGo = MakeRect("TabRail", parent).gameObject;
            _tabRail = (RectTransform)railGo.transform;
            var element = railGo.AddComponent<LayoutElement>();
            element.minWidth = JournalLayout.RailTabWidth;
            element.preferredWidth = JournalLayout.RailTabWidth;
            element.flexibleWidth = 0f;

            var layout = railGo.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            // From the top, with the paper below them: index tabs are cut down
            // from the head of a book, not centred on its height.
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = JournalLayout.RailTabSpacing;
            railGo.SetActive(false);
        }

        /// <summary>
        /// Move the journal's tabs between their two homes: the bar along the
        /// bottom edge in a column, the rail down the page's fore-edge on a
        /// spread.
        /// <para>
        /// This is the largest single thing the landscape book gets back.
        /// Landscape is short of exactly one measure and it is not width: a
        /// bottom bar costs <see cref="JournalLayout.TabDepth"/> of a ~1,150-unit canvas —
        /// an eighth of the scarce axis — to seat five short words that sit
        /// perfectly well in the abundant one. Down the fore-edge they cost
        /// <see cref="JournalLayout.RailTabWidth"/> of a 2,000-unit canvas
        /// instead, and read as what they have always been drawn as: index tabs
        /// cut into the edge of a book.
        /// </para>
        /// <para>
        /// The rail stands on the page's LEFT rather than its right, which
        /// looks like the wrong edge for a thumb index until you ask what the
        /// lit tab is fused to. The merge strip makes a raised tab part of the
        /// page it opens, and the page it opens is the left one of the spread —
        /// so the tabs have to stand outside THAT page, with the fusion facing
        /// in across the gap. On the far edge they would be fused to the Trail,
        /// which no tab opens.
        /// </para>
        /// </summary>
        private void ApplyTabFold(bool wide, float canvasHeight)
        {
            if (_tabsLayout == null || _tabRail == null || _tabsBar == null)
            {
                return;
            }

            // Wide is not the same question as tall enough — a 32:9 canvas is a
            // spread with no room for a column of fingertips beside the page,
            // and the bar is what always fits. The Trail keeps no tab on a
            // spread, so the rail seats one fewer than the book has.
            var railed = wide && JournalLayout.RailSeatsTabs(canvasHeight, Tabs.Length - 1);

            // The plates move; nothing else about them does. In Tabs order, so
            // the rail reads top-to-bottom the way the bar reads left-to-right.
            var host = railed ? _tabRail : _tabsBar;
            foreach (var id in Tabs)
            {
                if (!_tabButtons.TryGetValue(id, out var tab))
                {
                    continue;
                }

                if (tab.transform.parent != host)
                {
                    tab.transform.SetParent(host, false);
                }

                tab.transform.SetAsLastSibling();
                if (_tabMerges.TryGetValue(id, out var merge))
                {
                    OrientTabMerge(merge, railed);
                }
            }

            _tabsBar.gameObject.SetActive(!railed);
            _tabRail.gameObject.SetActive(railed);

            // A bar standing on a spread it could not rail is still a bar on a
            // very broad canvas: stretched five ways it hands each tab several
            // hundred units to seat one short word. Packed at the left margin
            // it reads as index tabs, which is the mock's own answer for a wide
            // bar (`body.wide .tabs { justify-content:flex-start }`).
            _tabsLayout.childForceExpandWidth = !wide;
            _tabsLayout.childAlignment = wide ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        }

        /// <summary>
        /// Face the merge strip at the page. It is paper drawn over the lit
        /// tab's own border and on across the gap, so the raised tab reads as
        /// PART of the page rather than as a lighter plate beside it — which
        /// means it has to lie along whichever edge the page is on: the tab's
        /// top in the bottom bar, its right-hand side in the fore-edge rail.
        /// </summary>
        private static void OrientTabMerge(GameObject merge, bool wide)
        {
            var rect = (RectTransform)merge.transform;
            if (wide)
            {
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = Vector2.one;
                // Left 4 to cover the tab's own rule, right across the rail's
                // gap; inset top and bottom so the other rules still frame it.
                rect.offsetMin = new Vector2(-4f, 3f);
                rect.offsetMax = new Vector2(TabRailGap + 2f, -3f);
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            // Down 4 to cover the tab's own top border, up 14 across the bar
            // padding and root spacing; inset so the side rules still frame it.
            rect.offsetMin = new Vector2(3f, -4f);
            rect.offsetMax = new Vector2(-3f, 14f);
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

            var side = JournalLayout.SideMargin(canvasWidth, PageMargin, _wide);
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
        private void ApplyWideLayout(bool wide, float canvasHeight)
        {
            // Idempotent on purpose: the first fit runs before the tab bar
            // exists (so the hide below is a no-op), and the one at the end of
            // BuildUi has to be able to reassert it without counting as a
            // change and forcing a second rebuild of a page just built.
            var changed = wide != _wide;
            _wide = wide;
            ApplyHeadFold(wide);
            ApplyTabFold(wide, canvasHeight);

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
                Dirty = true;
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
        /// The band the open page stands in: the journal's tabs down the
        /// fore-edge, and the scroll beside them.
        /// <para>
        /// The tabs are here rather than in a row of their own because
        /// landscape is short of one thing and it is not width. A bottom bar
        /// costs 132 units of a ~1,150-unit canvas — an eighth of the scarce
        /// axis — to say five short words that would sit perfectly happily in
        /// the abundant one, and a journal's index tabs run down its fore-edge
        /// anyway. In a column the bar goes back to the bottom edge, where a
        /// thumb expects it and where height is not the thing in short supply
        /// (see <see cref="ApplyTabFold"/>).
        /// </para>
        /// <para>
        /// ONE scroll, holding both columns of a spread — not a scroll per page.
        /// That was tried on 2026-08-14 and taken out again the same day: two
        /// scrollbars on one screen is a worse thing to look at than one, and
        /// the left page's rode the spine, where it read as a rule cutting the
        /// book in half rather than as a control. The answer to the Trail being
        /// dragged out of view is to spend less of the page on chrome so there
        /// is less scrolling to do, which is what everything else in this pass
        /// is for.
        /// </para>
        /// </summary>
        private void BuildPageArea(RectTransform root)
        {
            _pageArea = MakeRect("PageRow", root);
            // The only flexible row in the chrome — the page is what gives when
            // anything above it grows (see UpdateWorldGap).
            Flexible(_pageArea.gameObject, 2f);
            var row = _pageArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true;
            row.childControlHeight = true;
            // The rail keeps a width of its own; the page takes everything left.
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            row.childAlignment = TextAnchor.UpperLeft;
            row.spacing = TabRailGap;

            // Before the page, so the tabs stand on the outer edge with the
            // open page they belong to on their right — which is also the way
            // the merge strip has to face (see OrientTabMerge).
            BuildTabRail(_pageArea);
            _body = BuildScroll(_pageArea);
        }

        /// <summary>
        /// Build the spread's frame inside the scroll content: two equal
        /// columns side by side, top-aligned, and return them. The columns are
        /// ordinary vertical layouts, so a page cannot tell it is one.
        /// </summary>
        private void BuildSpreadColumns(out RectTransform left, out RectTransform right)
        {
            var spread = MakeRect("Spread", _body);
            var row = spread.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            // Top-aligned: the two pages are independent flows, and a short
            // Camp page must not stretch to the Trail's length.
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.UpperLeft;
            row.spacing = JournalLayout.SpreadGap;

            left = MakeColumn("PageLeft", spread);
            right = MakeColumn("PageRight", spread);
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
        /// The running head over one page of a spread: the page's name in
        /// small caps with a rule under it, which is the mock's
        /// <c>.running-head</c> down to the border (docs/wildgrove-journal.html
        /// § the 880px breakpoint).
        /// <para>
        /// BOTH pages carry one, as the mock's four sections do. The Trail's
        /// stood alone until 2026-08-14, and the two halves of the book then
        /// began on different lines — the left page's first card started level
        /// with the right page's HEAD rather than with its first card — so the
        /// spread read as two panes that happened to be side by side. The rule
        /// is the other half of it: without one the head floated in the paper
        /// under the world band with nothing to sit on.
        /// </para>
        /// <para>
        /// On the Camp and the Warden the head is the thing's own NAME, with
        /// the quill that changes it — see <see cref="PageIsNamed"/> for why
        /// that replaced a card. Those two are the only heads a single column
        /// draws: there the title above already says which page this is, so a
        /// label would restate it, and a name does not.
        /// </para>
        /// </summary>
        private void RunningHead(RectTransform column, string tab)
        {
            var named = PageIsNamed(tab);
            if (!_wide && !named)
            {
                return;
            }

            if (named)
            {
                // A row, because the quill stands beside the name and carries
                // the 48dp plate that sets this line's height whatever the name
                // is — the reason it is handed that height at build time rather
                // than growing into it (JournalWidgets.Row).
                var row = Row(column, NameHeadRow);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 2;

                var name = MakeText(row.transform, PageName(tab), 22, TextAnchor.MiddleCenter, Ink, _serif);
                var captured = tab;
                IconButton(row.transform, QuillSprite(), 38f, NameHeadTouch, () => OpenPageNaming(captured));
                _liveUpdaters.Add(() => name.text = PageName(captured));
            }
            else
            {
                MakeText(column, RunningHeadName(tab), 13, TextAnchor.MiddleCenter, Ink2, _smallCaps);
            }

            MakeHairline(column);
        }

        /// <summary>The quill's touch plate in a named running head — 120 units ≈ Android's 48dp floor.</summary>
        private const float NameHeadTouch = 120f;

        /// <summary>The line that holds it: the plate plus the row's own 6/6 padding.</summary>
        private const float NameHeadRow = NameHeadTouch + 12f;

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
                // The two rows that GIVE — the band being sized, and the page
                // area it is sized against. Everything else is pinned chrome
                // and is measured. (_pageArea, not _body: the page is a row of
                // scroll views now, and _body is the content inside one of
                // them — several viewports tall, and not a child of the root at
                // all, so it would neither be found here nor be the right
                // measure if it were.)
                if (child == _worldGap || child == _pageArea)
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
            _tabsBar = (RectTransform)barGo.transform;
            // ≥117 units ≈ Android's 48dp touch floor — 84 was ~32dp tabs.
            FixedHeight(barGo, JournalLayout.TabDepth);
            var layout = barGo.AddComponent<HorizontalLayoutGroup>();
            _tabsLayout = layout;
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
            var go = new GameObject("Tab_" + id, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(bar, false);
            // A size of its own, for the rail: a vertical group controls height
            // and does not force-expand it, so without this the tabs would
            // stack to nothing. Both are PREFERRED, never minimums — the
            // phone's bar shares 1,048 units between five tabs, and a floor of
            // the rail's width would overflow it.
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = JournalLayout.SpreadTabWidth;
            element.preferredHeight = JournalLayout.TabDepth;
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
            OrientTabMerge(merge, _wide);

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
            // Everything the rail beside it does not take.
            FlexibleWidth(scrollGo, 1f);

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
