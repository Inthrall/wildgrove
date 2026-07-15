using System.Collections.Generic;
using System.Globalization;
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
    /// loop (coin/crew readouts, a row per gathering node, and the hire / sell /
    /// tend actions) and routes the free-space Tend gesture through
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

        private Text _headerLabel;
        private Text _sellAllLabel;
        private Button _sellAllButton;
        private readonly List<RowView> _rows = new List<RowView>();
        private NodeState _selected;

        private void Awake()
        {
            _loop = GetComponent<GameLoop>();
            _input = new InputSystemGameInput();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureEventSystem();
            BuildUi();
        }

        private void Update()
        {
            HandleTendInput();
            Refresh();
        }

        /// <summary>Route the free-space Tend gesture (empty tap / Space / pad-A) to the selected node.</summary>
        private void HandleTendInput()
        {
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

            var (crew, crewLabel) = CreateButton("Crew", rowGo.transform, "+ Crew", () => _loop.HireCrew(node));
            SetPreferredWidth(crew.gameObject, 200f);

            var (sell, sellLabel) = CreateButton("Sell", rowGo.transform, "Sell", () => _loop.SellResource(node.resourceId));
            SetPreferredWidth(sell.gameObject, 200f);

            return new RowView
            {
                node = node,
                background = rowGo.GetComponent<Image>(),
                info = info,
                crewButton = crew,
                crewLabel = crewLabel,
                sellButton = sell,
                sellLabel = sellLabel,
            };
        }

        private void Select(NodeState node)
        {
            _selected = node;
        }

        private void Refresh()
        {
            var state = _loop.State;
            var economy = _loop.Data.economy;

            _headerLabel.text = "Coin " + NumberFormat.Short(state.coin) + "     Crew " + state.TotalCrew();

            var hireCost = _loop.NextCrewHireCost();
            var canAffordHire = state.coin >= hireCost;

            foreach (var row in _rows)
            {
                var node = row.node;
                var held = state.GetResource(node.resourceId);
                var rate = Simulation.YieldPerSecond(node, state, economy);
                var tending = node.tendBurstRemaining > 0.0 ? "  (tending)" : string.Empty;

                row.info.text = PrettyName(node.resourceId)
                                + "\n<size=24>" + NumberFormat.Short(held) + " held"
                                + "  •  " + node.crewCount + " crew"
                                + "  •  " + NumberFormat.Short(rate) + "/s" + tending + "</size>";

                row.crewLabel.text = "+ Crew\n<size=22>" + NumberFormat.Short(hireCost) + "</size>";
                row.crewButton.interactable = canAffordHire;

                var unitValue = Economy.SellValuePerUnit(_loop.Data, node.resourceId);
                var saleValue = held * unitValue;
                var canSell = unitValue > BigDouble.Zero && held > BigDouble.Zero;
                row.sellLabel.text = canSell
                    ? "Sell\n<size=22>" + NumberFormat.Short(saleValue) + "</size>"
                    : "Sell";
                row.sellButton.interactable = canSell;

                row.background.color = node == _selected ? RowSelectedColor : RowColor;
            }

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
                var unitValue = Economy.SellValuePerUnit(_loop.Data, pair.Key);
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
            public Button crewButton;
            public Text crewLabel;
            public Button sellButton;
            public Text sellLabel;
        }
    }
}
