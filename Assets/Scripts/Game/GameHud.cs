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
    /// docs/wildgrove-journal.html mock: a paper page with an eyebrow/title
    /// header, a resource ledger, a handwritten margin note, a pinned Rite /
    /// Fold tracker, and four journal tabs along the bottom (Trail · Camp ·
    /// Warden · Record) — set in the journal's own type (IM Fell / Caveat /
    /// Lora), ruled ink borders, paper grain, and the small motion touches
    /// (tend flash, the carrier on the trail line). All game logic stays in
    /// Wildgrove.Sim; this only reads state and calls <see cref="GameLoop"/>
    /// actions.
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

        private Text _eyebrow;
        private Text _title;
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

        // One open sheet at a time; the dim layer blocks input beneath it.
        private GameObject _sheet;

        // ─────────────────────────── Section access ──────────────────────────
        // The section builders reach shared HUD state and coordinator calls
        // through these; see JournalSection.

        internal GameLoop Loop => _loop;
        internal NodeState Selected => _selected;
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
            _sheets.PumpSheets();

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
            _foldButton = Button(trackerGo.transform, "Fold the camp", 260, _sheets.OpenMigrationSheet);
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
            _sheets.BuildCampActions(root);

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
                    var zone = _labels.LatestZone();
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
