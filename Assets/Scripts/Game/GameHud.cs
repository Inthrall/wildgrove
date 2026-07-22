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

namespace Wildgrove.Game
{
    /// <summary>
    /// The Warden's Journal — the code-built uGUI HUD, laid out after the
    /// docs/wildgrove-journal.html mock: a paper page with an eyebrow/title
    /// header, a resource ledger, a handwritten margin note, a pinned Rite /
    /// Fold tracker, and four journal tabs along the bottom (Trail · Camp ·
    /// Warden · Record) — set in the journal's own type (IM Fell / Caveat /
    /// Lora), ruled ink borders, paper grain, and the small motion touches
    /// (tend flash, the carrier on the trail line). All game logic stays in
    /// Wildgrove.Sim; this only
    /// reads state and calls <see cref="GameLoop"/> actions. The body rebuilds
    /// only when its structure (or the open tab) changes; live numbers refresh
    /// through <see cref="_liveUpdaters"/>.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class GameHud : MonoBehaviour
    {
        // The journal palette, lifted from the mock's CSS custom properties.
        private static readonly Color PagePaper = new Color(0.949f, 0.918f, 0.839f, 1f);      // #F2EAD6
        private static readonly Color CardPaper = new Color(0.918f, 0.878f, 0.776f, 1f);      // #EAE0C6
        private static readonly Color DeepPaper = new Color(0.867f, 0.812f, 0.682f, 1f);      // #DDCFAE
        private static readonly Color Ink = new Color(0.227f, 0.192f, 0.149f, 1f);            // #3A3126
        private static readonly Color Ink2 = new Color(0.431f, 0.376f, 0.278f, 1f);           // #6E6047
        private static readonly Color RulePaper = new Color(0.804f, 0.741f, 0.6f, 1f);        // #CDBD99
        private static readonly Color MossWash = new Color(0.83f, 0.815f, 0.7f, 1f);          // paper washed with moss
        private static readonly Color OchreWash = new Color(0.885f, 0.79f, 0.69f, 1f);        // paper washed with ochre
        private static readonly Color MossDeep = new Color(0.333f, 0.392f, 0.247f, 1f);       // #55643F
        private static readonly Color Ochre = new Color(0.627f, 0.353f, 0.212f, 1f);          // #A05A36
        private static readonly Color DimColor = new Color(0.09f, 0.075f, 0.051f, 0.78f);
        private static readonly Color NightInk = new Color(0.09f, 0.075f, 0.051f, 1f);        // #17130D
        private static readonly Color NightText = new Color(0.91f, 0.863f, 0.753f, 1f);       // #E8DCC0

        private const string InkHex = "#3A3126";
        private const string Ink2Hex = "#6E6047";
        private const string OchreHex = "#A05A36";
        // Ochre's small-text sibling: #A05A36 only manages ~4:1 contrast on
        // card paper, which fails at hint sizes — warnings inline in body
        // copy use this darker mix instead.
        private const string OchreInkHex = "#7E421F";
        private const string MossDeepHex = "#55643F";

        private const string TabTrail = "trail";
        private const string TabCamp = "camp";
        private const string TabWarden = "warden";
        private const string TabRecord = "record";
        private static readonly string[] Tabs = { TabTrail, TabCamp, TabWarden, TabRecord };

        private const int UpgradeWindow = 3;
        private const float RefreshInterval = 0.25f;

        private GameLoop _loop;
        private IGameInput _input;
        private Font _font;      // body — Lora
        private Font _serif;     // titles, verses, lore — IM Fell English
        private Font _smallCaps; // chrome: eyebrows, card heads, tabs, buttons — IM Fell English SC
        private Font _hand;      // margin notes, posted lines, flourishes — Caveat
        private WorldView _world;

        private Text _eyebrow;
        private Text _title;
        private Text _ledger;
        private Text _note;
        private Text _trackerText;
        private GameObject _trackerPanel;
        private Button _foldButton;

        // Persistent camp-action buttons (pinned under the node strip). The
        // time-skip ad credits this many hours of gathering.
        private const double TimeSkipHours = 2.0;
        private Button _timeSkipButton;
        private Button _removeAdsButton;
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
        private NodeState _selected;
        private string _exchangeFrom;
        private string _exchangeTo;

        // One open sheet at a time; the dim layer blocks input beneath it.
        private GameObject _sheet;

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
            EnsureEventSystem();
            BuildChrome();
            // Keyboard/gamepad hint only where one can exist — on a phone the
            // margin note is flavour, not a manual for keys it doesn't have.
            SetNote(Application.isMobilePlatform
                ? "tap a node's plate to tend it"
                : "tap a node's plate to tend it · space / (A) tends the selected node");
        }

        private void Update()
        {
            if (_loop == null || _loop.State == null)
            {
                return;
            }

            FitLayoutToScreen();
            ReportWorldStrip();
            HandleTend();
            PumpSheets();

            for (var i = 0; i < _frameUpdaters.Count; i++)
            {
                _frameUpdaters[i]();
            }

            _refreshCountdown -= Time.deltaTime;
            if (_refreshCountdown <= 0f)
            {
                _refreshCountdown = RefreshInterval;
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
        /// and the cards' outer rule — a lighter tint alone doesn't read at a
        /// glance against three near-identical paper buttons.
        /// </summary>
        private void StyleTab(string id, bool active)
        {
            _tabButtons[id].GetComponent<Image>().color = active ? PagePaper : DeepPaper;
            var label = _tabLabels[id];
            label.color = active ? Ink : Ink2;
            label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            _tabOuterRules[id].SetActive(active);
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

            // Header — eyebrow over the title, centred like the mock's page head.
            // The header band is transparent: the page paper shows through, but
            // it must NOT swallow pointer raycasts (an Image would).
            var headerGo = MakeRect("Header", root).gameObject;
            var headerLayout = headerGo.AddComponent<VerticalLayoutGroup>();
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.spacing = 0;
            _eyebrow = MakeText(headerGo.transform, string.Empty, 17, TextAnchor.MiddleCenter, Ink2, _smallCaps);
            _title = MakeText(headerGo.transform, string.Empty, 36, TextAnchor.MiddleCenter, Ink, _serif);

            // Ledger — the running stores line, hairline-ruled like the mock.
            MakeHairline(root);
            _ledger = MakeText(root, string.Empty, 19, TextAnchor.MiddleCenter, Ink);
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
            _foldButton = Button(trackerGo.transform, "Fold the camp", 260, OpenMigrationSheet);
            _foldButton.GetComponent<Image>().color = OchreWash;
            _foldButton.GetComponentInChildren<Text>().color = Ochre;
            _foldButton.gameObject.SetActive(false);

            // World gap — the WorldView strip draws here. Capped rather than
            // flexible: on tall screens the slack goes to the page (more cards
            // visible), not to empty paper around the node strip. The height
            // is set from the canvas size in FitLayoutToScreen.
            var gap = MakeRect("WorldGap", root);
            _worldGap = gap;
            _worldGapElement = gap.gameObject.AddComponent<LayoutElement>();

            // Persistent camp actions, pinned under the node strip so they stay
            // reachable from every journal page.
            BuildCampActions(root);

            // The open journal page — a scroll view the tab pages build into.
            _body = BuildScroll(root);

            // The journal tabs along the bottom edge.
            BuildTabsBar(root);

            // The page paper is the WORLD CAMERA's background (WorldView sets
            // it), not a canvas image — an overlay canvas draws above every
            // camera, so an opaque paper Image here would permanently hide the
            // world strip's node sprites in the gap.

            // Paper grain over the whole page (the mock's noise overlay sits
            // above content too, at low opacity), and the stitched spine.
            var grain = new GameObject("Grain", typeof(Image));
            grain.transform.SetParent(canvasGo.transform, false);
            var grainImage = grain.GetComponent<Image>();
            grainImage.sprite = GrainSprite();
            grainImage.type = Image.Type.Tiled;
            grainImage.raycastTarget = false;
            Stretch((RectTransform)grain.transform);

            var spine = new GameObject("Spine", typeof(Image));
            spine.transform.SetParent(canvasGo.transform, false);
            var spineImage = spine.GetComponent<Image>();
            spineImage.sprite = SpineSprite();
            spineImage.type = Image.Type.Tiled;
            spineImage.raycastTarget = false;
            var spineRect = (RectTransform)spine.transform;
            spineRect.anchorMin = new Vector2(0f, 0f);
            spineRect.anchorMax = new Vector2(0f, 1f);
            // Centred in the page's left gutter, clear of curved screen
            // corners; the rect width matches the sprite tile exactly so the
            // stitch column never gets a clipped second column.
            spineRect.offsetMin = new Vector2(8f, 16f);
            spineRect.offsetMax = new Vector2(14f, -16f);

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

            _worldGapElement.preferredHeight = canvasHeight * 0.26f;
        }

        private void BuildTabsBar(RectTransform root)
        {
            var barGo = MakeRect("Tabs", root).gameObject;
            FixedHeight(barGo, 84);
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
            _tabButtons[id] = button;
            _tabLabels[id] = text;
            _tabOuterRules[id] = outer;
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
            RefreshHeader();
            RefreshLedger();
            RefreshTracker();
        }

        private void RefreshHeader()
        {
            var state = _loop.State;
            string eyebrow;
            string title;
            switch (_tab)
            {
                case TabCamp:
                    eyebrow = "THE CAMP";
                    title = "Fire, Bench & Caravan";
                    break;
                case TabWarden:
                    eyebrow = "THE WARDEN";
                    title = "Kit & Crafts";
                    break;
                case TabRecord:
                    eyebrow = "THE RECORD";
                    title = "The Journal's Back Pages";
                    break;
                default:
                    // The title carries the zone — repeating its id in the
                    // eyebrow said the same thing twice in two type styles.
                    var zone = LatestZone();
                    eyebrow = "THE TRAIL";
                    title = zone != null ? zone.displayName : "The Trail";
                    break;
            }

            if (state.migrationCount > 0)
            {
                eyebrow += " · CAMP " + (state.migrationCount + 1);
            }

            _eyebrow.text = eyebrow;
            _title.text = title;
        }

        private void RefreshLedger()
        {
            var state = _loop.State;
            var parts = new List<string>();
            foreach (var resource in _loop.Data.resources)
            {
                var stock = state.GetResource(resource.id);
                // Only what the camp actually holds — "nuts 0" is noise, and
                // fewer entries keeps the line from wrapping mid-pair.
                if (stock > BigDouble.Zero)
                {
                    parts.Add(resource.id + " <b>" + NumberFormat.Short(stock) + "</b>");
                }
            }

            parts.Add("<color=" + OchreHex + ">RENOWN <b>" + NumberFormat.Short(state.renown) + "</b></color>");
            // Verdure appears once the fold economy is real — and styled as
            // RENOWN's peer, not lowercase flavour.
            if (state.verdurePoints > 0.0)
            {
                parts.Add("<color=" + MossDeepHex + ">VERDURE <b>" + Mathf.FloorToInt((float)state.verdurePoints) + "</b></color>");
            }

            if (state.amber > 0.0)
            {
                parts.Add("<color=" + OchreHex + ">amber <b>" + Mathf.FloorToInt((float)state.amber) + "</b></color>");
            }

            _ledger.text = string.Join(" · ", parts);
        }

        private void RefreshTracker()
        {
            if (_loop.CanMigrate())
            {
                var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
                _trackerText.text = "<color=" + OchreHex + ">THE FOLD</color> · +<b>"
                                    + Mathf.FloorToInt((float)gain) + "</b> Verdure banked";
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
                    _trackerText.text = "Verse of " + ZoneName(verse.zone) + " — <b>"
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

        private void SetNote(string text)
        {
            if (_note != null)
            {
                _note.text = text;
            }
        }

        private void ReportWorldStrip()
        {
            if (_world == null)
            {
                return;
            }

            _world.SelectedNode = _selected;
            _worldGap.GetWorldCorners(Corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);
            _world.StripScreenRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void HandleTend()
        {
            if (_sheet != null)
            {
                return;
            }

            if (_input.TendTriggered(out var screenPosition))
            {
                if (screenPosition.HasValue)
                {
                    var node = _world != null ? _world.NodeAtScreenPoint(screenPosition.Value) : null;
                    if (node != null)
                    {
                        _selected = node;
                        DoTend(node);
                    }
                    else if (screenPosition.Value.y > Screen.height * 0.45f)
                    {
                        // A tap in the world band that hit no node deselects.
                        _selected = null;
                    }
                }
                else if (_selected != null)
                {
                    DoTend(_selected);
                }
            }
        }

        private void DoTend(NodeState node)
        {
            _loop.Tend(node);
            _flashAges[node.id] = 0f;
            SetNote("turned the soil at the " + node.resourceId + ". the warden works here now.");
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
                    BuildCampPage();
                    break;
                case TabWarden:
                    BuildWardenPage();
                    break;
                case TabRecord:
                    BuildRecordPage();
                    break;
                default:
                    BuildTrailPage();
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
        private void Flash(Component near, string message, bool good)
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

        // ─────────────────────────── TRAIL ────────────────────────────────────

        private void BuildTrailPage()
        {
            var unlockedZones = ZonesInOrder();
            // Newest zone first — the page header names it, so the page must
            // open on it; older zones keep farming further down the scroll.
            unlockedZones.Reverse();
            var figure = 1;
            foreach (var zone in unlockedZones)
            {
                if (unlockedZones.Count > 1)
                {
                    var heading = MakeText(_body, zone.displayName.ToUpperInvariant(), 17, TextAnchor.MiddleCenter, Ink2, _smallCaps);
                    heading.gameObject.name = "ZoneHeading";
                }

                // The zone's keystone specimen heads its section (design §3).
                var keystone = ArtLibrary.ForZone(zone.id);
                if (keystone != null)
                {
                    PlateImage(_body, keystone, 240f).name = "Keystone";
                }

                foreach (var node in _loop.State.nodes)
                {
                    if (node.zoneId == zone.id)
                    {
                        BuildNodePlate(node, figure++);
                    }
                }
            }

            BuildTrailRow();

            foreach (var site in _loop.State.digSites)
            {
                BuildWatchPlate(site);
            }

            // Stationing lives with the land it points at — the design's
            // four-page journal gives the Trail page stationing; the Warden
            // page keeps the kit/crafts identity.
            BuildKithCard();

            BuildVerseCards();
            BuildWaystoneFooter();
        }

        private void BuildNodePlate(NodeState node, int figure)
        {
            var captured = node;
            var card = Card(null);
            var cardImage = card.GetComponent<Image>();

            // The resource's plate heads the card — the journal's FIG. figure,
            // the same specimen the world strip pins above the node.
            var face = ArtLibrary.ForResource(captured.resourceId);
            if (face != null)
            {
                PlateImage(card, face, 300f);
            }

            // Tending lives on the world plate above, so the card's top row
            // keeps just the figure label with Plant back at the right.
            var figRow = Row(card);
            var fig = MakeText(figRow.transform, "FIG. " + figure + ".", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);
            fig.gameObject.name = "Fig";
            FlexibleWidth(fig.gameObject, 1f);
            // A seedling marks Plant back — putting the specimen back in the ground.
            var seedling = ArtLibrary.ForLine("seedling");
            if (seedling != null)
            {
                IconImage(figRow.transform, seedling, 40f, Color.white);
            }

            Button replant = null;
            replant = Button(figRow.transform, "Plant back", 230, () =>
            {
                if (_loop.Replant(captured))
                {
                    Flash(replant, "planted back", true);
                    SetNote("planted " + captured.resourceId + " back into the ground. it earns tone, not numbers.");
                    _dirty = true;
                }
                else
                {
                    Flash(replant, "not enough " + captured.resourceId, false);
                    SetNote("not enough " + captured.resourceId + " to plant back. the land can wait.");
                }
            });

            var nameLine = MakeText(card, string.Empty, 26, TextAnchor.MiddleLeft, Ink, _serif);
            var postedLine = MakeText(card, string.Empty, 21, TextAnchor.MiddleLeft, Ink2, _hand);
            // Posting happens where the land is — tapping the line opens the
            // who-walks-here sheet for this node; fallow draws it as an empty
            // post (dashed outline) waiting to be filled in.
            var postedButton = postedLine.gameObject.AddComponent<Button>();
            postedButton.transition = Selectable.Transition.None;
            postedButton.onClick.AddListener(() => OpenPostingSheet(captured.id));
            postedLine.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            var postedDashes = AddDashedBorder(postedLine.gameObject);

            // The gift event (design §4): while it's live, every node offers a
            // dashed pile line — where the pile is left is where the newcomer
            // works, and which resource it costs. One pile, one yes; the line
            // vanishes for good once something says yes.
            var giftLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);
            var giftButton = giftLine.gameObject.AddComponent<Button>();
            giftButton.transition = Selectable.Transition.None;
            giftButton.onClick.AddListener(() =>
            {
                var arrived = _loop.LeaveGift(captured);
                if (arrived != null)
                {
                    SetNote("you left a pile of " + captured.resourceId + " and stepped back. something said yes.");
                    _dirty = true;
                }
                else
                {
                    SetNote("not enough " + captured.resourceId + " for a proper pile. keep picking.");
                }
            });
            giftLine.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            AddDashedBorder(giftLine.gameObject);

            var statsLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);

            // The tend flash — the mock's "+ yield" that rises and fades; sits
            // outside the layout at the plate's top edge, left of Plant back.
            var flash = MakeText(card, "+ yield", 24, TextAnchor.MiddleRight, MossDeep, _hand);
            flash.gameObject.name = "TendFlash";
            flash.raycastTarget = false;
            flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var flashRect = (RectTransform)flash.transform;
            flashRect.anchorMin = new Vector2(1f, 1f);
            flashRect.anchorMax = new Vector2(1f, 1f);
            flashRect.pivot = new Vector2(1f, 1f);
            flashRect.sizeDelta = new Vector2(260f, 52f);
            flashRect.anchoredPosition = new Vector2(-254f, -6f);
            flash.color = new Color(MossDeep.r, MossDeep.g, MossDeep.b, 0f);
            _tendFlashes[captured.id] = flash;
            _frameUpdaters.Add(() =>
            {
                if (!_flashAges.TryGetValue(captured.id, out var age) || age >= 1f)
                {
                    return;
                }

                age += Time.deltaTime;
                _flashAges[captured.id] = age;
                var t = Mathf.Clamp01(age);
                flash.color = new Color(MossDeep.r, MossDeep.g, MossDeep.b, 1f - t);
                flashRect.anchoredPosition = new Vector2(-254f, -6f + 20f * t);
            });

            // The basket bar — fill width driven by the live updater.
            var barGo = MakePanel("Basket", card, PagePaper);
            FixedHeight(barGo, 14);
            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = MossWash;
            fill.raycastTarget = false;
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            AddBorder(barGo, Ink2);

            if (_loop.PlantersUnlocked())
            {
                var actions = ActionRow(card);
                // One trellis heads the planter buttons — the structures raised
                // over the node — rather than repeating it on each.
                var trellis = ArtLibrary.ForLine("planter");
                if (trellis != null)
                {
                    IconImage(actions, trellis, 44f, Color.white);
                }

                foreach (var planter in _loop.NodePlanters())
                {
                    AddPlanterAction(actions, planter, captured.id);
                }
            }

            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var rich = captured.richnessLevel > 0 ? "  ·  richness " + Roman(captured.richnessLevel) : string.Empty;
                nameLine.text = captured.resourceId + rich;
                cardImage.color = captured == _selected ? MossWash : CardPaper;

                var names = PostedNames(captured);
                if (names.Count > 0)
                {
                    postedLine.font = _hand;
                    postedLine.fontSize = Mathf.RoundToInt(21 * FontScale);
                    postedLine.text = "posted: " + string.Join(", ", names);
                    postedDashes.SetActive(false);
                }
                else
                {
                    // Fallow is state, not flavour — body type, with the way
                    // out inked where the player is already looking.
                    postedLine.font = _font;
                    postedLine.fontSize = Mathf.RoundToInt(17 * FontScale);
                    postedLine.text = "<color=" + OchreInkHex + ">+  post someone here</color>";
                    postedDashes.SetActive(true);
                }

                if (_loop.GiftAvailable())
                {
                    giftLine.gameObject.SetActive(true);
                    var pile = NumberFormat.Short(_loop.GiftPileCost()) + " " + captured.resourceId;
                    giftLine.text = _loop.CanLeaveGift(captured)
                        ? "<color=" + OchreInkHex + ">+  leave a pile — " + pile + "</color>"
                        : "leave a pile — " + pile + " (not enough yet)";
                }
                else
                {
                    giftLine.gameObject.SetActive(false);
                }

                var cap = NodeBasketCapacity(captured);
                var fraction = cap > BigDouble.Zero ? Mathf.Clamp01((float)(captured.basket / cap).ToDouble()) : 0f;
                fillRect.anchorMax = new Vector2(fraction, 1f);
                var basketFull = fraction >= 0.999f;
                fill.color = basketFull ? OchreWash : MossWash;

                var rate = Simulation.YieldPerSecond(captured, state, _loop.Data, _loop.Data.economy);
                var stock = state.GetResource(captured.resourceId);
                statsLine.text = NumberFormat.Rate(rate) + "/s  ·  " + NumberFormat.Short(stock) + " at camp"
                                 + (basketFull ? "  ·  <color=" + OchreInkHex + ">basket full — waits on a carrier</color>" : string.Empty);

                SetButtonLabel(replant, "Plant back\n" + SizeOpen(15) + NumberFormat.Short(_loop.ReplantCost(captured)) + " " + captured.resourceId + "</size>");
                var ok = _loop.CanReplant(captured);
                replant.interactable = ok;
                SetButtonTint(replant, ok);
            });
        }

        private List<string> PostedNames(NodeState node)
        {
            var names = new List<string>();
            if (Warden.PostNodeId(_loop.State) == node.id)
            {
                names.Add("the warden");
            }

            foreach (var familiar in Stationing.AssignedTo(_loop.State, node.id))
            {
                names.Add(familiar.name);
            }

            return names;
        }

        private BigDouble NodeBasketCapacity(NodeState node)
        {
            var hauling = _loop.Data.economy?.hauling;
            if (hauling == null)
            {
                return BigDouble.Zero;
            }

            return new BigDouble(hauling.basketCapacity * Buildings.BasketCapacityMultiplier(_loop.State, _loop.Data))
                   * Planters.BasketCapacityMultiplier(_loop.State, _loop.Data, node);
        }

        private void BuildTrailRow()
        {
            // The mock's trail line: a dotted rule with the carrier walking it,
            // "the trail home" on one side, who's hauling on the other.
            var rowGo = MakeRect("TrailRow", _body).gameObject;
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.spacing = 10;

            MakeText(rowGo.transform, "the trail home", 20, TextAnchor.MiddleLeft, Ink2, _hand);

            var lineGo = MakeRect("Line", (RectTransform)rowGo.transform).gameObject;
            var lineElement = lineGo.AddComponent<LayoutElement>();
            lineElement.flexibleWidth = 1f;
            lineElement.minHeight = 22f;

            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(lineGo.transform, false);
            var ruleImage = rule.GetComponent<Image>();
            ruleImage.sprite = DashSprite();
            ruleImage.type = Image.Type.Tiled;
            ruleImage.raycastTarget = false;
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = new Vector2(0f, 0.5f);
            ruleRect.anchorMax = new Vector2(1f, 0.5f);
            ruleRect.offsetMin = new Vector2(0f, -1f);
            ruleRect.offsetMax = new Vector2(0f, 1f);

            var dot = new GameObject("Carrier", typeof(Image));
            dot.transform.SetParent(lineGo.transform, false);
            var dotImage = dot.GetComponent<Image>();
            dotImage.color = MossDeep;
            dotImage.raycastTarget = false;
            var dotRect = (RectTransform)dot.transform;
            dotRect.sizeDelta = new Vector2(14f, 14f);

            var status = MakeText(rowGo.transform, string.Empty, 20, TextAnchor.MiddleRight, Ink2, _hand);
            var statusButton = status.gameObject.AddComponent<Button>();
            statusButton.transition = Selectable.Transition.None;
            statusButton.onClick.AddListener(() => OpenPostingSheet(Familiar.TrailStation));
            var statusDashes = AddDashedBorder(status.gameObject);

            _frameUpdaters.Add(() =>
            {
                var state = _loop.State;
                var carriers = Stationing.TrailCarriers(state, _loop.Data);
                var tripSeconds = _loop.Data.economy?.hauling?.tripSeconds ?? 0.0;
                var show = carriers > 0.0 && tripSeconds > 0.0;
                dot.SetActive(show);
                if (show)
                {
                    var interval = tripSeconds / carriers;
                    var fraction = Mathf.Clamp01((float)(state.haulTripProgress / interval));
                    dotRect.anchorMin = new Vector2(fraction, 0.5f);
                    dotRect.anchorMax = new Vector2(fraction, 0.5f);
                    dotRect.anchoredPosition = Vector2.zero;
                }
            });

            _liveUpdaters.Add(() =>
            {
                var names = new List<string>();
                foreach (var familiar in Stationing.AssignedTo(_loop.State, Familiar.TrailStation))
                {
                    names.Add(familiar.name);
                }

                if (names.Count > 0)
                {
                    status.text = string.Join(", ", names) + " carrying";
                    statusDashes.SetActive(false);
                }
                else
                {
                    status.text = "<color=" + OchreInkHex + ">+ send a carrier</color>";
                    statusDashes.SetActive(true);
                }
            });
        }

        private void BuildWatchPlate(DigSiteState site)
        {
            var captured = site;
            var card = Card("THE WATCH · " + ZoneName(site.zoneId).ToUpperInvariant());
            var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
            var lineButton = line.gameObject.AddComponent<Button>();
            lineButton.transition = Selectable.Transition.None;
            lineButton.onClick.AddListener(() => OpenPostingSheet(Familiar.DigStationPrefix + captured.zoneId));
            line.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            var lineDashes = AddDashedBorder(line.gameObject);

            if (_loop.PlantersUnlocked() && _loop.DigSitePlanters().Count > 0)
            {
                var actions = ActionRow(card);
                foreach (var planter in _loop.DigSitePlanters())
                {
                    AddPlanterAction(actions, planter, captured.zoneId);
                }
            }

            _liveUpdaters.Add(() =>
            {
                var watchers = Stationing.AtDigSite(_loop.State, captured.zoneId);
                if (watchers > 0)
                {
                    line.text = watchers + " watching where the small lives cross";
                    lineDashes.SetActive(false);
                }
                else
                {
                    line.text = "<color=" + OchreInkHex + ">+ station a watcher</color>";
                    lineDashes.SetActive(true);
                }
            });
        }

        private void AddPlanterAction(Transform actions, PlanterData planter, string targetId)
        {
            var capturedPlanter = planter;
            var capturedTarget = targetId;
            // The same structure wears the name of the work it serves — a trellis
            // over berries, mineshaft beams over ore, set nets over a fishing run.
            var name = PlanterDisplayName(capturedPlanter, capturedTarget);
            if (_loop.PlanterBuilt(capturedPlanter, capturedTarget))
            {
                var built = MakeText(actions, name + " — raised", 16, TextAnchor.MiddleLeft, MossDeep);
                built.gameObject.name = "PlanterBuilt";
                return;
            }

            Button build = null;
            build = Button(actions, "Raise " + name
                                        + "\n" + SizeOpen(14) + BundleLabel(capturedPlanter.materials) + "</size>", 250, () =>
            {
                if (_loop.BuildPlanter(capturedPlanter, capturedTarget))
                {
                    Flash(build, "raised", true);
                    SetNote("raised a " + name.ToLowerInvariant() + ". the ground holds it now.");
                    _dirty = true;
                }
            });
            _liveUpdaters.Add(() =>
            {
                SetButtonLabel(build, "Raise " + name
                                     + "\n" + SizeOpen(14) + BundleHaveLabel(capturedPlanter.materials) + "</size>");
                var ok = _loop.CanBuildPlanter(capturedPlanter, capturedTarget);
                build.interactable = ok;
                SetButtonTint(build, ok);
            });
        }

        /// <summary>
        /// A planter's name in the language of the node it serves. Foraging nodes
        /// keep the garden names (frame, trellis); mining, delving, logging,
        /// fishing, husbandry and watching get names that fit the work. Dig sites
        /// and anything unmapped fall back to the planter's own displayName.
        /// </summary>
        private string PlanterDisplayName(PlanterData planter, string targetId)
        {
            var skill = NodeSkill(targetId);
            if (string.IsNullOrEmpty(skill))
            {
                return planter.displayName;
            }

            switch (planter.kind)
            {
                case "basketCapacityMult":
                    switch (skill)
                    {
                        case "mining": return "Mineshaft Beams";
                        case "delving": return "Pit Props";
                        case "logging": return "Log Cradle";
                        case "fishing": return "Fish Baskets";
                        case "husbandry": return "Feed Racks";
                        case "entomology": return "Netting Frames";
                        default: return planter.displayName;
                    }
                case "nodeYieldMult":
                    switch (skill)
                    {
                        case "mining": return "Ore Rig";
                        case "delving": return "Deep Hoist";
                        case "logging": return "Felling Rig";
                        case "fishing": return "Set Nets";
                        case "husbandry": return "Fenced Pens";
                        case "entomology": return "Light Traps";
                        default: return planter.displayName;
                    }
                default:
                    return planter.displayName;
            }
        }

        /// <summary>The gathering skill of the node with this id, or null when the target is a dig site.</summary>
        private string NodeSkill(string targetId)
        {
            foreach (var node in _loop.State.nodes)
            {
                if (node.id == targetId)
                {
                    return _loop.Data.ResourcesById.TryGetValue(node.resourceId, out var res) ? res.skill : null;
                }
            }

            return null;
        }

        private void BuildVerseCards()
        {
            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite == null || rite.verses == null)
            {
                return;
            }

            // Open verses keep their full cards; verses already sung collapse
            // into one summary so the Trail page doesn't grow without bound — a
            // finished verse is a memory, not a worklist.
            List<RiteVerseData> sung = null;
            foreach (var verse in rite.verses)
            {
                if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse))
                {
                    continue;
                }

                if (Rite.IsVerseComplete(_loop.State, _loop.Data, verse))
                {
                    (sung ?? (sung = new List<RiteVerseData>())).Add(verse);
                    continue;
                }

                var card = BuildVerseCard(verse);
                if (_firstVerseCard == null)
                {
                    // The tracker's deep-link lands on the first verse still open.
                    _firstVerseCard = card;
                }
            }

            if (sung != null)
            {
                BuildSungVersesCard(sung);
            }
        }

        /// <summary>
        /// The collapsed record of verses already answered — one line each, so a
        /// long-lived rite doesn't bury the open verses under finished ones.
        /// </summary>
        private void BuildSungVersesCard(List<RiteVerseData> verses)
        {
            var card = Card("SUNG VERSES");
            foreach (var verse in verses)
            {
                var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
                var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
                MakeText(card, ZoneName(verse.zone) + "  " + SizeOpen(15) + "<color=" + MossDeepHex
                               + ">" + site + " — sung</color></size>", 18, TextAnchor.MiddleLeft, Ink);
            }
        }

        private RectTransform BuildVerseCard(RiteVerseData verse)
        {
            var capturedVerse = verse;
            var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
            var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
            var card = Card("VERSE OF " + ZoneName(verse.zone).ToUpperInvariant());

            var siteLine = MakeText(card, site, 23, TextAnchor.MiddleCenter, Ink, _serif);
            siteLine.gameObject.name = "Site";
            var cairn = ArtLibrary.ForJournal("cairn");
            if (cairn != null)
            {
                PlateImage(card, cairn, 200f);
            }

            var verseLine = Narrative.VerseLine(_loop.Data, verse.zone);
            if (!string.IsNullOrEmpty(verseLine))
            {
                MakeText(card, "<i>“" + verseLine + "”</i>", 19, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            var progress = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                // Clamp for saves that over-delivered before expiry was
                // enforced — "5 of 3" reads broken, and any three IS answered.
                var answered = Mathf.Min(Rite.CompletedSlotCount(_loop.State, capturedVerse), _loop.Data.rites.chooseCount);
                progress.text = Rite.IsVerseComplete(_loop.State, _loop.Data, capturedVerse)
                    ? "<color=" + MossDeepHex + ">answered — the verse is sung</color>"
                    : "answered " + answered + " of " + _loop.Data.rites.chooseCount;
            });

            for (var i = 0; i < verse.slots.Count; i++)
            {
                BuildSlotRow(card, capturedVerse, i);
            }

            return card;
        }

        private void BuildSlotRow(RectTransform card, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            Button offer = null;
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    offer = Button(row.transform, "Set down", 180, () =>
                    {
                        var given = _loop.OfferResource(verse, slotIndex);
                        if (given > BigDouble.Zero)
                        {
                            Flash(offer, "set down " + Mathf.FloorToInt((float)given.ToDouble()) + " " + slot.resource, true);
                            SetNote("set down " + Mathf.FloorToInt((float)given.ToDouble()) + " " + slot.resource + ". no answer. not yet.");
                        }
                        else
                        {
                            Flash(offer, "the stores are empty", false);
                            SetNote("nothing in the stores to set down.");
                        }
                    });
                    break;
                case RiteSlotType.Specimen:
                    offer = Button(row.transform, "Offer one", 180, () =>
                    {
                        if (_loop.OfferSpecimen(verse, slotIndex))
                        {
                            Flash(offer, "set down", true);
                            SetNote("set the perfect one down. it deserved better than a page, maybe.");
                        }
                        else
                        {
                            Flash(offer, "no such find in hand", false);
                            SetNote("no such find in hand. the site is patient.");
                        }
                    });
                    break;
                case RiteSlotType.Sketch:
                    offer = Button(row.transform, "Offer a sketch", 220, () =>
                    {
                        if (_loop.OfferSketch(verse, slotIndex))
                        {
                            Flash(offer, "page torn out", true);
                            SetNote("tore the page out for them. that portion must be watched again.");
                        }
                        else
                        {
                            Flash(offer, "no finished sketch", false);
                            SetNote("no finished sketch to give.");
                        }
                    });
                    break;
            }

            _liveUpdaters.Add(() =>
            {
                var delivered = Rite.SlotDelivered(_loop.State, verse, slotIndex);
                var target = Rite.SlotTarget(slot);
                var done = delivered >= target;
                var expired = !done && Rite.IsVerseComplete(_loop.State, _loop.Data, verse);
                var name = SlotName(slot);
                if (done)
                {
                    label.text = "<color=" + MossDeepHex + ">" + name + " — set down</color>";
                }
                else if (expired)
                {
                    // Any three answer the verse; the rest expire (§8) — the
                    // spirits stopped listening to this one.
                    label.text = "<color=" + Ink2Hex + "><i>" + name + " — unasked now</i></color>";
                }
                else
                {
                    label.text = name + "  <color=" + Ink2Hex + ">" + Mathf.FloorToInt((float)delivered) + " / " + Mathf.FloorToInt((float)target) + "</color>";
                }

                if (offer != null)
                {
                    offer.gameObject.SetActive(!done && !expired);
                    var ok = _loop.CanOffer(verse, slotIndex);
                    offer.interactable = ok;
                    SetButtonTint(offer, ok);
                }
            });
        }

        private static string SlotName(RiteSlotData slot)
        {
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    return slot.resource;
                case RiteSlotType.Deed:
                    return slot.deed;
                case RiteSlotType.Specimen:
                    return "one " + (string.IsNullOrEmpty(slot.quality) ? "fine" : slot.quality) + " find";
                case RiteSlotType.Sketch:
                    return "a field sketch";
                default:
                    return slot.resource ?? slot.deed ?? "offering";
            }
        }

        private void BuildWaystoneFooter()
        {
            var zone = LatestZone();
            if (zone == null)
            {
                return;
            }

            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var stone = ArtLibrary.ForJournal("waystone");
            if (stone != null)
            {
                PlateImage(_body, stone, 200f).name = "WaystoneMark";
            }

            var footer = MakeText(_body, "<i>“" + text + "”</i>\n" + SizeOpen(14) + "WAYSTONE · " + zone.displayName.ToUpperInvariant() + "</size>",
                18, TextAnchor.MiddleCenter, Ink2, _serif);
            footer.gameObject.name = "Waystone";
        }

        // ─────────────────────────── CAMP ─────────────────────────────────────

        private void BuildCampPage()
        {
            BuildCraftingCard();
            BuildBuildingsCard();
            BuildExchangeCard();
            BuildLadderCard();
        }

        private void BuildCraftingCard()
        {
            var recipes = _loop.AvailableRecipes();
            if (recipes.Count == 0)
            {
                return;
            }

            var card = Card("THE FIRE & BENCH");
            foreach (var recipe in recipes)
            {
                var captured = recipe;
                var row = Row(card);
                var goodArt = ArtLibrary.ForGood(captured.output);
                if (goodArt != null)
                {
                    IconImage(row.transform, goodArt, 56f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                var toggle = Button(row.transform, "Craft", 160, () =>
                {
                    _loop.ToggleCraft(captured);
                    _dirty = true;
                });

                _liveUpdaters.Add(() =>
                {
                    var crafting = _loop.IsCrafting(captured);
                    var progress = crafting ? "  " + Mathf.RoundToInt((float)_loop.CraftProgress(captured) * 100f) + "%" : string.Empty;
                    var need = string.Empty;
                    if (!_loop.IsRecipeLevelMet(captured))
                    {
                        need += "  <color=" + OchreInkHex + "><b>needs " + captured.skill + " " + captured.skillLevel + "</b></color>";
                    }

                    if (!Crafting.StationLevelMet(_loop.State, _loop.Data, captured))
                    {
                        // The one gate the bundle line can't explain — a cold
                        // station looks ready when every input is in stock.
                        var line = _loop.Data.BuildingsById.TryGetValue(captured.station, out var building)
                            ? building.displayName
                            : captured.station;
                        need += "  <color=" + OchreInkHex + "><b>needs " + line + " level " + captured.stationLevel + "</b></color>";
                    }

                    label.text = captured.output + progress + need
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + BundleHaveLabel(captured.inputs) + "</color></size>";
                    SetButtonLabel(toggle, crafting ? "Stop" : "Craft");
                    // Stopping is always allowed; starting needs the gates AND
                    // a batch of inputs in camp stock.
                    var ok = crafting || (_loop.IsRecipeWorkable(captured) && _loop.CanCraft(captured));
                    toggle.interactable = ok;
                    SetButtonTint(toggle, ok);
                });
            }
        }

        /// <summary>What one bought level of a building line grants — the row's "say what it gives" clause.</summary>
        private static string PerLevelGivesLabel(BuildingData building)
        {
            var perLevel = building.perLevel;
            if (perLevel == null)
            {
                return null;
            }

            var pct = "+" + Mathf.RoundToInt((float)(perLevel.value * 100.0)) + "%";
            switch (perLevel.type)
            {
                case "stationSpeedBonus": return "each level: " + pct + " craft speed at this station";
                case "basketCapacityBonus": return "each level: " + pct + " basket capacity";
                case "comfort": return "each level: " + pct + " familiar XP while posted";
                default: return null;
            }
        }

        private void BuildBuildingsCard()
        {
            var card = Card("BUILDING LINES");
            foreach (var building in _loop.Data.buildings)
            {
                var captured = building;
                var gives = PerLevelGivesLabel(captured);
                var row = Row(card);
                var plate = ArtLibrary.ForBuilding(captured.id);
                if (plate != null)
                {
                    IconImage(row.transform, plate, 64f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button build = null;
                build = Button(row.transform, "Raise", 160, () =>
                {
                    if (_loop.BuyBuildingLevel(captured))
                    {
                        Flash(build, "raised", true);
                        SetNote(captured.displayName.ToLowerInvariant() + " goes up. the camp sleeps closer to the work.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  <color=" + Ink2Hex + ">level " + _loop.BuildingLevel(captured) + "</color>"
                                 + (gives != null ? "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + gives + "</color></size>" : string.Empty)
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">next: " + BundleHaveLabel(_loop.NextBuildingBundle(captured)) + "</color></size>";
                    var ok = _loop.CanAffordBuilding(captured);
                    build.interactable = ok;
                    SetButtonTint(build, ok);
                });
            }
        }

        private void BuildExchangeCard()
        {
            var card = Card("THE EXCHANGE");
            MakeText(card, "<i>a caravan idles at the camp edge. it trades; it does not sell.</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);
            var caravan = ArtLibrary.ForJournal("caravan");
            if (caravan != null)
            {
                PlateImage(card, caravan, 200f);
            }

            var tradeable = TradeableResources();
            if (tradeable.Count < 2)
            {
                MakeText(card, "gather more before the caravan will barter.", 18, TextAnchor.MiddleCenter, Ink2);
                return;
            }

            if (string.IsNullOrEmpty(_exchangeFrom) || !tradeable.Contains(_exchangeFrom))
            {
                _exchangeFrom = tradeable[0];
            }

            if (string.IsNullOrEmpty(_exchangeTo) || !tradeable.Contains(_exchangeTo) || _exchangeTo == _exchangeFrom)
            {
                _exchangeTo = tradeable[0] == _exchangeFrom && tradeable.Count > 1 ? tradeable[1] : tradeable[0];
            }

            var row = Row(card);
            var fromButton = Button(row.transform, "from", 250, () =>
            {
                _exchangeFrom = Cycle(TradeableResources(), _exchangeFrom);
                _dirty = true;
            });
            var toButton = Button(row.transform, "to", 250, () =>
            {
                _exchangeTo = Cycle(TradeableResources(), _exchangeTo);
                _dirty = true;
            });

            var quote = MakeText(card, string.Empty, 18, TextAnchor.MiddleCenter, Ink);
            var tradeRow = Row(card);
            Button trade = null;
            trade = Button(tradeRow.transform, "Trade all", 260, () =>
            {
                var have = _loop.State.GetResource(_exchangeFrom);
                var got = _loop.TradeAtExchange(_exchangeFrom, _exchangeTo, have);
                if (got > BigDouble.Zero)
                {
                    Flash(trade, "+" + NumberFormat.Short(got) + " " + _exchangeTo, true);
                    SetNote("traded " + _exchangeFrom + " for " + _exchangeTo + ". a nod. gone before the count.");
                }

                _dirty = true;
            });

            _liveUpdaters.Add(() =>
            {
                SetButtonLabel(fromButton, "Give: " + _exchangeFrom);
                SetButtonLabel(toButton, "Get: " + _exchangeTo);
                var have = _loop.State.GetResource(_exchangeFrom);
                var got = _loop.ExchangeQuote(_exchangeFrom, _exchangeTo, have);
                quote.text = NumberFormat.Short(have) + " " + _exchangeFrom + "  »  "
                             + NumberFormat.Short(got) + " " + _exchangeTo;
                trade.interactable = got > BigDouble.Zero;
                SetButtonTint(trade, got > BigDouble.Zero);
            });
        }

        private void BuildLadderCard()
        {
            var card = Card("THE LADDER");

            // The hatchet heads the rungs — the work sharpens as you climb.
            var tools = ArtLibrary.ForLine("tools");
            if (tools != null)
            {
                PlateImage(card, tools, 150f);
            }

            // The next few unpurchased rungs of the §9 ladder, in order.
            var next = new List<UpgradeData>();
            foreach (var upgrade in _loop.Data.upgrades.OrderBy(u => u.order))
            {
                if (_loop.IsUpgradePurchased(upgrade))
                {
                    continue;
                }

                next.Add(upgrade);
                if (next.Count >= UpgradeWindow)
                {
                    break;
                }
            }

            if (next.Count == 0)
            {
                MakeText(card, "every rung climbed.", 18, TextAnchor.MiddleCenter, Ink2);
                return;
            }

            foreach (var upgrade in next)
            {
                var captured = upgrade;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button buy = null;
                buy = Button(row.transform, "Take up", 170, () =>
                {
                    if (_loop.PurchaseUpgrade(captured))
                    {
                        Flash(buy, "taken up", true);
                        SetNote(captured.displayName.ToLowerInvariant() + " — the work changes shape.");
                        _dirty = true;
                    }
                });

                // What the rung gives is fixed data — settle it once; only the
                // requirement line and affordability move.
                var gives = EffectsLabel(captured.effects);
                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName
                                 + (gives.Length > 0 ? "\n" + SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>" : string.Empty)
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + UpgradeRequirement(captured) + "</color></size>";
                    var ok = !_loop.IsUpgradePurchased(captured) && _loop.CanAffordUpgrade(captured)
                             && _loop.MeetsUpgradeSkillGate(captured) && string.IsNullOrEmpty(_loop.MissingToolTier(captured));
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }

        // ─────────────────────────── WARDEN ───────────────────────────────────

        private void BuildWardenPage()
        {
            BuildKitCard();
            BuildCraftsCard();
            BuildRunCard();
        }

        private void BuildKitCard()
        {
            if (_loop.Data.gear == null || _loop.Data.gear.Count == 0)
            {
                return;
            }

            var card = Card("THE KIT");
            MakeText(card, "worn for the run, folded at Migration", 21, TextAnchor.MiddleCenter, Ink2, _hand);
            // Skill unlocks are part of the structure signature, so a locked
            // piece's hint can be settled once per rebuild.
            var unlockedSkills = Upgrades.UnlockedSkills(_loop.State, _loop.Data);
            foreach (var gear in _loop.Data.gear)
            {
                var captured = gear;
                var skillHint = string.Empty;
                if (!string.IsNullOrEmpty(captured.skill) && !unlockedSkills.Contains(captured.skill))
                {
                    // Materials alone can't explain this dead button — name
                    // the missing skill AND the Ladder rung that grants it.
                    var source = SkillSource(captured.skill);
                    skillHint = "  <color=" + OchreInkHex + "><b>needs " + captured.skill
                                + (source != null ? " — take up " + source.displayName : string.Empty)
                                + "</b></color>";
                }

                var row = Row(card);
                var kit = ArtLibrary.ForGear(captured.id);
                if (kit != null)
                {
                    IconImage(row.transform, kit, 64f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);

                var gives = EffectsLabel(captured.effects);
                var givesLine = gives.Length > 0
                    ? "\n" + SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>"
                    : string.Empty;
                if (Gear.IsEquipped(_loop.State, captured))
                {
                    label.text = captured.slot.ToUpperInvariant() + " — " + captured.displayName
                                 + "  <color=" + MossDeepHex + ">worn</color>" + givesLine;
                    continue;
                }

                Button craft = null;
                craft = Button(row.transform, "Craft", 160, () =>
                {
                    if (_loop.CraftGear(captured))
                    {
                        Flash(craft, "bound tight", true);
                        SetNote("bound the " + captured.displayName.ToLowerInvariant() + " tight. the work will mind it less.");
                        _dirty = true;
                    }
                });
                _liveUpdaters.Add(() =>
                {
                    label.text = captured.slot.ToUpperInvariant() + " — " + captured.displayName + givesLine
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + BundleHaveLabel(captured.materials) + "</color>" + skillHint + "</size>";
                    var ok = Gear.CanCraft(_loop.State, _loop.Data, captured);
                    craft.interactable = ok;
                    SetButtonTint(craft, ok);
                });
            }
        }

        private void BuildCraftsCard()
        {
            var card = Card("CRAFTS");
            foreach (var skill in _loop.UnlockedSkills())
            {
                var captured = skill;

                // The craft's mark leads its line. The glyphs are monochrome
                // silhouettes (some white-on-transparent), so ink-tint them for
                // a coherent sepia mark on the paper; no glyph → bare line.
                var glyph = ArtLibrary.ForSkill(captured);
                Text line;
                if (glyph != null)
                {
                    var craftRow = MakeRect("CraftRow", card).gameObject;
                    var layout = craftRow.AddComponent<HorizontalLayoutGroup>();
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    layout.spacing = 10;
                    // Glyphs are baked two-tone sepia — pass white so the ink
                    // tones show true rather than being multiplied down again.
                    IconImage(craftRow.transform, glyph, 40f, Color.white);
                    line = MakeText(craftRow.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                    FlexibleWidth(line.gameObject, 1f);
                }
                else
                {
                    line = MakeText(card, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                }

                _liveUpdaters.Add(() =>
                {
                    var progress = Mathf.RoundToInt((float)_loop.SkillProgress(captured) * 100f);
                    line.text = captured + " — level " + _loop.SkillLevel(captured)
                                + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + progress + "% to next</color></size>";
                });
            }
        }

        private void BuildKithCard()
        {
            var card = Card("THE KITH · roster & posts");
            var slotsLine = MakeText(card, string.Empty, 16, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                var held = _loop.KithCount();
                var slots = _loop.KithSlots();
                var max = _loop.Data.economy?.kith != null ? _loop.Data.economy.kith.slotsMax : slots;
                slotsLine.text = "<i>" + held + " of " + slots + " slots walked"
                                 + (slots < max ? " · the land holds the rest" : string.Empty) + "</i>";
            });

            foreach (var familiar in _loop.State.roster)
            {
                var captured = familiar;
                var row = Row(card);
                var portrait = ArtLibrary.ForSpecies(captured.speciesId);
                if (portrait != null)
                {
                    IconImage(row.transform, portrait, 64f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 22, TextAnchor.MiddleLeft, Ink, _serif);
                FlexibleWidth(label.gameObject, 1f);
                var rename = Button(row.transform, "Name", 150, () => OpenNamingSheet(captured));
                // Posting moved out to the land — each node plate, the trail
                // row, and the watch plates carry their own "post someone"
                // affordance. The roster keeps only the recall.
                var wander = Button(row.transform, "Let wander", 190, () => Station(captured, null));
                var powerup = Button(row.transform, "A lesson", 170, () => OpenPowerupSheet(captured));
                powerup.GetComponent<Image>().color = OchreWash;

                _liveUpdaters.Add(() =>
                {
                    var species = SpeciesName(captured.speciesId);
                    var bonded = captured.bonded ? " " + SizeOpen(15) + "<color=" + OchreHex + ">BONDED</color></size>" : string.Empty;
                    var kin = _loop.FamiliarKinship(captured) > 0
                        ? "  <color=" + OchreHex + ">KINSHIP " + Roman(_loop.FamiliarKinship(captured)) + "</color>"
                        : string.Empty;
                    var progress = Mathf.RoundToInt((float)_loop.FamiliarLevelProgress(captured) * 100f);
                    label.text = captured.name + bonded + "  " + SizeOpen(16) + "<color=" + Ink2Hex + ">" + species + "</color></size>" + kin
                                 + "\n" + SizeOpen(16) + "<color=" + Ink2Hex + ">level " + Roman(_loop.FamiliarLevel(captured))
                                 + " · " + progress + "% · " + StationLabel(captured.stationId) + "</color></size>";
                    powerup.gameObject.SetActive(_loop.HasPendingPowerup(captured));
                    wander.gameObject.SetActive(!string.IsNullOrEmpty(captured.stationId));
                });
            }
        }

        /// <summary>
        /// The posting sheet — every post affordance lives on the plate it
        /// fills (node, trail, watch), so the sheet asks only "who".
        /// </summary>
        private void OpenPostingSheet(string stationId)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Who walks here?", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, StationLabel(stationId).ToUpperInvariant(), 18, TextAnchor.UpperCenter, Ink2, _smallCaps);

            // The warden can only stand at a node — no warden entry on the
            // trail or watch sheets.
            var node = FindNode(stationId);
            if (node != null)
            {
                var wardenHere = Warden.PostNodeId(_loop.State) == node.id;
                var warden = Button(sheet, "the warden" + (wardenHere
                    ? "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">already here</color></size>"
                    : string.Empty), 560, () =>
                {
                    _loop.PostWarden(node);
                    SetNote("the warden walks to " + StationLabel(node.id) + ".");
                    CloseSheet();
                });
                warden.interactable = !wardenHere;
                SetButtonTint(warden, !wardenHere);
            }

            foreach (var familiar in _loop.State.roster)
            {
                var captured = familiar;
                var here = PostMatches(captured.stationId, stationId);
                var detail = here ? "posted here — tap to let them wander" : "now: " + StationLabel(captured.stationId);
                var button = Button(sheet, captured.name + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                           + SpeciesName(captured.speciesId) + " · " + detail + "</color></size>", 560, () =>
                {
                    Station(captured, here ? null : stationId);
                    CloseSheet();
                });
                if (here)
                {
                    button.GetComponent<Image>().color = MossWash;
                }
            }

            Button(sheet, "Never mind", 320, CloseSheet);
        }

        private NodeState FindNode(string stationId)
        {
            foreach (var node in _loop.State.nodes)
            {
                if (node.id == stationId)
                {
                    return node;
                }
            }

            return null;
        }

        private static bool PostMatches(string stationId, string buttonStationId)
        {
            return string.IsNullOrEmpty(stationId) ? string.IsNullOrEmpty(buttonStationId) : stationId == buttonStationId;
        }

        private void Station(Familiar familiar, string stationId)
        {
            _loop.StationFamiliar(familiar, stationId);
            SetNote(string.IsNullOrEmpty(stationId)
                ? familiar.name + " wanders — half a hand everywhere, a whole hand nowhere."
                : familiar.name + " walks to " + StationLabel(stationId) + ".");
        }

        private void BuildRunCard()
        {
            var card = Card("THIS RUN");
            var line = MakeText(card, string.Empty, 19, TextAnchor.MiddleCenter, Ink2, _serif);
            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var pristine = 0;
                foreach (var pair in state.pristineResources)
                {
                    if (pair.Value > BigDouble.Zero)
                    {
                        pristine++;
                    }
                }

                line.text = "camp " + (state.migrationCount + 1)
                            + " · renown " + NumberFormat.Short(state.renown)
                            + " · fold forecast +" + Mathf.FloorToInt((float)System.Math.Max(0.0, _loop.VerdureAfterMigration() - state.verdurePoints)) + " Verdure"
                            + (pristine > 0 ? " · Pristine kinds in hand " + pristine : string.Empty);
            });
        }

        // ─────────────────────────── RECORD ───────────────────────────────────

        private void BuildRecordPage()
        {
            BuildCompendiumCard();
            BuildFolioCard();
            BuildDeepPagesCard();
            BuildAlmanacCard();
        }

        private void BuildCompendiumCard()
        {
            var card = Card("THE COMPENDIUM");
            MakeText(card, Compendium.DiscoveredCount(_loop.State, _loop.Data) + " of "
                           + Compendium.TotalEntries(_loop.Data) + " recorded", 18, TextAnchor.MiddleCenter, Ink2);

            foreach (var resource in _loop.Data.resources)
            {
                if (!Compendium.IsResourceDiscovered(_loop.State, resource.id))
                {
                    continue;
                }

                var captured = resource;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    line.text = captured.id + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">lifetime "
                                + NumberFormat.Short(Compendium.LifetimeGathered(_loop.State, captured.id)) + "</color></size>";
                });
            }
        }

        private void BuildFolioCard()
        {
            if (_loop.Data.folioSpreads == null || _loop.Data.folioSpreads.Count == 0)
            {
                return;
            }

            var card = Card("THE FOLIO");
            foreach (var spread in _loop.Data.folioSpreads)
            {
                var captured = spread;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    var done = Folio.IsSpreadComplete(_loop.State, captured);
                    var progress = done
                        ? "<color=" + MossDeepHex + ">complete — it outlives every Migration</color>"
                        : Folio.FixedEntryCount(_loop.State, captured) + " of " + captured.entries.Count + " pressed";
                    line.text = captured.displayName + "  " + SizeOpen(15) + progress + "</size>";
                });
            }

            var anyPristine = false;
            foreach (var pair in _loop.State.pristineResources)
            {
                if (pair.Value <= BigDouble.Zero)
                {
                    continue;
                }

                anyPristine = true;
                var resourceId = pair.Key;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button fix = null;
                fix = Button(row.transform, "Press", 140, () =>
                {
                    if (_loop.FixSpecimen(resourceId))
                    {
                        Flash(fix, "pressed to the page", true);
                        SetNote("pressed it between these pages, where it will outlast the camp.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    var isFixed = Folio.IsFixed(_loop.State, resourceId);
                    var wanted = false;
                    foreach (var spread in _loop.Data.folioSpreads)
                    {
                        if (spread.entries.Contains(resourceId))
                        {
                            wanted = true;
                            break;
                        }
                    }

                    // A dead Fix button explains nothing — say why the page
                    // won't take it, and only offer the button when it might.
                    var hint = isFixed
                        ? "  ·  <color=" + MossDeepHex + ">pressed — the page keeps it</color>"
                        : !wanted ? "  ·  <color=" + Ink2Hex + ">no spread asks for it</color>" : string.Empty;
                    label.text = "Pristine " + resourceId + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                 + NumberFormat.Short(_loop.State.GetPristine(resourceId)) + " held</color>" + hint + "</size>";
                    fix.gameObject.SetActive(!isFixed && wanted);
                    var ok = Folio.CanFix(_loop.State, _loop.Data, resourceId);
                    fix.interactable = ok;
                    SetButtonTint(fix, ok);
                });
            }

            if (anyPristine)
            {
                MakeText(card, "<i>pressing consumes the find — the page keeps it instead of you.</i>", 16, TextAnchor.MiddleCenter, Ink2, _serif);
            }
        }

        private void BuildDeepPagesCard()
        {
            if (_loop.Data.insects == null || _loop.Data.insects.Count == 0)
            {
                return;
            }

            var card = Card("THE DEEP PAGES");
            foreach (var insect in _loop.Data.insects)
            {
                var captured = insect;
                if (Insects.IsRecorded(_loop.State, captured))
                {
                    // A recorded specimen shows its deep-page plate; unrecorded
                    // ones stay a mystery (text only), per the design.
                    var art = ArtLibrary.ForInsect(captured.id);
                    if (art != null)
                    {
                        PlateImage(card, art, 260f);
                    }

                    var plate = Narrative.InsectPlate(_loop.Data, captured.id);
                    MakeText(card, captured.displayName + "  <color=" + MossDeepHex + ">recorded</color>"
                                   + (string.IsNullOrEmpty(plate) ? string.Empty : "\n" + SizeOpen(15) + "<i>" + plate + "</i></size>"),
                        18, TextAnchor.MiddleLeft, Ink);
                    continue;
                }

                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
                _liveUpdaters.Add(() =>
                {
                    // A page not yet earned keeps its secret — no name, no haunt,
                    // just the sense of something. Only sketching resolves it,
                    // and even then the name waits for the full record.
                    var sketches = Insects.SketchCount(_loop.State, captured.id);
                    line.text = sketches > 0
                        ? "<i>a shape half-caught — " + sketches + " of " + captured.sketches + " sketched</i>"
                        : "<i>— something not yet caught —</i>";
                });
            }

            MakeText(card, "<i>nothing you sketch is ever taken — the creature is let go, and only these pages remain.</i>",
                16, TextAnchor.MiddleCenter, Ink2, _serif);
        }

        private void BuildAlmanacCard()
        {
            if (_loop.State.verdurePoints <= 0.0 && _loop.State.almanacNodeIds.Count == 0)
            {
                return;
            }

            var card = Card("THE ALMANAC");
            var header = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                header.text = Mathf.FloorToInt((float)_loop.AvailableVerdure()) + " Verdure unspent";
            });

            var tree = ArtLibrary.ForJournal("almanac");
            if (tree != null)
            {
                PlateImage(card, tree, 240f);
            }

            foreach (var node in _loop.Data.almanac)
            {
                if (_loop.State.almanacNodeIds.Contains(node.id))
                {
                    MakeText(card, node.displayName + "  <color=" + MossDeepHex + ">learned</color>", 18, TextAnchor.MiddleLeft, Ink);
                    continue;
                }

                var captured = node;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button buy = null;
                buy = Button(row.transform, "Learn", 160, () =>
                {
                    if (_loop.BuyAlmanacNode(captured))
                    {
                        Flash(buy, "learned", true);
                        SetNote("the almanac takes a new line. it crosses every fold with you.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                 + Mathf.CeilToInt((float)captured.costVerdure) + " Verdure</color></size>";
                    var ok = _loop.AvailableVerdure() >= captured.costVerdure;
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }

        // ─────────────────────────── Sheets (modals) ─────────────────────────

        private void PumpSheets()
        {
            if (_sheet != null)
            {
                return;
            }

            var arrival = _loop.PeekPendingArrival();
            if (arrival != null)
            {
                OpenArrivalSheet(arrival);
                return;
            }

            var bond = _loop.TakePendingBondCelebration();
            if (bond != null)
            {
                OpenBondSheet(bond);
                return;
            }

            var summary = _loop.TakePendingOfflineSummary();
            if (summary != null && summary.creditedSeconds >= GameLoop.WelcomeBackMinSeconds)
            {
                OpenWelcomeSheet(summary);
                return;
            }

            var waystoneZone = Narrative.NextUnreadWaystone(_loop.State, _loop.Data);
            if (waystoneZone != null)
            {
                OpenWaystoneSheet(waystoneZone);
            }
        }

        private void OpenWaystoneSheet(ZoneData zone)
        {
            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            var sheet = BeginSheet();
            MakeText(sheet, "A waystone", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, zone.displayName.ToUpperInvariant(), 18, TextAnchor.UpperCenter, Ink2, _smallCaps);
            if (!string.IsNullOrEmpty(text))
            {
                MakeText(sheet, "<i>“" + text + "”</i>", 24, TextAnchor.MiddleCenter, Ink, _serif);
            }

            Button(sheet, "Walk on", 320, () =>
            {
                _loop.MarkWaystoneRead(zone.id);
                CloseSheet();
            });
        }

        private void OpenArrivalSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "A new friend", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "a " + SpeciesName(familiar.speciesId) + " arrives", 22, TextAnchor.UpperCenter, Ink2, _hand);

            var field = MakeInputField(sheet, familiar.name);
            Button(sheet, "Walk together", 320, () =>
            {
                var typed = field.text;
                if (!string.IsNullOrWhiteSpace(typed))
                {
                    _loop.RenameFamiliar(familiar, typed);
                }

                _loop.TakePendingArrival();
                _dirty = true;
                CloseSheet();
            });
        }

        private void OpenBondSheet(BondData bond)
        {
            // First bond earned unlocks "First kith" (idempotent — later bonds no-op).
            _loop.GameServices.UnlockAchievement(AchievementIds.FirstKith);

            var sheet = BeginSheet();
            MakeText(sheet, "A bond is made", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, bond.displayName + " will cross every fold with you.", 22, TextAnchor.UpperCenter, Ink2, _serif);
            Button(sheet, "Walk together", 320, CloseSheet);
        }

        private void OpenWelcomeSheet(OfflineSummary summary)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Welcome back", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "Away " + NumberFormat.Duration(summary.realSeconds)
                            + " · credited " + NumberFormat.Duration(summary.creditedSeconds), 20, TextAnchor.UpperCenter, Ink2);

            var lines = 0;
            foreach (var pair in summary.gains)
            {
                if (lines++ >= 6)
                {
                    break;
                }

                MakeText(sheet, "+" + NumberFormat.Short(pair.Value) + " " + pair.Key, 18, TextAnchor.MiddleCenter, Ink);
            }

            // Opt-in rewarded ad: watch to double the haul just credited.
            if (summary.gains.Count > 0 && _loop.Ads.IsRewardedReady)
            {
                var doubleIt = Button(sheet, "Double it — watch a short ad", 360, () =>
                {
                    _loop.Ads.ShowRewarded(RewardedPlacement.OfflineBoost,
                        () =>
                        {
                            _loop.GrantOfflineBonus(summary);
                            _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "offline_boost"));
                            SetNote("The land gives twice — your haul is doubled.");
                            _dirty = true;
                        },
                        CloseSheet);
                });
                doubleIt.GetComponent<Image>().color = OchreWash;
                doubleIt.GetComponentInChildren<Text>().color = Ochre;
            }

            Button(sheet, "Continue", 320, CloseSheet);
        }

        private void OpenMigrationSheet()
        {
            var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
            var sheet = BeginSheet();
            MakeText(sheet, "Fold the camp", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "Everything stays behind: the stores, the kit, the richness,\nthe buildings — <i>they were never yours</i>.",
                20, TextAnchor.MiddleCenter, Ink2, _serif);
            MakeText(sheet, "<color=" + MossDeepHex + ">The journal crosses entire, with +"
                            + Mathf.FloorToInt((float)gain) + " Verdure.\nThe kith walks with you.</color>",
                20, TextAnchor.MiddleCenter, Ink);
            Button(sheet, "Stay a while", 320, CloseSheet);
            var migrate = Button(sheet, "Migrate", 320, () =>
            {
                CloseSheet();
                if (_loop.Migrate())
                {
                    _dirty = true;
                    OpenVignette(gain);
                }
            });
            migrate.GetComponent<Image>().color = OchreWash;
            migrate.GetComponentInChildren<Text>().color = Ochre;
        }

        /// <summary>The full-dark Migration vignette — the mock's fixed overlay; tap anywhere to walk on.</summary>
        private void OpenVignette(double verdureGained)
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, NightInk);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;
            var tap = dim.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(CloseSheet);

            var layout = dim.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(40, 40, 60, 60);
            layout.spacing = 26;

            var lines = _loop.Data.dialogue?.migrationVignette;
            if (lines == null || lines.Count == 0)
            {
                lines = new List<string> { "The camp folds.", "The land exhales.", "What you gave, it keeps." };
            }

            foreach (var line in lines)
            {
                MakeText(dim.transform, "<i>" + line + "</i>", 30, TextAnchor.MiddleCenter, NightText, _serif);
            }

            MakeText(dim.transform, "+" + Mathf.FloorToInt((float)verdureGained) + " VERDURE", 20,
                TextAnchor.MiddleCenter, new Color(0.624f, 0.682f, 0.494f, 1f), _smallCaps);
            MakeText(dim.transform, "TAP TO WALK ON", 15, TextAnchor.MiddleCenter,
                new Color(NightText.r, NightText.g, NightText.b, 0.45f), _smallCaps);
        }

        private void OpenNamingSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Rename", 32, TextAnchor.UpperCenter, Ink, _serif);
            var field = MakeInputField(sheet, familiar.name);
            Button(sheet, "Save", 320, () =>
            {
                _loop.RenameFamiliar(familiar, field.text);
                _dirty = true;
                CloseSheet();
            });
            Button(sheet, "Cancel", 320, CloseSheet);
        }

        private void OpenPowerupSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet, familiar.name + " has learned something", 30, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "choose what — a build is a commitment", 21, TextAnchor.UpperCenter, Ink2, _hand);

            foreach (var powerup in _loop.OfferablePowerups(familiar))
            {
                var captured = powerup;
                Button(sheet, captured.displayName + " — " + captured.description, 560, () =>
                {
                    _loop.ChoosePowerup(familiar, captured.id);
                    SetNote(familiar.name + " chose: " + captured.displayName.ToLowerInvariant() + ".");
                    _dirty = true;
                    CloseSheet();
                });
            }

            Button(sheet, "Later", 320, CloseSheet);
        }

        /// <summary>
        /// The persistent camp-actions strip under the node/world gap: the
        /// rewarded time-skip and the one-off remove-ads purchase, reachable
        /// from every journal page.
        /// </summary>
        private void BuildCampActions(Transform root)
        {
            var bar = MakePanel("CampActions", (RectTransform)root, CardPaper);
            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 10;
            var element = bar.AddComponent<LayoutElement>();
            element.flexibleHeight = 0;
            AddBorder(bar, Ink2);

            _timeSkipButton = Button(bar.transform, "Hasten a while — watch a short ad", 380, OnTimeSkip);
            _timeSkipButton.GetComponent<Image>().color = OchreWash;
            _timeSkipButton.GetComponentInChildren<Text>().color = Ochre;

            _removeAdsButton = Button(bar.transform, "Remove ads", 200, OnRemoveAds);
            if (_loop.Store.RemoveAdsOwned)
            {
                _removeAdsButton.gameObject.SetActive(false);
            }
        }

        private void OnTimeSkip()
        {
            _loop.Ads.ShowRewarded(RewardedPlacement.TimeSkip,
                () =>
                {
                    _loop.CreditTimeSkip(TimeSkipHours);
                    _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "time_skip"));
                    SetNote(NumberFormat.Duration(TimeSkipHours * 3600.0)
                            + " pass in a breath — the kith kept to the work.");
                    _dirty = true;
                });
        }

        private void OnRemoveAds()
        {
            _loop.Store.Purchase(StoreProductIds.RemoveAds, result =>
            {
                switch (result)
                {
                    case StoreResult.Purchased:
                    case StoreResult.AlreadyOwned:
                        if (_removeAdsButton != null)
                        {
                            _removeAdsButton.gameObject.SetActive(false);
                        }

                        SetNote("The ads step aside. Thank you for keeping the grove.");
                        break;
                    case StoreResult.Failed:
                        SetNote("That didn't go through — nothing was charged.");
                        break;
                }
            });
        }

        private Transform BeginSheet()
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, DimColor);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;

            var panel = MakePanel("Panel", (RectTransform)dim.transform, PagePaper);
            AddBorder(panel, Ink2);
            AddBorder(panel, RulePaper, 6f);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Fixed width, hugged height — a fixed height leaves a welcome-back
            // note floating in a half-empty card.
            rt.sizeDelta = new Vector2(800, 0);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 36);
            layout.spacing = 16;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel.transform;
        }

        private void CloseSheet()
        {
            if (_sheet != null)
            {
                Destroy(_sheet);
                _sheet = null;
            }
        }

        // ─────────────────────────── Helpers ─────────────────────────────────

        private List<ZoneData> ZonesInOrder()
        {
            var unlocked = Upgrades.UnlockedZoneIds(_loop.State, _loop.Data);
            var zones = new List<ZoneData>();
            foreach (var zone in _loop.Data.zones.OrderBy(z => z.order))
            {
                if (unlocked.Contains(zone.id))
                {
                    zones.Add(zone);
                }
            }

            return zones;
        }

        private ZoneData LatestZone()
        {
            var zones = ZonesInOrder();
            return zones.Count > 0 ? zones[zones.Count - 1] : null;
        }

        private string ZoneName(string zoneId)
        {
            return _loop.Data.ZonesById.TryGetValue(zoneId ?? string.Empty, out var zone)
                ? zone.displayName
                : zoneId;
        }

        /// <summary>The Ladder rung whose effects unlock <paramref name="skill"/>, or null when nothing grants it.</summary>
        private UpgradeData SkillSource(string skill)
        {
            foreach (var upgrade in _loop.Data.upgrades)
            {
                foreach (var effect in upgrade.effects)
                {
                    if (effect.type == EffectType.UnlockSkill && effect.skill == skill)
                    {
                        return upgrade;
                    }
                }
            }

            return null;
        }

        private static string Roman(int value)
        {
            if (value <= 0)
            {
                return "—";
            }

            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < values.Length && value > 0; i++)
            {
                while (value >= values[i])
                {
                    sb.Append(symbols[i]);
                    value -= values[i];
                }
            }

            return sb.ToString();
        }

        private List<string> TradeableResources()
        {
            var list = new List<string>();
            if (_loop.Data.resources != null)
            {
                foreach (var resource in _loop.Data.resources)
                {
                    list.Add(resource.id);
                }
            }

            if (_loop.Data.recipes != null)
            {
                foreach (var recipe in _loop.Data.recipes)
                {
                    if (recipe.kind == "trade" && recipe.output != null && !list.Contains(recipe.output))
                    {
                        list.Add(recipe.output);
                    }
                }
            }

            return list;
        }

        private static string Cycle(List<string> options, string current)
        {
            if (options.Count == 0)
            {
                return current;
            }

            var index = options.IndexOf(current);
            return options[(index + 1) % options.Count];
        }

        /// <summary>
        /// What a rung or kit piece actually does, in journal ink — effects
        /// are machine-readable, so this is where they turn into words.
        /// </summary>
        private string EffectsLabel(List<EffectData> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var effect in effects)
            {
                var label = EffectLabel(effect);
                if (!string.IsNullOrEmpty(label))
                {
                    parts.Add(label);
                }
            }

            return string.Join(" · ", parts);
        }

        private string EffectLabel(EffectData effect)
        {
            switch (effect.type)
            {
                case EffectType.YieldMult:
                    return YieldTarget(effect) + " ×" + PlainNumber(effect.value);
                case EffectType.YieldBonus:
                    return YieldTarget(effect) + " +" + Percent(effect.value);
                case EffectType.HaulMult:
                    return "hauling ×" + PlainNumber(effect.value);
                case EffectType.SellValueBonus:
                    return effect.resource + " sells +" + Percent(effect.value);
                case EffectType.CraftSpeedMult:
                    return (string.IsNullOrEmpty(effect.skill) ? "crafting" : effect.skill) + " ×" + PlainNumber(effect.value) + " faster";
                case EffectType.DigSpeedMult:
                    return "digs ×" + PlainNumber(effect.value) + " faster";
                case EffectType.PristineChanceBonus:
                    return "pristine chance +" + Percent(effect.value);
                case EffectType.FolioSpreadBonusMult:
                    return "folio spreads ×" + PlainNumber(effect.value);
                case EffectType.OfflineCapHours:
                    return "away credit up to " + PlainNumber(effect.value) + "h";
                case EffectType.OfflineCapBonusHours:
                    return "away credit +" + PlainNumber(effect.value) + "h";
                case EffectType.OfflineNightFullRate:
                    return "full pace through the night away";
                case EffectType.TendingBurstBonus:
                    return "tending burst +" + Percent(effect.value);
                case EffectType.CarrierCapacityBonus:
                    return "carriers hold +" + Percent(effect.value);
                case EffectType.NoSpoilage:
                    return effect.resource + " never spoils";
                case EffectType.UnlockZone:
                    return "opens " + ZoneName(effect.zone);
                case EffectType.UnlockSkill:
                    return "unlocks " + effect.skill;
                case EffectType.UnlockRecipe:
                    return "teaches " + effect.recipe;
                case EffectType.UnlockDigSite:
                    return "opens the watch in " + ZoneName(effect.zone);
                case EffectType.UnlockVerdureForecast:
                    return "reveals the verdure forecast";
                case EffectType.KithSlot:
                    return "+" + PlainNumber(effect.value) + " kith " + (effect.value > 1 ? "slots" : "slot");
                default:
                    return string.Empty;
            }
        }

        private string YieldTarget(EffectData effect)
        {
            if (!string.IsNullOrEmpty(effect.skill))
            {
                return effect.skill + " yield";
            }

            if (!string.IsNullOrEmpty(effect.zone))
            {
                return ZoneName(effect.zone) + " yield";
            }

            if (!string.IsNullOrEmpty(effect.resource))
            {
                return effect.resource + " yield";
            }

            return "all yields";
        }

        private static string PlainNumber(double value)
        {
            return value % 1.0 == 0.0 ? ((long)value).ToString() : value.ToString("0.##");
        }

        private static string Percent(double value)
        {
            return Mathf.RoundToInt((float)(value * 100.0)) + "%";
        }

        private string StationLabel(string stationId)
        {
            if (string.IsNullOrEmpty(stationId))
            {
                return "wandering";
            }

            if (stationId == Familiar.TrailStation)
            {
                return "the trail";
            }

            if (stationId.StartsWith(Familiar.DigStationPrefix))
            {
                return "watch: " + ZoneName(stationId.Substring(Familiar.DigStationPrefix.Length));
            }

            foreach (var node in _loop.State.nodes)
            {
                if (node.id == stationId)
                {
                    return node.resourceId;
                }
            }

            return stationId;
        }

        private string SpeciesName(string speciesId)
        {
            return _loop.Data.SpeciesById != null && _loop.Data.SpeciesById.TryGetValue(speciesId ?? string.Empty, out var species)
                ? species.displayName
                : speciesId;
        }

        private string UpgradeRequirement(UpgradeData upgrade)
        {
            var parts = new List<string>();
            if (!_loop.MeetsUpgradeSkillGate(upgrade) && !string.IsNullOrEmpty(upgrade.gateSkill))
            {
                parts.Add("needs " + upgrade.gateSkill + " " + upgrade.gateLevel);
            }

            var tool = _loop.MissingToolTier(upgrade);
            if (!string.IsNullOrEmpty(tool))
            {
                parts.Add("needs " + tool + " tools");
            }

            if (upgrade.materials != null && upgrade.materials.Count > 0)
            {
                parts.Add(BundleHaveLabel(upgrade.materials));
            }

            return parts.Count == 0 ? "ready" : string.Join(" · ", parts);
        }

        private static string BundleLabel(List<ItemAmount> materials)
        {
            if (materials == null || materials.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var material in materials)
            {
                parts.Add(material.amount + " " + material.id);
            }

            return string.Join(", ", parts);
        }

        private static string BundleLabel(List<Buildings.MaterialCost> bundle)
        {
            if (bundle == null || bundle.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var cost in bundle)
            {
                parts.Add(NumberFormat.Short(cost.amount) + " " + cost.id);
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// A bundle line that also says what the camp holds of each item —
        /// "4 berries (have 35.8K), 2 nuts (have 48)" — with any shortfall
        /// inked in ochre so the blocking item is the one that stands out.
        /// </summary>
        private string BundleHaveLabel(IEnumerable<(string id, BigDouble amount)> bundle)
        {
            var parts = new List<string>();
            foreach (var (id, amount) in bundle)
            {
                var have = _loop.State.GetResource(id);
                var part = NumberFormat.Short(amount) + " " + id + " (have " + NumberFormat.Short(have) + ")";
                parts.Add(have < amount ? "<color=" + OchreInkHex + ">" + part + "</color>" : part);
            }

            return string.Join(", ", parts);
        }

        private string BundleHaveLabel(List<ItemAmount> materials)
        {
            return BundleHaveLabel(Costs(materials));
        }

        private string BundleHaveLabel(List<Buildings.MaterialCost> bundle)
        {
            return BundleHaveLabel(Costs(bundle));
        }

        private static IEnumerable<(string id, BigDouble amount)> Costs(List<ItemAmount> materials)
        {
            foreach (var material in materials ?? new List<ItemAmount>())
            {
                yield return (material.id, new BigDouble(material.amount));
            }
        }

        private static IEnumerable<(string id, BigDouble amount)> Costs(List<Buildings.MaterialCost> bundle)
        {
            foreach (var cost in bundle ?? new List<Buildings.MaterialCost>())
            {
                yield return (cost.id, cost.amount);
            }
        }

        /// <summary>A journal card — the mock's bordered paper panel, with an optional small-caps head.</summary>
        private RectTransform Card(string head)
        {
            var go = MakePanel(head == null ? "Plate" : "Card_" + head, _body, CardPaper);
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
                var headText = MakeText(go.transform, head, 15, TextAnchor.MiddleCenter, Ink2, _smallCaps);
                headText.gameObject.name = "CardHead";
            }

            return (RectTransform)go.transform;
        }

        /// <summary>The action strip along a plate's bottom edge.</summary>
        private Transform ActionRow(RectTransform card)
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

        private GameObject Row(RectTransform parent)
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

        private void MakeHairline(RectTransform parent)
        {
            var go = MakePanel("Rule", parent, RulePaper);
            FixedHeight(go, 2);
            go.GetComponent<Image>().raycastTarget = false;
        }

        private Button Button(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
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
            var label = MakeText(go.transform, text, 19, TextAnchor.MiddleCenter, Ink, _smallCaps);
            Stretch((RectTransform)label.transform);
            return button;
        }

        private static void SetButtonLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetButtonTint(Button button, bool on)
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

        private InputField MakeInputField(Transform parent, string value)
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

        private static Font LoadFont(string name, Font fallback)
        {
            var font = Resources.Load<Font>("Fonts/" + name);
            return font != null ? font : fallback;
        }

        private Text MakeText(Transform parent, string value, int size, TextAnchor anchor)
        {
            return MakeText(parent, value, size, anchor, Ink);
        }

        private Text MakeText(Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            return MakeText(parent, value, size, anchor, color, _font);
        }

        /// <summary>
        /// Global type scale. Authored sizes track the mock's rem values 1:1,
        /// but on a phone the mock's page is ~2.25x the CSS pixel size while
        /// the canvas reference gives ~1.5x — this closes the gap. Applied in
        /// MakeText and SizeOpen so every glyph scales together.
        /// </summary>
        private const float FontScale = 1.5f;

        /// <summary>An inline rich-text size tag, scaled like MakeText sizes.</summary>
        private static string SizeOpen(int size)
        {
            return "<size=" + Mathf.RoundToInt(size * FontScale) + ">";
        }

        private Text MakeText(Transform parent, string value, int size, TextAnchor anchor, Color color, Font font)
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

        // ─────────────────────────── Generated art ───────────────────────────

        private static Sprite _borderSprite;
        private static Sprite _grainSprite;
        private static Sprite _dashSprite;
        private static Sprite _dashAcrossSprite;
        private static Sprite _spineSprite;

        /// <summary>A sliced border-only sprite — the journal's ruled ink outlines.</summary>
        private static Sprite BorderSprite()
        {
            if (_borderSprite == null)
            {
                const int size = 8;
                const int thickness = 2;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var edge = x < thickness || y < thickness || x >= size - thickness || y >= size - thickness;
                        texture.SetPixel(x, y, edge ? Color.white : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _borderSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
            }

            return _borderSprite;
        }

        /// <summary>The page's paper-grain noise — the mock's fractal-noise overlay, seeded for a stable look.</summary>
        private static Sprite GrainSprite()
        {
            if (_grainSprite == null)
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var rng = new System.Random(1897);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var alpha = rng.NextDouble() < 0.5 ? 0f : (float)rng.NextDouble() * 0.08f;
                        texture.SetPixel(x, y, new Color(Ink.r, Ink.g, Ink.b, alpha));
                    }
                }

                texture.Apply();
                _grainSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _grainSprite;
        }

        /// <summary>
        /// The stitched spine's thread — a soft-edged vertical stitch, faded
        /// at the ends and sides so the tiled column reads as sewn thread
        /// rather than aliased blocks. Separate from <see cref="DashSprite"/>:
        /// the trail row's rule samples that tile's bottom rows and would
        /// change appearance if this softening were applied there.
        /// </summary>
        private static Sprite SpineSprite()
        {
            if (_spineSprite == null)
            {
                const int width = 6;
                const int height = 28;
                const int stitch = 14; // thread length; the rest of the tile is gap
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var along = Mathf.Clamp01(Mathf.Min(y + 1, stitch - y) / 2f);
                        var across = Mathf.Clamp01(Mathf.Min(x + 1, width - x) / 2f);
                        var alpha = y < stitch ? 0.5f * along * across : 0f;
                        texture.SetPixel(x, y, new Color(Ink2.r, Ink2.g, Ink2.b, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _spineSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            return _spineSprite;
        }

        /// <summary>A vertical dash pattern — the trail row's dotted rule.</summary>
        private static Sprite DashSprite()
        {
            if (_dashSprite == null)
            {
                const int width = 4;
                const int height = 24;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        texture.SetPixel(x, y, y < height / 2 ? new Color(Ink2.r, Ink2.g, Ink2.b, 0.45f) : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _dashSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            return _dashSprite;
        }

        /// <summary>A horizontal dash pattern — the dashed outlines' top and bottom rules.</summary>
        private static Sprite DashAcrossSprite()
        {
            if (_dashAcrossSprite == null)
            {
                const int width = 24;
                const int height = 4;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        texture.SetPixel(x, y, x < width / 2 ? new Color(Ink2.r, Ink2.g, Ink2.b, 0.45f) : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _dashAcrossSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            return _dashAcrossSprite;
        }

        /// <summary>
        /// A dashed outline drawn just outside a control's bounds — the empty
        /// post: a pencilled box waiting for someone to stand in it. Toggle
        /// the returned object with the fallow/filled state.
        /// </summary>
        private GameObject AddDashedBorder(GameObject host)
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

        private static void DashStrip(RectTransform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 thickness)
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
        private GameObject AddBorder(GameObject host, Color color, float inset = 0f)
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

        private GameObject MakePanel(string name, RectTransform parent, Color color)
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
        private GameObject PlateImage(Transform parent, Sprite sprite, float height)
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
        private GameObject IconImage(Transform parent, Sprite sprite, float size, Color tint)
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

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void FixedHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0;
        }

        private static void Flexible(GameObject go, float weight)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleHeight = weight;
        }

        /// <summary>
        /// Take the row's horizontal slack (the mock's flex:1 label). Distinct
        /// from Flexible: a flexibleHeight on a row label bubbles up through
        /// the layout groups and lets the whole panel soak up screen slack.
        /// </summary>
        private static void FlexibleWidth(GameObject go, float weight)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleWidth = weight;
        }
    }
}
