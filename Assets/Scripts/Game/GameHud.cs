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
        private Text _carrierLabel;
        private Button _carrierButton;
        private Transform _canvas;
        private Transform _nodesPanel;
        private GameObject _welcomeSheet;
        private WorldView _world;
        private RectTransform _worldStrip;
        private static readonly Vector3[] CornerBuffer = new Vector3[4];
        private readonly List<RowView> _rows = new List<RowView>();
        private readonly List<UpgradeRowView> _upgradeRows = new List<UpgradeRowView>();
        private readonly List<CraftRowView> _craftRows = new List<CraftRowView>();
        private readonly List<BuildingRowView> _buildingRows = new List<BuildingRowView>();
        private readonly HashSet<string> _availableRecipeIds = new HashSet<string>();
        private string _skillsLine = string.Empty;
        private float _sectionRefreshCountdown;
        private NodeState _selected;

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
            if (_welcomeSheet != null)
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

            // Keep the world layer in step: where the free gap is on screen this
            // frame, and which node should wear the selection ring.
            if (_world != null && _worldStrip != null)
            {
                _world.StripScreenRect = ScreenRectOf(_worldStrip);
                _world.SelectedNode = _selected;
            }

            var carrierSlots = _loop.CarrierSlots();
            _headerLabel.text = "Coin " + NumberFormat.Short(state.coin)
                                + "     Familiars " + state.TotalFamiliars()
                                + "     Carriers " + state.carrierCount
                                + (carrierSlots != int.MaxValue ? " / " + carrierSlots : string.Empty);

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
                var rate = Simulation.YieldPerSecond(node, state, economy);
                var tending = string.Empty;
                if (node.tendBurstRemaining > 0.0 && economy.tending != null)
                {
                    rate = rate * economy.tending.burstYieldMult + new BigDouble(economy.tending.handGatherPerSecond);
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
                row.info.text = PrettyName(node.resourceId)
                                + "\n<size=24>" + NumberFormat.Short(held) + " held" + basket + "</size>"
                                + "\n<size=24>" + node.familiarCount + " familiars"
                                + "  •  " + NumberFormat.Rate(rate) + "/s" + tending + mastery + "</size>";

                // Gatherer gifts cost the node's own resource, from camp stock
                // (design §13) — so affordability is per node.
                row.giftLabel.text = "Gift\n<size=22>+1 Familiar\n" + NumberFormat.Short(giftCost)
                                     + " " + PrettyName(node.resourceId) + "</size>";
                // Affordability AND the zone's flock cap (design §8).
                row.giftButton.interactable = _loop.CanGiftGatherer(node);

                var unitValue = Economy.SellValuePerUnit(state, _loop.Data, node.resourceId);
                var saleValue = held * unitValue;
                var canSell = unitValue > BigDouble.Zero && held > BigDouble.Zero;
                row.sellLabel.text = canSell
                    ? "Sell\n<size=22>" + NumberFormat.Short(saleValue) + "</size>"
                    : "Sell";
                row.sellButton.interactable = canSell;

                row.background.color = node == _selected ? RowSelectedColor : RowColor;
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
                row.buyLabel.text = BuyLabel(row.upgrade);
                row.buyButton.interactable = _loop.CanAffordUpgrade(row.upgrade);
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
