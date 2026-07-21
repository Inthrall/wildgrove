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
        private Button _foldButton;
        private RectTransform _worldGap;
        private RectTransform _body;
        private Transform _modalLayer;
        private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
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
            SetNote("tap a node's plate to tend it · space / (A) tends the selected node");
        }

        private void Update()
        {
            if (_loop == null || _loop.State == null)
            {
                return;
            }

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
            foreach (var pair in _tabButtons)
            {
                pair.Value.GetComponent<Image>().color = pair.Key == _tab ? PagePaper : DeepPaper;
            }
        }

        // ─────────────────────────── Chrome ──────────────────────────────────

        private void BuildChrome()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var root = MakeRect("Root", canvasGo.transform);
            Stretch(root);
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.padding = new RectOffset(16, 16, 10, 0);
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
            var trackerLayout = trackerGo.AddComponent<HorizontalLayoutGroup>();
            trackerLayout.childControlWidth = true;
            trackerLayout.childControlHeight = true;
            trackerLayout.childForceExpandWidth = false;
            trackerLayout.childForceExpandHeight = true;
            trackerLayout.childAlignment = TextAnchor.MiddleCenter;
            trackerLayout.padding = new RectOffset(12, 12, 6, 6);
            trackerLayout.spacing = 10;
            var trackerButton = trackerGo.AddComponent<Button>();
            trackerButton.onClick.AddListener(() => OpenTab(TabTrail));
            AddBorder(trackerGo, Ink2);
            _trackerText = MakeText(trackerGo.transform, string.Empty, 21, TextAnchor.MiddleCenter, Ink, _serif);
            Flexible(_trackerText.gameObject, 1f);
            _foldButton = Button(trackerGo.transform, "Fold the camp", 260, OpenMigrationSheet);
            _foldButton.GetComponent<Image>().color = OchreWash;
            _foldButton.GetComponentInChildren<Text>().color = Ochre;
            _foldButton.gameObject.SetActive(false);

            // World gap — the WorldView strip draws here; flexible so it takes
            // the slack above the scrolling page.
            var gap = MakeRect("WorldGap", root);
            _worldGap = gap;
            Flexible(gap.gameObject, 1f);

            // The open journal page — a scroll view the tab pages build into.
            _body = BuildScroll(root);

            // The journal tabs along the bottom edge.
            BuildTabsBar(root);

            // The page paper itself — behind the whole column.
            var paper = new GameObject("Paper", typeof(Image));
            paper.transform.SetParent(canvasGo.transform, false);
            paper.transform.SetAsFirstSibling();
            paper.GetComponent<Image>().color = PagePaper;
            paper.GetComponent<Image>().raycastTarget = false;
            Stretch((RectTransform)paper.transform);

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
            spineImage.sprite = DashSprite();
            spineImage.type = Image.Type.Tiled;
            spineImage.raycastTarget = false;
            var spineRect = (RectTransform)spine.transform;
            spineRect.anchorMin = new Vector2(0f, 0f);
            spineRect.anchorMax = new Vector2(0f, 1f);
            spineRect.offsetMin = new Vector2(7f, 8f);
            spineRect.offsetMax = new Vector2(11f, -8f);

            // Modal layer, above everything, initially empty.
            var modalGo = MakeRect("Modals", canvasGo.transform);
            Stretch(modalGo);
            _modalLayer = modalGo;

            Canvas.ForceUpdateCanvases();
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
            go.GetComponent<Image>().color = id == _tab ? PagePaper : DeepPaper;
            AddBorder(go, Ink2);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => OpenTab(id));
            var text = MakeText(go.transform, label, 22, TextAnchor.MiddleCenter, Ink2, _smallCaps);
            Stretch((RectTransform)text.transform);
            _tabButtons[id] = button;
        }

        private RectTransform BuildScroll(RectTransform parent)
        {
            var scrollGo = new GameObject("Body", typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            Flexible(scrollGo, 2f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

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
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

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
                    title = "Kit, Crafts & Kith";
                    break;
                case TabRecord:
                    eyebrow = "THE RECORD";
                    title = "The Journal's Back Pages";
                    break;
                default:
                    var zone = LatestZone();
                    eyebrow = "THE TRAIL" + (zone != null ? " · " + zone.id.ToUpperInvariant() : string.Empty);
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
                if (stock > BigDouble.Zero || Compendium.IsResourceDiscovered(state, resource.id))
                {
                    parts.Add(resource.id + " <b>" + NumberFormat.Short(stock) + "</b>");
                }
            }

            parts.Add("<color=" + OchreHex + ">RENOWN <b>" + NumberFormat.Short(state.renown) + "</b></color>");
            parts.Add("<color=" + MossDeepHex + ">verdure <b>" + Mathf.FloorToInt((float)state.verdurePoints) + "</b></color>");
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
                return;
            }

            _foldButton.gameObject.SetActive(false);
            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite == null || rite.verses == null)
            {
                _trackerText.text = string.Empty;
                return;
            }

            foreach (var verse in rite.verses)
            {
                if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse) || Rite.IsVerseComplete(_loop.State, _loop.Data, verse))
                {
                    continue;
                }

                var done = Rite.CompletedSlotCount(_loop.State, verse);
                var need = _loop.Data.rites.chooseCount;
                _trackerText.text = "Verse of " + ZoneName(verse.zone) + " — <b>"
                                    + Mathf.Min(done, need) + " of " + need + "</b> answered";
                return;
            }

            _trackerText.text = string.Empty;
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
        }

        // ─────────────────────────── TRAIL ────────────────────────────────────

        private void BuildTrailPage()
        {
            var unlockedZones = ZonesInOrder();
            var figure = 1;
            foreach (var zone in unlockedZones)
            {
                if (unlockedZones.Count > 1)
                {
                    var heading = MakeText(_body, zone.displayName.ToUpperInvariant(), 17, TextAnchor.MiddleCenter, Ink2, _smallCaps);
                    heading.gameObject.name = "ZoneHeading";
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

            BuildVerseCards();
            BuildWaystoneFooter();
        }

        private void BuildNodePlate(NodeState node, int figure)
        {
            var captured = node;
            var card = Card(null);
            var cardImage = card.GetComponent<Image>();

            var fig = MakeText(card, "FIG. " + figure + ".", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);
            fig.gameObject.name = "Fig";
            var nameLine = MakeText(card, string.Empty, 26, TextAnchor.MiddleLeft, Ink, _serif);
            var postedLine = MakeText(card, string.Empty, 21, TextAnchor.MiddleLeft, Ink2, _hand);
            var statsLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);

            // The tend flash — the mock's "+ yield" that rises and fades; sits
            // outside the layout at the plate's top-right corner.
            var flash = MakeText(card, "+ yield", 24, TextAnchor.MiddleRight, MossDeep, _hand);
            flash.gameObject.name = "TendFlash";
            flash.raycastTarget = false;
            flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var flashRect = (RectTransform)flash.transform;
            flashRect.anchorMin = new Vector2(1f, 1f);
            flashRect.anchorMax = new Vector2(1f, 1f);
            flashRect.pivot = new Vector2(1f, 1f);
            flashRect.sizeDelta = new Vector2(180f, 34f);
            flashRect.anchoredPosition = new Vector2(-12f, -6f);
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
                flashRect.anchoredPosition = new Vector2(-12f, -6f + 20f * t);
            });

            // The basket bar — fill width driven by the live updater.
            var barGo = MakePanel("Basket", card, PagePaper);
            FixedHeight(barGo, 10);
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

            var actions = ActionRow(card);
            var tend = Button(actions, "Tend here", 200, () =>
            {
                _selected = captured;
                DoTend(captured);
            });
            tend.GetComponent<Image>().color = MossWash;

            var replant = Button(actions, "Plant back", 230, () =>
            {
                if (_loop.Replant(captured))
                {
                    SetNote("planted " + captured.resourceId + " back into the ground. it earns tone, not numbers.");
                    _dirty = true;
                }
                else
                {
                    SetNote("not enough " + captured.resourceId + " to plant back. the land can wait.");
                }
            });

            if (_loop.PlantersUnlocked())
            {
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

                postedLine.text = PostedLabel(captured);

                var rate = Simulation.YieldPerSecond(captured, state, _loop.Data, _loop.Data.economy);
                var stock = state.GetResource(captured.resourceId);
                statsLine.text = NumberFormat.Rate(rate) + "/s  ·  " + NumberFormat.Short(stock) + " at camp";

                var cap = NodeBasketCapacity(captured);
                var fraction = cap > BigDouble.Zero ? Mathf.Clamp01((float)(captured.basket / cap).ToDouble()) : 0f;
                fillRect.anchorMax = new Vector2(fraction, 1f);
                fill.color = fraction >= 0.999f ? OchreWash : MossWash;

                SetButtonLabel(replant, "Plant back\n<size=15>" + NumberFormat.Short(_loop.ReplantCost(captured)) + " " + captured.resourceId + "</size>");
                var ok = _loop.CanReplant(captured);
                replant.interactable = ok;
                SetButtonTint(replant, ok);
            });
        }

        private string PostedLabel(NodeState node)
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

            return names.Count > 0 ? "posted: " + string.Join(", ", names) : "fallow";
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

                status.text = names.Count > 0 ? string.Join(", ", names) + " carrying" : "the trail waits";
            });
        }

        private void BuildWatchPlate(DigSiteState site)
        {
            var captured = site;
            var card = Card("THE WATCH · " + ZoneName(site.zoneId).ToUpperInvariant());
            var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);

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
                line.text = watchers > 0
                    ? watchers + " watching where the small lives cross"
                    : "no one watches — station a familiar here from the Warden page";
            });
        }

        private void AddPlanterAction(Transform actions, PlanterData planter, string targetId)
        {
            var capturedPlanter = planter;
            var capturedTarget = targetId;
            if (_loop.PlanterBuilt(capturedPlanter, capturedTarget))
            {
                var built = MakeText(actions, capturedPlanter.displayName + " — raised", 16, TextAnchor.MiddleLeft, MossDeep);
                built.gameObject.name = "PlanterBuilt";
                return;
            }

            var build = Button(actions, "Raise " + capturedPlanter.displayName
                                        + "\n<size=14>" + BundleLabel(capturedPlanter.materials) + "</size>", 250, () =>
            {
                if (_loop.BuildPlanter(capturedPlanter, capturedTarget))
                {
                    SetNote("raised a " + capturedPlanter.displayName.ToLowerInvariant() + ". the ground holds it now.");
                    _dirty = true;
                }
            });
            _liveUpdaters.Add(() =>
            {
                var ok = _loop.CanBuildPlanter(capturedPlanter, capturedTarget);
                build.interactable = ok;
                SetButtonTint(build, ok);
            });
        }

        private void BuildVerseCards()
        {
            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite == null || rite.verses == null)
            {
                return;
            }

            foreach (var verse in rite.verses)
            {
                if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse))
                {
                    continue;
                }

                BuildVerseCard(verse);
            }
        }

        private void BuildVerseCard(RiteVerseData verse)
        {
            var capturedVerse = verse;
            var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
            var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
            var card = Card("VERSE OF " + ZoneName(verse.zone).ToUpperInvariant());

            var siteLine = MakeText(card, site, 23, TextAnchor.MiddleCenter, Ink, _serif);
            siteLine.gameObject.name = "Site";
            var verseLine = Narrative.VerseLine(_loop.Data, verse.zone);
            if (!string.IsNullOrEmpty(verseLine))
            {
                MakeText(card, "<i>“" + verseLine + "”</i>", 19, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            var progress = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                progress.text = "answered " + Rite.CompletedSlotCount(_loop.State, capturedVerse)
                                + " of " + _loop.Data.rites.chooseCount;
            });

            for (var i = 0; i < verse.slots.Count; i++)
            {
                BuildSlotRow(card, capturedVerse, i);
            }
        }

        private void BuildSlotRow(RectTransform card, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            Flexible(label.gameObject, 1f);

            Button offer = null;
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    offer = Button(row.transform, "Set down", 180, () =>
                    {
                        var given = _loop.OfferResource(verse, slotIndex);
                        if (given > BigDouble.Zero)
                        {
                            SetNote("set down " + Mathf.FloorToInt((float)given.ToDouble()) + " " + slot.resource + ". no answer. not yet.");
                        }
                        else
                        {
                            SetNote("nothing in the stores to set down.");
                        }
                    });
                    break;
                case RiteSlotType.Specimen:
                    offer = Button(row.transform, "Offer one", 180, () =>
                    {
                        if (_loop.OfferSpecimen(verse, slotIndex))
                        {
                            SetNote("set the perfect one down. it deserved better than a page, maybe.");
                        }
                        else
                        {
                            SetNote("no such find in hand. the site is patient.");
                        }
                    });
                    break;
                case RiteSlotType.Sketch:
                    offer = Button(row.transform, "Offer a sketch", 220, () =>
                    {
                        if (_loop.OfferSketch(verse, slotIndex))
                        {
                            SetNote("tore the page out for them. that portion must be watched again.");
                        }
                        else
                        {
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
                var name = SlotName(slot);
                label.text = done
                    ? "<color=" + MossDeepHex + ">" + name + " — set down</color>"
                    : name + "  <color=" + Ink2Hex + ">" + Mathf.FloorToInt((float)delivered) + " / " + Mathf.FloorToInt((float)target) + "</color>";
                if (offer != null)
                {
                    offer.interactable = !done;
                    SetButtonTint(offer, !done);
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

            var footer = MakeText(_body, "<i>“" + text + "”</i>\n<size=14>WAYSTONE · " + zone.displayName.ToUpperInvariant() + "</size>",
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
                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                Flexible(label.gameObject, 1f);
                var toggle = Button(row.transform, "Craft", 160, () =>
                {
                    _loop.ToggleCraft(captured);
                    _dirty = true;
                });

                _liveUpdaters.Add(() =>
                {
                    var crafting = _loop.IsCrafting(captured);
                    var progress = crafting ? "  " + Mathf.RoundToInt((float)_loop.CraftProgress(captured) * 100f) + "%" : string.Empty;
                    var need = _loop.IsRecipeLevelMet(captured)
                        ? string.Empty
                        : "  <color=" + OchreHex + ">needs " + captured.skill + " " + captured.skillLevel + "</color>";
                    label.text = captured.output + progress + need
                                 + "\n<size=15><color=" + Ink2Hex + ">" + BundleLabel(captured.inputs) + "</color></size>";
                    SetButtonLabel(toggle, crafting ? "Stop" : "Craft");
                    var ok = crafting || _loop.IsRecipeWorkable(captured);
                    toggle.interactable = ok;
                    SetButtonTint(toggle, ok);
                });
            }
        }

        private void BuildBuildingsCard()
        {
            var card = Card("BUILDING LINES");
            foreach (var building in _loop.Data.buildings)
            {
                var captured = building;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                Flexible(label.gameObject, 1f);
                var build = Button(row.transform, "Raise", 160, () =>
                {
                    if (_loop.BuyBuildingLevel(captured))
                    {
                        SetNote(captured.displayName.ToLowerInvariant() + " goes up. the camp sleeps closer to the work.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  <color=" + Ink2Hex + ">level " + _loop.BuildingLevel(captured) + "</color>"
                                 + "\n<size=15><color=" + Ink2Hex + ">next: " + BundleLabel(_loop.NextBuildingBundle(captured)) + "</color></size>";
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
            var trade = Button(tradeRow.transform, "Trade all", 260, () =>
            {
                var have = _loop.State.GetResource(_exchangeFrom);
                var got = _loop.TradeAtExchange(_exchangeFrom, _exchangeTo, have);
                if (got > BigDouble.Zero)
                {
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
                Flexible(label.gameObject, 1f);
                var buy = Button(row.transform, "Take up", 170, () =>
                {
                    if (_loop.PurchaseUpgrade(captured))
                    {
                        SetNote(captured.displayName.ToLowerInvariant() + " — the work changes shape.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName
                                 + "\n<size=15><color=" + Ink2Hex + ">" + UpgradeRequirement(captured) + "</color></size>";
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
            BuildKithCard();
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
            foreach (var gear in _loop.Data.gear)
            {
                var captured = gear;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                Flexible(label.gameObject, 1f);

                if (Gear.IsEquipped(_loop.State, captured))
                {
                    label.text = captured.slot.ToUpperInvariant() + " — " + captured.displayName
                                 + "  <color=" + MossDeepHex + ">worn</color>";
                    continue;
                }

                label.text = captured.slot.ToUpperInvariant() + " — " + captured.displayName
                             + "\n<size=15><color=" + Ink2Hex + ">" + BundleLabel(captured.materials) + "</color></size>";
                var craft = Button(row.transform, "Craft", 160, () =>
                {
                    if (_loop.CraftGear(captured))
                    {
                        SetNote("bound the " + captured.displayName.ToLowerInvariant() + " tight. the work will mind it less.");
                        _dirty = true;
                    }
                });
                _liveUpdaters.Add(() =>
                {
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
                var line = MakeText(card, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    var progress = Mathf.RoundToInt((float)_loop.SkillProgress(captured) * 100f);
                    line.text = captured + " — level " + _loop.SkillLevel(captured)
                                + "  <size=15><color=" + Ink2Hex + ">" + progress + "% to next</color></size>";
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
                var label = MakeText(row.transform, string.Empty, 22, TextAnchor.MiddleLeft, Ink, _serif);
                Flexible(label.gameObject, 1f);
                var rename = Button(row.transform, "Name", 110, () => OpenNamingSheet(captured));
                var powerup = Button(row.transform, "A lesson", 170, () => OpenPowerupSheet(captured));
                powerup.GetComponent<Image>().color = OchreWash;

                var posts = PostButtonsGrid(card, captured);

                _liveUpdaters.Add(() =>
                {
                    var species = SpeciesName(captured.speciesId);
                    var bonded = captured.bonded ? " <size=15><color=" + OchreHex + ">BONDED</color></size>" : string.Empty;
                    var kin = _loop.FamiliarKinship(captured) > 0
                        ? "  <color=" + OchreHex + ">KINSHIP " + Roman(_loop.FamiliarKinship(captured)) + "</color>"
                        : string.Empty;
                    var progress = Mathf.RoundToInt((float)_loop.FamiliarLevelProgress(captured) * 100f);
                    label.text = captured.name + bonded + "  <size=16><color=" + Ink2Hex + ">" + species + "</color></size>" + kin
                                 + "\n<size=16><color=" + Ink2Hex + ">level " + _loop.FamiliarLevel(captured)
                                 + " · " + progress + "% · " + StationLabel(captured.stationId) + "</color></size>";
                    powerup.gameObject.SetActive(_loop.HasPendingPowerup(captured));
                    foreach (var pair in posts)
                    {
                        pair.button.GetComponent<Image>().color =
                            PostMatches(captured.stationId, pair.stationId) ? MossWash : DeepPaper;
                    }
                });
            }
        }

        private List<(Button button, string stationId)> PostButtonsGrid(RectTransform card, Familiar familiar)
        {
            var gridGo = MakeRect("Posts", card).gameObject;
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(196, 58);
            grid.spacing = new Vector2(6, 6);
            grid.padding = new RectOffset(12, 12, 0, 6);
            // Fixed columns so the grid reports a deterministic preferred height
            // inside the content-size-fitted page (flexible constraint needs a
            // settled width, which the first layout pass doesn't have).
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            var buttons = new List<(Button, string)>();
            foreach (var node in _loop.State.nodes)
            {
                var captured = node;
                buttons.Add((PostButton(grid.transform, captured.resourceId, () => Station(familiar, captured.id)), captured.id));
            }

            buttons.Add((PostButton(grid.transform, "the trail", () => Station(familiar, Familiar.TrailStation)), Familiar.TrailStation));
            foreach (var site in _loop.State.digSites)
            {
                var stationId = Familiar.DigStationPrefix + site.zoneId;
                buttons.Add((PostButton(grid.transform, "watch: " + ZoneName(site.zoneId), () => Station(familiar, stationId)), stationId));
            }

            buttons.Add((PostButton(grid.transform, "wander", () => Station(familiar, null)), null));
            return buttons;
        }

        private Button PostButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Post", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = DeepPaper;
            AddBorder(go, Ink2);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = MakeText(go.transform, label, 16, TextAnchor.MiddleCenter, Ink, _smallCaps);
            Stretch((RectTransform)text.transform);
            return button;
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
                    line.text = captured.id + "  <size=15><color=" + Ink2Hex + ">lifetime "
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
                        : Folio.FixedEntryCount(_loop.State, captured) + " of " + captured.entries.Count + " fixed";
                    line.text = captured.displayName + "  <size=15>" + progress + "</size>";
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
                Flexible(label.gameObject, 1f);
                var fix = Button(row.transform, "Fix", 140, () =>
                {
                    if (_loop.FixSpecimen(resourceId))
                    {
                        SetNote("pressed it between these pages, where it will outlast the camp.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = "Pristine " + resourceId + "  <size=15><color=" + Ink2Hex + ">"
                                 + NumberFormat.Short(_loop.State.GetPristine(resourceId)) + " held</color></size>";
                    var ok = Folio.CanFix(_loop.State, _loop.Data, resourceId);
                    fix.interactable = ok;
                    SetButtonTint(fix, ok);
                });
            }

            if (anyPristine)
            {
                MakeText(card, "<i>fixing consumes the find — the page keeps it instead of you.</i>", 16, TextAnchor.MiddleCenter, Ink2, _serif);
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
                    var plate = Narrative.InsectPlate(_loop.Data, captured.id);
                    MakeText(card, captured.displayName + "  <color=" + MossDeepHex + ">recorded</color>"
                                   + (string.IsNullOrEmpty(plate) ? string.Empty : "\n<size=15><i>" + plate + "</i></size>"),
                        18, TextAnchor.MiddleLeft, Ink);
                    continue;
                }

                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
                _liveUpdaters.Add(() =>
                {
                    var sketches = Insects.SketchCount(_loop.State, captured.id);
                    line.text = sketches > 0
                        ? captured.displayName + " — " + sketches + " of " + captured.sketches + " portions sketched"
                        : "<i>unrecorded — something haunts " + string.Join(", ", captured.habitats) + "</i>";
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
                Flexible(label.gameObject, 1f);
                var buy = Button(row.transform, "Learn", 160, () =>
                {
                    if (_loop.BuyAlmanacNode(captured))
                    {
                        SetNote("the almanac takes a new line. it crosses every fold with you.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  <size=15><color=" + Ink2Hex + ">"
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
            rt.sizeDelta = new Vector2(800, 660);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 16;
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

        private static string Roman(int value)
        {
            string[] numerals = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return value >= 0 && value < numerals.Length ? numerals[value] : value.ToString();
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
                parts.Add(BundleLabel(upgrade.materials));
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
            fitter.minHeight = 68;
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
            element.preferredHeight = 68;
            element.minHeight = 68;
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
        }

        private InputField MakeInputField(Transform parent, string value)
        {
            var go = new GameObject("Field", typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = CardPaper;
            AddBorder(go, Ink2);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 520;
            element.minWidth = 520;
            element.preferredHeight = 72;
            element.minHeight = 72;

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

        private Text MakeText(Transform parent, string value, int size, TextAnchor anchor, Color color, Font font)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
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

        /// <summary>A vertical dash pattern — the stitched spine down the page's left edge.</summary>
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

        /// <summary>Overlay a ruled outline on a panel. Positive inset draws it outside the bounds (the mock's offset outline).</summary>
        private void AddBorder(GameObject host, Color color, float inset = 0f)
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
        }

        private GameObject MakePanel(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
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
    }
}
