using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Camp page — the camp's own actions, a card per crafting station,
    /// the building lines, the Ladder's next rungs, and the caravan exchange
    /// beneath them. What the camp makes of what the trail brings home.
    /// </summary>
    internal sealed class CampPage : JournalSection
    {
        // How many unpurchased Ladder rungs the camp shows at once.
        private const int UpgradeWindow = 3;

        // The caravan exchange's from/to selection, remembered across rebuilds.
        private string _exchangeFrom;
        private string _exchangeTo;

        // How much of the give-good a trade spends, as a fraction of what's
        // held. Half by default: the whole stock is the one amount that can't
        // be walked back, so it isn't what a stray tap reaches for — and it
        // alone asks first.
        private double _exchangeFraction = 0.5;

        // The amounts the caravan deals in. Fractions rather than numbers so
        // the choice still means something at 8 berries and at 8 million.
        private static readonly (string Label, double Fraction)[] ExchangeAmounts =
        {
            ("a quarter", 0.25),
            ("half", 0.5),
            ("all", 1.0),
        };

        internal CampPage(GameHud hud) : base(hud) { }

        internal void BuildCampPage()
        {
            // The camp actions (the rewarded time-skip, and Remove Ads until
            // it's owned) used to be a bar pinned in the chrome, read on all
            // four tabs and paid for by the open page on all four. They're camp
            // business, so they head the camp's page — and being in the body
            // means they refresh off the page's own updater pool rather than
            // the HUD's chrome pass.
            _hud.Sheets.BuildCampActions(_body);
            _liveUpdaters.Add(() => _hud.Sheets.RefreshCampActions());

            BuildCraftingCards();
            BuildBuildingsCard();
            BuildLadderCard();
            BuildExchangeCard();
            BuildAmberCard();
        }

        /// <summary>
        /// The Amber lines (design §10/§11): spend it on a full-rate time-skip,
        /// earn it from a rewarded ad or the weekly Play Games cache, or buy a
        /// pack. The whole card stays hidden while the amber economy is inert.
        /// </summary>
        private void BuildAmberCard()
        {
            var economy = _loop.Data.economy;
            if (economy?.amber == null)
            {
                return;
            }

            var card = Card("THE AMBER");

            if (Amber.Configured(economy))
            {
                BuildTimeSkipRow(card, economy);
            }

            if (economy.amber.weeklyCacheAmber > 0.0)
            {
                BuildWeeklyCacheRow(card, economy);
            }

            if (economy.amber.adDripAmber > 0.0)
            {
                BuildAmberDripRow(card, economy);
            }

            if (economy.store != null)
            {
                BuildAmberPackRow(card, StoreProductIds.AmberPackSmall, economy.store.amberPackSmall);
                BuildAmberPackRow(card, StoreProductIds.AmberPackLarge, economy.store.amberPackLarge);
            }
        }

        private void BuildTimeSkipRow(RectTransform card, EconomyData economy)
        {
            var hours = economy.amber.timeSkipHours;
            var cost = economy.amber.timeSkipCostAmber;
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            label.text = "hasten " + NumberFormat.Duration(hours * 3600.0) + " at full pace"
                         + SizeOpen(15) + "<color=" + OchreHex + ">  " + Mathf.FloorToInt((float)cost) + " amber</color></size>";
            var amber = Mathf.FloorToInt((float)cost);
            Button skip = null;
            skip = Button(row.transform, "Hasten", 170, () =>
            {
                if (!_loop.CanTimeSkip())
                {
                    return;
                }

                // Amber is premium and hard-won — never spend it on a stray tap.
                _hud.Sheets.OpenConfirmSheet(
                    "Spend " + amber + " amber",
                    "Hasten " + NumberFormat.Duration(hours * 3600.0) + " of gathering at full pace?",
                    "Spend " + amber + " amber",
                    () =>
                    {
                        if (_loop.TimeSkip() > 0.0)
                        {
                            SetNote("amber spent — " + NumberFormat.Duration(hours * 3600.0) + " of gathering, in a breath.");
                            _dirty = true;
                        }
                    });
            });

            _liveUpdaters.Add(() =>
            {
                var ok = _loop.CanTimeSkip();
                skip.interactable = ok;
                SetButtonTint(skip, ok);
            });
        }

        /// <summary>
        /// The weekly Amber cache (design §11) — a Play Games Reward, not a tap
        /// the game grants itself. Play sets one out at most weekly for a Social
        /// Challenge and delivers it through the store, so this row reports the
        /// week's state and offers a re-read for a player who redeemed a moment
        /// ago; the cache itself lands through the reward sheet either way.
        /// </summary>
        private void BuildWeeklyCacheRow(RectTransform card, EconomyData economy)
        {
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var text = "the weekly amber cache"
                       + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)economy.amber.weeklyCacheAmber) + " amber</color></size>";
            var checking = false;
            Button look = null;
            look = Button(row.transform, "Look", 170, () =>
            {
                // Signed out, the button IS the sign-in — hiding the row hid the
                // cache from exactly the players it should convert, and a reward
                // can't be awarded to someone Play Games doesn't know at all
                // (the Standing card's pattern).
                if (!_loop.GameServices.IsSignedIn)
                {
                    Flash(look, "asking Play Games", true);
                    _loop.GameServices.SignInInteractive(signedIn => SetNote(signedIn
                        ? "signed in — Play Games can set the cache out now."
                        : "Play Games didn't answer — the cache keeps for now."));
                    return;
                }

                if (checking)
                {
                    return;
                }

                checking = true;
                SetButtonLabel(look, "Looking…");
                _loop.CheckPlayRewards(found =>
                {
                    checking = false;
                    if (found > 0)
                    {
                        // The reward sheet says what arrived and by whose hand —
                        // this line only stops the page reading dead.
                        _dirty = true;
                        return;
                    }

                    SetNote("nothing set out yet. Play Games leaves the cache for a challenge met.");
                });
            });

            _liveUpdaters.Add(() =>
            {
                var signedIn = _loop.GameServices.IsSignedIn;
                var due = _loop.WeeklyCacheDue;
                label.text = signedIn
                    ? text + (due
                        ? SizeOpen(15) + "<color=" + Ink2Hex + ">  set out by Play Games</color></size>"
                        : WaitingTail(_loop.WeeklyCacheCooldownRemaining))
                    : text + SizeOpen(15) + "<color=" + Ink2Hex + ">  Play Games isn't signed in</color></size>";
                if (!checking)
                {
                    SetButtonLabel(look, signedIn ? "Look" : "Sign in");
                }

                var live = !checking;
                look.interactable = live;
                SetButtonTint(look, live);
            });
        }

        private void BuildAmberDripRow(RectTransform card, EconomyData economy)
        {
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var text = "a little amber" + _loop.RewardedActionSuffix
                       + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)economy.amber.adDripAmber) + " amber</color></size>";
            label.text = text;
            Button watch = null;
            watch = Button(row.transform, "Watch", 170, () =>
            {
                _loop.WatchRewarded(RewardedPlacement.AmberDrip, () =>
                {
                    var amount = _loop.GrantAmberDrip();
                    if (amount > 0.0)
                    {
                        Flash(watch, "+" + Mathf.FloorToInt((float)amount) + " amber", true);
                        SetNote("a little amber, for your patience.");
                        _dirty = true;
                    }
                });
            });

            // The drip's own cooldown is what the warden can wait out, so it's
            // what the line counts down; an unloaded ad only greys the button.
            _liveUpdaters.Add(() =>
            {
                var offCooldown = _loop.CanWatchAmberDrip;
                var ready = _loop.RewardedReady(RewardedPlacement.AmberDrip) && offCooldown;
                label.text = text + (offCooldown ? string.Empty : WaitingTail(_loop.AmberDripCooldownRemaining));
                watch.interactable = ready;
                SetButtonTint(watch, ready);
            });
        }

        /// <summary>The muted "ready in 6d 4h" tail an amber line wears while its cooldown holds.</summary>
        private static string WaitingTail(double seconds)
        {
            return SizeOpen(15) + "<color=" + Ink2Hex + ">  ready in " + NumberFormat.Countdown(seconds) + "</color></size>";
        }

        /// <summary>
        /// Which pack a line is offering. Both rows read "an amber pack" before,
        /// so the only thing telling the small pile from the large was the
        /// number beside it — two near-identical lines, one above the other.
        /// </summary>
        private static string AmberPackTitle(string productId)
        {
            if (productId == StoreProductIds.AmberPackLarge)
            {
                return "a hoard of amber";
            }

            return "a handful of amber";
        }

        private void BuildAmberPackRow(RectTransform card, string productId, double amount)
        {
            if (amount <= 0.0)
            {
                return;
            }

            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var baseText = AmberPackTitle(productId)
                           + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)amount) + " amber</color></size>";
            label.text = baseText;
            // The store's price lands after the (lazy) catalogue fetch — keep
            // the line current so the tap is never a surprise dialog.
            _liveUpdaters.Add(() => label.text = baseText + PriceTail(productId));
            Button buy = null;
            buy = Button(row.transform, "Buy", 170, () =>
            {
                _loop.PurchaseAmberPack(productId, result =>
                {
                    if (result == StoreResult.Purchased)
                    {
                        Flash(buy, "+" + Mathf.FloorToInt((float)amount) + " amber", true);
                        SetNote("the caravan trades in resin — amber for the coffer.");
                        _dirty = true;
                    }
                    else if (result == StoreResult.Failed)
                    {
                        SetNote("that didn't go through — nothing was charged.");
                    }
                });
            });
        }

        /// <summary>
        /// The crafting stations, a card each. Every recipe used to sit in one
        /// flat list, which said nothing about the rule underneath it: a
        /// station works ONE recipe at a time, and the stations work at once.
        /// So starting a second recipe either silently stopped the first (same
        /// station) or didn't (a different one), and the page gave no way to
        /// tell which — nor any hint of why several bars could turn together.
        /// A card per station is that rule made into the shape of the page.
        /// </summary>
        private void BuildCraftingCards()
        {
            var stations = CraftingStations();
            foreach (var station in stations)
            {
                BuildStationCard(station.Key, station.Value, stations);
            }
        }

        /// <summary>
        /// The visible recipes grouped by station, in building-line order so
        /// the cards sit in the same order the Building Lines card lists them.
        /// </summary>
        private List<KeyValuePair<string, List<RecipeData>>> CraftingStations()
        {
            var byStation = new Dictionary<string, List<RecipeData>>();
            var order = new List<string>();
            foreach (var recipe in _loop.AvailableRecipes())
            {
                if (!byStation.TryGetValue(recipe.station, out var list))
                {
                    list = new List<RecipeData>();
                    byStation[recipe.station] = list;
                    order.Add(recipe.station);
                }

                list.Add(recipe);
            }

            // Building order first; a station no line claims (hand-built data)
            // keeps its recipe-order place at the back rather than vanishing —
            // OrderBy for the stable sort that promise needs.
            var stations = new List<KeyValuePair<string, List<RecipeData>>>();
            foreach (var id in order.OrderBy(BuildingOrder))
            {
                stations.Add(new KeyValuePair<string, List<RecipeData>>(id, byStation[id]));
            }

            return stations;
        }

        private int BuildingOrder(string stationId)
        {
            var buildings = _loop.Data.buildings;
            for (var i = 0; buildings != null && i < buildings.Count; i++)
            {
                if (buildings[i].id == stationId)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        /// <summary>The station's name for a card head — the building line's, or the raw id.</summary>
        private string CraftStationName(string stationId)
        {
            return _loop.Data.BuildingsById.TryGetValue(stationId, out var building)
                ? building.displayName
                : GoodName(stationId);
        }

        /// <summary>
        /// "one work at a time — the bench and the forge keep their own." The
        /// second clause is the answer to "why can I craft several things at
        /// once", so it names the sibling stations rather than gesturing at
        /// them; with no siblings it's simply left off.
        /// </summary>
        private string StationRule(string stationId, List<KeyValuePair<string, List<RecipeData>>> stations)
        {
            var others = new List<string>();
            foreach (var station in stations)
            {
                if (station.Key != stationId)
                {
                    others.Add(CraftStationName(station.Key).ToLowerInvariant());
                }
            }

            if (others.Count == 0)
            {
                return "one work at a time.";
            }

            if (others.Count == 1)
            {
                return "one work at a time — " + others[0] + " keeps its own.";
            }

            var last = others.Count - 1;
            return "one work at a time — " + string.Join(", ", others.GetRange(0, last))
                   + " and " + others[last] + " keep their own.";
        }

        private void BuildStationCard(string stationId, List<RecipeData> recipes,
            List<KeyValuePair<string, List<RecipeData>>> stations)
        {
            var card = Card(CraftStationName(stationId).ToUpperInvariant());
            // The same plate the Building Lines card wears for this line — the
            // two cards are the one place, seen from its two sides.
            var plate = ArtLibrary.ForBuilding(stationId);
            if (plate != null)
            {
                PlateImage(card, plate, 120f);
            }

            MakeText(card, "<i>" + StationRule(stationId, stations) + "</i>",
                17, TextAnchor.MiddleCenter, Ink2, _serif);

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
                var toggle = Button(row.transform, "Craft", 210, () =>
                {
                    // Displacement is silent in the sim (the old batch's inputs
                    // come back, no bar anywhere reports the swap) — so the
                    // page has to be the one that says what was set aside.
                    var displaced = _loop.IsCrafting(captured) ? null : _loop.StationRecipe(captured.station);
                    _loop.ToggleCraft(captured);
                    if (displaced != null && _loop.IsCrafting(captured))
                    {
                        SetNote(CraftStationName(captured.station).ToLowerInvariant() + " sets aside the "
                                + GoodName(displaced.output) + " and takes up the " + GoodName(captured.output) + ".");
                    }

                    _dirty = true;
                });

                _liveUpdaters.Add(() =>
                {
                    var crafting = _loop.IsCrafting(captured);
                    var halted = crafting && _loop.IsCraftHalted(captured);
                    var progress = string.Empty;
                    if (halted)
                    {
                        // A frozen bar at 0% looked identical to a slow one. Say
                        // it plainly, and name the good that stopped it.
                        var missing = _loop.MissingCraftInput(captured);
                        progress = "  <color=" + AlarmHex + "><b>Crafting halted</b>"
                                   + (missing != null ? " — out of " + missing : string.Empty) + "</color>";
                    }
                    else if (crafting)
                    {
                        progress = "  <color=" + MossDeepHex + ">crafting · "
                                   + Mathf.RoundToInt((float)_loop.CraftProgress(captured) * 100f) + "%</color>";
                    }

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

                    // With every input in stock, itemising them wrapped the
                    // line and buried nothing useful; the itemised (have N)
                    // treatment is saved for the shortfall, where it earns
                    // its space by naming exactly what's blocking.
                    string inputsLine;
                    if (_loop.CanCraft(captured))
                    {
                        inputsLine = BundleLabel(captured.inputs) + " — in hand";
                    }
                    else
                    {
                        var shortOf = new List<string>();
                        var metCount = 0;
                        foreach (var input in captured.inputs)
                        {
                            var have = _loop.State.GetResource(input.id);
                            if (have >= input.amount)
                            {
                                metCount++;
                                continue;
                            }

                            shortOf.Add(input.amount + " " + input.id + " <color=" + OchreInkHex + ">(have "
                                        + NumberFormat.Short(have) + ")</color>");
                        }

                        inputsLine = string.Join(", ", shortOf)
                                     + (metCount > 0 ? "<color=" + Ink2Hex + ">, the rest in hand</color>" : string.Empty);
                    }

                    // The inputs already say what the camp holds of each; the
                    // OUTPUT didn't, so the one number you want while deciding
                    // whether to keep a batch running was the missing one.
                    label.text = captured.output + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">(have "
                                 + NumberFormat.Short(_loop.State.GetResource(captured.output)) + ")</color></size>" + progress + need
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + inputsLine + "</color></size>";
                    // "Stop" alone read as a state ("it is stopped"), not an
                    // action — the row's own status line is what reports state.
                    // "Craft instead" is the tap that costs you something: the
                    // station is on another recipe and this would displace it,
                    // which the plain "Craft" gave no warning of.
                    var busyElsewhere = !crafting && _loop.StationRecipe(captured.station) != null;
                    SetButtonLabel(toggle, crafting ? "Stop crafting" : busyElsewhere ? "Craft instead" : "Craft");
                    // Stopping is always allowed; starting needs the gates AND
                    // a batch of inputs in camp stock.
                    var ok = crafting || (_loop.IsRecipeWorkable(captured) && _loop.CanCraft(captured));
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
                // The crafting card filters to what the run can see; this one
                // used to firehose every line in the game data from minute
                // one, naming resources the player hadn't met. A line waits
                // until every good its next level asks for has been gathered.
                if (!BundleDiscovered(_loop.NextBuildingBundle(captured)))
                {
                    continue;
                }

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

        private bool BundleDiscovered(List<Buildings.MaterialCost> bundle)
        {
            foreach (var cost in bundle ?? new List<Buildings.MaterialCost>())
            {
                if (!GoodDiscovered(cost.id))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A good is known by doing: gathered as a raw find, or crafted at least once.</summary>
        private bool GoodDiscovered(string id)
        {
            if (Compendium.IsResourceDiscovered(_loop.State, id))
            {
                return true;
            }

            foreach (var recipe in _loop.Data.recipes)
            {
                if (recipe.output == id && Compendium.IsRecipeDiscovered(_loop.State, recipe.id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The caravan (design §9): goods for goods. Two field rows say what
        /// leaves and what comes back — each NAMES its good and what's held of
        /// it, with the list behind a Change button, because a plate reading
        /// "Give: berries" is a plate a player taps expecting to give (and a
        /// one-way cycler through the whole catalogue has no way back). The
        /// amount is a choice rather than the old lone "Trade all" — which was
        /// both the only offer and the only irreversible one — and the trade
        /// itself is labelled with the deal it's about to strike.
        /// </summary>
        private void BuildExchangeCard()
        {
            var card = Card("THE EXCHANGE");
            MakeText(card, "<i>a caravan idles at the camp edge. it trades; it does not sell.</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);
            var caravan = ArtLibrary.ForJournal("caravan");
            if (caravan != null)
            {
                PlateImage(card, caravan, 200f);
            }

            if (!Exchange.Configured(_loop.Data))
            {
                MakeText(card, "the caravan has not come this way.", 18, TextAnchor.MiddleCenter, Ink2);
                return;
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
                _exchangeTo = tradeable[0] == _exchangeFrom ? tradeable[1] : tradeable[0];
            }

            // Every widget here reads the same two goods and one amount, so
            // they refresh together — on the HUD's cadence, and again the
            // instant a tap changes any of them. Waiting a quarter second to
            // acknowledge a tap is what makes a picker feel broken.
            System.Action refresh = null;
            var giveField = BuildExchangeSide(card, "GIVE", true, () => refresh());
            var takeField = BuildExchangeSide(card, "GET", false, () => refresh());
            var rate = MakeText(card, string.Empty, 16, TextAnchor.MiddleCenter, Ink2);

            var amountRow = Row(card);
            amountRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var chips = new List<(Button Plate, double Fraction)>();
            foreach (var amount in ExchangeAmounts)
            {
                var fraction = amount.Fraction;
                chips.Add((Button(amountRow.transform, amount.Label, 170, () =>
                {
                    _exchangeFraction = fraction;
                    refresh();
                }), fraction));
            }

            var tradeRow = Row(card);
            tradeRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Button trade = null;
            trade = Button(tradeRow.transform, "Trade", 800, () => OfferTrade(trade));
            KeyAction(trade);

            refresh = () =>
            {
                giveField.text = ExchangeSideLabel("GIVE", _exchangeFrom);
                takeField.text = ExchangeSideLabel("GET", _exchangeTo);
                // Per-unit, and the caravan's cut said plainly: barter replaced
                // selling for Coin, so this card is the game's only price
                // signal — it used to show one pre-computed lump and no rate.
                rate.text = GoodName(_exchangeFrom) + " → " + GoodName(_exchangeTo) + " at "
                            + NumberFormat.Rate(_loop.ExchangeRate(_exchangeFrom, _exchangeTo)) + " each"
                            + "  ·  <color=" + OchreInkHex + ">the caravan keeps "
                            + Percent(_loop.Data.exchange.spread) + "</color>";

                foreach (var chip in chips)
                {
                    SetButtonChosen(chip.Plate, System.Math.Abs(chip.Fraction - _exchangeFraction) < 0.001);
                }

                var spend = ExchangeSpend();
                var got = _loop.ExchangeQuote(_exchangeFrom, _exchangeTo, spend);
                var live = got > BigDouble.Zero;
                trade.interactable = live;
                SetButtonTint(trade, live, true);
                SetButtonLabel(trade, live
                    ? "Trade " + ExchangeDeal(spend, got)
                    : "no " + GoodName(_exchangeFrom) + " to give");
            };

            refresh();
            _liveUpdaters.Add(refresh);
        }

        /// <summary>
        /// One side of the caravan's deal: a field naming its good and the
        /// holding behind it, with the picker on a Change button — the page's
        /// own idiom (the label describes, the button acts) rather than a plate
        /// wearing the verb of the trade. The taking side carries the swap,
        /// since it sits between the two goods it would reverse.
        /// </summary>
        private Text BuildExchangeSide(RectTransform card, string field, bool giving, System.Action refresh)
        {
            var row = Row(card);
            var label = MakeText(row.transform, ExchangeSideLabel(field, giving ? _exchangeFrom : _exchangeTo),
                19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            if (!giving)
            {
                // Reversing the pair was a full lap of both cyclers, and it's
                // the commonest second thought at a barter table.
                Button(row.transform, "Swap", 140, () =>
                {
                    var was = _exchangeFrom;
                    _exchangeFrom = _exchangeTo;
                    _exchangeTo = was;
                    refresh();
                });
            }

            Button(row.transform, "Change", 170, () => OpenExchangePicker(giving, refresh));
            return label;
        }

        /// <summary>"GIVE / berries / 240 held" — the field, its good, and the holding under it.</summary>
        private string ExchangeSideLabel(string field, string id)
        {
            return SizeOpen(15) + "<color=" + Ink2Hex + ">" + field + "</color></size>  " + GoodName(id)
                   + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + ExchangeHeld(id) + "</color></size>";
        }

        /// <summary>What the camp holds of a good, for a picker line or a field.</summary>
        private string ExchangeHeld(string id)
        {
            var have = _loop.State.GetResource(id);
            return have > BigDouble.Zero ? NumberFormat.Short(have) + " held" : "none held";
        }

        /// <summary>The stock the chosen amount comes to right now.</summary>
        private BigDouble ExchangeSpend()
        {
            return Exchange.Portion(_loop.State.GetResource(_exchangeFrom), _exchangeFraction);
        }

        /// <summary>
        /// "120 berries → 18 wildflowers". Fraction-capable throughout: half of
        /// five berries is 2.5, and the whole-unit formatter would call that 2
        /// while the caravan took two and a half.
        /// </summary>
        private string ExchangeDeal(BigDouble spend, BigDouble got)
        {
            return NumberFormat.Rate(spend) + " " + GoodName(_exchangeFrom)
                   + " → " + NumberFormat.Rate(got) + " " + GoodName(_exchangeTo);
        }

        /// <summary>
        /// Strike the deal — or, for the whole stock, ask first. Amber asks
        /// before it's spent for the same reason: emptying a good the camp has
        /// been gathering all session is not a thing to do by accident.
        /// </summary>
        private void OfferTrade(Button trade)
        {
            var spend = ExchangeSpend();
            var got = _loop.ExchangeQuote(_exchangeFrom, _exchangeTo, spend);
            if (got <= BigDouble.Zero)
            {
                return;
            }

            if (_exchangeFraction < 1.0)
            {
                CommitTrade(trade, spend);
                return;
            }

            _hud.Sheets.OpenConfirmSheet(
                "Trade all your " + GoodName(_exchangeFrom) + "?",
                ExchangeDeal(spend, got) + ", and the trail starts that pile again.",
                "Trade all",
                () => CommitTrade(trade, spend));
        }

        private void CommitTrade(Button trade, BigDouble spend)
        {
            var got = _loop.TradeAtExchange(_exchangeFrom, _exchangeTo, spend);
            if (got > BigDouble.Zero)
            {
                Flash(trade, "+" + NumberFormat.Rate(got) + " " + GoodName(_exchangeTo), true);
                SetNote("traded " + GoodName(_exchangeFrom) + " for " + GoodName(_exchangeTo)
                        + ". a nod. gone before the count.");
            }

            _dirty = true;
        }

        /// <summary>
        /// The caravan's list for one side. The giving list says what's held;
        /// the taking list also says what the chosen amount would fetch, which
        /// is the number the choice actually turns on.
        /// </summary>
        private void OpenExchangePicker(bool giving, System.Action refresh)
        {
            var spend = ExchangeSpend();
            _hud.Sheets.OpenGoodPickSheet(
                giving ? "What will you give?" : "What will you take?",
                TradeableResources(),
                giving ? _exchangeTo : _exchangeFrom,
                id => giving ? ExchangeHeld(id) : ExchangeHeld(id) + ExchangeFetchTail(id, spend),
                id =>
                {
                    if (giving)
                    {
                        _exchangeFrom = id;
                    }
                    else
                    {
                        _exchangeTo = id;
                    }

                    refresh();
                });
        }

        /// <summary>"· 120 berries buys 18" — what this good would come to, on the taking list.</summary>
        private string ExchangeFetchTail(string id, BigDouble spend)
        {
            var got = _loop.ExchangeQuote(_exchangeFrom, id, spend);
            return got <= BigDouble.Zero
                ? string.Empty
                : "  ·  " + NumberFormat.Rate(spend) + " " + GoodName(_exchangeFrom)
                  + " buys " + NumberFormat.Rate(got);
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

            // The next few unpurchased rungs of the §9 ladder, in order. A
            // recruit rung whose familiar already walks (the kith crossed a
            // fold) has nothing left to give — it doesn't reappear.
            var next = new List<UpgradeData>();
            foreach (var upgrade in _loop.Data.upgrades.OrderBy(u => u.order))
            {
                if (_loop.IsUpgradePurchased(upgrade) || Upgrades.IsSpentRecruit(_loop.State, upgrade))
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
    }
}
