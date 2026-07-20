using System;
using System.Collections.Generic;
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
    /// The on-screen layer for the v0.11 model: a code-built uGUI HUD surfacing
    /// the core loop — the crew (name / station / powerups), the gathering nodes
    /// (tend), the Exchange (barter), and the upgrade / craft / build / Rite /
    /// Almanac / kit sections — plus the naming, powerup, welcome-back, migration
    /// and bond sheets. Deliberately programmer-art (real layout is Phase 2);
    /// all game logic stays in Wildgrove.Sim, this only reads state and calls
    /// <see cref="GameLoop"/> actions. Rebuilds the body only when its structure
    /// changes; live numbers refresh through <see cref="_liveUpdaters"/>.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.12f, 0.16f, 0.13f, 0.92f);
        private static readonly Color SectionColor = new Color(0.09f, 0.13f, 0.10f, 0.95f);
        private static readonly Color RowColor = new Color(0.18f, 0.23f, 0.19f, 0.90f);
        private static readonly Color RowSelectedColor = new Color(0.24f, 0.34f, 0.24f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.24f, 0.34f, 0.24f, 1f);
        private static readonly Color ButtonOffColor = new Color(0.16f, 0.19f, 0.16f, 1f);
        private static readonly Color SheetColor = new Color(0.10f, 0.14f, 0.11f, 0.98f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.90f, 1f);
        private static readonly Color FieldColor = new Color(0.06f, 0.09f, 0.07f, 1f);

        private const int UpgradeWindow = 3;
        private const float RefreshInterval = 0.25f;

        private GameLoop _loop;
        private IGameInput _input;
        private Font _font;
        private WorldView _world;

        private Text _header;
        private RectTransform _worldGap;
        private RectTransform _body;
        private Transform _modalLayer;
        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly List<Action> _liveUpdaters = new List<Action>();
        private float _refreshCountdown;
        private string _structureSignature;
        private bool _dirty;

        private NodeState _selected;
        private string _exchangeFrom;
        private string _exchangeTo;

        // One open sheet at a time; the dim layer blocks input beneath it.
        private GameObject _sheet;
        private Familiar _namingFamiliar;

        private void Awake()
        {
            _loop = GetComponent<GameLoop>();
            _input = new InputSystemGameInput();
            _world = GetComponent<WorldView>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildChrome();
        }

        private void Update()
        {
            if (_loop == null || _loop.State == null)
            {
                return;
            }

            RefreshHeader();
            ReportWorldStrip();
            HandleTend();
            PumpSheets();

            _refreshCountdown -= Time.deltaTime;
            if (_refreshCountdown <= 0f)
            {
                _refreshCountdown = RefreshInterval;
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
        /// Store-screenshot seam kept for <see cref="StoreCaptureRunner"/>. The
        /// v0.11 HUD is a single flat scroll with no tabs, so this is a no-op;
        /// the rig captures the one view. (Restore per-page capture if tabs
        /// return — see docs/todo.md.)
        /// </summary>
        public void OpenTab(string tab)
        {
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
            rootLayout.padding = new RectOffset(12, 12, 12, 12);
            rootLayout.spacing = 8;

            // Header.
            var headerGo = MakePanel("Header", root, PanelColor);
            FixedHeight(headerGo, 130);
            _header = MakeText(headerGo.transform, string.Empty, 26, TextAnchor.UpperLeft);
            Stretch((RectTransform)_header.transform);

            // World gap — the WorldView strip draws here; flexible so it takes
            // the slack above the scrolling body.
            var gap = MakeRect("WorldGap", root);
            _worldGap = gap;
            Flexible(gap.gameObject, 1f);

            // Body — a scroll view of all the sections.
            _body = BuildScroll(root);

            // Tend hint.
            var hintGo = MakePanel("Hint", root, PanelColor);
            FixedHeight(hintGo, 46);
            var hint = MakeText(hintGo.transform, "Tap a node to tend it · space / (A) tends the selected node", 20, TextAnchor.MiddleCenter);
            Stretch((RectTransform)hint.transform);

            // Modal layer, above everything, initially empty.
            var modalGo = MakeRect("Modals", canvasGo.transform);
            Stretch(modalGo);
            _modalLayer = modalGo;

            Canvas.ForceUpdateCanvases();
        }

        private RectTransform BuildScroll(RectTransform parent)
        {
            var scrollGo = new GameObject("Body", typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
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
            layout.spacing = 6;
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

        // ─────────────────────────── Header & world ──────────────────────────

        private void RefreshHeader()
        {
            var state = _loop.State;
            var line = "Renown " + NumberFormat.Short(state.renown)
                       + "     Verdure " + Mathf.FloorToInt((float)state.verdurePoints);
            if (state.amber > 0.0)
            {
                line += "     Amber " + Mathf.FloorToInt((float)state.amber);
            }

            line += "\n<size=20>Crew " + state.roster.Count;
            foreach (var skill in _loop.UnlockedSkills())
            {
                line += "   " + skill + " " + _loop.SkillLevel(skill);
            }

            line += "</size>";
            _header.text = line;
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
                        _loop.Tend(node);
                    }
                    else if (screenPosition.Value.y > Screen.height * 0.45f)
                    {
                        // A tap in the world band that hit no node deselects.
                        _selected = null;
                    }
                }
                else if (_selected != null)
                {
                    _loop.Tend(_selected);
                }
            }
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

            return state.roster.Count + "/" + state.nodes.Count + "/" + state.digSites.Count
                   + "/" + owned + "/" + recipes + "/" + buildings + "/" + pristine
                   + "/" + _loop.UnlockedSkills().Count;
        }

        private void RebuildBody()
        {
            _liveUpdaters.Clear();
            for (var i = _body.childCount - 1; i >= 0; i--)
            {
                Destroy(_body.GetChild(i).gameObject);
            }

            BuildCrewSection();
            BuildNodesSection();
            BuildExchangeSection();
            BuildUpgradesSection();
            BuildCraftingSection();
            BuildBuildingsSection();
            BuildRiteSection();
            BuildAlmanacSection();
            BuildKitSection();
            BuildSpecimensSection();
        }

        private void BuildCrewSection()
        {
            Section("The Crew");
            foreach (var familiar in _loop.State.roster)
            {
                var captured = familiar;
                var row = Row();
                var label = MakeText(row.transform, string.Empty, 22, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);

                var stationButton = Button(row.transform, "…", 150, () =>
                {
                    _loop.StationFamiliar(captured, NextStation(captured.stationId));
                    _dirty = true;
                });

                var renameButton = Button(row.transform, "✎", 60, () => OpenNamingSheet(captured));

                var powerupButton = Button(row.transform, "Powerup", 150, () => OpenPowerupSheet(captured));

                _liveUpdaters.Add(() =>
                {
                    var species = SpeciesName(captured.speciesId);
                    var bonded = captured.bonded ? " ★" : string.Empty;
                    label.text = captured.name + bonded + "  <size=18>" + species
                                 + " · Lv " + _loop.FamiliarLevel(captured)
                                 + (_loop.FamiliarKinship(captured) > 0 ? " · Kin " + _loop.FamiliarKinship(captured) : string.Empty)
                                 + "\n@ " + StationLabel(captured.stationId) + "</size>";
                    SetButtonLabel(stationButton, "Move");
                    var pending = _loop.HasPendingPowerup(captured);
                    powerupButton.gameObject.SetActive(pending);
                });
            }
        }

        private void BuildNodesSection()
        {
            Section("Nodes");
            foreach (var node in _loop.State.nodes)
            {
                var captured = node;
                var row = Row();
                var rowImage = row.GetComponent<Image>();
                var label = MakeText(row.transform, string.Empty, 22, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);

                var tend = Button(row.transform, "Tend", 150, () =>
                {
                    _selected = captured;
                    _loop.Tend(captured);
                });

                _liveUpdaters.Add(() =>
                {
                    var rate = Simulation.YieldPerSecond(captured, _loop.State, _loop.Data, _loop.Data.economy);
                    var here = Stationing.CountAssignedTo(_loop.State, captured.id);
                    var stock = _loop.State.GetResource(captured.resourceId);
                    label.text = captured.resourceId + "  <size=18>" + NumberFormat.Rate(rate) + "/s · "
                                 + here + " here · " + NumberFormat.Short(stock) + " at camp</size>";
                    rowImage.color = captured == _selected ? RowSelectedColor : RowColor;
                });
            }
        }

        private void BuildExchangeSection()
        {
            Section("The Exchange");
            var tradeable = TradeableResources();
            if (tradeable.Count < 2)
            {
                MakeText(Row().transform, "Gather more before the caravan will barter.", 18, TextAnchor.MiddleLeft);
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

            var row = Row();
            var fromButton = Button(row.transform, "from", 220, () =>
            {
                _exchangeFrom = Cycle(TradeableResources(), _exchangeFrom);
                _dirty = true;
            });
            var toButton = Button(row.transform, "to", 220, () =>
            {
                _exchangeTo = Cycle(TradeableResources(), _exchangeTo);
                _dirty = true;
            });

            var quote = MakeText(Row().transform, string.Empty, 18, TextAnchor.MiddleLeft);
            Flexible(quote.gameObject, 1f);

            var trade = Button(Row().transform, "Trade all", 240, () =>
            {
                var have = _loop.State.GetResource(_exchangeFrom);
                _loop.TradeAtExchange(_exchangeFrom, _exchangeTo, have);
                _dirty = true;
            });

            _liveUpdaters.Add(() =>
            {
                SetButtonLabel(fromButton, "Give: " + _exchangeFrom);
                SetButtonLabel(toButton, "Get: " + _exchangeTo);
                var have = _loop.State.GetResource(_exchangeFrom);
                var got = _loop.ExchangeQuote(_exchangeFrom, _exchangeTo, have);
                quote.text = NumberFormat.Short(have) + " " + _exchangeFrom + "  →  "
                             + NumberFormat.Short(got) + " " + _exchangeTo;
                trade.interactable = got > BigDouble.Zero;
                SetButtonTint(trade, got > BigDouble.Zero);
            });
        }

        private void BuildUpgradesSection()
        {
            Section("Upgrades");
            var shown = 0;
            foreach (var upgrade in _loop.Data.upgrades)
            {
                if (shown >= UpgradeWindow)
                {
                    break;
                }

                if (_loop.IsUpgradePurchased(upgrade))
                {
                    continue;
                }

                shown++;
                var captured = upgrade;
                var row = Row();
                var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);
                var buy = Button(row.transform, "Buy", 150, () =>
                {
                    if (_loop.PurchaseUpgrade(captured))
                    {
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  <size=16>" + UpgradeRequirement(captured) + "</size>";
                    var ok = !_loop.IsUpgradePurchased(captured) && _loop.CanAffordUpgrade(captured)
                             && _loop.MeetsUpgradeSkillGate(captured) && string.IsNullOrEmpty(_loop.MissingToolTier(captured));
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });

                if (_loop.IsUpgradePurchased(upgrade))
                {
                    shown--;
                }
            }

            if (shown == 0)
            {
                MakeText(Row().transform, "Every upgrade owned.", 18, TextAnchor.MiddleLeft);
            }
        }

        private void BuildCraftingSection()
        {
            var recipes = _loop.AvailableRecipes();
            if (recipes.Count == 0)
            {
                return;
            }

            Section("Crafting");
            foreach (var recipe in recipes)
            {
                var captured = recipe;
                var row = Row();
                var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);
                var toggle = Button(row.transform, "Craft", 150, () =>
                {
                    _loop.ToggleCraft(captured);
                    _dirty = true;
                });

                _liveUpdaters.Add(() =>
                {
                    var crafting = _loop.IsCrafting(captured);
                    var progress = crafting ? "  " + Mathf.RoundToInt((float)_loop.CraftProgress(captured) * 100f) + "%" : string.Empty;
                    var need = _loop.IsRecipeLevelMet(captured) ? string.Empty : "  <size=16>(locked)</size>";
                    label.text = captured.output + progress + need;
                    SetButtonLabel(toggle, crafting ? "Stop" : "Craft");
                    var ok = crafting || _loop.IsRecipeWorkable(captured);
                    toggle.interactable = ok;
                    SetButtonTint(toggle, ok);
                });
            }
        }

        private void BuildBuildingsSection()
        {
            Section("Camp");
            foreach (var building in _loop.Data.buildings)
            {
                var captured = building;
                var row = Row();
                var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);
                var build = Button(row.transform, "Build", 200, () =>
                {
                    if (_loop.BuyBuildingLevel(captured))
                    {
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + " Lv " + _loop.BuildingLevel(captured)
                                 + "  <size=16>" + BundleLabel(_loop.NextBuildingBundle(captured)) + "</size>";
                    var ok = _loop.CanAffordBuilding(captured);
                    build.interactable = ok;
                    SetButtonTint(build, ok);
                });
            }
        }

        private void BuildRiteSection()
        {
            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite == null || rite.verses == null)
            {
                return;
            }

            Section("The Rite");
            foreach (var verse in rite.verses)
            {
                if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse))
                {
                    continue;
                }

                var capturedVerse = verse;
                var heading = MakeText(Row().transform, string.Empty, 20, TextAnchor.MiddleLeft);
                Flexible(heading.gameObject, 1f);
                _liveUpdaters.Add(() =>
                {
                    heading.text = "Verse of " + capturedVerse.zone + "  <size=16>"
                                   + Rite.CompletedSlotCount(_loop.State, capturedVerse) + " offered</size>";
                });

                for (var i = 0; i < verse.slots.Count; i++)
                {
                    var slotIndex = i;
                    var slot = verse.slots[i];
                    if (slot.type != RiteSlotType.Resource)
                    {
                        continue;
                    }

                    var row = Row();
                    var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft);
                    Flexible(label.gameObject, 1f);
                    var offer = Button(row.transform, "Offer", 150, () =>
                    {
                        _loop.OfferResource(capturedVerse, slotIndex);
                        _dirty = true;
                    });

                    _liveUpdaters.Add(() =>
                    {
                        var delivered = Rite.SlotDelivered(_loop.State, capturedVerse, slotIndex);
                        var target = Rite.SlotTarget(slot);
                        label.text = slot.resource + "  " + Mathf.FloorToInt((float)delivered) + " / " + Mathf.FloorToInt((float)target);
                        var have = _loop.State.GetResource(slot.resource) > BigDouble.Zero;
                        offer.interactable = have && delivered < target;
                        SetButtonTint(offer, offer.interactable);
                    });
                }
            }

            var migrateRow = Row();
            var migrate = Button(migrateRow.transform, "Migrate", 260, OpenMigrationSheet);
            _liveUpdaters.Add(() =>
            {
                var can = _loop.CanMigrate();
                migrate.gameObject.SetActive(can);
            });
        }

        private void BuildAlmanacSection()
        {
            if (_loop.State.verdurePoints <= 0.0 && _loop.State.almanacNodeIds.Count == 0)
            {
                return;
            }

            Section("The Almanac");
            foreach (var node in _loop.Data.almanac)
            {
                if (_loop.State.almanacNodeIds.Contains(node.id))
                {
                    continue;
                }

                var captured = node;
                var row = Row();
                var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);
                var buy = Button(row.transform, "Learn", 160, () =>
                {
                    if (_loop.BuyAlmanacNode(captured))
                    {
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  <size=16>" + Mathf.CeilToInt((float)captured.costVerdure) + " Verdure</size>";
                    var ok = _loop.AvailableVerdure() >= captured.costVerdure;
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }

        private void BuildKitSection()
        {
            if (_loop.Data.gear == null || _loop.Data.gear.Count == 0)
            {
                return;
            }

            Section("The Kit");
            foreach (var gear in _loop.Data.gear)
            {
                var captured = gear;
                var row = Row();
                var label = MakeText(row.transform, captured.displayName + "  <size=16>" + BundleLabel(captured.materials) + "</size>", 20, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);
                Button(row.transform, "Craft", 150, () =>
                {
                    if (_loop.CraftGear(captured))
                    {
                        _dirty = true;
                    }
                });
            }
        }

        private void BuildSpecimensSection()
        {
            var any = false;
            foreach (var pair in _loop.State.pristineResources)
            {
                if (pair.Value > BigDouble.Zero)
                {
                    any = true;
                    break;
                }
            }

            if (!any)
            {
                return;
            }

            Section("Specimens (Pristine)");
            foreach (var pair in _loop.State.pristineResources)
            {
                if (pair.Value <= BigDouble.Zero)
                {
                    continue;
                }

                var resourceId = pair.Key;
                var row = Row();
                var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft);
                Flexible(label.gameObject, 1f);
                Button(row.transform, "Donate", 160, () =>
                {
                    _loop.DonatePristine(resourceId);
                    _dirty = true;
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = resourceId + "  <size=16>" + NumberFormat.Short(_loop.State.GetPristine(resourceId)) + " held</size>";
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
            }
        }

        private void OpenArrivalSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet.transform, "A new friend", 30, TextAnchor.UpperCenter);
            MakeText(sheet.transform, "a " + SpeciesName(familiar.speciesId) + " arrives", 20, TextAnchor.UpperCenter);

            var field = MakeInputField(sheet.transform, familiar.name);
            Button(sheet.transform, "Walk together", 320, () =>
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
            MakeText(sheet.transform, "A bond is made", 30, TextAnchor.UpperCenter);
            MakeText(sheet.transform, bond.displayName + " will cross every fold with you.", 20, TextAnchor.UpperCenter);
            Button(sheet.transform, "Walk together", 320, CloseSheet);
        }

        private void OpenWelcomeSheet(OfflineSummary summary)
        {
            var sheet = BeginSheet();
            MakeText(sheet.transform, "Welcome back", 30, TextAnchor.UpperCenter);
            MakeText(sheet.transform, "Away " + NumberFormat.Duration(summary.realSeconds)
                                      + " · credited " + NumberFormat.Duration(summary.creditedSeconds), 20, TextAnchor.UpperCenter);

            var lines = 0;
            foreach (var pair in summary.gains)
            {
                if (lines++ >= 6)
                {
                    break;
                }

                MakeText(sheet.transform, "+" + NumberFormat.Short(pair.Value) + " " + pair.Key, 18, TextAnchor.MiddleCenter);
            }

            Button(sheet.transform, "Continue", 320, CloseSheet);
        }

        private void OpenMigrationSheet()
        {
            var sheet = BeginSheet();
            MakeText(sheet.transform, "Migrate", 30, TextAnchor.UpperCenter);
            MakeText(sheet.transform, "Verdure " + Mathf.FloorToInt((float)_loop.State.verdurePoints)
                                      + " → " + Mathf.FloorToInt((float)_loop.VerdureAfterMigration())
                                      + "\nThe crew crosses with you; the camp folds.", 20, TextAnchor.UpperCenter);
            Button(sheet.transform, "Migrate", 320, () =>
            {
                _loop.Migrate();
                _dirty = true;
                CloseSheet();
            });
            Button(sheet.transform, "Stay a while longer", 320, CloseSheet);
        }

        private void OpenNamingSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet.transform, "Rename", 30, TextAnchor.UpperCenter);
            var field = MakeInputField(sheet.transform, familiar.name);
            Button(sheet.transform, "Save", 320, () =>
            {
                _loop.RenameFamiliar(familiar, field.text);
                _dirty = true;
                CloseSheet();
            });
            Button(sheet.transform, "Cancel", 320, CloseSheet);
        }

        private void OpenPowerupSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet.transform, "Choose a powerup", 30, TextAnchor.UpperCenter);
            MakeText(sheet.transform, "for " + familiar.name, 20, TextAnchor.UpperCenter);

            foreach (var powerup in _loop.OfferablePowerups(familiar))
            {
                var captured = powerup;
                Button(sheet.transform, captured.displayName + " — " + captured.description, 480, () =>
                {
                    _loop.ChoosePowerup(familiar, captured.id);
                    _dirty = true;
                    CloseSheet();
                });
            }

            Button(sheet.transform, "Later", 320, CloseSheet);
        }

        private Transform BeginSheet()
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, DimColor);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;

            var panel = MakePanel("Panel", (RectTransform)dim.transform, SheetColor);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760, 620);
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

        /// <summary>The next stationing post in the cycle: wander → each node → the trail → each dig site → wander.</summary>
        private string NextStation(string current)
        {
            var options = new List<string> { null };
            foreach (var node in _loop.State.nodes)
            {
                options.Add(node.id);
            }

            options.Add(Familiar.TrailStation);
            foreach (var site in _loop.State.digSites)
            {
                options.Add(Familiar.DigStationPrefix + site.zoneId);
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
                return "dig: " + stationId.Substring(Familiar.DigStationPrefix.Length);
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

        private void Section(string title)
        {
            var go = MakePanel("Section_" + title, _body, SectionColor);
            FixedHeight(go, 44);
            var text = MakeText(go.transform, title, 22, TextAnchor.MiddleLeft);
            Stretch((RectTransform)text.transform);
            var padded = (RectTransform)text.transform;
            padded.offsetMin = new Vector2(14, 0);
        }

        private GameObject Row()
        {
            var go = MakePanel("Row", _body, RowColor);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 8;
            var fitter = go.AddComponent<LayoutElement>();
            fitter.minHeight = 72;
            return go;
        }

        private Button Button(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ButtonColor;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            var label = MakeText(go.transform, text, 20, TextAnchor.MiddleCenter);
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
                image.color = on ? ButtonColor : ButtonOffColor;
            }
        }

        private InputField MakeInputField(Transform parent, string value)
        {
            var go = new GameObject("Field", typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = FieldColor;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 520;
            element.minWidth = 520;
            element.preferredHeight = 72;
            element.minHeight = 72;

            var text = MakeText(go.transform, value, 24, TextAnchor.MiddleLeft);
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

        private Text MakeText(Transform parent, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
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
