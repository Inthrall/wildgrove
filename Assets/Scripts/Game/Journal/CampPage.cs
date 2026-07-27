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
    /// The Camp page — the fire &amp; bench (crafting), the building lines, the
    /// Ladder's next rungs, and the caravan exchange beneath them. What the camp
    /// makes of what the trail brings home.
    /// </summary>
    internal sealed class CampPage : JournalSection
    {
        // How many unpurchased Ladder rungs the camp shows at once.
        private const int UpgradeWindow = 3;

        // The caravan exchange's from/to selection, remembered across rebuilds.
        private string _exchangeFrom;
        private string _exchangeTo;

        internal CampPage(GameHud hud) : base(hud) { }

        internal void BuildCampPage()
        {
            BuildCraftingCard();
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

        private void BuildWeeklyCacheRow(RectTransform card, EconomyData economy)
        {
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var text = "the weekly amber cache"
                       + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)economy.amber.weeklyCacheAmber) + " amber</color></size>";
            Button claim = null;
            claim = Button(row.transform, "Claim", 170, () =>
            {
                // Signed out, the button IS the sign-in — hiding the row hid
                // the free weekly claim from exactly the players it should
                // convert (the Standing card's pattern).
                if (!_loop.GameServices.IsSignedIn)
                {
                    Flash(claim, "asking Play Games", true);
                    _loop.GameServices.SignInInteractive(signedIn => SetNote(signedIn
                        ? "signed in — the week's cache is yours to claim."
                        : "Play Games didn't answer — the cache keeps for now."));
                    return;
                }

                var amount = _loop.ClaimWeeklyCache();
                if (amount > 0.0)
                {
                    Flash(claim, "+" + Mathf.FloorToInt((float)amount) + " amber", true);
                    SetNote("the week's cache — a little resin, freely given.");
                    _dirty = true;
                }
            });

            // A claimed cache stays on the page counting down to the week's
            // turn rather than vanishing.
            _liveUpdaters.Add(() =>
            {
                var signedIn = _loop.GameServices.IsSignedIn;
                var ready = _loop.CanClaimWeeklyCache();
                label.text = signedIn
                    ? text + (ready ? string.Empty : WaitingTail(_loop.WeeklyCacheCooldownRemaining))
                    : text + SizeOpen(15) + "<color=" + Ink2Hex + ">  Play Games isn't signed in</color></size>";
                SetButtonLabel(claim, signedIn ? "Claim" : "Sign in");
                var live = !signedIn || ready;
                claim.interactable = live;
                SetButtonTint(claim, live);
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

        private void BuildAmberPackRow(RectTransform card, string productId, double amount)
        {
            if (amount <= 0.0)
            {
                return;
            }

            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var baseText = "an amber pack"
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
                var toggle = Button(row.transform, "Craft", 210, () =>
                {
                    _loop.ToggleCraft(captured);
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
                    SetButtonLabel(toggle, crafting ? "Stop crafting" : "Craft");
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
            // The cyclers may never land the two sides on the same good — a
            // self-trade quote reads as a broken caravan.
            var fromButton = Button(row.transform, "from", 250, () =>
            {
                var options = TradeableResources();
                _exchangeFrom = Cycle(options, _exchangeFrom);
                if (_exchangeFrom == _exchangeTo && options.Count > 1)
                {
                    _exchangeTo = Cycle(options, _exchangeTo);
                }

                _dirty = true;
            });
            var toButton = Button(row.transform, "to", 250, () =>
            {
                var options = TradeableResources();
                _exchangeTo = Cycle(options, _exchangeTo);
                if (_exchangeTo == _exchangeFrom && options.Count > 1)
                {
                    _exchangeTo = Cycle(options, _exchangeTo);
                }

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
