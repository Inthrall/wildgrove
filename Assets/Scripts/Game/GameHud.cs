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

        // The Sunfield-reachable upgrades (design §12 Phase 1). A hardcoded
        // whitelist until the full §9 ladder gets real content gating (zones,
        // stations, materials) in Phase 3 — see docs/todo.md.
        private static readonly HashSet<string> PhaseOneUpgradeIds = new HashSet<string>
        {
            "flint-sickle", "waxed-satchel", "drying-rack", "handcart", "root-cellar",
        };

        // Below this much credited absence the welcome-back sheet stays quiet —
        // quick restarts and editor recompiles shouldn't greet the player.
        private const double WelcomeBackMinSeconds = 60.0;
        private const int WelcomeBackMaxGainLines = 6;

        private Text _headerLabel;
        private Text _upgradesHeader;
        private Text _sellAllLabel;
        private Button _sellAllButton;
        private Transform _canvas;
        private GameObject _welcomeSheet;
        private readonly List<RowView> _rows = new List<RowView>();
        private readonly List<UpgradeRowView> _upgradeRows = new List<UpgradeRowView>();
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
        }

        private void Initialise()
        {
            _input = new InputSystemGameInput();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            BuildUi();
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
            // (its own onClick runs) — don't also tend behind it. Non-positional
            // confirms (Space / pad-A) and taps on empty space tend the selection.
            // NOTE: with a gamepad, pad-South is also uGUI's Submit, so tending
            // while a button is focused can double-fire — refined in the Phase 2
            // controller/focus pass.
            var overWidget = screenPosition.HasValue
                             && EventSystem.current != null
                             && EventSystem.current.IsPointerOverGameObject();
            if (overWidget)
            {
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

            var root = CreatePanel("Root", canvasGo.transform, PanelColor);
            StretchFull(root.GetComponent<RectTransform>());
            var column = root.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(32, 32, 32, 32);
            column.spacing = 20f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            _headerLabel = CreateText("Header", root.transform, 48, TextAnchor.MiddleLeft, TextColor);
            SetPreferredHeight(_headerLabel.gameObject, 72f);

            var nodesPanel = CreatePanel("Nodes", root.transform, new Color(0f, 0f, 0f, 0f));
            var nodesLayout = nodesPanel.AddComponent<VerticalLayoutGroup>();
            nodesLayout.spacing = 12f;
            nodesLayout.childControlWidth = true;
            nodesLayout.childControlHeight = true;
            nodesLayout.childForceExpandWidth = true;
            nodesLayout.childForceExpandHeight = false;
            var nodesFlex = nodesPanel.AddComponent<LayoutElement>();
            nodesFlex.flexibleHeight = 1f;

            foreach (var node in _loop.State.nodes)
            {
                _rows.Add(BuildRow(nodesPanel.transform, node));
            }

            _selected = _loop.State.nodes.Count > 0 ? _loop.State.nodes[0] : null;

            _upgradesHeader = CreateText("UpgradesHeader", root.transform, 34, TextAnchor.MiddleLeft, TextColor);
            _upgradesHeader.text = "Upgrades";
            SetPreferredHeight(_upgradesHeader.gameObject, 48f);

            var upgradesPanel = CreatePanel("Upgrades", root.transform, new Color(0f, 0f, 0f, 0f));
            var upgradesLayout = upgradesPanel.AddComponent<VerticalLayoutGroup>();
            upgradesLayout.spacing = 12f;
            upgradesLayout.childControlWidth = true;
            upgradesLayout.childControlHeight = true;
            upgradesLayout.childForceExpandWidth = true;
            upgradesLayout.childForceExpandHeight = false;

            foreach (var upgrade in _loop.Data.upgrades)
            {
                if (PhaseOneUpgradeIds.Contains(upgrade.id))
                {
                    _upgradeRows.Add(BuildUpgradeRow(upgradesPanel.transform, upgrade));
                }
            }

            var (sellAll, sellAllLabel) = CreateButton("SellAll", root.transform, "Sell All", () => _loop.SellAll());
            _sellAllButton = sellAll;
            _sellAllLabel = sellAllLabel;
            SetPreferredHeight(sellAll.gameObject, 96f);

            var hint = CreateText("Hint", root.transform, 28, TextAnchor.MiddleCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.6f));
            hint.text = "Tap a node, Space, or (A) to tend";
            SetPreferredHeight(hint.gameObject, 44f);
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

            var (tend, _) = CreateButton("Tend", rowGo.transform, "Tend", () => Select(node));
            SetPreferredWidth(tend.gameObject, 150f);
            // Select-and-tend on the same tap so the row becomes the Space/pad-A target.
            tend.onClick.AddListener(() => _loop.Tend(node));

            var (gift, giftLabel) = CreateButton("Gift", rowGo.transform, "+ Familiar", () => _loop.GiftFamiliar(node));
            SetPreferredWidth(gift.gameObject, 200f);

            var (sell, sellLabel) = CreateButton("Sell", rowGo.transform, "Sell", () => _loop.SellResource(node.resourceId));
            SetPreferredWidth(sell.gameObject, 200f);

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
            info.text = upgrade.displayName + "\n<size=22>" + PrettyName(upgrade.track) + "</size>";

            var (buy, buyLabel) = CreateButton("Buy", rowGo.transform, "Buy", () => _loop.PurchaseUpgrade(upgrade));
            SetPreferredWidth(buy.gameObject, 200f);

            return new UpgradeRowView
            {
                upgrade = upgrade,
                root = rowGo,
                buyButton = buy,
                buyLabel = buyLabel,
            };
        }

        private void Select(NodeState node)
        {
            _selected = node;
        }

        /// <summary>
        /// The welcome-back sheet: a one-shot modal over the HUD reporting what
        /// the load-time offline catch-up credited. Only shown when the absence
        /// was long enough to be worth greeting.
        /// </summary>
        private void ShowWelcomeBackIfEarned()
        {
            var summary = _loop.TakePendingOfflineSummary();
            if (summary == null || summary.creditedSeconds < WelcomeBackMinSeconds || summary.gains.Count == 0)
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

            _headerLabel.text = "Coin " + NumberFormat.Short(state.coin) + "     Familiars " + state.TotalFamiliars();

            var giftCost = _loop.NextFamiliarGiftCost();
            var canAffordGift = state.coin >= giftCost;

            foreach (var row in _rows)
            {
                var node = row.node;
                var held = state.GetResource(node.resourceId);
                var rate = Simulation.YieldPerSecond(node, state, economy);
                var tending = node.tendBurstRemaining > 0.0 ? "  (tending)" : string.Empty;

                row.info.text = PrettyName(node.resourceId)
                                + "\n<size=24>" + NumberFormat.Short(held) + " held"
                                + "  •  " + node.familiarCount + " familiars"
                                + "  •  " + NumberFormat.Short(rate) + "/s" + tending + "</size>";

                row.giftLabel.text = "+ Familiar\n<size=22>" + NumberFormat.Short(giftCost) + "</size>";
                row.giftButton.interactable = canAffordGift;

                var unitValue = Economy.SellValuePerUnit(state, _loop.Data, node.resourceId);
                var saleValue = held * unitValue;
                var canSell = unitValue > BigDouble.Zero && held > BigDouble.Zero;
                row.sellLabel.text = canSell
                    ? "Sell\n<size=22>" + NumberFormat.Short(saleValue) + "</size>"
                    : "Sell";
                row.sellButton.interactable = canSell;

                row.background.color = node == _selected ? RowSelectedColor : RowColor;
            }

            // Bought upgrades leave the shop; the header goes with the last one.
            var anyUpgradeOnOffer = false;
            foreach (var row in _upgradeRows)
            {
                var purchased = _loop.IsUpgradePurchased(row.upgrade);
                row.root.SetActive(!purchased);
                if (purchased)
                {
                    continue;
                }

                anyUpgradeOnOffer = true;
                row.buyLabel.text = "Buy\n<size=22>" + NumberFormat.Short(row.upgrade.costCoin) + "</size>";
                row.buyButton.interactable = _loop.CanAffordUpgrade(row.upgrade);
            }

            _upgradesHeader.gameObject.SetActive(anyUpgradeOnOffer);

            var totalSaleValue = TotalSellableValue(state);
            _sellAllLabel.text = totalSaleValue > BigDouble.Zero
                ? "Sell All  (" + NumberFormat.Short(totalSaleValue) + ")"
                : "Sell All";
            _sellAllButton.interactable = totalSaleValue > BigDouble.Zero;
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

        private static string PrettyName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            var words = id.Replace('-', ' ').Replace('_', ' ').Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0], CultureInfo.InvariantCulture) + words[i].Substring(1);
                }
            }

            return string.Join(" ", words);
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
    }
}
