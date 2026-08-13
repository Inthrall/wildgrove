using System.Collections.Generic;
using BreakInfinity;
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
    /// The Stores page — what the camp holds right now, drawn as a drawer of
    /// square plates rather than a list of names and numerals.
    /// <para>
    /// The Compendium keeps the record; this page keeps the stock. Held figures
    /// belong here rather than as a clause per Compendium entry beside the
    /// lifetime tallies — one page answering both <em>what have I ever
    /// found</em> and <em>what can I spend</em> buries the second, which is the
    /// one a player asks mid-craft, three pages back among entries they were not
    /// looking for.
    /// </para>
    /// <para>
    /// Two cards act on the drawer rather than reading it: the bottles brewed
    /// from it (<see cref="BuildBrewsCard"/>) and the caravan that trades it
    /// (<c>StoresPage.Exchange</c>, the Camp page's until 2026-08-13). Both came
    /// here from elsewhere, and for the same reason: what a card is ABOUT is
    /// which page it belongs on.
    /// </para>
    /// <para>
    /// One tile per <em>stack</em>, not per resource: a resource held Poor,
    /// Decent and Choice at once is three tiles, because the three pools are
    /// spent on different things and a single tile could only ever wear one
    /// border. The border IS the grade (design §5) — white, green, blue — and
    /// it is the only place in the journal the three are told apart at a
    /// glance.
    /// </para>
    /// </summary>
    internal sealed partial class StoresPage : JournalSection
    {
        internal StoresPage(GameHud hud) : base(hud) { }

        /// <summary>The tile the grid aims for, and the floor for its caption strip.</summary>
        private const float IdealTile = 190f;
        private const float CaptionHeight = 46f;

        /// <summary>The grade rule's drawn weight — matches the thickness baked into <see cref="JournalSprites.GradeBorderSprite"/>.</summary>
        private const float RuleWeight = 8f;

        internal void BuildStoresPage()
        {
            // The two cards that DO something lead the page, and the drawer they
            // act on follows: it grows a tile per stack, so anything under it
            // drifts further out of reach with every zone the trail opens.
            BuildBrewsCard();
            BuildExchangeCard();
            BuildStockCard();
        }

        // ── The drawer ────────────────────────────────────────────────────

        /// <summary>
        /// Every stack the camp holds, in data order so the drawer does not
        /// reshuffle itself under the player's thumb as things are gathered
        /// and spent — a grid that reorders is a grid nobody learns.
        /// </summary>
        private void BuildStockCard()
        {
            var card = Card("THE STORES");
            MakeText(card, "<i>what the ground gave and the fire made</i>",
                15, TextAnchor.MiddleCenter, Ink2, _serif);

            var grid = Grid(card, IdealTile);
            var empty = MakeText(card, "<i>the stores are bare. work a node and the drawer fills.</i>",
                17, TextAnchor.MiddleCenter, Ink2, _serif);

            // Tiles are built once, for every id the run could ever hold, and
            // then shown or hidden as the pools move. Rebuilding the grid as
            // stock came and went would destroy the tile under a finger — and
            // the journal only rebuilds a page when its STRUCTURE changes,
            // which a changing number is not.
            var tiles = new List<StackTile>();
            foreach (var stack in StockOrder())
            {
                tiles.Add(BuildTile(grid, stack.id, Grade.Poor));
                if (!stack.gradeable)
                {
                    continue;
                }

                // Only what the ground gives is ever graded — the quality roll
                // is per delivery batch at a node (Simulation), and a crafted
                // batch lands whole in the plain stores. Tiles for grades a
                // crafted good can never hold would be furniture that never
                // once appears.
                tiles.Add(BuildTile(grid, stack.id, Grade.Decent));
                tiles.Add(BuildTile(grid, stack.id, Grade.Choice));
            }

            _liveUpdaters.Add(() =>
            {
                var held = 0;
                foreach (var tile in tiles)
                {
                    held += tile.Refresh(_loop.State) ? 1 : 0;
                }

                empty.gameObject.SetActive(held == 0);
            });
        }

        // The drawer carries no prose about what the grades are FOR. The
        // Compendium's "decent finds are kept apart from the stores; a verse
        // sometimes asks for one" came across with the pools and went again
        // (2026-08-04): a paragraph explaining a border is bookkeeping, and it
        // was the longest thing on a page whose whole argument is that plates
        // say it faster than words. What a decent find buys is learned where
        // it is spent — at the verse that asks for one.

        /// <summary>
        /// Every id the drawer can show, in a fixed order: the gatherables as
        /// the data lists them, then the crafted goods as the recipes make
        /// them. Tinctures are held out — they are goods, so they would
        /// otherwise appear twice, and their own card above is where they can
        /// be drunk.
        /// </summary>
        private List<(string id, bool gradeable)> StockOrder()
        {
            var order = new List<(string, bool)>();
            var seen = new HashSet<string>();
            foreach (var tincture in _loop.Data.tinctures ?? new List<TinctureData>())
            {
                seen.Add(tincture.id);
            }

            foreach (var resource in _loop.Data.resources ?? new List<ResourceData>())
            {
                if (seen.Add(resource.id))
                {
                    order.Add((resource.id, true));
                }
            }

            foreach (var recipe in _loop.Data.recipes ?? new List<RecipeData>())
            {
                if (!string.IsNullOrEmpty(recipe.output) && seen.Add(recipe.output))
                {
                    order.Add((recipe.output, false));
                }
            }

            return order;
        }

        // ── The brews ─────────────────────────────────────────────────────

        /// <summary>
        /// The tinctures (design §5, Apothecary), moved here from the Warden
        /// page: a bottle on a shelf is stock, and it was the one thing the
        /// warden "wore" that could be counted, spent and run out. Hidden until
        /// the craft is learned (the Mistfen map teaches it). Brewing still
        /// happens at the fire with the other recipes; this is the drinking.
        /// </summary>
        private void BuildBrewsCard()
        {
            if (_loop.Data.tinctures == null || _loop.Data.tinctures.Count == 0
                || !Upgrades.UnlockedSkills(_loop.State, _loop.Data).Contains("apothecary"))
            {
                return;
            }

            var card = Card("THE TINCTURES");
            MakeText(card, "<i>tap a bottle to drink it</i>", 15, TextAnchor.MiddleCenter, Ink2, _serif);

            var grid = Grid(card, IdealTile);
            foreach (var tincture in _loop.Data.tinctures)
            {
                BuildBrewTile(grid, tincture);
            }
        }

        private void BuildBrewTile(RectTransform grid, TinctureData tincture)
        {
            var tile = Tile(grid, ArtLibrary.ForGood(tincture.id), GradePoor, out var caption, out var rule);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                // Every tap reads the bottle aloud, the way the drawer's plates
                // do, because the tile is unlabelled art and a bottle is the
                // one plate here that a tap SPENDS. "drunk" alone answers that
                // something happened and not what: the buff is a number on
                // nodes three pages away, and by the time the player goes
                // looking, the shelf is one emptier and cannot be asked again.
                if (_loop.CanDrinkTincture(tincture))
                {
                    _loop.DrinkTincture(tincture);
                    Flash(tile, "drunk", true);
                    // Read AFTER the drink: a second bottle banks its duration
                    // on top, so the clock is the only honest total.
                    SetNote(BrewReading(tincture, "drunk. "
                        + NumberFormat.Duration(_loop.TinctureRemainingSeconds(tincture)) + " to run"));
                    return;
                }

                // A drink is refused for one reason, a bare shelf. Whether the
                // last bottle is still working changes what to do about it, not
                // why this tap did nothing.
                var live = _loop.TinctureRemainingSeconds(tincture);
                SetNote(BrewReading(tincture, live > 0.0
                    ? "none in stock. the one drunk has " + NumberFormat.Duration(live) + " to run"
                    : "none brewed. it is cooked at the fire with the other recipes"));
            });

            _liveUpdaters.Add(() =>
            {
                var remaining = _loop.TinctureRemainingSeconds(tincture);
                var have = _loop.State.GetResource(tincture.id);
                // A live brew turns its own rule green rather than growing a
                // second ring outside the first — at grade weight two rules
                // touch. The brews card carries no grades, so green cannot be
                // misread as "decent" here; it is just the working colour.
                rule.color = remaining > 0.0 ? GradeDecent : GradePoor;
                caption.text = remaining > 0.0
                    ? "<color=" + MossDeepHex + ">" + NumberFormat.Duration(remaining) + "</color>"
                    : (have > BigDouble.Zero
                        ? "<b>" + NumberFormat.Short(have) + "</b>"
                        : "<color=" + Ink2Hex + ">·</color>");
                SetTilePaper(tile, have > BigDouble.Zero || remaining > 0.0);
            });
        }

        /// <summary>
        /// The bottle said in words: which one it is, what drinking it does, and
        /// where the tap left it. The middle clause is the bottle's authored
        /// line (tinctures.json), which the data validator has always insisted
        /// on and the journal had nowhere to show.
        /// </summary>
        private static string BrewReading(TinctureData tincture, string outcome)
        {
            return tincture.displayName + "  ·  " + tincture.description + "  ·  " + outcome;
        }

        // ── Tiles ─────────────────────────────────────────────────────────

        private enum Grade
        {
            Poor,
            Decent,
            Choice
        }

        /// <summary>
        /// One drawer tile, bound to one id and one grade. Holds its own
        /// refresh so the page's live pass is a loop over tiles rather than a
        /// closure per tile per pool.
        /// </summary>
        private sealed class StackTile
        {
            internal string Id;
            internal Grade Grade;
            internal RectTransform Root;
            internal Text Caption;

            /// <summary>Show or hide the tile for what the state now holds. Returns true when it is on the page.</summary>
            internal bool Refresh(GameState state)
            {
                var held = Held(state, Id, Grade);
                var any = held > BigDouble.Zero;
                if (Root.gameObject.activeSelf != any)
                {
                    Root.gameObject.SetActive(any);
                }

                if (any)
                {
                    Caption.text = "<b>" + NumberFormat.Short(held) + "</b>";
                }

                return any;
            }

            private static BigDouble Held(GameState state, string id, Grade grade)
            {
                switch (grade)
                {
                    case Grade.Decent:
                        return state.GetDecent(id);
                    case Grade.Choice:
                        return state.GetChoice(id);
                    default:
                        return state.GetResource(id);
                }
            }
        }

        private StackTile BuildTile(RectTransform grid, string id, Grade grade)
        {
            var plate = ArtLibrary.ForGood(id);
            var tile = Tile(grid, plate, BorderInk(grade), out var caption, out _);
            tile.gameObject.SetActive(false);

            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile.GetComponent<Image>();
            // Tapping reads the stack aloud rather than doing anything to it.
            // A drawer of plates is unlabelled by design — the plate is the
            // name — so there has to be somewhere the name still exists, and
            // the note line is where the journal already says small things.
            button.onClick.AddListener(() => SetNote(StackReading(id, grade)));

            return new StackTile { Id = id, Grade = grade, Root = tile, Caption = caption };
        }

        /// <summary>The stack said in words: what it is, which grade, and what the book has ever recorded of it.</summary>
        private string StackReading(string id, Grade grade)
        {
            var lifetime = Compendium.LifetimeGathered(_loop.State, id);
            var everSaid = lifetime > BigDouble.Zero
                ? "  ·  " + NumberFormat.Short(lifetime) + " gathered in all"
                : string.Empty;
            return GoodName(id) + "  ·  " + GradeWord(grade) + everSaid;
        }

        private static string GradeWord(Grade grade)
        {
            switch (grade)
            {
                case Grade.Decent:
                    return "decent";
                case Grade.Choice:
                    return "choice";
                default:
                    return "plain";
            }
        }

        private static Color BorderInk(Grade grade)
        {
            switch (grade)
            {
                case Grade.Decent:
                    return GradeDecent;
                case Grade.Choice:
                    return GradeChoice;
                default:
                    return GradePoor;
            }
        }

        // ── Furniture ─────────────────────────────────────────────────────

        /// <summary>
        /// One square: the plate filling it, a grade rule around it, and a
        /// caption strip along the bottom inside edge carrying the count. The
        /// strip is inside the square on purpose — a label under the tile
        /// would make the cell oblong and the drawer ragged.
        /// </summary>
        private static RectTransform Tile(RectTransform grid, Sprite plate, Color border,
            out Text caption, out Image rule)
        {
            var go = MakePanel("Tile", grid, DeepPaper);
            var tile = (RectTransform)go.transform;

            if (plate != null)
            {
                var image = new GameObject("Plate", typeof(Image));
                image.transform.SetParent(tile, false);
                var art = image.GetComponent<Image>();
                art.sprite = plate;
                art.preserveAspect = true;
                art.raycastTarget = false;
                var rect = (RectTransform)image.transform;
                Stretch(rect);
                // Inside the rule, and clear of the caption strip below — a
                // plate read through either is a plate read twice.
                rect.offsetMin = new Vector2(RuleWeight + 3f, CaptionHeight);
                rect.offsetMax = new Vector2(-(RuleWeight + 3f), -(RuleWeight + 3f));
            }

            var strip = MakePanel("Caption", tile, CardPaper);
            var stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = new Vector2(RuleWeight, RuleWeight);
            stripRect.offsetMax = new Vector2(-RuleWeight, CaptionHeight - RuleWeight);
            strip.GetComponent<Image>().raycastTarget = false;

            caption = MakeText(strip.transform, string.Empty, 19, TextAnchor.MiddleCenter, Ink);
            Stretch((RectTransform)caption.transform);
            caption.raycastTarget = false;

            // The grade rule, on the thick sprite: this border is the only
            // thing telling the three pools apart, so it cannot be the card
            // hairline. White especially — a 2-unit white line on tile
            // paper is not a border, it is a rendering artefact.
            rule = AddBorder(go, border, 0f, JournalSprites.GradeBorderSprite()).GetComponent<Image>();
            return tile;
        }

        /// <summary>Dim a bottle the warden has none of, without dimming it out of legibility.</summary>
        private static void SetTilePaper(RectTransform tile, bool stocked)
        {
            tile.GetComponent<Image>().color = stocked ? DeepPaper : MossWash;
        }
    }
}
