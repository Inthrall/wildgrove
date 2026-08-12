using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// A card per crafting station, and the recipe rows inside them: the cost
    /// strip that keeps itself current, and the line that says whether a craft is
    /// running, halted, or waiting on something not yet found.
    /// </summary>
    internal sealed partial class CampPage
    {
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
    }
}
