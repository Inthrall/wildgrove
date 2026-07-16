using System.Collections.Generic;
using System.Globalization;
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
    /// The Phase 1 on-screen layer: a code-built uGUI HUD that surfaces the core
    /// loop (coin/familiar readouts, a row per gathering node, the gift / sell /
    /// tend actions, and the Phase-1 upgrade shop) and routes the free-space Tend gesture through
    /// <see cref="IGameInput"/>. Deliberately programmer-art — real layout and
    /// adaptive form factors are Phase 2. All game logic stays in Wildgrove.Sim;
    /// this only reads state and calls the <see cref="GameLoop"/> actions.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.12f, 0.16f, 0.13f, 0.92f);
        private static readonly Color RowColor = new Color(0.18f, 0.23f, 0.19f, 0.90f);
        private static readonly Color RowSelectedColor = new Color(0.24f, 0.34f, 0.24f, 0.95f);
        private static readonly Color AccentColor = new Color(0.35f, 0.55f, 0.34f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.90f, 1f);

        private GameLoop _loop;
        private IGameInput _input;
        private Font _font;

        // The shop shows the next few unpurchased rungs of the §9 ladder in
        // order — enough to see what's coming without a thirty-row wall.
        // Material-costed rungs appear too (unaffordable until crafting lands),
        // honestly previewing the crafting system rather than hiding it.
        private const int UpgradeShopWindow = 3;

        private const int WelcomeBackMaxGainLines = 6;

        // Recipe availability and skill levels change on the order of
        // purchases and level-ups, not frames — recomputing them per frame
        // was the HUD's main source of per-frame garbage (list + hash sets
        // per query) on a mobile target. A quarter second of staleness is
        // imperceptible next to the button press that changes them.
        private const float SectionRefreshInterval = 0.25f;

        private Text _headerLabel;
        private Text _upgradesHeader;
        private Text _craftingHeader;
        private Text _buildingsHeader;
        private Text _sellAllLabel;
        private Button _sellAllButton;
        private Text _timeSkipLabel;
        private Button _timeSkipButton;
        private Text _carrierLabel;
        private Button _carrierButton;
        private Transform _canvas;
        private Transform _nodesPanel;
        private Text _specimensHeader;
        private Transform _specimensPanel;
        private Text _excavationHeader;
        private Transform _excavationPanel;
        private Text _riteHeader;
        private Button _migrateButton;
        private GameObject _migrationSheet;
        private Text _almanacHeader;
        private GameObject _almanacPanel;
        private Text _kitHeader;
        private Text _museumHeader;
        private GameObject _museumPanel;
        private readonly List<MuseumRowView> _museumRows = new List<MuseumRowView>();
        private Text _compendiumHeader;
        private GameObject _compendiumPanel;
        private Text _compendiumText;
        private GameObject _welcomeSheet;
        private WorldView _world;
        private RectTransform _worldStrip;
        private static readonly Vector3[] CornerBuffer = new Vector3[4];
        private readonly List<RowView> _rows = new List<RowView>();
        private readonly List<SpecimenRowView> _specimenRows = new List<SpecimenRowView>();
        private readonly List<DigRowView> _digRows = new List<DigRowView>();
        private readonly List<VerseHeadingView> _verseHeadings = new List<VerseHeadingView>();
        private readonly List<RiteSlotRowView> _riteRows = new List<RiteSlotRowView>();
        private readonly List<AlmanacRowView> _almanacRows = new List<AlmanacRowView>();
        private readonly List<GearRowView> _gearRows = new List<GearRowView>();
        private readonly List<UpgradeRowView> _upgradeRows = new List<UpgradeRowView>();
        private readonly List<CraftRowView> _craftRows = new List<CraftRowView>();
        private readonly List<BuildingRowView> _buildingRows = new List<BuildingRowView>();
        private readonly HashSet<string> _availableRecipeIds = new HashSet<string>();
        private string _skillsLine = string.Empty;
        private float _sectionRefreshCountdown;
        private NodeState _selected;
        private GameState _boundState;

        private void Update()
        {
            // Built lazily (and re-built after a recompile during Play, which
            // reloads the app domain: non-serialised fields reset and Awake does
            // not re-run). Waits until GameLoop has re-initialised its state.
            if (_input == null)
            {
                if (_loop == null)
                {
                    _loop = GetComponent<GameLoop>();
                }

                if (_loop == null || _loop.State == null)
                {
                    return;
                }

                Initialise();
            }

            // A Migration swapped in a fresh run — every row holds references
            // into the old state, so rebuild the whole HUD against the new one.
            if (!ReferenceEquals(_boundState, _loop.State))
            {
                Initialise();
            }

            HandleTendInput();
            Refresh();

            // A pause→resume catch-up can queue a summary while the HUD is
            // already built — surface it the same way the load-time one shows.
            if (_welcomeSheet == null && _loop.PendingOfflineSummary != null)
            {
                ShowWelcomeBackIfEarned();
            }
        }

        private void Initialise()
        {
            _input = new InputSystemGameInput();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _world = GetComponent<WorldView>();

            EnsureEventSystem();

            // A previous app domain may have left its canvas behind — all our
            // references to it are gone, so tear it down and rebuild.
            var staleCanvas = transform.Find("HudCanvas");
            if (staleCanvas != null)
            {
                Destroy(staleCanvas.gameObject);
            }

            _rows.Clear();
            _upgradeRows.Clear();
            _craftRows.Clear();
            _buildingRows.Clear();
            _specimenRows.Clear();
            _digRows.Clear();
            _verseHeadings.Clear();
            _riteRows.Clear();
            _almanacRows.Clear();
            _gearRows.Clear();
            _museumRows.Clear();
            _welcomeSheet = null;
            _migrationSheet = null;
            _boundState = _loop.State;
            BuildUi();
            // Compute the layout now so the world-gap rect is valid this frame
            // (otherwise the node strip spends its first frame at zero size).
            Canvas.ForceUpdateCanvases();
            ShowWelcomeBackIfEarned();
        }

        /// <summary>Route the free-space Tend gesture (empty tap / Space / pad-A) to the selected node.</summary>
        private void HandleTendInput()
        {
            // While the welcome-back sheet is up, its dim layer owns the screen —
            // a non-positional confirm (Space / pad-A) shouldn't tend behind it.
            if (_welcomeSheet != null || _migrationSheet != null)
            {
                return;
            }

            if (!_input.TendTriggered(out var screenPosition))
            {
                return;
            }

            // A pointer press that landed on a widget is that widget's business
            // (its own onClick runs) — don't also tend behind it, and drop the
            // node selection (row buttons re-select their own node on click,
            // camp-wide widgets leave nothing selected).
            // NOTE: with a gamepad, pad-South is also uGUI's Submit, so tending
            // while a button is focused can double-fire — refined in the Phase 2
            // controller/focus pass.
            var overWidget = screenPosition.HasValue
                             && EventSystem.current != null
                             && EventSystem.current.IsPointerOverGameObject();
            if (overWidget)
            {
                _selected = null;
                return;
            }

            // A positional tap resolves against the node sprites: hitting one
            // selects and tends it, missing everything deselects. Only the
            // non-positional confirms (Space / pad-A) tend the current selection.
            if (screenPosition.HasValue && _world != null)
            {
                var hit = _world.NodeAtScreenPoint(screenPosition.Value);
                if (hit != null)
                {
                    _selected = hit;
                    _loop.Tend(hit);
                }
                else
                {
                    _selected = null;
                }

                return;
            }

            if (_selected != null)
            {
                _loop.Tend(_selected);
            }
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.transform;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // The root is a plain container, not a panel: the screen's middle
            // stays open (visually and for raycasts) so the world layer's node
            // strip shows through and taps there reach the world hit test.
            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            StretchFull(root.GetComponent<RectTransform>());
            var column = root.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(32, 32, 32, 32);
            column.spacing = 20f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var headerPanel = CreatePanel("Header", root.transform, PanelColor);
            // Two lines when the XP system is live: currencies, then skill levels.
            SetPreferredHeight(headerPanel, _loop.Data.economy?.xp != null ? 132f : 88f);
            _headerLabel = CreateText("HeaderLabel", headerPanel.transform, 48, TextAnchor.MiddleLeft, TextColor);
            var headerRect = _headerLabel.GetComponent<RectTransform>();
            StretchFull(headerRect);
            headerRect.offsetMin = new Vector2(24f, 0f);
            headerRect.offsetMax = new Vector2(-24f, 0f);

            // The deliberate gap between header and controls — WorldView lays
            // the node sprites out inside this rect.
            var worldGap = new GameObject("WorldGap", typeof(RectTransform), typeof(LayoutElement));
            worldGap.transform.SetParent(root.transform, false);
            worldGap.GetComponent<LayoutElement>().flexibleHeight = 1f;
            _worldStrip = worldGap.GetComponent<RectTransform>();

            var lowerPanel = CreatePanel("LowerPanel", root.transform, PanelColor);
            var lowerLayout = lowerPanel.AddComponent<VerticalLayoutGroup>();
            lowerLayout.padding = new RectOffset(24, 24, 24, 24);
            lowerLayout.spacing = 20f;
            lowerLayout.childControlWidth = true;
            lowerLayout.childControlHeight = true;
            lowerLayout.childForceExpandWidth = true;
            lowerLayout.childForceExpandHeight = false;

            // The sections (nodes, shop, crafting, camp) live in a height-capped
            // scroll view — three zones is ~10 node rows, which outgrows a
            // portrait screen as a straight column. The camp-wide actions row
            // and the hint stay pinned below it, always reachable.
            var sections = BuildScrollSection(lowerPanel.transform, canvasGo.GetComponent<RectTransform>());

            var nodesPanel = CreatePanel("Nodes", sections, new Color(0f, 0f, 0f, 0f));
            var nodesLayout = nodesPanel.AddComponent<VerticalLayoutGroup>();
            nodesLayout.spacing = 12f;
            nodesLayout.childControlWidth = true;
            nodesLayout.childControlHeight = true;
            nodesLayout.childForceExpandWidth = true;
            nodesLayout.childForceExpandHeight = false;
            _nodesPanel = nodesPanel.transform;

            // Pristine windfalls (design §5) — hidden until the first Pristine
            // batch lands; the sale stays an explicit act, apart from Sell All.
            _specimensHeader = CreateText("SpecimensHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _specimensHeader.text = "Specimens";
            SetPreferredHeight(_specimensHeader.gameObject, 48f);

            var specimensPanel = CreatePanel("Specimens", sections, new Color(0f, 0f, 0f, 0f));
            var specimensLayout = specimensPanel.AddComponent<VerticalLayoutGroup>();
            specimensLayout.spacing = 12f;
            specimensLayout.childControlWidth = true;
            specimensLayout.childControlHeight = true;
            specimensLayout.childForceExpandWidth = true;
            specimensLayout.childForceExpandHeight = false;
            _specimensPanel = specimensPanel.transform;

            RebuildNodeRows();

            _selected = _loop.State.nodes.Count > 0 ? _loop.State.nodes[0] : null;

            _upgradesHeader = CreateText("UpgradesHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _upgradesHeader.text = "Upgrades";
            SetPreferredHeight(_upgradesHeader.gameObject, 48f);

            var upgradesPanel = CreatePanel("Upgrades", sections, new Color(0f, 0f, 0f, 0f));
            var upgradesLayout = upgradesPanel.AddComponent<VerticalLayoutGroup>();
            upgradesLayout.spacing = 12f;
            upgradesLayout.childControlWidth = true;
            upgradesLayout.childControlHeight = true;
            upgradesLayout.childForceExpandWidth = true;
            upgradesLayout.childForceExpandHeight = false;

            // A row per ladder rung, in §9 order; Refresh keeps only the next
            // few unpurchased ones visible.
            foreach (var upgrade in _loop.Data.upgrades.OrderBy(u => u.order))
            {
                _upgradeRows.Add(BuildUpgradeRow(upgradesPanel.transform, upgrade));
            }

            _craftingHeader = CreateText("CraftingHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _craftingHeader.text = "Crafting";
            SetPreferredHeight(_craftingHeader.gameObject, 48f);

            var craftingPanel = CreatePanel("Crafting", sections, new Color(0f, 0f, 0f, 0f));
            var craftingLayout = craftingPanel.AddComponent<VerticalLayoutGroup>();
            craftingLayout.spacing = 12f;
            craftingLayout.childControlWidth = true;
            craftingLayout.childControlHeight = true;
            craftingLayout.childForceExpandWidth = true;
            craftingLayout.childForceExpandHeight = false;

            // A row per recipe in data order; Refresh shows only the ones the
            // run can craft (known + skill unlocked).
            foreach (var recipe in _loop.Data.recipes)
            {
                _craftRows.Add(BuildCraftRow(craftingPanel.transform, recipe));
            }

            // Dig sites (design §5) — appear as trail maps unlock them.
            _excavationHeader = CreateText("ExcavationHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _excavationHeader.text = "Excavation";
            SetPreferredHeight(_excavationHeader.gameObject, 48f);

            var excavationPanel = CreatePanel("Excavation", sections, new Color(0f, 0f, 0f, 0f));
            var excavationLayout = excavationPanel.AddComponent<VerticalLayoutGroup>();
            excavationLayout.spacing = 12f;
            excavationLayout.childControlWidth = true;
            excavationLayout.childControlHeight = true;
            excavationLayout.childForceExpandWidth = true;
            excavationLayout.childForceExpandHeight = false;
            _excavationPanel = excavationPanel.transform;

            RebuildDigRows();

            _buildingsHeader = CreateText("BuildingsHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _buildingsHeader.text = "Camp";
            SetPreferredHeight(_buildingsHeader.gameObject, 48f);

            var buildingsPanel = CreatePanel("Buildings", sections, new Color(0f, 0f, 0f, 0f));
            var buildingsLayout = buildingsPanel.AddComponent<VerticalLayoutGroup>();
            buildingsLayout.spacing = 12f;
            buildingsLayout.childControlWidth = true;
            buildingsLayout.childControlHeight = true;
            buildingsLayout.childForceExpandWidth = true;
            buildingsLayout.childForceExpandHeight = false;

            foreach (var building in _loop.Data.buildings)
            {
                _buildingRows.Add(BuildBuildingRow(buildingsPanel.transform, building));
            }

            _buildingsHeader.gameObject.SetActive(_buildingRows.Count > 0);

            // The Museum (design §5): set progress and the permanent bonuses
            // donations have banked. Hidden until specimens enter the picture.
            _museumHeader = CreateText("MuseumHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _museumHeader.text = "Museum";
            SetPreferredHeight(_museumHeader.gameObject, 48f);

            _museumPanel = CreatePanel("Museum", sections, new Color(0f, 0f, 0f, 0f));
            var museumLayout = _museumPanel.AddComponent<VerticalLayoutGroup>();
            museumLayout.spacing = 12f;
            museumLayout.childControlWidth = true;
            museumLayout.childControlHeight = true;
            museumLayout.childForceExpandWidth = true;
            museumLayout.childForceExpandHeight = false;

            foreach (var set in _loop.Data.museumSets)
            {
                var rowGo = CreatePanel("Set_" + set.id, _museumPanel.transform, RowColor);
                SetPreferredHeight(rowGo, 100f);
                var setLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
                setLayout.padding = new RectOffset(16, 16, 12, 12);
                setLayout.childControlWidth = true;
                setLayout.childControlHeight = true;
                setLayout.childForceExpandWidth = true;
                setLayout.childForceExpandHeight = true;
                var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
                info.horizontalOverflow = HorizontalWrapMode.Wrap;
                _museumRows.Add(new MuseumRowView { set = set, root = rowGo, info = info });
            }

            // The Compendium (design §5): the lifetime record — every
            // gatherable, recipe, and companion, discovered by doing. The
            // hand-drawn plates and entry text arrive with the art/narrative
            // pass; this is the field-notes rendering of the same record.
            _compendiumHeader = CreateText("CompendiumHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _compendiumHeader.text = "Compendium";
            SetPreferredHeight(_compendiumHeader.gameObject, 48f);

            _compendiumPanel = CreatePanel("Compendium", sections, RowColor);
            var compendiumLayout = _compendiumPanel.AddComponent<VerticalLayoutGroup>();
            compendiumLayout.padding = new RectOffset(16, 16, 12, 12);
            compendiumLayout.childControlWidth = true;
            compendiumLayout.childControlHeight = true;
            compendiumLayout.childForceExpandWidth = true;
            compendiumLayout.childForceExpandHeight = false;
            _compendiumText = CreateText("Entries", _compendiumPanel.transform, 28, TextAnchor.UpperLeft, TextColor);
            _compendiumText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // The warden's kit (design §4): three slots of crafted survival
            // gear. Rows show as their craft skill unlocks.
            _kitHeader = CreateText("KitHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _kitHeader.text = "The Kit";
            SetPreferredHeight(_kitHeader.gameObject, 48f);

            var kitPanel = CreatePanel("Kit", sections, new Color(0f, 0f, 0f, 0f));
            var kitLayout = kitPanel.AddComponent<VerticalLayoutGroup>();
            kitLayout.spacing = 12f;
            kitLayout.childControlWidth = true;
            kitLayout.childControlHeight = true;
            kitLayout.childForceExpandWidth = true;
            kitLayout.childForceExpandHeight = false;

            foreach (var gearItem in _loop.Data.gear)
            {
                _gearRows.Add(BuildGearRow(kitPanel.transform, gearItem));
            }

            // The Rite (design §7): each unlocked zone's verse and its
            // offering slots. Static per data — Refresh drives visibility.
            _riteHeader = CreateText("RiteHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _riteHeader.text = "The Rite";
            SetPreferredHeight(_riteHeader.gameObject, 48f);

            var ritePanel = CreatePanel("Rite", sections, new Color(0f, 0f, 0f, 0f));
            var riteLayout = ritePanel.AddComponent<VerticalLayoutGroup>();
            riteLayout.spacing = 12f;
            riteLayout.childControlWidth = true;
            riteLayout.childControlHeight = true;
            riteLayout.childForceExpandWidth = true;
            riteLayout.childForceExpandHeight = false;

            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite != null)
            {
                foreach (var verse in rite.verses)
                {
                    var heading = CreateText("Verse_" + verse.id, ritePanel.transform, 30, TextAnchor.MiddleLeft, TextColor);
                    SetPreferredHeight(heading.gameObject, 44f);
                    _verseHeadings.Add(new VerseHeadingView { verse = verse, text = heading });

                    for (var i = 0; i < verse.slots.Count; i++)
                    {
                        _riteRows.Add(BuildRiteSlotRow(ritePanel.transform, verse, i));
                    }
                }
            }

            // The gate, not the timer (design §7): visible once the Rite
            // consents; the confirm sheet makes leaving a deliberate act.
            var (migrate, _) = CreateButton("Migrate", ritePanel.transform, "Migrate — fold the camp", ShowMigrationSheet);
            SetPreferredHeight(migrate.gameObject, 96f);
            _migrateButton = migrate;
            _migrateButton.gameObject.SetActive(false);

            _riteHeader.gameObject.SetActive(rite != null);

            // The Almanac (design §7): the permanent Verdure tree. Hidden
            // until the first Migration banks any Verdure.
            _almanacHeader = CreateText("AlmanacHeader", sections, 34, TextAnchor.MiddleLeft, TextColor);
            _almanacHeader.text = "Almanac";
            SetPreferredHeight(_almanacHeader.gameObject, 48f);

            _almanacPanel = CreatePanel("Almanac", sections, new Color(0f, 0f, 0f, 0f));
            var almanacLayout = _almanacPanel.AddComponent<VerticalLayoutGroup>();
            almanacLayout.spacing = 12f;
            almanacLayout.childControlWidth = true;
            almanacLayout.childControlHeight = true;
            almanacLayout.childForceExpandWidth = true;
            almanacLayout.childForceExpandHeight = false;

            foreach (var node in _loop.Data.almanac)
            {
                _almanacRows.Add(BuildAlmanacRow(_almanacPanel.transform, node));
            }

            // Camp-wide actions side by side: the carrier gift (carriers are a
            // camp pool, not per-node — design §8) and Sell All.
            var actionsRow = CreatePanel("Actions", lowerPanel.transform, new Color(0f, 0f, 0f, 0f));
            SetPreferredHeight(actionsRow, 120f);
            var actionsLayout = actionsRow.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 12f;
            actionsLayout.childControlWidth = true;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = true;
            actionsLayout.childForceExpandHeight = true;

            var (giftCarrier, giftCarrierLabel) = CreateButton("FillFeeder", actionsRow.transform, "Gift", () => _loop.GiftCarrier());
            _carrierButton = giftCarrier;
            _carrierLabel = giftCarrierLabel;

            var (sellAll, sellAllLabel) = CreateButton("SellAll", actionsRow.transform, "Sell All", () => _loop.SellAll());
            _sellAllButton = sellAll;
            _sellAllLabel = sellAllLabel;

            // The Amber time-skip (design §10) — hidden entirely until any
            // Amber has ever been held, so the pre-dig game never shows a
            // premium-currency button.
            var (timeSkip, timeSkipLabel) = CreateButton("TimeSkip", actionsRow.transform, "Skip", () => _loop.TimeSkip());
            _timeSkipButton = timeSkip;
            _timeSkipLabel = timeSkipLabel;
            _timeSkipButton.gameObject.SetActive(false);

            var hint = CreateText("Hint", lowerPanel.transform, 28, TextAnchor.MiddleCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.6f));
            hint.text = "Tap a node, Space, or (A) to tend";
            SetPreferredHeight(hint.gameObject, 44f);
        }

        /// <summary>
        /// A vertical scroll view whose height hugs its content until the cap
        /// (a share of the canvas height, via <see cref="HeightClampedElement"/>),
        /// then scrolls. Returns the content transform sections parent to.
        /// </summary>
        private Transform BuildScrollSection(Transform parent, RectTransform canvasRect)
        {
            var sectionGo = new GameObject("Sections", typeof(RectTransform), typeof(ScrollRect), typeof(HeightClampedElement));
            sectionGo.transform.SetParent(parent, false);

            // The viewport carries a fully transparent Image so presses on the
            // empty space between rows still raycast here — that's what lets a
            // drag anywhere in the section reach the ScrollRect (and keeps the
            // tend gesture treating the area as "over a widget").
            var viewport = CreatePanel("Viewport", sectionGo.transform, new Color(0f, 0f, 0f, 0f));
            viewport.AddComponent<RectMask2D>();
            var viewportRect = viewport.GetComponent<RectTransform>();
            StretchFull(viewportRect);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var clamp = sectionGo.GetComponent<HeightClampedElement>();
            clamp.content = contentRect;
            clamp.canvas = canvasRect;

            var scroll = sectionGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            return contentGo.transform;
        }

        private RowView BuildRow(Transform parent, NodeState node)
        {
            var rowGo = CreatePanel("Row_" + node.resourceId, parent, RowColor);
            SetPreferredHeight(rowGo, 140f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 32, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            // Stats wrap inside the label's own column instead of overflowing
            // under the buttons to its right.
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            var (tend, _) = CreateButton("Tend", rowGo.transform, "Tend", () => Select(node));
            SetPreferredWidth(tend.gameObject, 120f);
            // Select-and-tend on the same tap so the row becomes the Space/pad-A target.
            tend.onClick.AddListener(() => _loop.Tend(node));

            var (gift, giftLabel) = CreateButton("Gift", rowGo.transform, "Gift",
                () => { Select(node); _loop.GiftGatherer(node); });
            SetPreferredWidth(gift.gameObject, 200f);

            var (sell, sellLabel) = CreateButton("Sell", rowGo.transform, "Sell",
                () => { Select(node); _loop.SellResource(node.resourceId); });
            SetPreferredWidth(sell.gameObject, 150f);

            return new RowView
            {
                node = node,
                background = rowGo.GetComponent<Image>(),
                info = info,
                giftButton = gift,
                giftLabel = giftLabel,
                sellButton = sell,
                sellLabel = sellLabel,
            };
        }

        private UpgradeRowView BuildUpgradeRow(Transform parent, UpgradeData upgrade)
        {
            var rowGo = CreatePanel("Upgrade_" + upgrade.id, parent, RowColor);
            SetPreferredHeight(rowGo, 120f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.text = upgrade.displayName + "\n<size=22>" + PrettyName(upgrade.track) + "</size>";

            var (buy, buyLabel) = CreateButton("Buy", rowGo.transform, "Buy", () => _loop.PurchaseUpgrade(upgrade));
            SetPreferredWidth(buy.gameObject, 240f);

            return new UpgradeRowView
            {
                upgrade = upgrade,
                root = rowGo,
                buyButton = buy,
                buyLabel = buyLabel,
            };
        }

        private CraftRowView BuildCraftRow(Transform parent, RecipeData recipe)
        {
            var rowGo = CreatePanel("Craft_" + recipe.id, parent, RowColor);
            SetPreferredHeight(rowGo, 100f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            var (toggle, toggleLabel) = CreateButton("Toggle", rowGo.transform, "Craft", () => _loop.ToggleCraft(recipe));
            SetPreferredWidth(toggle.gameObject, 160f);

            return new CraftRowView
            {
                recipe = recipe,
                root = rowGo,
                info = info,
                toggleButton = toggle,
                toggleLabel = toggleLabel,
                // Static per recipe — built once, not per frame.
                inputsLine = string.Join(" + ",
                    recipe.inputs.Select(i => i.amount + " " + PrettyName(i.id))),
            };
        }

        private BuildingRowView BuildBuildingRow(Transform parent, BuildingData building)
        {
            var rowGo = CreatePanel("Building_" + building.id, parent, RowColor);
            SetPreferredHeight(rowGo, 100f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            var (build, buildLabel) = CreateButton("Build", rowGo.transform, "Build", () => _loop.BuyBuildingLevel(building));
            SetPreferredWidth(build.gameObject, 200f);

            return new BuildingRowView
            {
                building = building,
                info = info,
                buildButton = build,
                buildLabel = buildLabel,
            };
        }

        /// <summary>What each bought level of the line grants, for the row's subtitle.</summary>
        private string PerLevelDescription(BuildingPerLevelData perLevel)
        {
            if (perLevel == null)
            {
                return string.Empty;
            }

            switch (perLevel.type)
            {
                case "stationSpeedBonus":
                    return "+" + (int)(perLevel.value * 100.0) + "% " + PrettyName(perLevel.station) + " speed / level";
                case "basketCapacityBonus":
                    return "+" + (int)(perLevel.value * 100.0) + "% basket capacity / level";
                case "familiarCaps":
                    var caps = _loop.Data.economy.familiarCaps;
                    return caps != null
                        ? "+" + caps.flockCapPerRoostLevel + " flock cap, +" + caps.carrierSlotsPerRoostLevel + " carrier slot / level"
                        : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private void Select(NodeState node)
        {
            _selected = node;
        }

        /// <summary>
        /// The purchase-and-level-driven queries, refreshed on the slow
        /// cadence: which recipes are on offer, and the header's skills line.
        /// </summary>
        private void RefreshSectionCaches()
        {
            _availableRecipeIds.Clear();
            foreach (var recipe in _loop.AvailableRecipes())
            {
                _availableRecipeIds.Add(recipe.id);
            }

            _skillsLine = string.Empty;
            if (_loop.Data.economy?.xp != null)
            {
                foreach (var skill in _loop.UnlockedSkills())
                {
                    _skillsLine += (_skillsLine.Length > 0 ? "   " : string.Empty)
                                   + PrettyName(skill) + " " + _loop.SkillLevel(skill);
                }
            }
        }

        /// <summary>
        /// (Re)create a row per gathering node — called at build, and again
        /// whenever the node list changes (a trail map unlocked a zone).
        /// </summary>
        private void RebuildNodeRows()
        {
            foreach (var row in _rows)
            {
                Destroy(row.background.gameObject);
            }

            _rows.Clear();
            foreach (var node in _loop.State.nodes)
            {
                _rows.Add(BuildRow(_nodesPanel, node));
            }

            foreach (var row in _specimenRows)
            {
                Destroy(row.root);
            }

            _specimenRows.Clear();
            var specimenResources = new HashSet<string>();
            foreach (var node in _loop.State.nodes)
            {
                // One row per resource — a resource worked from two zones
                // shares one Pristine pool.
                if (node.resourceId != null && specimenResources.Add(node.resourceId))
                {
                    _specimenRows.Add(BuildSpecimenRow(_specimensPanel, node.resourceId));
                }
            }
        }

        /// <summary>
        /// (Re)create a row per unlocked dig site — called at build, and again
        /// whenever a trail map opens a new site.
        /// </summary>
        private void RebuildDigRows()
        {
            foreach (var row in _digRows)
            {
                Destroy(row.root);
            }

            _digRows.Clear();
            foreach (var site in _loop.State.digSites)
            {
                _digRows.Add(BuildDigRow(_excavationPanel, site));
            }
        }

        private DigRowView BuildDigRow(Transform parent, DigSiteState site)
        {
            var rowGo = CreatePanel("Dig_" + site.zoneId, parent, RowColor);
            SetPreferredHeight(rowGo, 120f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            var (gift, giftLabel) = CreateButton("Gift", rowGo.transform, "Gift", () => _loop.GiftDigger(site));
            SetPreferredWidth(gift.gameObject, 200f);

            return new DigRowView
            {
                site = site,
                root = rowGo,
                info = info,
                giftButton = gift,
                giftLabel = giftLabel,
            };
        }

        private GearRowView BuildGearRow(Transform parent, GearData gear)
        {
            var rowGo = CreatePanel("Gear_" + gear.id, parent, RowColor);
            SetPreferredHeight(rowGo, 100f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            var (craft, craftLabel) = CreateButton("Craft", rowGo.transform, "Craft", () => _loop.CraftGear(gear));
            SetPreferredWidth(craft.gameObject, 200f);

            return new GearRowView
            {
                gear = gear,
                root = rowGo,
                info = info,
                craftButton = craft,
                craftLabel = craftLabel,
                // Static per piece — built once, not per frame.
                materialsLine = string.Join(" + ",
                    gear.materials.Select(m => m.amount + " " + PrettyName(m.id))),
            };
        }

        private AlmanacRowView BuildAlmanacRow(Transform parent, AlmanacNodeData node)
        {
            var rowGo = CreatePanel("Almanac_" + node.id, parent, RowColor);
            SetPreferredHeight(rowGo, 100f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            // A bond node's promise is the companion itself, not an effect line.
            var nodeBond = Bonds.BondForSource(_loop.Data, "almanacNode", node.id);
            var nodeSummary = nodeBond != null
                ? nodeBond.displayName + " crosses every Migration with you"
                : EffectSummary(node.effects);
            info.text = node.displayName + "\n<size=24>" + nodeSummary + "</size>";

            var (buy, buyLabel) = CreateButton("Buy", rowGo.transform, "Buy", () => _loop.BuyAlmanacNode(node));
            SetPreferredWidth(buy.gameObject, 200f);
            buyLabel.text = "Buy\n<size=22>" + NumberFormat.Short(new BigDouble(node.costVerdure)) + " Verdure</size>";

            return new AlmanacRowView
            {
                node = node,
                root = rowGo,
                buyButton = buy,
            };
        }

        /// <summary>What a set of effects grants, for Almanac and Kit row subtitles.</summary>
        /// <summary>
        /// The Compendium's field-notes list: one line per DISCOVERED entry
        /// (undiscovered ones stay off the page entirely — the count in the
        /// header is the only hint at what's left to find).
        /// </summary>
        private void RefreshCompendium(GameState state)
        {
            var discovered = Compendium.DiscoveredCount(state, _loop.Data);
            var visible = discovered > 0;
            _compendiumHeader.gameObject.SetActive(visible);
            _compendiumPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _compendiumHeader.text = "Compendium — " + discovered + " / "
                                     + Compendium.TotalEntries(_loop.Data) + " entries";

            var lines = new System.Text.StringBuilder();
            foreach (var resource in _loop.Data.resources)
            {
                if (!Compendium.IsResourceDiscovered(state, resource.id))
                {
                    continue;
                }

                lines.Append(PrettyName(resource.id))
                     .Append(" — ").Append(NumberFormat.Short(Compendium.LifetimeGathered(state, resource.id)))
                     .Append(" gathered");
                var pristine = Compendium.LifetimePristine(state, resource.id);
                if (pristine > BigDouble.Zero)
                {
                    lines.Append("  •  ").Append(NumberFormat.Short(pristine)).Append(" pristine");
                }

                lines.Append('\n');
            }

            foreach (var recipe in _loop.Data.recipes)
            {
                if (Compendium.IsRecipeDiscovered(state, recipe.id))
                {
                    lines.Append(PrettyName(recipe.output))
                         .Append(" — ").Append((long)Compendium.LifetimeCrafted(state, recipe.id))
                         .Append(" crafted").Append('\n');
                }
            }

            foreach (var bond in Bonds.Earned(state, _loop.Data))
            {
                lines.Append(bond.displayName).Append(" — bonded").Append('\n');
            }

            _compendiumText.text = lines.ToString().TrimEnd('\n');
            SetPreferredHeight(_compendiumText.gameObject, 10f + 36f * discovered);
        }

        private static string EffectSummary(List<EffectData> effects)
        {
            var parts = new List<string>();
            foreach (var effect in effects)
            {
                switch (effect.type)
                {
                    case EffectType.YieldBonus:
                        parts.Add("+" + (int)(effect.value * 100.0) + "% yields");
                        break;
                    case EffectType.OfflineCapHours:
                        parts.Add("offline cap " + effect.value + " h");
                        break;
                    case EffectType.OfflineCapBonusHours:
                        parts.Add("offline cap +" + effect.value + " h");
                        break;
                    case EffectType.HaulMult:
                        parts.Add("carry ×" + effect.value);
                        break;
                    case EffectType.CarrierCapacityBonus:
                        parts.Add("+" + (int)(effect.value * 100.0) + "% carry");
                        break;
                    case EffectType.CraftSpeedMult:
                        parts.Add("craft speed ×" + effect.value);
                        break;
                    case EffectType.DigSpeedMult:
                        parts.Add("dig speed ×" + effect.value);
                        break;
                    case EffectType.PristineChanceBonus:
                        parts.Add("+" + effect.value * 100.0 + "pt Pristine");
                        break;
                    case EffectType.TendingBurstBonus:
                        parts.Add("tending burst +" + (int)(effect.value * 100.0) + "%");
                        break;
                    default:
                        parts.Add(PrettyName(effect.type.ToString()));
                        break;
                }
            }

            return string.Join("  •  ", parts);
        }

        private RiteSlotRowView BuildRiteSlotRow(Transform parent, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            var rowGo = CreatePanel("Slot_" + verse.id + "_" + slotIndex, parent, RowColor);
            SetPreferredHeight(rowGo, 100f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button offer = null;
            Text offerLabel = null;
            if (slot.type != RiteSlotType.Deed)
            {
                // Deeds fill themselves as the warden acts; everything else is
                // an explicit act of offering.
                (offer, offerLabel) = CreateButton("Offer", rowGo.transform, "Offer", () =>
                {
                    switch (slot.type)
                    {
                        case RiteSlotType.Resource:
                            _loop.OfferResource(verse, slotIndex);
                            break;
                        case RiteSlotType.Specimen:
                            _loop.OfferSpecimen(verse, slotIndex);
                            break;
                        case RiteSlotType.Fragment:
                            _loop.OfferFragment(verse, slotIndex);
                            break;
                    }
                });
                SetPreferredWidth(offer.gameObject, 160f);
            }

            return new RiteSlotRowView
            {
                verse = verse,
                slotIndex = slotIndex,
                root = rowGo,
                info = info,
                offerButton = offer,
                offerLabel = offerLabel,
            };
        }

        /// <summary>The slot's ask, for its row label ("300 Berries", "Tend 25 times", "1 Pristine specimen", "1 fossil fragment").</summary>
        private static string SlotAsk(RiteSlotData slot)
        {
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    return NumberFormat.Short(new BigDouble(slot.amount)) + " " + PrettyName(slot.resource);
                case RiteSlotType.Deed:
                    return PrettyName(slot.deed) + " " + slot.count + " times";
                case RiteSlotType.Specimen:
                    return slot.count + " " + PrettyName(slot.quality) + " specimen" + (slot.count > 1 ? "s" : string.Empty);
                default:
                    return slot.count + " fossil fragment" + (slot.count > 1 ? "s" : string.Empty);
            }
        }

        private SpecimenRowView BuildSpecimenRow(Transform parent, string resourceId)
        {
            var rowGo = CreatePanel("Specimen_" + resourceId, parent, RowColor);
            SetPreferredHeight(rowGo, 100f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 12, 12);
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var info = CreateText("Info", rowGo.transform, 30, TextAnchor.MiddleLeft, TextColor);
            info.GetComponent<LayoutElement>().flexibleWidth = 1f;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            // The §5 three-way choice, two of its forks side by side: the
            // windfall sale, or permanence in the Museum. (The third — the
            // Rite — asks from its own section.)
            var (donate, _) = CreateButton("Donate", rowGo.transform, "Donate", () => _loop.DonatePristine(resourceId));
            SetPreferredWidth(donate.gameObject, 150f);

            var (sell, sellLabel) = CreateButton("Sell", rowGo.transform, "Sell", () => _loop.SellPristine(resourceId));
            SetPreferredWidth(sell.gameObject, 200f);

            return new SpecimenRowView
            {
                resourceId = resourceId,
                root = rowGo,
                info = info,
                donateButton = donate,
                sellButton = sell,
                sellLabel = sellLabel,
            };
        }

        /// <summary>
        /// The welcome-back sheet: a one-shot modal over the HUD reporting what
        /// the load-time offline catch-up credited. Only shown when the absence
        /// was long enough to be worth greeting.
        /// </summary>
        private void ShowWelcomeBackIfEarned()
        {
            var summary = _loop.TakePendingOfflineSummary();
            if (summary == null || summary.creditedSeconds < GameLoop.WelcomeBackMinSeconds || summary.gains.Count == 0)
            {
                return;
            }

            // Full-screen dim layer — its Image is a raycast target, so it also
            // swallows taps meant for the HUD underneath.
            _welcomeSheet = CreatePanel("WelcomeBack", _canvas, new Color(0f, 0f, 0f, 0.65f));
            StretchFull(_welcomeSheet.GetComponent<RectTransform>());

            var sheet = CreatePanel("Sheet", _welcomeSheet.transform, PanelColor);
            var sheetRect = sheet.GetComponent<RectTransform>();
            sheetRect.anchorMin = new Vector2(0.06f, 0.5f);
            sheetRect.anchorMax = new Vector2(0.94f, 0.5f);
            sheetRect.pivot = new Vector2(0.5f, 0.5f);
            sheetRect.offsetMin = new Vector2(0f, 0f);
            sheetRect.offsetMax = new Vector2(0f, 0f);
            var sheetLayout = sheet.AddComponent<VerticalLayoutGroup>();
            sheetLayout.padding = new RectOffset(40, 40, 36, 36);
            sheetLayout.spacing = 16f;
            sheetLayout.childControlWidth = true;
            sheetLayout.childControlHeight = true;
            sheetLayout.childForceExpandWidth = true;
            sheetLayout.childForceExpandHeight = false;
            var fitter = sheet.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = CreateText("Title", sheet.transform, 44, TextAnchor.MiddleCenter, TextColor);
            title.text = "Welcome back";
            SetPreferredHeight(title.gameObject, 64f);

            // Report the credited time, and be honest when the cap trimmed it.
            var capped = summary.creditedSeconds < summary.realSeconds - 1.0;
            var away = CreateText("Away", sheet.transform, 30, TextAnchor.MiddleCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.8f));
            away.text = capped
                ? "Away " + NumberFormat.Duration(summary.realSeconds)
                  + "  •  " + NumberFormat.Duration(summary.creditedSeconds) + " credited (offline cap)"
                : "Away " + NumberFormat.Duration(summary.realSeconds);
            SetPreferredHeight(away.gameObject, 44f);

            foreach (var gain in summary.gains.OrderByDescending(g => g.Value).Take(WelcomeBackMaxGainLines))
            {
                var line = CreateText("Gain_" + gain.Key, sheet.transform, 32, TextAnchor.MiddleCenter, TextColor);
                line.text = "+" + NumberFormat.Short(gain.Value) + "  " + PrettyName(gain.Key);
                SetPreferredHeight(line.gameObject, 44f);
            }

            var remainder = summary.gains.Count - WelcomeBackMaxGainLines;
            if (remainder > 0)
            {
                var more = CreateText("More", sheet.transform, 26, TextAnchor.MiddleCenter,
                    new Color(TextColor.r, TextColor.g, TextColor.b, 0.6f));
                more.text = "…and " + remainder + " more";
                SetPreferredHeight(more.gameObject, 38f);
            }

            var (dismiss, _) = CreateButton("Continue", sheet.transform, "Continue", DismissWelcomeBack);
            SetPreferredHeight(dismiss.gameObject, 96f);
        }

        private void DismissWelcomeBack()
        {
            if (_welcomeSheet != null)
            {
                Destroy(_welcomeSheet);
                _welcomeSheet = null;
            }
        }

        /// <summary>
        /// The Migration confirm (design §12: forecast, the vignette, and a
        /// deliberate confirm — players fear their first prestige, so sell it
        /// hard and make staying easy).
        /// </summary>
        private void ShowMigrationSheet()
        {
            if (_migrationSheet != null || !_loop.CanMigrate())
            {
                return;
            }

            _migrationSheet = CreatePanel("Migration", _canvas, new Color(0f, 0f, 0f, 0.65f));
            StretchFull(_migrationSheet.GetComponent<RectTransform>());

            var sheet = CreatePanel("Sheet", _migrationSheet.transform, PanelColor);
            var sheetRect = sheet.GetComponent<RectTransform>();
            sheetRect.anchorMin = new Vector2(0.06f, 0.5f);
            sheetRect.anchorMax = new Vector2(0.94f, 0.5f);
            sheetRect.pivot = new Vector2(0.5f, 0.5f);
            sheetRect.offsetMin = Vector2.zero;
            sheetRect.offsetMax = Vector2.zero;
            var sheetLayout = sheet.AddComponent<VerticalLayoutGroup>();
            sheetLayout.padding = new RectOffset(40, 40, 36, 36);
            sheetLayout.spacing = 16f;
            sheetLayout.childControlWidth = true;
            sheetLayout.childControlHeight = true;
            sheetLayout.childForceExpandWidth = true;
            sheetLayout.childForceExpandHeight = false;
            var fitter = sheet.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = CreateText("Title", sheet.transform, 44, TextAnchor.MiddleCenter, TextColor);
            title.text = "Migration";
            SetPreferredHeight(title.gameObject, 64f);

            // The vignette — the game's only scripted beat (design §6). MVP
            // lines may still be unauthored; show only the written ones.
            var vignette = _loop.Data.dialogue?.migrationVignette;
            if (vignette != null)
            {
                foreach (var line in vignette)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var text = CreateText("Vignette", sheet.transform, 28, TextAnchor.MiddleCenter,
                        new Color(TextColor.r, TextColor.g, TextColor.b, 0.8f));
                    text.text = line;
                    SetPreferredHeight(text.gameObject, 40f);
                }
            }

            var forecast = CreateText("Forecast", sheet.transform, 32, TextAnchor.MiddleCenter, TextColor);
            forecast.text = "Verdure " + NumberFormat.Short(new BigDouble(_loop.State.verdurePoints))
                            + "  →  " + NumberFormat.Short(new BigDouble(_loop.VerdureAfterMigration()));
            SetPreferredHeight(forecast.gameObject, 48f);

            var warning = CreateText("Warning", sheet.transform, 26, TextAnchor.MiddleCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.6f));
            warning.text = "The camp folds: Coin, familiars, tools and zones reset.\nThe Compendium, fossils and Verdure remain.";
            SetPreferredHeight(warning.gameObject, 76f);

            var (confirm, _) = CreateButton("Confirm", sheet.transform, "Migrate", () =>
            {
                DismissMigrationSheet();
                _loop.Migrate();
            });
            SetPreferredHeight(confirm.gameObject, 96f);

            var (stay, _) = CreateButton("Stay", sheet.transform, "Stay a while longer", DismissMigrationSheet);
            SetPreferredHeight(stay.gameObject, 96f);
        }

        private void DismissMigrationSheet()
        {
            if (_migrationSheet != null)
            {
                Destroy(_migrationSheet);
                _migrationSheet = null;
            }
        }

        private void Refresh()
        {
            var state = _loop.State;
            var economy = _loop.Data.economy;

            _sectionRefreshCountdown -= Time.deltaTime;
            if (_sectionRefreshCountdown <= 0f)
            {
                _sectionRefreshCountdown = SectionRefreshInterval;
                RefreshSectionCaches();
            }

            // A trail-map purchase grew the node list — mirror it in the rows.
            if (_rows.Count != state.nodes.Count)
            {
                RebuildNodeRows();
            }

            if (_digRows.Count != state.digSites.Count)
            {
                RebuildDigRows();
            }

            // Keep the world layer in step: where the free gap is on screen this
            // frame, and which node should wear the selection ring.
            if (_world != null && _worldStrip != null)
            {
                _world.StripScreenRect = ScreenRectOf(_worldStrip);
                _world.SelectedNode = _selected;
            }

            var carrierSlots = _loop.CarrierSlots();
            var bondedCarriers = Bonds.BondedCarriers(state, _loop.Data);
            _headerLabel.text = "Coin " + NumberFormat.Short(state.coin)
                                + "     Familiars " + state.TotalFamiliars()
                                + "     Carriers " + state.carrierCount
                                + (carrierSlots != int.MaxValue ? " / " + carrierSlots : string.Empty)
                                + (bondedCarriers > 0 ? " (+" + bondedCarriers + " bonded)" : string.Empty)
                                + (state.renown > BigDouble.Zero ? "     Renown " + NumberFormat.Short(state.renown) : string.Empty)
                                + (state.amber > 0.0 ? "     Amber " + (long)state.amber : string.Empty);

            // The skills readout (design §4): each unlocked skill's level.
            if (_skillsLine.Length > 0)
            {
                _headerLabel.text += "\n<size=26>" + _skillsLine + "</size>";
            }

            foreach (var row in _rows)
            {
                var node = row.node;
                var giftCost = _loop.NextGathererGiftCost(node);
                var held = state.GetResource(node.resourceId);

                // While a burst is live the row shows the effective rate — the
                // familiars' bursted yield plus the warden's hand-gather — so
                // tending visibly earns even on a node with no familiars.
                var rate = Simulation.YieldPerSecond(node, state, _loop.Data, economy);
                var tending = string.Empty;
                if (node.tendBurstRemaining > 0.0 && economy.tending != null)
                {
                    var burstMult = economy.tending.burstYieldMult * (1.0 + Upgrades.TendingBurstBonus(state, _loop.Data));
                    rate = rate * burstMult + new BigDouble(economy.tending.handGatherPerSecond);
                    tending = "  (tending)";
                }
                var basketFull = economy.hauling != null
                                 && node.basket >= new BigDouble(economy.hauling.basketCapacity
                                                                 * Buildings.BasketCapacityMultiplier(state, _loop.Data));
                var basket = "  •  " + NumberFormat.Short(node.basket)
                             + (basketFull ? " in basket (full!)" : " in basket");

                var mastery = Mastery.Configured(economy)
                    ? "  •  Mastery " + Mastery.Level(node, economy)
                    : string.Empty;
                var fine = state.GetFine(node.resourceId);
                var fineHeld = fine > BigDouble.Zero
                    ? " (+" + NumberFormat.Short(fine) + " fine)"
                    : string.Empty;
                var bondedHere = Bonds.BondedGatherersAt(state, _loop.Data, node);
                row.info.text = PrettyName(node.resourceId)
                                + "\n<size=24>" + NumberFormat.Short(held) + " held" + fineHeld + basket + "</size>"
                                + "\n<size=24>" + node.familiarCount + " familiars"
                                + (bondedHere > 0 ? " (+" + bondedHere + " bonded)" : string.Empty)
                                + "  •  " + NumberFormat.Rate(rate) + "/s" + tending + mastery + "</size>";

                // Gatherer gifts cost the node's own resource, from camp stock
                // (design §13) — so affordability is per node.
                row.giftLabel.text = "Gift\n<size=22>+1 Familiar\n" + NumberFormat.Short(giftCost)
                                     + " " + PrettyName(node.resourceId) + "</size>";
                // Affordability AND the zone's flock cap (design §8).
                row.giftButton.interactable = _loop.CanGiftGatherer(node);

                // The sale covers common and Fine stock together — Fine at the
                // design §5 quality bonus (Pristine stays in the Specimens
                // section; nothing sells it in passing).
                var unitValue = Economy.SellValuePerUnit(state, _loop.Data, node.resourceId);
                var fineMult = economy.quality != null ? economy.quality.fineValueMult : 1.0;
                var saleValue = (held + fine * fineMult) * unitValue;
                var canSell = unitValue > BigDouble.Zero && saleValue > BigDouble.Zero;
                row.sellLabel.text = canSell
                    ? "Sell\n<size=22>" + NumberFormat.Short(saleValue) + "</size>"
                    : "Sell";
                row.sellButton.interactable = canSell;

                row.background.color = node == _selected ? RowSelectedColor : RowColor;
            }

            // Pristine windfalls: a row per resource holding specimens; the
            // whole section disappears when there are none.
            var anySpecimens = false;
            foreach (var row in _specimenRows)
            {
                var pristine = state.GetPristine(row.resourceId);
                var show = pristine > BigDouble.Zero;
                row.root.SetActive(show);
                if (!show)
                {
                    continue;
                }

                anySpecimens = true;
                var pristineMult = economy.quality != null ? economy.quality.pristineValueMult : 1.0;
                var windfall = pristine * Economy.SellValuePerUnit(state, _loop.Data, row.resourceId) * pristineMult;
                row.info.text = "Pristine " + PrettyName(row.resourceId)
                                + "\n<size=24>" + NumberFormat.Short(pristine) + " held</size>";
                row.sellLabel.text = windfall > BigDouble.Zero
                    ? "Sell\n<size=22>" + NumberFormat.Short(windfall) + "</size>"
                    : "Sell";
                row.sellButton.interactable = windfall > BigDouble.Zero;
                row.donateButton.interactable = Museum.CanDonate(state, _loop.Data, row.resourceId);
            }

            _specimensHeader.gameObject.SetActive(anySpecimens);

            // The Museum: set progress, visible once specimens are in play.
            var museumVisible = anySpecimens || state.donatedResources.Count > 0;
            _museumHeader.gameObject.SetActive(museumVisible);
            _museumPanel.SetActive(museumVisible);
            if (museumVisible)
            {
                foreach (var row in _museumRows)
                {
                    var complete = Museum.IsSetComplete(state, row.set);
                    var bond = Bonds.BondForSource(_loop.Data, "museumSet", row.set.id);
                    var bondLine = bond == null
                        ? string.Empty
                        : "\n<size=24>" + bond.displayName + (complete ? " — bonded, at your side for good" : " — bonds when the set completes") + "</size>";
                    row.info.text = row.set.displayName
                                    + (complete
                                        ? "  <size=24>— complete</size>"
                                        : "  <size=24>— " + Museum.DonatedEntryCount(state, row.set)
                                          + "/" + row.set.entries.Count + " donated</size>")
                                    + "\n<size=24>" + EffectSummary(row.set.effects) + "</size>"
                                    + bondLine;
                }
            }

            RefreshCompendium(state);

            // Dig sites: who's turning soil, and how each fossil is assembling.
            foreach (var row in _digRows)
            {
                var zoneName = _loop.Data.ZonesById.TryGetValue(row.site.zoneId, out var zone)
                    ? zone.displayName
                    : PrettyName(row.site.zoneId);

                var progress = string.Empty;
                if (_loop.Data.fossils != null)
                {
                    foreach (var fossil in _loop.Data.fossils)
                    {
                        if (fossil.digSites == null || !fossil.digSites.Contains(row.site.zoneId))
                        {
                            continue;
                        }

                        var found = Fossils.FragmentCount(state, fossil.id);
                        progress += "  •  " + fossil.displayName + " "
                                    + (Fossils.IsComplete(state, fossil)
                                        ? "assembled!"
                                        : found + "/" + fossil.fragments);
                    }
                }

                row.info.text = zoneName + " dig site"
                                + "\n<size=24>" + row.site.familiarCount + " diggers" + progress + "</size>";

                var costEach = _loop.NextDiggerGiftCostEach(row.site);
                row.giftLabel.text = "Gift\n<size=22>+1 Digger\n"
                                     + NumberFormat.Short(costEach) + " of each zone find</size>";
                row.giftButton.interactable = _loop.CanGiftDigger(row.site);
            }

            _excavationHeader.gameObject.SetActive(_digRows.Count > 0);

            // The Kit: pieces appear as their craft skill unlocks; worn pieces
            // show as such and the section hides until anything is craftable.
            var anyKit = false;
            var unlockedSkills = Upgrades.UnlockedSkills(state, _loop.Data);
            foreach (var row in _gearRows)
            {
                var worn = Gear.IsEquipped(state, row.gear);
                var show = worn || string.IsNullOrEmpty(row.gear.skill) || unlockedSkills.Contains(row.gear.skill);
                row.root.SetActive(show);
                if (!show)
                {
                    continue;
                }

                anyKit = true;
                row.info.text = row.gear.displayName + "  <size=24>(" + PrettyName(row.gear.slot) + ")</size>"
                                + "\n<size=24>" + EffectSummary(row.gear.effects)
                                + (worn ? "  •  worn" : string.Empty) + "</size>";
                row.craftLabel.text = worn
                    ? "Worn"
                    : "Craft\n<size=20>" + row.materialsLine + "</size>";
                row.craftButton.interactable = !worn && Gear.CanCraft(state, _loop.Data, row.gear);
            }

            _kitHeader.gameObject.SetActive(anyKit);

            // The Rite: revealed verses show their heading and, while
            // incomplete, their offering slots. A sung verse folds down to its
            // heading; the header carries the eligibility line when all are.
            var riteComplete = Rite.IsRiteComplete(state, _loop.Data);
            _riteHeader.text = riteComplete
                ? "The Rite\n<size=24>The Rite is complete — the trail calls onward.</size>"
                : "The Rite";
            _migrateButton.gameObject.SetActive(riteComplete);

            // The Almanac: hidden until Verdure exists; bought nodes leave
            // the list (permanent — nothing to show but the effect itself).
            var almanacVisible = state.verdurePoints > 0.0 || state.almanacNodeIds.Count > 0;
            _almanacHeader.gameObject.SetActive(almanacVisible);
            _almanacPanel.SetActive(almanacVisible);
            if (almanacVisible)
            {
                _almanacHeader.text = "Almanac\n<size=24>Verdure " + NumberFormat.Short(new BigDouble(state.verdurePoints))
                                      + "  •  " + NumberFormat.Short(new BigDouble(_loop.AvailableVerdure())) + " free</size>";
                foreach (var row in _almanacRows)
                {
                    var owned = Almanac.IsOwned(state, row.node);
                    row.root.SetActive(!owned);
                    if (!owned)
                    {
                        row.buyButton.interactable = Almanac.CanBuy(state, _loop.Data, row.node);
                    }
                }
            }
            foreach (var heading in _verseHeadings)
            {
                var revealed = Rite.IsVerseRevealed(state, _loop.Data, heading.verse);
                heading.text.gameObject.SetActive(revealed);
                if (!revealed)
                {
                    continue;
                }

                var zoneName = _loop.Data.ZonesById.TryGetValue(heading.verse.zone, out var zone)
                    ? zone.displayName
                    : PrettyName(heading.verse.zone);
                heading.text.text = Rite.IsVerseComplete(state, _loop.Data, heading.verse)
                    ? "Verse of " + zoneName + " — sung"
                    : "Verse of " + zoneName + " — "
                      + Rite.CompletedSlotCount(state, heading.verse) + "/" + _loop.Data.rites.chooseCount + " offerings";
            }

            foreach (var row in _riteRows)
            {
                var show = Rite.IsVerseRevealed(state, _loop.Data, row.verse)
                           && !Rite.IsVerseComplete(state, _loop.Data, row.verse);
                row.root.SetActive(show);
                if (!show)
                {
                    continue;
                }

                var slot = row.verse.slots[row.slotIndex];
                var delivered = Rite.SlotDelivered(state, row.verse, row.slotIndex);
                var complete = Rite.IsSlotComplete(state, row.verse, row.slotIndex);
                row.info.text = SlotAsk(slot)
                                + "\n<size=24>" + (complete
                                    ? "offered"
                                    : NumberFormat.Short(new BigDouble(delivered)) + " of "
                                      + NumberFormat.Short(new BigDouble(Rite.SlotTarget(slot))) + " given") + "</size>";

                if (row.offerButton != null)
                {
                    row.offerButton.interactable = !complete && CanOfferAnything(state, slot);
                }
            }

            // Bought upgrades leave the shop; only the next few unpurchased
            // rungs of the ladder are on offer at once.
            var onOffer = 0;
            foreach (var row in _upgradeRows)
            {
                var show = !_loop.IsUpgradePurchased(row.upgrade) && onOffer < UpgradeShopWindow;
                row.root.SetActive(show);
                if (!show)
                {
                    continue;
                }

                onOffer++;
                // The §3 tool gate: a trail map stays visible (an honest
                // preview, like material costs) but can't be bought until the
                // required tool tier is owned.
                var missingTool = _loop.MissingToolTier(row.upgrade);
                row.buyLabel.text = missingTool == null
                    ? BuyLabel(row.upgrade)
                    : BuyLabel(row.upgrade) + "\n<size=20>needs " + PrettyName(missingTool) + " tools</size>";
                row.buyButton.interactable = missingTool == null && _loop.CanAffordUpgrade(row.upgrade);
            }

            _upgradesHeader.gameObject.SetActive(onOffer > 0);

            // The crafting section: recipes the run can see, plus anything a
            // station is actively working (a running recipe must always have a
            // row, or a data retune could leave an unstoppable station).
            var anyCraftable = false;
            foreach (var row in _craftRows)
            {
                var crafting = _loop.IsCrafting(row.recipe);
                var show = crafting || _availableRecipeIds.Contains(row.recipe.id);
                row.root.SetActive(show);
                if (!show)
                {
                    continue;
                }

                anyCraftable = true;

                var workable = _loop.IsRecipeWorkable(row.recipe);
                var status = string.Empty;
                if (!_loop.IsRecipeLevelMet(row.recipe))
                {
                    // A visible goal: the requirement spelled out.
                    status = "  •  needs " + PrettyName(row.recipe.skill) + " " + row.recipe.skillLevel;
                }
                else if (!workable)
                {
                    // Some other gate closed after assignment (data retune).
                    status = "  •  locked";
                }
                else if (crafting)
                {
                    var progress = _loop.CraftProgress(row.recipe);
                    status = progress > 0.0
                        ? "  •  crafting " + (int)(progress * 100.0) + "%"
                        : "  •  waiting for goods";
                }

                row.info.text = PrettyName(row.recipe.id)
                                + "\n<size=22>" + row.inputsLine + status + "</size>";
                row.toggleLabel.text = crafting ? "Stop" : "Craft";
                // Stop must always be reachable — only starting is gated.
                row.toggleButton.interactable = crafting || workable;
            }

            _craftingHeader.gameObject.SetActive(anyCraftable);

            // The camp building lines — the always-available Coin sink.
            foreach (var row in _buildingRows)
            {
                var level = _loop.BuildingLevel(row.building);
                var cost = _loop.NextBuildingCost(row.building);
                row.info.text = row.building.displayName
                                + "\n<size=22>Level " + level + "  •  " + PerLevelDescription(row.building.perLevel) + "</size>";
                row.buildLabel.text = "Build\n<size=22>" + NumberFormat.Short(cost) + "</size>";
                row.buildButton.interactable = state.coin >= cost;
            }

            // The Feeder: a bundle of every worked resource buys a carrier.
            var carrierCostEach = _loop.NextCarrierGiftCostEach();
            _carrierLabel.text = "Gift\n<size=22>+1 Carrier\nFeeder: " + NumberFormat.Short(carrierCostEach) + " of each</size>";
            _carrierButton.interactable = _loop.CanGiftCarrier();

            var totalSaleValue = TotalSellableValue(state);
            _sellAllLabel.text = totalSaleValue > BigDouble.Zero
                ? "Sell All  (" + NumberFormat.Short(totalSaleValue) + ")"
                : "Sell All";
            _sellAllButton.interactable = totalSaleValue > BigDouble.Zero;

            var amberEconomy = economy.amber;
            var showTimeSkip = Amber.Configured(economy) && state.amber > 0.0;
            _timeSkipButton.gameObject.SetActive(showTimeSkip);
            if (showTimeSkip)
            {
                _timeSkipLabel.text = "Skip " + amberEconomy.timeSkipHours + "h\n<size=22>"
                                      + amberEconomy.timeSkipCostAmber + " Amber</size>";
                _timeSkipButton.interactable = _loop.CanTimeSkip();
            }
        }

        /// <summary>
        /// The buy button's text: Coin cost, plus a line per crafted material
        /// the rung asks for (spent by <c>Upgrades.TryPurchase</c>).
        /// </summary>
        private static string BuyLabel(UpgradeData upgrade)
        {
            var label = "Buy\n<size=22>" + NumberFormat.Short(upgrade.costCoin) + "</size>";
            foreach (var material in upgrade.materials)
            {
                label += "\n<size=20>" + material.amount + " " + PrettyName(material.id) + "</size>";
            }

            return label;
        }

        private BigDouble TotalSellableValue(GameState state)
        {
            var total = BigDouble.Zero;
            foreach (var pair in state.resources)
            {
                var unitValue = Economy.SellValuePerUnit(state, _loop.Data, pair.Key);
                if (unitValue > BigDouble.Zero)
                {
                    total += pair.Value * unitValue;
                }
            }

            // Fine stock sells with Sell All (at its bonus); Pristine never does.
            var fineMult = _loop.Data.economy?.quality != null ? _loop.Data.economy.quality.fineValueMult : 1.0;
            foreach (var pair in state.fineResources)
            {
                var unitValue = Economy.SellValuePerUnit(state, _loop.Data, pair.Key);
                if (unitValue > BigDouble.Zero)
                {
                    total += pair.Value * unitValue * fineMult;
                }
            }

            return total;
        }

        // ---- uGUI construction helpers -------------------------------------

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private (Button, Text) CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = AccentColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = AccentColor;
            colors.highlightedColor = new Color(AccentColor.r + 0.08f, AccentColor.g + 0.08f, AccentColor.b + 0.08f, 1f);
            colors.pressedColor = new Color(AccentColor.r - 0.08f, AccentColor.g - 0.08f, AccentColor.b - 0.08f, 1f);
            colors.disabledColor = new Color(0.25f, 0.28f, 0.25f, 0.7f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var text = CreateText("Label", go.transform, 30, TextAnchor.MiddleCenter, TextColor);
            StretchFull(text.GetComponent<RectTransform>());
            text.text = label;

            return (button, text);
        }

        private static Rect ScreenRectOf(RectTransform rect)
        {
            // On a ScreenSpaceOverlay canvas, world corners are screen pixels.
            rect.GetWorldCorners(CornerBuffer);
            return new Rect(CornerBuffer[0].x, CornerBuffer[0].y,
                CornerBuffer[2].x - CornerBuffer[0].x, CornerBuffer[2].y - CornerBuffer[0].y);
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetPreferredHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void SetPreferredWidth(GameObject go, float width)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            element.preferredWidth = width;
            element.minWidth = width;
        }

        // Memoised — PrettyName runs many times per frame over a small, fixed
        // set of content ids, and the split/join otherwise allocates each call.
        private static readonly Dictionary<string, string> PrettyNameCache = new Dictionary<string, string>();

        private static string PrettyName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            if (PrettyNameCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var words = id.Replace('-', ' ').Replace('_', ' ').Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0], CultureInfo.InvariantCulture) + words[i].Substring(1);
                }
            }

            var pretty = string.Join(" ", words);
            PrettyNameCache[id] = pretty;
            return pretty;
        }

        private sealed class RowView
        {
            public NodeState node;
            public Image background;
            public Text info;
            public Button giftButton;
            public Text giftLabel;
            public Button sellButton;
            public Text sellLabel;
        }

        /// <summary>True when the camp holds anything the slot would take — the Offer button's enabled state.</summary>
        private bool CanOfferAnything(GameState state, RiteSlotData slot)
        {
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    return state.GetResource(slot.resource) > BigDouble.Zero;

                case RiteSlotType.Specimen:
                    var pool = slot.quality == "pristine" ? state.pristineResources : state.fineResources;
                    foreach (var pair in pool)
                    {
                        if (pair.Value >= BigDouble.One)
                        {
                            return true;
                        }
                    }

                    return false;

                case RiteSlotType.Fragment:
                    if (_loop.Data.fossils != null)
                    {
                        foreach (var fossil in _loop.Data.fossils)
                        {
                            if (Fossils.FragmentCount(state, fossil.id) > 0 && !Fossils.IsComplete(state, fossil))
                            {
                                return true;
                            }
                        }
                    }

                    return false;

                default:
                    return false;
            }
        }

        private sealed class DigRowView
        {
            public DigSiteState site;
            public GameObject root;
            public Text info;
            public Button giftButton;
            public Text giftLabel;
        }

        private sealed class SpecimenRowView
        {
            public string resourceId;
            public GameObject root;
            public Text info;
            public Button donateButton;
            public Button sellButton;
            public Text sellLabel;
        }

        private sealed class MuseumRowView
        {
            public MuseumSetData set;
            public GameObject root;
            public Text info;
        }

        private sealed class AlmanacRowView
        {
            public AlmanacNodeData node;
            public GameObject root;
            public Button buyButton;
        }

        private sealed class GearRowView
        {
            public GearData gear;
            public GameObject root;
            public Text info;
            public Button craftButton;
            public Text craftLabel;
            public string materialsLine;
        }

        private sealed class VerseHeadingView
        {
            public RiteVerseData verse;
            public Text text;
        }

        private sealed class RiteSlotRowView
        {
            public RiteVerseData verse;
            public int slotIndex;
            public GameObject root;
            public Text info;
            public Button offerButton;
            public Text offerLabel;
        }

        private sealed class UpgradeRowView
        {
            public UpgradeData upgrade;
            public GameObject root;
            public Button buyButton;
            public Text buyLabel;
        }

        private sealed class CraftRowView
        {
            public RecipeData recipe;
            public GameObject root;
            public Text info;
            public Button toggleButton;
            public Text toggleLabel;
            public string inputsLine;
        }

        private sealed class BuildingRowView
        {
            public BuildingData building;
            public Text info;
            public Button buildButton;
            public Text buildLabel;
        }
    }
}
