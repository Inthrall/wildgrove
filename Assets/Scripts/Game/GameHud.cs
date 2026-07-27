using System;
using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Input;
using Wildgrove.Game.Services;
using Wildgrove.Game.World;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalSprites;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Warden's Journal — the code-built uGUI HUD, laid out after the
    /// docs/wildgrove-journal.html mock: a paper page with a title header, a
    /// currency ledger, a handwritten margin note, a pinned Rite / Fold
    /// tracker, and four journal tabs along the bottom (Trail · Camp ·
    /// Warden · Record) — set in the journal's own type (IM Fell / Caveat /
    /// Lora), ruled ink borders, paper grain, and the small motion touches
    /// (tend flash, the carrier on the trail line). All game logic stays in
    /// Wildgrove.Sim; this only reads state and calls <see cref="GameLoop"/>
    /// actions.
    /// <para>
    /// The chrome is kept deliberately thin: everything pinned here is paid
    /// for out of the open page, which is the only row that can give. A bar
    /// belongs up here only if it is read on every tab — the trail-home line
    /// went back to the Trail page and the camp actions to the Camp page for
    /// exactly that reason, and the ledger carries the three meta currencies
    /// rather than the whole stores list (which grows all game).
    /// </para>
    /// <para>
    /// This is the HUD coordinator: it owns the MonoBehaviour lifecycle, the
    /// persistent chrome (header/ledger/tracker/tabs), the scroll body and its
    /// live-update loop, and the section builders (<see cref="TrailPage"/>,
    /// <see cref="CampPage"/>, <see cref="WardenPage"/>, <see cref="RecordPage"/>,
    /// <see cref="JournalSheets"/>) that build each tab's cards. The journal's
    /// palette, generated sprites, uGUI factory, and formatters live in the
    /// static <c>Journal*</c> helpers. The body rebuilds only when its structure
    /// (or the open tab) changes; live numbers refresh through
    /// <see cref="_liveUpdaters"/>.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class GameHud : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;

        // The world strip's share of the page, and the floor the open page is
        // never squeezed below. See UpdateWorldGap.
        private const float StripShareMax = 0.26f;
        private const float StripShareMin = 0.14f;
        private const float MinPageShare = 0.32f;

        private GameLoop _loop;
        private IGameInput _input;
        private Font _font;      // body — Lora
        private Font _serif;     // titles, verses, lore — IM Fell English
        private Font _smallCaps; // chrome: eyebrows, card heads, tabs, buttons — IM Fell English SC
        private Font _hand;      // margin notes, posted lines, flourishes — Caveat
        private WorldView _world;

        // The journal's section builders — one per tab, plus the modal sheets.
        private JournalText _labels;
        private TrailPage _trail;
        private CampPage _camp;
        private WardenPage _warden;
        private RecordPage _record;
        private JournalSheets _sheets;

        private Text _title;
        private Text _slotCounter;
        private Text _ledger;
        private Text _note;
        private Text _trackerText;
        private GameObject _trackerPanel;
        private Button _foldButton;

        private RectTransform _worldGap;
        private RectTransform _body;
        private Transform _modalLayer;
        private Canvas _canvas;
        private RectTransform _root;
        private ScrollRect _scroll;
        private LayoutElement _worldGapElement;
        private RectTransform _feedbackLayer;
        private RectTransform _firstVerseCard;
        private string _pendingScroll;
        private string _builtTab;
        private Rect _appliedSafeArea;
        private float _appliedCanvasHeight;
        private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> _tabLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, GameObject> _tabOuterRules = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _tabMerges = new Dictionary<string, GameObject>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly List<Action> _liveUpdaters = new List<Action>();
        // Per-frame animation hooks (tend flash, the carrier on the trail line) —
        // cleared with the body they animate.
        private readonly List<Action> _frameUpdaters = new List<Action>();
        private readonly Dictionary<string, float> _flashAges = new Dictionary<string, float>();
        private readonly Dictionary<string, Text> _tendFlashes = new Dictionary<string, Text>();
        private float _refreshCountdown;
        private string _structureSignature;
        private bool _dirty;

        private string _tab = TabTrail;

        // One open sheet at a time; the dim layer blocks input beneath it.
        private GameObject _sheet;

        // First-run teaching. The margin note used to be overwritten forever by
        // the first action's outcome note — now outcome notes drift back to the
        // teaching line after a few seconds until each core gesture (post
        // someone, catch a windfall) has been performed once. Posting is
        // derived from state (a loaded save with a posted kith has learned it);
        // the catch is a one-time flag that survives restarts.
        private const string HintCaughtKey = "wildgrove.hint.caught";
        private const float NoteRevertSeconds = 6f;
        private bool _hintPostDone;
        private bool _hintCatchDone;
        private float _noteRevert;

        // ─────────────────────────── Section access ──────────────────────────
        // The section builders reach shared HUD state and coordinator calls
        // through these; see JournalSection.

        internal GameLoop Loop => _loop;
        internal bool Dirty { get => _dirty; set => _dirty = value; }
        internal RectTransform Body => _body;
        internal Transform ModalLayer => _modalLayer;
        internal GameObject Sheet { get => _sheet; set => _sheet = value; }
        internal RectTransform FirstVerseCard { get => _firstVerseCard; set => _firstVerseCard = value; }
        internal List<Action> LiveUpdaters => _liveUpdaters;
        internal List<Action> FrameUpdaters => _frameUpdaters;
        internal Dictionary<string, float> FlashAges => _flashAges;
        internal Dictionary<string, Text> TendFlashes => _tendFlashes;
        internal JournalText Labels => _labels;
        internal JournalSheets Sheets => _sheets;

        private void Awake()
        {
            _loop = GetComponent<GameLoop>();
            _input = new InputSystemGameInput();
            _world = GetComponent<WorldView>();
            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _font = LoadFont("Lora", builtin);
            _serif = LoadFont("IMFellEnglish", _font);
            _smallCaps = LoadFont("IMFellEnglishSC", _serif);
            _hand = LoadFont("Caveat", _font);
            JournalWidgets.Init(_font, _serif, _smallCaps, _hand);

            _labels = new JournalText(_loop);
            _trail = new TrailPage(this);
            _camp = new CampPage(this);
            _warden = new WardenPage(this);
            _record = new RecordPage(this);
            _sheets = new JournalSheets(this);

            EnsureEventSystem();
            BuildChrome();
            _hintCatchDone = PlayerPrefs.GetInt(HintCaughtKey, 0) == 1;
            var hint = HintText();
            if (hint != null)
            {
                _note.text = hint;
            }
        }

        /// <summary>
        /// The teaching line for whichever core gesture is still unlearned, or
        /// null once both are. Keyboard/gamepad tail only where one can exist —
        /// on a phone the margin note is flavour, not a manual for keys it
        /// doesn't have.
        /// </summary>
        private string HintText()
        {
            var catchTail = Application.isMobilePlatform ? string.Empty : " · space / (A) catches one";
            if (!_hintPostDone)
            {
                return _hintCatchDone
                    ? "tap a plate to post someone — the land only gives to the posted."
                    : "tap a plate to post someone · catch the windfalls drifting up the strip" + catchTail;
            }

            return _hintCatchDone
                ? null
                : "catch the windfalls drifting up the strip — each pays a burst of goods" + catchTail;
        }

        private void Update()
        {
            if (_loop == null || _loop.State == null)
            {
                return;
            }

            FitLayoutToScreen();
            ReportWorldStrip();
            HandleBack();
            HandleWorldTap();

            for (var i = 0; i < _frameUpdaters.Count; i++)
            {
                _frameUpdaters[i]();
            }

            // An outcome note drifts back to the teaching line while a core
            // gesture is still unlearned — the one instruction in the game
            // must survive its reader's first tap.
            if (_noteRevert > 0f)
            {
                _noteRevert -= Time.deltaTime;
                if (_noteRevert <= 0f)
                {
                    var hint = HintText();
                    if (hint != null && _note != null)
                    {
                        _note.text = hint;
                    }
                }
            }

            _refreshCountdown -= Time.deltaTime;
            if (_refreshCountdown <= 0f)
            {
                _refreshCountdown = RefreshInterval;
                // On the cadence, not per-frame: PumpSheets scans all zones for
                // the next unread waystone — a quarter-second delay to raise a
                // sheet is imperceptible and keeps that scan off the hot path.
                _sheets.PumpSheets();
                RefreshChrome(); // cadence, not per-frame — avoids string allocs every frame
                var signature = StructureSignature();
                if (_dirty || signature != _structureSignature)
                {
                    _dirty = false;
                    _structureSignature = signature;
                    RebuildBody();
                }

                for (var i = 0; i < _liveUpdaters.Count; i++)
                {
                    _liveUpdaters[i]();
                }
            }
        }

        /// <summary>
        /// Open a journal tab by name. Legacy store-capture page names from the
        /// pre-journal HUD map onto the tab that now carries that content.
        /// </summary>
        public void OpenTab(string tab)
        {
            switch (tab)
            {
                case "gather":
                case "rite":
                    tab = TabTrail;
                    break;
                case "craft":
                case "build":
                    tab = TabCamp;
                    break;
                case "collect":
                    tab = TabRecord;
                    break;
            }

            if (Array.IndexOf(Tabs, tab) < 0 || tab == _tab)
            {
                return;
            }

            _tab = tab;
            _dirty = true;
            foreach (var id in Tabs)
            {
                StyleTab(id, id == _tab);
            }
        }

        /// <summary>
        /// The open tab wears the page: paper background, full-ink bold label,
        /// the cards' outer rule, and the merge strip — the mock's raised tab
        /// physically fuses with the page, and that continuity cue reads
        /// pre-attentively where a lighter tint alone never did.
        /// </summary>
        private void StyleTab(string id, bool active)
        {
            _tabButtons[id].GetComponent<Image>().color = active ? PagePaper : DeepPaper;
            var label = _tabLabels[id];
            label.color = active ? Ink : Ink2;
            label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            _tabOuterRules[id].SetActive(active);
            _tabMerges[id].SetActive(active);
        }

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
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.padding = new RectOffset(16, 16, 10, 6);
            rootLayout.spacing = 6;

            // Header — the page title, centred like the mock's page head. The
            // mock's eyebrow above it ("THE RECORD") only ever restated the lit
            // tab three rows below, in a second type style, for a line of
            // height the page needed more; the camp number it also carried now
            // reads on the Record page's Standing card. The header band is
            // transparent: the page paper shows through, but it must NOT
            // swallow pointer raycasts (an Image would).
            var headerGo = MakeRect("Header", root).gameObject;
            var headerLayout = headerGo.AddComponent<VerticalLayoutGroup>();
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.spacing = 0;
            // 27 authored ≈ the old 36 at the previous FontScale — the title
            // was already big enough; the scale bump is for the working text.
            _title = MakeText(headerGo.transform, string.Empty, 27, TextAnchor.MiddleCenter, Ink, _serif);

            // Ledger — the three meta currencies, hairline-ruled like the mock.
            // It used to run every held resource, which is the one chrome row
            // whose height GROWS with the save: two lines at Sunfield, four by
            // the second camp, and every one of them taken off the page. The
            // stores now read on the Record page beside their own entries, and
            // a tap here goes there.
            MakeHairline(root);
            _ledger = MakeText(root, string.Empty, 19, TextAnchor.MiddleCenter, Ink);
            var ledgerButton = _ledger.gameObject.AddComponent<Button>();
            ledgerButton.targetGraphic = _ledger;
            ledgerButton.onClick.AddListener(() => OpenTab(TabRecord));
            MakeHairline(root);

            // Margin note — the handwritten aside.
            _note = MakeText(root, string.Empty, 24, TextAnchor.MiddleLeft, Ink2, _hand);

            // Pinned tracker — verse progress before the Rite consents, the
            // Fold forecast (with its button) after.
            var trackerGo = MakePanel("Tracker", root, CardPaper);
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
            var trackerButton = trackerGo.AddComponent<Button>();
            trackerButton.onClick.AddListener(() => ScrollToOnTrail("verse"));
            AddBorder(trackerGo, Ink2);
            _trackerText = MakeText(trackerGo.transform, string.Empty, 21, TextAnchor.MiddleCenter, Ink, _serif);
            FlexibleWidth(_trackerText.gameObject, 1f);
            _foldButton = Button(trackerGo.transform, "Fold the camp", 260, _sheets.OpenMigrationSheet);
            KeyAction(_foldButton);
            _foldButton.gameObject.SetActive(false);

            // World gap — the WorldView strip draws here. Capped rather than
            // flexible: on tall screens the slack goes to the page (more cards
            // visible), not to empty paper around the node strip. The height
            // is set from the canvas size in FitLayoutToScreen.
            var gap = MakeRect("WorldGap", root);
            _worldGap = gap;
            _worldGapElement = gap.gameObject.AddComponent<LayoutElement>();

            // Slots-in-use counter, pinned inside the strip band's top-right
            // corner — it counts the badges below it, so it belongs with them
            // rather than up in the page head. Never a raycast target: the
            // whole band is the posting/catching surface.
            _slotCounter = MakeText(gap, string.Empty, 17, TextAnchor.MiddleRight, Ink2, _smallCaps);
            _slotCounter.gameObject.name = "SlotCounter";
            _slotCounter.raycastTarget = false;
            var counterRect = (RectTransform)_slotCounter.transform;
            counterRect.anchorMin = Vector2.one;
            counterRect.anchorMax = Vector2.one;
            counterRect.pivot = Vector2.one;
            counterRect.sizeDelta = new Vector2(380f, 44f);
            counterRect.anchoredPosition = new Vector2(-6f, -4f);

            // The trail-home line and the camp actions used to be pinned here,
            // between the strip and the page. Both are page chrome wearing a
            // global badge — the carrier walking home is the Trail's business
            // and the rewarded time-skip is the Camp's — and together they cost
            // the page a fifth of its height on every tab. They now head their
            // own pages; the strip above stays global, so catching and posting
            // are still reachable from everywhere.

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
        /// cutout and gesture areas (the tabs bar used to sit flush with the
        /// screen edge, inside Android's home-swipe zone), and cap the world
        /// strip's share of the screen. Re-applied whenever the safe area or
        /// canvas size changes.
        /// </summary>
        private void FitLayoutToScreen()
        {
            var safe = Screen.safeArea;
            var canvasHeight = ((RectTransform)_canvas.transform).rect.height;
            if (safe == _appliedSafeArea && Mathf.Approximately(canvasHeight, _appliedCanvasHeight))
            {
                return;
            }

            _appliedSafeArea = safe;
            _appliedCanvasHeight = canvasHeight;

            var scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            _root.offsetMin = new Vector2(safe.xMin / scale, safe.yMin / scale);
            _root.offsetMax = new Vector2((safe.xMax - Screen.width) / scale, (safe.yMax - Screen.height) / scale);

            Canvas.ForceUpdateCanvases();
            UpdateWorldGap();
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
        /// short one.
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

            var target = Mathf.Clamp(
                available - chrome - available * MinPageShare,
                available * StripShareMin,
                available * StripShareMax);
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

        // ─────────────────────────── Chrome refresh ──────────────────────────

        private void RefreshChrome()
        {
            if (!_hintPostDone)
            {
                var state = _loop.State;
                _hintPostDone = Kith.Walking(state) > 0
                                || Warden.PostNodeId(state) != null
                                || Warden.IsWandering(state);
                if (_hintPostDone && _noteRevert <= 0f && _note != null)
                {
                    // The gesture just landed (or a posted save just loaded) —
                    // advance the teaching line rather than leaving stale advice.
                    _note.text = HintText() ?? string.Empty;
                }
            }

            RefreshHeader();
            RefreshLedger();
            RefreshTracker();
            UpdateWorldGap();
        }

        private void RefreshHeader()
        {
            string title;
            switch (_tab)
            {
                case TabCamp:
                    title = "Fire, Bench & Caravan";
                    break;
                case TabWarden:
                    title = "Kit & Crafts";
                    break;
                case TabRecord:
                    title = "The Journal's Back Pages";
                    break;
                default:
                    var zone = _labels.LatestZone();
                    title = zone != null ? zone.displayName : "The Trail";
                    break;
            }

            _title.text = title;
            _slotCounter.text = _loop.KithWalking() + " / " + _loop.KithSlots() + " POSTED";
        }

        private void RefreshLedger()
        {
            // Legacy Text wraps at any plain space — a non-breaking one inside
            // each label-value pair means the line only ever breaks BETWEEN
            // entries, never between a name and its number.
            const string pair = " ";
            var state = _loop.State;
            // The three meta currencies only. The held stores used to run here
            // too — one entry per resource, so the line grew a wrap every zone
            // and quietly ate the page it sits above. They read on the Record
            // page now, each beside its own compendium entry, where "how much
            // do I hold" is asked deliberately rather than glanced at.
            var parts = new List<string>();

            // OchreInk, not Ochre — the theme's own rule: plain ochre fails
            // contrast at ledger size, and Renown is read hundreds of times.
            parts.Add("<color=" + OchreInkHex + ">RENOWN" + pair + "<b>" + NumberFormat.Short(state.renown) + "</b></color>");
            // Verdure appears once the fold economy is real — and styled as
            // RENOWN's peer, not lowercase flavour. Both meta numbers go
            // through NumberFormat so they never read "1234" beside "1.23K".
            if (state.verdurePoints > 0.0)
            {
                parts.Add("<color=" + MossDeepHex + ">VERDURE" + pair + "<b>"
                          + NumberFormat.Short(new BigDouble(System.Math.Floor(state.verdurePoints))) + "</b></color>");
            }

            if (state.amber > 0.0)
            {
                // The paid currency wears its own resin ink and full caps —
                // lowercase-ochre made it a visual twin of RENOWN, and that's
                // a real-money misread waiting to happen.
                parts.Add("<color=" + AmberInkHex + ">AMBER" + pair + "<b>"
                          + NumberFormat.Short(new BigDouble(System.Math.Floor(state.amber))) + "</b></color>");
            }

            // The tracker's guillemet marks a banner as a link; the ledger is
            // one too now (it opens the Record page's stores), so it wears the
            // same mark rather than being a tap nobody would guess at.
            _ledger.text = string.Join(" · ", parts)
                           + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">»</color></size>";
        }

        private void RefreshTracker()
        {
            if (_loop.CanMigrate())
            {
                var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
                // The banner carries the curve now — a percentage is the one
                // form of "how close am I" a player can read without knowing
                // what a Renown threshold is.
                _trackerText.text = "<color=" + OchreInkHex + ">THE FOLD</color> · +<b>"
                                    + Mathf.FloorToInt((float)gain) + "</b> Verdure banked · "
                                    + Mathf.FloorToInt((float)_loop.ProgressToNextVerdure() * 100f) + "% to the next";
                _foldButton.gameObject.SetActive(true);
                _trackerPanel.SetActive(true);
                return;
            }

            _foldButton.gameObject.SetActive(false);
            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite != null && rite.verses != null)
            {
                foreach (var verse in rite.verses)
                {
                    if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse) || Rite.IsVerseComplete(_loop.State, _loop.Data, verse))
                    {
                        continue;
                    }

                    var done = Rite.CompletedSlotCount(_loop.State, verse);
                    var need = _loop.Data.rites.chooseCount;
                    // The trailing guillemet marks the banner as a link — it
                    // jumps to the verse card, far down the Trail page.
                    _trackerText.text = "Verse of " + _labels.ZoneName(verse.zone) + " — <b>"
                                        + Mathf.Min(done, need) + " of " + need + "</b> answered  »";
                    _trackerPanel.SetActive(true);
                    return;
                }
            }

            // Nothing pinned — hide the panel outright; an empty bordered
            // strip reads as a rendering bug.
            _trackerText.text = string.Empty;
            _trackerPanel.SetActive(false);
        }

        internal void SetNote(string text)
        {
            if (_note != null)
            {
                _note.text = text;
                _noteRevert = NoteRevertSeconds;
            }
        }

        /// <summary>
        /// Android's hardware/gesture Back (Escape on desktop): dismiss the open
        /// sheet the safe way, step back to the Trail tab, then follow platform
        /// convention and exit (the run saves on pause/quit).
        /// </summary>
        private void HandleBack()
        {
            if (!_input.BackTriggered)
            {
                return;
            }

            if (_sheet != null)
            {
                _sheets.DismissSheet();
            }
            else if (_tab != TabTrail)
            {
                OpenTab(TabTrail);
            }
            else if (Application.isMobilePlatform)
            {
                Application.Quit();
            }
        }

        private void ReportWorldStrip()
        {
            if (_world == null)
            {
                return;
            }

            // Windfalls freeze under a sheet — they're ephemeral presentation,
            // and burning their lifetime behind a modal punished opening one.
            _world.Frozen = _sheet != null;
            _world.CatchHintPending = !_hintCatchDone;
            _worldGap.GetWorldCorners(Corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);
            _world.StripScreenRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void HandleWorldTap()
        {
            if (_sheet != null)
            {
                return;
            }

            if (_input.TendTriggered(out var screenPosition))
            {
                if (screenPosition.HasValue)
                {
                    // A drifting bubble floats over everything — the catch
                    // wins before any plate or badge underneath it.
                    var caught = _world != null ? _world.PopBubbleAt(screenPosition.Value) : null;
                    if (caught != null)
                    {
                        CollectBubble(caught);
                        return;
                    }

                    // A whiffed catch near a windfall must NOT punish the miss
                    // with a full posting sheet — swallow it as a near miss.
                    if (_world != null && _world.NudgeNearMiss(screenPosition.Value))
                    {
                        return;
                    }

                    // Plates and badges resolve together, nearest centre wins —
                    // a node plate IS the assign gesture now (tap-to-tend
                    // became the bubbles above), and the trail/wander plates
                    // resolve to their station.
                    var station = _world != null ? _world.PostAtScreenPoint(screenPosition.Value, out _) : null;
                    if (station != null)
                    {
                        _sheets.OpenPostingSheet(station);
                    }
                }
                else
                {
                    // Space / pad-A: catch the longest-adrift bubble.
                    var caught = _world != null ? _world.PopOldestBubble() : null;
                    if (caught != null)
                    {
                        CollectBubble(caught);
                    }
                }
            }
        }

        private void CollectBubble(NodeState node)
        {
            if (!_hintCatchDone)
            {
                _hintCatchDone = true;
                PlayerPrefs.SetInt(HintCaughtKey, 1);
            }

            var gained = _loop.PopBubble(node);
            if (gained <= BigDouble.Zero)
            {
                // The node went fallow while the bubble drifted — it pops
                // empty, and the strip itself says so (grey deflate), not
                // just a sentence elsewhere.
                _world?.ResolveCatch("nothing inside", false);
                SetNote("the windfall bursts over the " + node.resourceId + " — nothing inside.");
                return;
            }

            // The reward lands where the eye is: a burst and a rising "+N" at
            // the catch point. The journal-row flash and margin note echo it.
            _world?.ResolveCatch("+" + NumberFormat.Short(gained) + " " + node.resourceId, true);
            if (_tendFlashes.TryGetValue(node.id, out var flash))
            {
                flash.text = "+ " + NumberFormat.Short(gained) + " " + node.resourceId;
            }

            _flashAges[node.id] = 0f;
            SetNote("caught a windfall — " + NumberFormat.Short(gained) + " " + node.resourceId + ", straight to camp.");
        }

        // ─────────────────────────── Body ────────────────────────────────────

        private string StructureSignature()
        {
            var state = _loop.State;
            var owned = state.purchasedUpgradeIds.Count;
            var recipes = _loop.AvailableRecipes().Count;
            var buildings = 0;
            foreach (var pair in state.buildingLevels)
            {
                buildings += pair.Value;
            }

            var pristine = 0;
            foreach (var pair in state.pristineResources)
            {
                if (pair.Value > BigDouble.Zero)
                {
                    pristine++;
                }
            }

            var recordedInsects = 0;
            foreach (var insect in _loop.Data.insects)
            {
                if (Insects.IsRecorded(state, insect))
                {
                    recordedInsects++;
                }
            }

            var revealedVerses = 0;
            var rite = Rite.CurrentRite(state, _loop.Data);
            if (rite != null && rite.verses != null)
            {
                foreach (var verse in rite.verses)
                {
                    if (Rite.IsVerseRevealed(state, _loop.Data, verse))
                    {
                        revealedVerses++;
                    }
                }
            }

            return _tab + "/" + state.roster.Count + "/" + state.nodes.Count + "/" + state.digSites.Count
                   + "/" + owned + "/" + recipes + "/" + buildings + "/" + pristine
                   + "/" + _loop.UnlockedSkills().Count + "/" + state.gearBySlot.Count
                   + "/" + state.fixedResources.Count + "/" + recordedInsects
                   + "/" + state.builtPlanters.Count + "/" + revealedVerses
                   + "/" + Compendium.DiscoveredCount(state, _loop.Data);
        }

        private void RebuildBody()
        {
            // A structure change mid-read shouldn't snap the reader back to
            // the top of the page — keep the scroll unless the tab changed.
            var keepPosition = _builtTab == _tab && _scroll != null ? _scroll.verticalNormalizedPosition : 1f;
            _builtTab = _tab;
            var landmark = _pendingScroll;
            _pendingScroll = null;
            _firstVerseCard = null;

            _liveUpdaters.Clear();
            _frameUpdaters.Clear();
            _tendFlashes.Clear();
            for (var i = _body.childCount - 1; i >= 0; i--)
            {
                Destroy(_body.GetChild(i).gameObject);
            }

            switch (_tab)
            {
                case TabCamp:
                    _camp.BuildCampPage();
                    break;
                case TabWarden:
                    _warden.BuildWardenPage();
                    break;
                case TabRecord:
                    _record.BuildRecordPage();
                    break;
                default:
                    _trail.BuildTrailPage();
                    break;
            }

            StartCoroutine(SettleScroll(keepPosition, landmark));
        }

        /// <summary>
        /// Open the Trail tab and bring a landmark card ("verse") into view —
        /// the tracker deep-links into a page that is otherwise a long scroll.
        /// </summary>
        private void ScrollToOnTrail(string landmark)
        {
            _pendingScroll = landmark;
            OpenTab(TabTrail);
            if (!_dirty)
            {
                // Already on the Trail with no rebuild coming — jump now.
                StartCoroutine(SettleScroll(_scroll.verticalNormalizedPosition, _pendingScroll));
                _pendingScroll = null;
            }
        }

        private RectTransform LandmarkCard(string landmark)
        {
            switch (landmark)
            {
                case "verse":
                    return _firstVerseCard;
                default:
                    return null;
            }
        }

        private System.Collections.IEnumerator SettleScroll(float normalized, string landmark)
        {
            // Destroyed children leave the layout at end of frame — wait one
            // so the fresh page has its real height before positioning it.
            yield return null;
            if (_scroll == null || _body == null)
            {
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            var target = LandmarkCard(landmark);
            if (target != null)
            {
                ScrollTo(target);
            }
            else
            {
                _scroll.verticalNormalizedPosition = Mathf.Clamp01(normalized);
            }
        }

        private void ScrollTo(RectTransform target)
        {
            var range = _body.rect.height - _scroll.viewport.rect.height;
            if (range <= 0f)
            {
                return;
            }

            // The content's pivot sits at its top, so a child's top edge in
            // content-local space is its distance down the page.
            var local = (Vector2)_body.InverseTransformPoint(target.position);
            var distanceFromTop = -(local.y + target.rect.yMax);
            _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((distanceFromTop - 6f) / range);
        }

        /// <summary>
        /// A transient outcome note that rises and fades where the action
        /// happened — the margin note alone is usually off-screen from the
        /// button that was pressed.
        /// </summary>
        internal void Flash(Component near, string message, bool good)
        {
            if (near == null || _feedbackLayer == null)
            {
                return;
            }

            var flash = MakeText(_feedbackLayer, message, 22, TextAnchor.MiddleCenter, good ? MossDeep : Ochre, _hand);
            flash.raycastTarget = false;
            var rect = (RectTransform)flash.transform;
            rect.sizeDelta = new Vector2(700f, 60f);
            rect.position = near.transform.position;
            StartCoroutine(RiseAndFade(flash, rect));
        }

        private System.Collections.IEnumerator RiseAndFade(Text flash, RectTransform rect)
        {
            var colour = flash.color;
            // Start above the control so the note never covers its label.
            var start = rect.anchoredPosition + new Vector2(0f, 34f);
            const float life = 1.1f;
            for (var age = 0f; age < life; age += Time.deltaTime)
            {
                var t = age / life;
                flash.color = new Color(colour.r, colour.g, colour.b, 1f - t);
                rect.anchoredPosition = start + new Vector2(0f, 26f * t);
                yield return null;
            }

            Destroy(flash.gameObject);
        }
    }
}
