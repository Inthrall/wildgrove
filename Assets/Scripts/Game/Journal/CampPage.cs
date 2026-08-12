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
            // it's owned) are camp business, so they head the camp's page
            // rather than a bar pinned in the chrome that all four tabs would
            // pay for. Being in the body also means they refresh off the page's
            // own updater pool rather than the HUD's chrome pass.
            _hud.Sheets.BuildCampActions(_body);
            _liveUpdaters.Add(() => _hud.Sheets.RefreshCampActions());

            BuildCampNameCard();
            BuildCraftingCards();
            BuildBuildingsCard();
            BuildLadderCard();
            BuildExchangeCard();
            BuildAmberCard();
        }

        /// <summary>
        /// The page's own head: what this camp is called (design §9's sink
        /// slate) — the Warden page's name card, worn by the run instead of
        /// the player. The quill opens the naming sheet, which carries the
        /// price; the card says the name and nothing else, for the same
        /// measured-at-zero-width reason the warden's does.
        /// </summary>
        private void BuildCampNameCard()
        {
            var card = Card("THE CAMP");

            var row = Row(card);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2;

            var name = MakeText(row.transform, _loop.CampName(), 30, TextAnchor.MiddleCenter, Ink, _serif);
            IconButton(row.transform, JournalSprites.QuillSprite(), 40f, 120f,
                () => _hud.Sheets.OpenCampNamingSheet());

            _liveUpdaters.Add(() => name.text = _loop.CampName());
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

            if (economy.amber.secondQueueCostAmber > 0.0)
            {
                BuildSecondQueueRow(card, economy);
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
            var text = "hasten " + NumberFormat.Duration(hours * 3600.0) + " at full pace"
                       + SizeOpen(15) + "<color=" + OchreHex + ">  " + Mathf.FloorToInt((float)cost) + " amber</color></size>";
            label.text = text;
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
                            SetNote("amber spent: " + NumberFormat.Duration(hours * 3600.0) + " of gathering, in a breath.");
                            _dirty = true;
                        }
                    });
            });

            // The skip budget is what the warden can wait out, so it's what
            // the line counts down (like the drip's cooldown); being short of
            // amber only greys the button.
            _liveUpdaters.Add(() =>
            {
                var ok = _loop.CanTimeSkip();
                label.text = text + (_loop.TimeSkipBudgetSpent()
                    ? WaitingTail(_loop.TimeSkipBudgetRemaining)
                    : string.Empty);
                skip.interactable = ok;
                SetButtonTint(skip, ok);
            });
        }

        /// <summary>
        /// The run's second craft-queue slot (design §9's sink slate): each
        /// station may hold two standing orders until the fold. One purchase,
        /// so one row — the station cards' own rule lines say the new count.
        /// </summary>
        private void BuildSecondQueueRow(RectTransform card, EconomyData economy)
        {
            var cost = Mathf.FloorToInt((float)economy.amber.secondQueueCostAmber);
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var offer = "a second queue at every station, until the fold"
                        + SizeOpen(15) + "<color=" + OchreHex + ">  " + cost + " amber</color></size>";
            var held = "a second queue at every station"
                       + SizeOpen(15) + "<color=" + MossDeepHex + ">  held, this run</color></size>";
            Button buy = null;
            buy = Button(row.transform, "Buy", 170, () =>
            {
                if (!_loop.CanBuySecondQueue())
                {
                    return;
                }

                // Amber is premium and hard-won — never spend it on a stray tap.
                _hud.Sheets.OpenConfirmSheet(
                    "Spend " + cost + " amber",
                    "A second standing order at every station, until the camp folds?",
                    "Spend " + cost + " amber",
                    () =>
                    {
                        if (_loop.BuySecondQueue())
                        {
                            SetNote("amber spent: the stations each take a second work in hand.");
                            _dirty = true;
                        }
                    });
            });

            _liveUpdaters.Add(() =>
            {
                var owned = _loop.SecondQueueOwned;
                label.text = owned ? held : offer;
                buy.gameObject.SetActive(!owned);
                if (!owned)
                {
                    var ok = _loop.CanBuySecondQueue();
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                }
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
                        ? "signed in, so Play Games can set the cache out now."
                        : "Play Games didn't answer, so the cache keeps for now."));
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
                    if (found == null)
                    {
                        // Play was never asked, so "nothing set out" would be a
                        // guess dressed as an answer — and the cache might well
                        // be waiting.
                        SetNote("Play couldn't be reached just now. the cache keeps.");
                        return;
                    }

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
                        : WaitingTail(_loop.WeeklyCacheNextDueIn))
                    : text + SizeOpen(15) + "<color=" + Ink2Hex + ">  Play Games isn't signed in</color></size>";
                if (!checking)
                {
                    SetButtonLabel(look, signedIn ? "Look" : "Sign in");
                }

                // Greys out once this week's cache has been taken, like the
                // drip's Watch — a live button that only ever answers "nothing
                // set out yet" teaches the player to stop reading the row.
                // Signed out it stays live, because there it is the sign-in.
                // Cost of greying: an off-cadence delivery (Play's week need not
                // start on the warden's Monday) waits for the next launch's
                // purchase fetch, which receives it unprompted — deferred,
                // never lost.
                var live = !checking && (!signedIn || due);
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
                        SetNote("the caravan trades in resin, amber for the coffer.");
                        _dirty = true;
                    }
                    else if (result == StoreResult.Failed)
                    {
                        SetNote("that didn't go through, nothing was charged.");
                    }
                    else if (result == StoreResult.Unavailable)
                    {
                        SetNote("the caravan couldn't be reached, nothing was charged. try again shortly.");
                    }
                    else if (result == StoreResult.Deferred)
                    {
                        SetNote("the payment hasn't cleared yet. the amber arrives when Play finishes it.");
                    }
                });
            });
        }

        /// <summary>
        /// The crafting stations, a card each — the rule made into the shape of
        /// the page: a station works ONE recipe at a time, and the stations
        /// work at once. A flat list of every recipe says none of that, so
        /// starting a second recipe appears to either silently stop the first
        /// (same station) or not (a different one), with nothing to tell which
        /// — nor any hint of why several bars can turn together.
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
        /// them; with no siblings it's simply left off. With the second queue
        /// bought (design §9's sink slate) the count changes for the run, so
        /// the line is re-read live.
        /// </summary>
        private string StationRule(string stationId, List<KeyValuePair<string, List<RecipeData>>> stations)
        {
            var works = _loop.StationOrderCapacity() > 1 ? "two works at a time" : "one work at a time";
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
                return works + ".";
            }

            if (others.Count == 1)
            {
                return works + ": " + others[0] + " keeps its own.";
            }

            var last = others.Count - 1;
            return works + ": " + string.Join(", ", others.GetRange(0, last))
                   + " and " + others[last] + " keep their own.";
        }

        /// <summary>
        /// The height every recipe row holds, in every state. The button is the
        /// tallest thing in it (120 units, the touch minimum) and neither the
        /// two lines of words nor the cost chips beside them reach that, so the
        /// row has one height and keeps it.
        /// </summary>
        private const float CraftRowHeight = 132f;

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

            var rule = MakeText(card, "<i>" + StationRule(stationId, stations) + "</i>",
                17, TextAnchor.MiddleCenter, Ink2, _serif);
            _liveUpdaters.Add(() => rule.text = "<i>" + StationRule(stationId, stations) + "</i>");

            foreach (var recipe in recipes)
            {
                var captured = recipe;
                // Fixed at the height of its own tallest piece (the button), so
                // the row is the same shape working, idle, halted or blocked.
                // It used to be sized by a label that gained a clause the moment
                // a batch started — the row grew, and every card below it moved,
                // while the finger that started the batch was still on the glass.
                var row = Row(card, CraftRowHeight);
                // A slot rather than a plate: a good whose art is missing must
                // still cost the row its picture's width, or that one row's
                // words start somewhere the others' don't.
                PictureSlot(row.transform, ArtLibrary.ForGood(captured.output), 56f);

                var column = Column(row.transform);
                FlexibleWidth(column, 1f);
                var title = MakeText(column.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                // Always built, always one line, whether or not it has anything
                // to say — the line is the row's shape, and its words are only
                // what happens to be in it.
                var state = MakeText(column.transform, string.Empty, 15, TextAnchor.MiddleLeft, Ink2);

                var costs = BuildCostStrip(row.transform, captured);

                // Wide enough for the longest thing this plate ever says
                // ("Craft instead"). At 210 that label wrapped to two lines and
                // took the whole row down with it.
                var toggle = Button(row.transform, "Craft", 300, () =>
                {
                    // Displacement is silent in the sim (the old batch's inputs
                    // come back, no bar anywhere reports the swap) — so the
                    // page has to be the one that says what was set aside.
                    // WouldDisplace mirrors Assign exactly, so with the second
                    // queue's spare slot open this stays null and no swap is
                    // announced that didn't happen.
                    var displaced = _loop.IsCrafting(captured) ? null : _loop.CraftWouldDisplace(captured);
                    _loop.ToggleCraft(captured);
                    if (displaced != null && _loop.IsCrafting(captured))
                    {
                        SetNote(CraftStationName(captured.station).ToLowerInvariant() + " sets aside the "
                                + GoodName(displaced.output) + " and takes up the " + GoodName(captured.output) + ".");
                    }

                    _dirty = true;
                });

                var fill = ButtonFill(toggle);

                _liveUpdaters.Add(() =>
                {
                    var crafting = _loop.IsCrafting(captured);
                    var halted = crafting && _loop.IsCraftHalted(captured);

                    // The OUTPUT's own count, which nothing else on the page
                    // carries — the one number you want while deciding whether
                    // to keep a batch running.
                    title.text = captured.output + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">(have "
                                 + NumberFormat.Short(_loop.State.GetResource(captured.output)) + ")</color></size>";
                    state.text = CraftStateLine(captured, crafting, halted);
                    RefreshCostStrip(costs);

                    // The count rides the plate rather than the row's words, so
                    // the only thing on the row that moves four times a second
                    // is the thing already being watched. It keeps its verb
                    // though: "Stop" alone read as a state ("it is stopped")
                    // rather than an action, and a plate reading only "49%" is a
                    // readout — a readout is not something a thumb reaches for.
                    // Three characters wide throughout, so climbing 7% → 43% →
                    // 100% never moves the word in front of it. A halted order
                    // drops the count entirely: it is standing at nothing, and
                    // "0%" beside "halted" reads as a batch that has barely
                    // begun rather than one that has stopped.
                    // "Craft instead" is the tap that costs you something: the
                    // station is full and this would displace an order, which
                    // the plain "Craft" gave no warning of. With the second
                    // queue's slot free, starting costs nothing to set aside —
                    // so it reads "Craft".
                    var progress = crafting && !halted ? (float)_loop.CraftProgress(captured) : 0f;
                    var wouldDisplace = !crafting && _loop.CraftWouldDisplace(captured) != null;
                    SetButtonLabel(toggle, crafting
                        ? halted ? "Stop crafting" : "Stop · " + Mathf.RoundToInt(progress * 100f).ToString().PadLeft(3) + "%"
                        : wouldDisplace ? "Craft instead" : "Craft");
                    // Stopping is always allowed; starting needs the gates AND
                    // a batch of inputs in camp stock.
                    var ok = crafting || (_loop.IsRecipeWorkable(captured) && _loop.CanCraft(captured));
                    toggle.interactable = ok;
                    SetButtonTint(toggle, ok);
                });

                // The band alone walks per frame, like the trail's carrier dot.
                // The sim advances it every frame, so painting it on the page's
                // quarter-second cadence stepped it four times a second — a
                // stutter on the one thing whose whole job is to look like time
                // passing. Nothing else on the row moves with it: the words and
                // the plate's own count stay on the cadence, where they cost a
                // string a quarter-second rather than a string a frame.
                // A halted plate empties rather than freezing part-filled: a
                // band stopped at 12% looks like a slow batch, and the row has
                // already said "halted" in alarm ink.
                _frameUpdaters.Add(() =>
                {
                    // Reading the fraction costs a batch-time lookup, so an idle
                    // row is turned away on the cheap test first — every row on
                    // the card runs this, and at most a couple are working.
                    var running = _loop.IsCrafting(captured) && !_loop.IsCraftHalted(captured);
                    SetButtonFill(fill, running ? (float)_loop.CraftProgress(captured) : 0f);
                });
            }
        }

        /// <summary>
        /// One chip per input, and the tap that reads the whole bundle aloud.
        /// The chips carry what a batch COSTS and nothing else — what the camp
        /// holds is on the note line, one tap away, and in the row's own state
        /// line the moment a shortfall is what's stopping the work. A number
        /// that is only worth reading when it's short does not need to be on
        /// the page while it isn't.
        /// </summary>
        private CostStripBinding BuildCostStrip(Transform row, RecipeData recipe)
        {
            var binding = new CostStripBinding { Recipe = recipe, Captions = new List<Text>() };
            binding.Root = CostStrip(row, () => SetNote(CostReading(recipe)));
            foreach (var input in recipe.inputs)
            {
                CostChip(binding.Root.transform, ArtLibrary.ForGood(input.id), GoodName(input.id), out var caption);
                binding.Captions.Add(caption);
            }

            RefreshCostStrip(binding);
            return binding;
        }

        /// <summary>
        /// Repaint the chips: the numbers hold still, and their ink says whether
        /// the stores cover them. Ochre is the journal's "this is what's
        /// stopping you" everywhere else on the page, so a glance down a station
        /// card finds the short recipe without reading a word of it.
        /// </summary>
        private void RefreshCostStrip(CostStripBinding binding)
        {
            var inputs = binding.Recipe.inputs;
            for (var index = 0; index < binding.Captions.Count && index < inputs.Count; index++)
            {
                var input = inputs[index];
                var covered = _loop.State.GetResource(input.id) >= input.amount;
                var amount = PlainNumber(input.amount);
                binding.Captions[index].text = covered
                    ? amount
                    : "<color=" + OchreInkHex + "><b>" + amount + "</b></color>";
            }
        }

        /// <summary>The whole bundle said in words, for the note line the cost strip taps into.</summary>
        private string CostReading(RecipeData recipe)
        {
            var parts = new List<string>();
            foreach (var input in recipe.inputs)
            {
                parts.Add(PlainNumber(input.amount) + " " + GoodName(input.id)
                          + " (" + NumberFormat.Short(_loop.State.GetResource(input.id)) + " in the stores)");
            }

            return "a batch of " + GoodName(recipe.output) + " takes " + string.Join(", ", parts) + ".";
        }

        /// <summary>
        /// What the recipe is doing, in ONE line that is always present. Only
        /// the most pressing thing is said: a station can be short of two goods
        /// and under-levelled at once, and a row that listed all of it would be
        /// back to wrapping. The chips already mark every shortfall — this line
        /// is for the one the player would act on, and for the gates no chip can
        /// show.
        /// </summary>
        private string CraftStateLine(RecipeData recipe, bool crafting, bool halted)
        {
            if (halted)
            {
                // A band frozen part-way looked identical to a slow one. Say it
                // plainly, and name the good that stopped it.
                var missing = _loop.MissingCraftInput(recipe);
                return "<color=" + AlarmHex + "><b>halted</b>"
                       + (missing != null ? ", out of " + GoodName(missing) : string.Empty) + "</color>";
            }

            if (crafting)
            {
                return "<color=" + MossDeepHex + ">working</color>";
            }

            if (!_loop.IsRecipeLevelMet(recipe))
            {
                return "<color=" + OchreInkHex + "><b>needs " + recipe.skill + " " + recipe.skillLevel + "</b></color>";
            }

            if (!Crafting.StationLevelMet(_loop.State, _loop.Data, recipe))
            {
                // The one gate no chip can explain — a cold station looks ready
                // when every input is in stock.
                // No article of our own: the building line's display name
                // carries one ("The Fire"), and adding a second read "the the".
                return "<color=" + OchreInkHex + "><b>needs "
                       + CraftStationName(recipe.station).ToLowerInvariant()
                       + " at level " + recipe.stationLevel + "</b></color>";
            }

            foreach (var input in recipe.inputs)
            {
                var have = _loop.State.GetResource(input.id);
                if (have < input.amount)
                {
                    // What a batch costs is already on the chip below, in ink
                    // that has gone ochre — repeating it here said the same
                    // number twice and pushed the one thing this line adds,
                    // what the camp actually holds, to the end of a clause.
                    return "<color=" + OchreInkHex + ">need more " + GoodName(input.id)
                           + ", have " + NumberFormat.Short(have) + "</color>";
                }
            }

            // Why the plate below says "Craft instead" rather than "Craft" —
            // the warning is on the button, the reason for it is here.
            var displaced = _loop.CraftWouldDisplace(recipe);
            if (displaced != null)
            {
                // Not "the fire is working the jam" — the card this row sits on
                // is headed THE FIRE, and every recipe a row can displace is one
                // of its own. Naming the station again was a word the reader had
                // just read, and it doubled the article the display name already
                // carries ("The Fire" → "the the fire").
                return "busy with the " + GoodName(displaced.output);
            }

            return "ready";
        }

        /// <summary>A recipe's cost chips and the recipe they were built for, so the live pass can repaint them.</summary>
        private sealed class CostStripBinding
        {
            internal RecipeData Recipe;
            internal GameObject Root;
            internal List<Text> Captions;
        }

        private void BuildBuildingsCard()
        {
            var card = Card("BUILDING LINES");
            foreach (var building in _loop.Data.buildings)
            {
                var captured = building;
                // Filter to what the run can see, like the crafting card —
                // every line in the game data from minute one names resources
                // the player hasn't met. A line waits until every good its next
                // level asks for has been gathered.
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

        // The quality tiers a deal is answered at, in row order.
        private static readonly QualityTier[] ExchangeTiers =
        {
            QualityTier.Poor,
            QualityTier.Decent,
            QualityTier.Choice,
        };

        /// <summary>
        /// The portion chips, 200 apiece (was 170). MEASURED against
        /// IMFellEnglishSC at the 38px uGUI renders a 19pt label: "a quarter" is
        /// 158 where a 170 plate leaves 150 usable, so it wrapped to two lines
        /// beside a one-line "half" (71) and "all" (50) and the row sat at three
        /// different heights.
        /// <para>
        /// Worse, it wrapped ON SELECTION. SetButtonChosen switches the label to
        /// bold, which takes "a quarter" from 158 to 164, so the row could change
        /// height as the player tapped between the three. 184 is the floor that
        /// holds the widest chosen label; 200 leaves a little air. Three at 200
        /// plus spacing is 616, inside both a portrait card (~906) and a 4:3
        /// spread column (~748).
        /// </para>
        /// </summary>
        private const float ExchangeChipPlate = 200f;

        /// <summary>
        /// The trade plates run the card's full measure — this is the primary act
        /// and it earns the width.
        /// </summary>
        private const float ExchangeTradePlate = 800f;

        /// <summary>
        /// The consideration's plate, and the arrows on it — a square mark, not
        /// a line of the card. Every width this had before (800, then 320) was
        /// still a plate in a row of its own at the foot of the card, which gave
        /// a 5-amber re-draw a whole line of the measure the trades are asking
        /// for, and ended the card on a purchase rather than on the deal.
        /// </summary>
        private const float ExchangeConsiderationPlate = 120f;
        private const float ExchangeConsiderationGlyph = 56f;

        /// <summary>
        /// The corner the consideration stands in: the plate, and the price on
        /// the line under it. 160 wide so "25 amber" in small caps at 13 keeps
        /// one line beneath a 120 plate; the inset holds the pair off the top
        /// right of the caravan's own plate, so the mark reads as pinned to the
        /// picture rather than balanced on its edge.
        /// </summary>
        private const float ExchangeConsiderationMeasure = 160f;
        private const float ExchangeConsiderationInset = 10f;

        /// <summary>The traded goods' own plates, either side of the arrow on the deal row.</summary>
        private const float ExchangeGoodGlyph = 72f;

        /// <summary>
        /// The caravan (design §9): goods for goods, but the deal is the
        /// caravan's to name now — one give-good for one take-good, drawn from
        /// the wall-clock window and turning every few minutes. The player
        /// chooses only how much to answer with, at whichever quality tiers
        /// the camp holds of the asked good: Decent and Choice trade in at
        /// their §5 value multipliers and are always paid out in plain goods,
        /// which is finally an exit for the windfall pools.
        /// </summary>
        private void BuildExchangeCard()
        {
            var card = Card("THE EXCHANGE");
            MakeText(card, "<i>a caravan idles at the camp edge. it trades; it does not sell.</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);
            var caravan = ArtLibrary.ForJournal("caravan");
            // The plate is kept hold of because the consideration is pinned into
            // its corner — a 200-deep band of art with nothing else in it is the
            // one place on this card a mark can stand without landing on a line
            // of words. ArtLibraryTests pins the caravan, so the null arm is for
            // art not yet drawn rather than a state the game reaches.
            var caravanPlate = caravan != null ? PlateImage(card, caravan, 200f) : null;

            if (!Exchange.Configured(_loop.Data))
            {
                MakeText(card, "the caravan has not come this way.", 18, TextAnchor.MiddleCenter, Ink2);
                return;
            }

            var deal = MakeText(card, string.Empty, 21, TextAnchor.MiddleCenter, Ink, _serif);

            // The trade itself, in pictures: the asked good, the arrow, the paid
            // good. The card named the pair twice in words (the deal line above
            // and the rate line's "reeds → clay" below) and showed neither, while
            // the only picture on it was the caravan — decoration, the same
            // whatever is being traded. Both plates are guaranteed: the caravan
            // only ever offers a discovered resource or a "trade" recipe output
            // (Exchange.TradeableGoods), and ArtLibraryTests pins a plate to
            // every one of both. The sprites still swap on the refresh cadence,
            // because the deal turns on its own every few minutes.
            var goodsRow = Row(card);
            goodsRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var fromPlate = IconImage(goodsRow.transform, null, ExchangeGoodGlyph, Color.white)
                .GetComponent<Image>();
            var arrow = MakeText(goodsRow.transform, "→", 26, TextAnchor.MiddleCenter, Ink2, _serif);
            var toPlate = IconImage(goodsRow.transform, null, ExchangeGoodGlyph, Color.white)
                .GetComponent<Image>();

            var rate = MakeText(card, string.Empty, 16, TextAnchor.MiddleCenter, Ink2);

            System.Action refresh = null;
            var amountRow = Row(card);
            amountRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var chips = new List<(Button Plate, double Fraction)>();
            foreach (var amount in ExchangeAmounts)
            {
                var fraction = amount.Fraction;
                chips.Add((Button(amountRow.transform, amount.Label, ExchangeChipPlate, () =>
                {
                    _exchangeFraction = fraction;
                    refresh();
                }), fraction));
            }

            // One trade row per quality tier; a row only shows while the camp
            // holds that tier of the asked good, so most of the time this is
            // the one plain row it always was.
            var tierRows = new List<(QualityTier Quality, GameObject Row, Button Trade)>();
            foreach (var tier in ExchangeTiers)
            {
                var captured = tier;
                var row = Row(card);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                Button trade = null;
                trade = Button(row.transform, "Trade", ExchangeTradePlate, () => OfferTrade(trade, captured));
                if (captured == QualityTier.Poor)
                {
                    KeyAction(trade);
                }

                tierRows.Add((captured, row, trade));
            }

            var idle = MakeText(card, string.Empty, 18, TextAnchor.MiddleCenter, Ink2);

            // A consideration for the drover (design §9's sink slate): a
            // little amber and the deal re-draws now, never repeating itself.
            // The corner hides while the sink is unconfigured or no deal stands.
            var considerationCost = Mathf.FloorToInt((float)_loop.ConsiderationCost());
            GameObject considerationCorner = null;
            Button press = null;
            Image pressGlyph = null;
            Text pressCost = null;
            if (considerationCost > 0)
            {
                // Pinned into the top-right of the caravan's plate rather than
                // laid out with the rows: a reroll is a mark ON the deal, and
                // the card's own lines are the deal, the pair, the rate, the
                // portions and the trades. Anchored by hand the way the tile's
                // corner mark and the focus ring are; ignoreLayout is what keeps
                // it out of the flow in the plateless fallback, where the card
                // itself is the host.
                var cornerHost = caravanPlate != null ? (RectTransform)caravanPlate.transform : card;
                considerationCorner = MakeRect("Consideration", cornerHost).gameObject;
                considerationCorner.AddComponent<LayoutElement>().ignoreLayout = true;
                var cornerRect = (RectTransform)considerationCorner.transform;
                cornerRect.anchorMin = Vector2.one;
                cornerRect.anchorMax = Vector2.one;
                cornerRect.pivot = Vector2.one;
                cornerRect.anchoredPosition = new Vector2(-ExchangeConsiderationInset, -ExchangeConsiderationInset);
                cornerRect.sizeDelta = new Vector2(ExchangeConsiderationMeasure, 0f);
                var cornerLayout = considerationCorner.AddComponent<VerticalLayoutGroup>();
                cornerLayout.childControlWidth = true;
                cornerLayout.childControlHeight = true;
                cornerLayout.childForceExpandWidth = false;
                cornerLayout.childForceExpandHeight = false;
                cornerLayout.childAlignment = TextAnchor.UpperCenter;
                cornerLayout.spacing = 2;
                // Nothing outside sizes this rect — anchored to a point, its
                // height is its own to state, so the plate and the price both
                // have somewhere to stand.
                var cornerFitter = considerationCorner.AddComponent<ContentSizeFitter>();
                cornerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Two circular arrows, where a line of prose and a "Press" plate
                // used to sit. A reroll is the one idiom every player already
                // reads at a glance, and the words belong to the confirm sheet
                // anyway: that is the one that has to be sure before amber leaves
                // the pouch.
                press = GlyphButton(considerationCorner.transform, JournalSprites.RerollSprite(),
                    "Turn", ExchangeConsiderationPlate, ExchangeConsiderationGlyph, () =>
                {
                    // Amber is premium and hard-won — never spend it on a stray tap.
                    _hud.Sheets.OpenConfirmSheet(
                        "Spend " + considerationCost + " amber",
                        "Press a consideration on the drover, and the deal turns now?",
                        "Spend " + considerationCost + " amber",
                        () =>
                        {
                            var redealt = _loop.PressConsideration();
                            if (redealt != null)
                            {
                                SetNote("the drover pockets the resin and names another deal.");
                                _dirty = true;
                            }
                        });
                });

                // The arrows carry the dead-plate ink as well. SetButtonTint
                // reaches a plate, its rule and its label; a glyph left at full
                // strength is the one channel that would still read live.
                pressGlyph = press.transform.Find("Glyph").GetComponent<Image>();
                pressGlyph.color = Ink;

                // The price on the line after the arrows, outside the plate: it
                // is what the mark costs, not what it says. Ochre is the cost
                // ink every amber line on this page already wears — and it is
                // outside the button, so SetButtonTint never reaches it.
                pressCost = MakeText(considerationCorner.transform, considerationCost + " amber",
                    13, TextAnchor.MiddleCenter, Ochre, SmallCapsFont);
            }

            refresh = () =>
            {
                var offer = _loop.CurrentExchangeOffer();
                var open = offer != null;
                deal.gameObject.SetActive(open);
                goodsRow.SetActive(open);
                rate.gameObject.SetActive(open);
                amountRow.SetActive(open);
                if (considerationCorner != null)
                {
                    considerationCorner.SetActive(open);
                    if (open)
                    {
                        var canPress = _loop.CanPressConsideration();
                        press.interactable = canPress;
                        SetButtonTint(press, canPress);
                        pressGlyph.color = canPress ? Ink : Ink2;
                        pressCost.color = canPress ? Ochre : Ink2;
                    }
                }

                if (!open)
                {
                    foreach (var tierRow in tierRows)
                    {
                        tierRow.Row.SetActive(false);
                    }

                    idle.gameObject.SetActive(true);
                    idle.text = "gather more before the caravan will barter.";
                    return;
                }

                deal.text = "the caravan asks " + GoodName(offer.from) + ", and pays in " + GoodName(offer.to);

                // A plate with no sprite draws a plain white square, so the
                // picture is switched off rather than emptied — the same guard
                // the post plates use for content added ahead of its art.
                var fromArt = ArtLibrary.ForGood(offer.from);
                var toArt = ArtLibrary.ForGood(offer.to);
                fromPlate.sprite = fromArt;
                fromPlate.enabled = fromArt != null;
                toPlate.sprite = toArt;
                toPlate.enabled = toArt != null;
                arrow.gameObject.SetActive(fromArt != null || toArt != null);

                // Per-unit, the caravan's cut, and the deal's clock — this card
                // is the game's only price signal, and a deal that turns on its
                // own must say when.
                //
                // Two lines, and both changes are about a measure that would not
                // hold. The good names left it because the plates above and the
                // deal line already say the pair twice; that alone took the line
                // from 1207px to 882 against a ~906 portrait measure, which is
                // 2px of slack at the widest real values ("123.46 each · the
                // caravan keeps 15% · a new deal in 12m 34s" is 904) — a line
                // that wraps for SOME deals and not others, which reads worse
                // than one that always did. So the clock, the only fact here
                // that moves every second, takes the second line: 543 and 318 at
                // their widest, inside a 4:3 spread column's ~748 as well.
                rate.text = NumberFormat.Rate(_loop.ExchangeRate(offer.from, offer.to)) + " each"
                            + "  ·  <color=" + OchreInkHex + ">the caravan keeps "
                            + Percent(_loop.Data.exchange.spread) + "</color>"
                            + "\n" + "a new deal in " + NumberFormat.Duration(_loop.ExchangeOfferSecondsRemaining());

                foreach (var chip in chips)
                {
                    SetButtonChosen(chip.Plate, System.Math.Abs(chip.Fraction - _exchangeFraction) < 0.001);
                }

                var anyHeld = false;
                foreach (var (quality, row, trade) in tierRows)
                {
                    var held = Exchange.Held(_loop.State, offer.from, quality);

                    // A whole unit is the smallest thing the caravan will take,
                    // so a sub-unit crumb shows no row at all rather than one
                    // reading "0" beside a dead button.
                    var show = held >= BigDouble.One;
                    row.SetActive(show);
                    if (!show)
                    {
                        continue;
                    }

                    anyHeld = true;
                    var spend = Exchange.Portion(held, _exchangeFraction);
                    var got = _loop.ExchangeQuote(offer.from, offer.to, spend, quality);

                    // And a whole unit is the smallest thing it will pay: a
                    // deal that comes back under one would read as "→ 0".
                    var live = got >= BigDouble.One;
                    trade.interactable = live;
                    SetButtonTint(trade, live, true);
                    SetButtonLabel(trade, "Trade " + ExchangeDeal(offer, quality, spend, got)
                                          + (quality == QualityTier.Poor
                                              ? string.Empty
                                              : "\n" + SizeOpen(14) + TierName(quality).TrimEnd() + " trades in at ×"
                                                + PlainNumber(Exchange.QualityValueMultiplier(_loop.Data, quality)) + "</size>"));
                }

                idle.gameObject.SetActive(!anyHeld);
                idle.text = "no " + GoodName(offer.from) + " to give. the deal turns on its own; wait it out.";
            };

            refresh();
            _liveUpdaters.Add(refresh);
        }

        /// <summary>"decent " / "choice " — the tier as the deal speaks it; plain goods go unmarked.</summary>
        private static string TierName(QualityTier quality)
        {
            switch (quality)
            {
                case QualityTier.Decent:
                    return "decent ";
                case QualityTier.Choice:
                    return "choice ";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// "120 decent berries → 18 wildflowers". Whole units on both sides,
        /// like every other resource readout in the journal — the caravan is the
        /// one card whose rates would otherwise speak in halves and thirds, and
        /// a choice pile trading in at ×2.5 makes a meal of it.
        /// </summary>
        private string ExchangeDeal(ExchangeOffer offer, QualityTier quality, BigDouble spend, BigDouble got)
        {
            return NumberFormat.Short(spend) + " " + TierName(quality) + GoodName(offer.from)
                   + " → " + NumberFormat.Short(got) + " " + GoodName(offer.to);
        }

        /// <summary>
        /// Strike the deal — or, for the whole stock, ask first. Amber asks
        /// before it's spent for the same reason: emptying a good the camp has
        /// been gathering all session is not a thing to do by accident.
        /// </summary>
        private void OfferTrade(Button trade, QualityTier quality)
        {
            var offer = _loop.CurrentExchangeOffer();
            if (offer == null)
            {
                return;
            }

            var spend = Exchange.Portion(Exchange.Held(_loop.State, offer.from, quality), _exchangeFraction);
            var got = _loop.ExchangeQuote(offer.from, offer.to, spend, quality);
            if (got < BigDouble.One)
            {
                return;
            }

            if (_exchangeFraction < 1.0)
            {
                CommitTrade(trade, offer, quality, spend);
                return;
            }

            // Choice finds have two other suitors — the folio's pages and the
            // rite's specimen slots — so emptying that pool warns of both.
            var caution = quality == QualityTier.Choice
                ? " the folio and the rite ask for choice finds too."
                : string.Empty;
            _hud.Sheets.OpenConfirmSheet(
                "Trade all your " + TierName(quality) + GoodName(offer.from) + "?",
                ExchangeDeal(offer, quality, spend, got) + ", and the trail starts that pile again." + caution,
                "Trade all",
                () => CommitTrade(trade, offer, quality, spend));
        }

        private void CommitTrade(Button trade, ExchangeOffer offer, QualityTier quality, BigDouble spend)
        {
            // Nothing captured when the confirm opened is taken on trust here.
            // The sheet can sit open while the caravan turns its deal over and the
            // camp goes on working, and TryTrade checks the pile but never the
            // standing offer — so an expired pair would still be traded, and a
            // spend larger than the pile just answers zero, which read as a
            // confirmed trade that quietly did nothing.
            var current = _loop.CurrentExchangeOffer();
            if (current == null || current.from != offer.from || current.to != offer.to)
            {
                SetNote("the caravan had already turned that deal over. nothing traded.");
                _dirty = true;
                return;
            }

            // Never more than was quoted (the pile may have grown since), never
            // nothing merely because it shrank.
            var held = Exchange.Held(_loop.State, offer.from, quality);
            if (held < spend)
            {
                spend = held;
            }

            // Quoted before it is struck: a pile that shrank far enough for the
            // deal to come back under a whole unit is refused outright, rather
            // than traded away for a flash reading "+0".
            var got = _loop.ExchangeQuote(offer.from, offer.to, spend, quality) >= BigDouble.One
                ? _loop.TradeAtExchange(offer.from, offer.to, spend, quality)
                : BigDouble.Zero;
            if (got > BigDouble.Zero)
            {
                Flash(trade, "+" + NumberFormat.Short(got) + " " + GoodName(offer.to), true);
                SetNote("traded " + TierName(quality) + GoodName(offer.from) + " for " + GoodName(offer.to)
                        + ". a nod. gone before the count.");
            }
            else
            {
                SetNote("that pile was spoken for before the deal was struck. nothing traded.");
            }

            _dirty = true;
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
                        SetNote(captured.displayName.ToLowerInvariant() + ": the work changes shape.");
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
                             && _loop.MeetsUpgradeSkillGate(captured) && string.IsNullOrEmpty(_loop.MissingToolTier(captured))
                             && _loop.FoldsUntilUpgrade(captured) == 0;
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }
    }
}
