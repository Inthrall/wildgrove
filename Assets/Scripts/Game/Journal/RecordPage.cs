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
    /// The Record page — the journal's back pages: the compendium of recorded
    /// resources, the folio's pressed spreads, the deep pages of insects, and
    /// the almanac's cross-Migration learnings.
    /// <para>
    /// The page is drawn in PLATES, not in names (design §6: every collectible
    /// is a plate). It was a column of eighty-odd text rows, which is a strange
    /// shape for the one page in the book whose whole subject is a collection of
    /// pictures — the Stores drawer made the same move for stock on 2026-08-04
    /// and the argument is the stronger here, because a drawer is a tally and a
    /// record is the thing itself.
    /// </para>
    /// <para>
    /// And what only records now FOLDS (see <see cref="JournalCardFolds"/>),
    /// which is the Trail's rule for its grounds applied to the longer page.
    /// The Almanac alone leads and stays open, because it is where a fold's
    /// Verdure is spent and a fold is what sends anyone here. The Folio folded
    /// in with the rest on 2026-08-14: pressing a spread is done when a Choice
    /// find turns up, not on the way past nine spreads of plates.
    /// </para>
    /// </summary>
    internal sealed class RecordPage : JournalSection
    {
        internal RecordPage(GameHud hud) : base(hud) { }

        /// <summary>The tile a collection's drawer aims for — the Stores drawer's square, so the two read as one kind of thing.</summary>
        private const float RecordTile = 190f;

        /// <summary>The plate on a pressing row, and the empty frame standing for a plate not yet drawn.</summary>
        private const float RowPlate = 56f;

        /// <summary>One sketched portion, as a mark.</summary>
        private const float SketchMark = 18f;

        /// <summary>
        /// A specimen a spread still wants, drawn as its plate at a fifth of its
        /// ink — pencilled in rather than pressed. Alpha rather than a paler
        /// colour, because these are full-colour naturalist plates and
        /// multiplying one by a wash turns it a different colour instead of a
        /// fainter one.
        /// </summary>
        private static readonly Color GhostPlate = new Color(1f, 1f, 1f, 0.22f);

        internal void BuildRecordPage()
        {
            BuildWrittenCard();
            BuildAlmanacCard();
            BuildFolioCard();
            BuildCompendiumCard();
            BuildDeepPagesCard();
            BuildWheelCard();
            BuildStandingCard();
            BuildInsideCoverCard();
        }

        /// <summary>
        /// How much of the book is written — one figure for the whole pane,
        /// first thing on it. Every card below counts its own kind (entries
        /// recorded, specimens pressed, plates drawn), so the page could say how
        /// each collection was going without ever answering the question a
        /// collector actually asks.
        /// <para>
        /// It reads as the page's index now that the record cards fold: the
        /// figure at the top, and four heads under it each carrying its own
        /// tally.
        /// </para>
        /// </summary>
        private void BuildWrittenCard()
        {
            var card = Card(null);
            var reading = MakeText(card, string.Empty, 20, TextAnchor.MiddleCenter, Ink, _serif);
            var detail = MakeText(card, string.Empty, 15, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                var (recorded, total) = Compendium.RecordProgress(_loop.State, _loop.Data);
                reading.text = "This book is " + (total > 0 ? Percent(recorded / (double)total) : "0%") + " written";
                detail.text = recorded + " of " + total + " across these pages";
            });
        }

        // ── The Compendium ────────────────────────────────────────────────

        /// <summary>
        /// Every gatherable the book has met, as a drawer of plates. One tile
        /// per entry, the lifetime tally in its caption, and the entry read
        /// aloud on a tap — the drawer's own idiom (a plate is the name, and the
        /// note line is where the journal says small things).
        /// <para>
        /// The undiscovered keep the policy the list had, drawn instead of
        /// written: a resource whose node is on the trail gets a BLANK tile, so
        /// one glance says how many finds this ground still holds, where N
        /// identical "… something on the trail, not yet gathered …" lines said
        /// it N times and could not be told apart. Anything further out still
        /// folds into a count, so a far-zone name cannot leak.
        /// </para>
        /// </summary>
        private void BuildCompendiumCard()
        {
            var discovered = Compendium.DiscoveredCount(_loop.State, _loop.Data);
            var total = Compendium.TotalEntries(_loop.Data);
            var card = FoldingCard(JournalCardFolds.Compendium, "THE COMPENDIUM",
                discovered + " of " + total + " recorded", out var open, out var heading);
            AddHeadingMark(heading, ArtLibrary.ForJournal("compendium"));
            if (!open)
            {
                return;
            }

            MakeText(card, discovered + " of " + total + " recorded", 18, TextAnchor.MiddleCenter, Ink2);

            var onTheTrail = new HashSet<string>();
            foreach (var node in _loop.State.nodes)
            {
                onTheTrail.Add(node.resourceId);
            }

            var grid = Grid(card, RecordTile);
            var beyondTheTrail = 0;
            foreach (var resource in _loop.Data.resources)
            {
                var captured = resource;
                if (Compendium.IsResourceDiscovered(_loop.State, captured.id))
                {
                    BuildFindTile(grid, ArtLibrary.ForResource(captured.id),
                        () => Compendium.LifetimeGathered(_loop.State, captured.id),
                        () => FindReading(captured.id));
                    continue;
                }

                if (onTheTrail.Contains(resource.id))
                {
                    BuildEmptyTile(grid, "something on the trail the book has never been shown.");
                    continue;
                }

                beyondTheTrail++;
            }

            if (beyondTheTrail > 0)
            {
                MakeText(card, "<i>…and " + beyondTheTrail + " more beyond the trail.</i>", 18, TextAnchor.MiddleLeft, Ink2);
            }

            // The "what decent is for" note went to the Stores drawer with the
            // pools it explains — a green border needs the note beside it, and
            // here it sat under lifetime tallies that never mention a grade.

            BuildCompendiumCrafts(card);
        }

        /// <summary>
        /// The Compendium's second drawer — what the warden has ever made. The
        /// card's head counts recipes among its entries (see
        /// <see cref="Compendium.TotalEntries"/>), but only the gatherables were
        /// ever listed, so a third of the tally had no page: lifetimeCrafted
        /// climbed for the whole life of a save and fed nothing but achievements
        /// and Game Stats. Undiscovered recipes fold into one count, the same
        /// way the finds above do, so a far-zone recipe name can't leak — and
        /// unlike a gatherable there is no blank tile for one, because a recipe
        /// is not standing on ground the warden can see.
        /// </summary>
        private void BuildCompendiumCrafts(RectTransform card)
        {
            if (_loop.Data.recipes == null || _loop.Data.recipes.Count == 0)
            {
                return;
            }

            MakeHairline(card);
            MakeText(card, "THE CRAFTS", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);

            var grid = Grid(card, RecordTile);
            var unmade = 0;
            foreach (var recipe in _loop.Data.recipes)
            {
                if (!Compendium.IsRecipeDiscovered(_loop.State, recipe.id))
                {
                    unmade++;
                    continue;
                }

                var captured = recipe;
                // Batches, not units — a batch is the thing the station
                // finishes, and it's what the skill gates are counted in
                // (forgecraft 40 is ~1770 of them).
                BuildFindTile(grid, ArtLibrary.ForGood(captured.output),
                    () => new BigDouble(Compendium.LifetimeCrafted(_loop.State, captured.id)),
                    () => CraftReading(captured.id));
            }

            if (unmade > 0)
            {
                MakeText(card, "<i>…and " + unmade + " never yet made.</i>", 18, TextAnchor.MiddleLeft, Ink2);
            }
        }

        /// <summary>
        /// One entry in a collection's drawer: its plate, a figure in the caption
        /// strip, and the whole entry read aloud on a tap.
        /// <para>
        /// The caption is always the same fact — how many have ever passed
        /// through the camp — which is what lets a plate-less entry be told from
        /// an undiscovered one at a glance: an empty tile carrying a number has
        /// been recorded, an empty tile carrying nothing has not.
        /// </para>
        /// </summary>
        private void BuildFindTile(RectTransform grid, Sprite plate,
            System.Func<BigDouble> tally, System.Func<string> reading)
        {
            PlateTile(grid, plate, string.Empty, null, () => SetNote(reading()),
                out var caption, out _, out _);

            // The drawer's caption is a FIGURE here, not a name — the tile's
            // sizing is built around two lines of a companion's name, and a
            // lifetime tally read in that hand beside the Stores drawer's own
            // count looked like a footnote to it rather than the same fact.
            caption.fontSize = 19;
            caption.font = _font;
            _liveUpdaters.Add(() => caption.text = "<b>" + NumberFormat.Short(tally()) + "</b>");
        }

        /// <summary>
        /// A slot in the drawer the book has never filled: the square, ruled in
        /// the faint hand rather than in ink, and nothing in it. Nothing names it
        /// — this page has never said what waits on ground the warden hasn't
        /// worked — and the tap says only that much.
        /// </summary>
        private void BuildEmptyTile(RectTransform grid, string note)
        {
            var tile = PlateTile(grid, null, string.Empty, null, () => SetNote(note),
                out _, out _, out var rule);
            // The rule is the whole difference between an empty slot and a
            // recorded one whose plate hasn't been drawn yet, so it carries the
            // faint hand every unearned thing in the journal wears.
            rule.color = RulePaper;
        }

        /// <summary>The entry said in words: what it is, and what the book has ever recorded of it.</summary>
        private string FindReading(string resourceId)
        {
            var everChoice = Compendium.LifetimeChoice(_loop.State, resourceId);
            var choiceEver = everChoice > BigDouble.Zero
                ? ",  " + NumberFormat.Short(everChoice) + " of them choice"
                : string.Empty;
            // Lifetime only. The three held figures ran here too until the
            // Stores drawer took them (2026-08-04) — one entry then answered
            // both "what have I ever found" and "what can I spend", and neither
            // legibly. This page is the record; the drawer is the stock.
            return GoodName(resourceId) + "  ·  lifetime "
                   + NumberFormat.Short(Compendium.LifetimeGathered(_loop.State, resourceId)) + choiceEver;
        }

        private string CraftReading(string recipeId)
        {
            var batches = Compendium.LifetimeCrafted(_loop.State, recipeId);
            return GoodName(recipeId) + "  ·  " + NumberFormat.Short(new BigDouble(batches))
                   + (batches == 1.0 ? " batch made" : " batches made");
        }

        // ── The Folio ─────────────────────────────────────────────────────

        private void BuildFolioCard()
        {
            if (_loop.Data.folioSpreads == null || _loop.Data.folioSpreads.Count == 0)
            {
                return;
            }

            var complete = 0;
            foreach (var spread in _loop.Data.folioSpreads)
            {
                if (Folio.IsSpreadComplete(_loop.State, spread))
                {
                    complete++;
                }
            }

            var card = FoldingCard(JournalCardFolds.Folio, "THE FOLIO",
                complete + " of " + _loop.Data.folioSpreads.Count + " spreads pressed",
                out var open, out var heading);
            AddHeadingMark(heading, ArtLibrary.ForJournal("folio"));
            if (!open)
            {
                return;
            }

            MakeText(card, "<i>Press finds to keep a permanent record</i>", 16, TextAnchor.MiddleCenter, Ink2, _serif);

            foreach (var spread in _loop.Data.folioSpreads)
            {
                BuildFolioSpread(card, spread);
            }

            BuildFolioPressings(card);
        }

        /// <summary>
        /// One spread, drawn as the spread it is: the specimens it asks for as a
        /// strip of plates, in full ink where one has been pressed and pencilled
        /// in where the page is still bare.
        /// <para>
        /// It was the line "Meadow Blooms  2 of 3 pressed", which is the one
        /// thing a collector cannot act on — it never said WHICH of the three
        /// the page still wants, and the held-Choice rows below answered a
        /// different question (what the drawer holds, not what the page asks).
        /// Drawn, the question answers itself and the count is redundant.
        /// </para>
        /// <para>
        /// It also says what the spread GIVES, which nothing here ever did. The
        /// card asks the warden to consume a Choice find forever — the
        /// windfall's third fork, against the Exchange and the Rite (design §6)
        /// — and priced it at nothing but a name. The Almanac card below carries
        /// its own line for exactly this reason.
        /// </para>
        /// <para>
        /// Settled at build time, not on the cadence: a press moves
        /// <c>fixedResources</c>, which is in the page's structure signature, so
        /// the whole card is rebuilt the moment any of this changes.
        /// </para>
        /// </summary>
        private void BuildFolioSpread(RectTransform card, FolioSpreadData spread)
        {
            MakeHairline(card);
            var done = Folio.IsSpreadComplete(_loop.State, spread);
            var mark = done
                ? "  " + SizeOpen(15) + "<color=" + MossDeepHex + ">complete</color></size>"
                : string.Empty;
            MakeText(card, "<b>" + spread.displayName + "</b>" + mark, 18, TextAnchor.MiddleLeft, Ink);

            var gives = EffectsLabel(spread.effects);
            if (gives.Length > 0)
            {
                MakeText(card, SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>",
                    15, TextAnchor.MiddleLeft, Ink2);
            }

            // Tapped as a whole rather than plate by plate — a single chip is
            // well under a fingertip — and what it says is the names, which is
            // the one thing a strip of pictures cannot carry.
            var strip = CostStrip(card, () => SetNote(SpreadReading(spread)));
            foreach (var entry in spread.entries)
            {
                BuildSpreadSpecimen(strip.transform, entry);
            }
        }

        /// <summary>One specimen in a spread's strip: pressed in full ink, wanted in pencil.</summary>
        private void BuildSpreadSpecimen(Transform strip, string resourceId)
        {
            var plate = ArtLibrary.ForResource(resourceId);
            if (plate == null)
            {
                // Keep the specimen's room in the strip so the row still counts
                // what the spread asks for. An Image holding no sprite draws a
                // plain white square, which is why this isn't a tinted glyph.
                PictureSlot(strip, null, CostChipGlyph);
                return;
            }

            IconImage(strip, plate, CostChipGlyph,
                Folio.IsFixed(_loop.State, resourceId) ? Color.white : GhostPlate);
        }

        /// <summary>The spread said in words — which specimens it still wants, or what it holds now that it is whole.</summary>
        private string SpreadReading(FolioSpreadData spread)
        {
            var wanted = new List<string>();
            foreach (var entry in spread.entries)
            {
                if (!Folio.IsFixed(_loop.State, entry))
                {
                    wanted.Add(GoodName(entry));
                }
            }

            if (wanted.Count > 0)
            {
                return spread.displayName + " still wants: " + string.Join(" · ", wanted.ToArray());
            }

            var gives = EffectsLabel(spread.effects);
            return spread.displayName + " is pressed entire"
                   + (gives.Length > 0 ? ", and holds " + gives : string.Empty);
        }

        /// <summary>
        /// What can be pressed right now — one row per held Choice specimen a
        /// spread still asks for, with its plate beside the name so the rows and
        /// the strips above them are read in the same language, and the spreads
        /// it would fill so the choice is a choice rather than a guess.
        /// <para>
        /// The settled specimens are a COUNT rather than a row each. A pressed
        /// one already stands in full ink in its spread's strip above, and its
        /// held figure is the thing 2026-08-04 took off this page: ten rows of
        /// "1.01K held  ·  pressed" restated a fact the drawer keeps and a fact
        /// the strip draws.
        /// </para>
        /// </summary>
        private void BuildFolioPressings(RectTransform card)
        {
            var pressable = new List<string>();
            var pressed = 0;
            var unasked = 0;
            // Data order, not the drawer's — choiceResources is a dictionary,
            // and rows keyed off its enumeration reshuffle under the thumb
            // reaching for them (the Stores drawer's rule: a grid that reorders
            // is a grid nobody learns).
            foreach (var resource in _loop.Data.resources)
            {
                if (_loop.State.GetChoice(resource.id) <= BigDouble.Zero)
                {
                    continue;
                }

                if (Folio.IsFixed(_loop.State, resource.id))
                {
                    pressed++;
                    continue;
                }

                if (!WantedBySpread(resource.id))
                {
                    unasked++;
                    continue;
                }

                pressable.Add(resource.id);
            }

            MakeHairline(card);
            if (pressable.Count > 0)
            {
                MakeText(card, "TO PRESS", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);
                foreach (var resourceId in pressable)
                {
                    BuildPressingRow(card, resourceId);
                }
            }

            var settled = new List<string>();
            if (pressed > 0)
            {
                settled.Add(pressed + (pressed == 1 ? " specimen already pressed" : " specimens already pressed"));
            }

            if (unasked > 0)
            {
                settled.Add(unasked + " no spread asks for");
            }

            if (settled.Count > 0)
            {
                MakeText(card, "<i>" + string.Join(" · ", settled.ToArray()) + ".</i>",
                    17, TextAnchor.MiddleLeft, Ink2, _serif);
                return;
            }

            if (pressable.Count == 0)
            {
                MakeText(card, "<i>no choice find in the drawer. the pages wait.</i>",
                    17, TextAnchor.MiddleCenter, Ink2, _serif);
            }
        }

        private bool WantedBySpread(string resourceId)
        {
            foreach (var spread in _loop.Data.folioSpreads)
            {
                if (spread.entries.Contains(resourceId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One specimen the page will take: its plate, its name, the spreads it fills, and the press.</summary>
        private void BuildPressingRow(RectTransform card, string resourceId)
        {
            var row = Row(card);
            PictureSlot(row.transform, ArtLibrary.ForResource(resourceId), RowPlate);
            var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            Button fix = null;
            fix = Button(row.transform, "Press", 140, () =>
            {
                if (_loop.FixSpecimen(resourceId))
                {
                    Flash(fix, "pressed to the page", true);
                    SetNote("pressed between these pages. it outlasts the camp.");
                    _dirty = true;
                }
            });

            var fills = SpreadsWanting(resourceId);
            _liveUpdaters.Add(() =>
            {
                // The one live clause is the short fall: a drawer holding less
                // than a whole find can't be pressed, and crossing that mark
                // doesn't move the page's structure signature, so a build-time
                // label would go stale while the Press button sat dead.
                var held = _loop.State.GetChoice(resourceId);
                var shortfall = held < BigDouble.One
                    ? "  ·  <color=" + OchreInkHex + ">not yet a whole find</color>"
                    : string.Empty;
                label.text = "Choice " + GoodName(resourceId)
                             + (fills.Length > 0
                                 ? "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">presses into " + fills + "</color></size>"
                                 : string.Empty)
                             + shortfall;

                var ok = Folio.CanFix(_loop.State, _loop.Data, resourceId);
                fix.interactable = ok;
                SetButtonTint(fix, ok);
            });
        }

        /// <summary>The spreads a specimen would fill. Every spread listing an unpressed entry is by definition still open.</summary>
        private string SpreadsWanting(string resourceId)
        {
            var names = new List<string>();
            foreach (var spread in _loop.Data.folioSpreads)
            {
                if (spread.entries.Contains(resourceId))
                {
                    names.Add(spread.displayName);
                }
            }

            return string.Join(" · ", names.ToArray());
        }

        // ── The Deep Pages ────────────────────────────────────────────────

        private void BuildDeepPagesCard()
        {
            if (_loop.Data.insects == null || _loop.Data.insects.Count == 0)
            {
                return;
            }

            // A newly recorded plate moves the structure signature, so the
            // tally is settled here rather than recounted on the cadence.
            var recorded = 0;
            foreach (var insect in _loop.Data.insects)
            {
                if (Insects.IsRecorded(_loop.State, insect))
                {
                    recorded++;
                }
            }

            var card = FoldingCard(JournalCardFolds.DeepPages, "THE DEEP PAGES",
                recorded + " of " + _loop.Data.insects.Count + " recorded", out var open, out var heading);
            AddHeadingMark(heading, ArtLibrary.ForJournal("deep-pages"));
            if (!open)
            {
                return;
            }

            MakeText(card, recorded + " of " + _loop.Data.insects.Count + " recorded",
                18, TextAnchor.MiddleCenter, Ink2);

            // What sketching is for goes at the head of the card, the way the
            // Folio's does. At the foot it sat between the last plate and the
            // deep amber, where it read as the amber's caption.
            MakeText(card, "<i>Nothing you sketch is ever taken: the creature is let go, and only these pages remain</i>",
                16, TextAnchor.MiddleCenter, Ink2, _serif);

            // Recorded plates first, still-out-there after. In data order the
            // two kinds interleaved, so a full-width plate could land between
            // two mystery lines and each entry's picture ran straight into the
            // next entry's name — the card had no rhythm to read by.
            foreach (var insect in _loop.Data.insects)
            {
                if (!Insects.IsRecorded(_loop.State, insect))
                {
                    continue;
                }

                MakeHairline(card);
                var art = ArtLibrary.ForInsect(insect.id);
                if (art != null)
                {
                    PlateImage(card, art, 260f);
                }

                var plate = Narrative.InsectPlate(_loop.Data, insect.id);
                MakeText(card, insect.displayName + "  " + SizeOpen(15) + "<color=" + MossDeepHex + ">recorded</color></size>"
                               + (string.IsNullOrEmpty(plate) ? string.Empty : "\n" + SizeOpen(15) + "<i>" + plate + "</i></size>"),
                    18, TextAnchor.MiddleLeft, Ink);

                // What a finished plate is worth. Design §6 calls it the land
                // rewarding attention paid, and the card said only "recorded":
                // the largest permanent multipliers in the game were the ones
                // nothing named.
                var gives = EffectsLabel(insect.effects);
                if (gives.Length > 0)
                {
                    MakeText(card, SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>",
                        15, TextAnchor.MiddleLeft, Ink2);
                }
            }

            BuildUncaughtEntries(card);
            BuildDeepAmberEntries(card);
            BuildFinalWaystoneEntries(card);
        }

        /// <summary>
        /// What the book hasn't caught yet, gathered below the plates rather
        /// than shuffled in among them.
        /// <para>
        /// A row each, with the sketch ladder drawn as MARKS. Seven identical
        /// "… something not yet caught …" lines could not be told apart, and the
        /// two that had a sketch on them said "1 of 4" in words: a card whose
        /// whole subject is slow progress showed almost none of it. The marks
        /// are the Kith's own idiom — a short countable ladder said as marks
        /// rather than as "n of m", so the places beyond the ones you hold are
        /// visible at a glance.
        /// </para>
        /// <para>
        /// Still no name and no haunt: a page not yet earned keeps its secret,
        /// and only sketching resolves it. The marks leak nothing — how many
        /// portions a plate takes is not who it is.
        /// </para>
        /// </summary>
        private void BuildUncaughtEntries(RectTransform card)
        {
            var uncaught = new List<InsectData>();
            foreach (var insect in _loop.Data.insects)
            {
                if (!Insects.IsRecorded(_loop.State, insect))
                {
                    uncaught.Add(insect);
                }
            }

            if (uncaught.Count == 0)
            {
                return;
            }

            MakeHairline(card);
            MakeText(card, "STILL OUT THERE", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);
            foreach (var insect in uncaught)
            {
                BuildUncaughtRow(card, insect);
            }
        }

        private void BuildUncaughtRow(RectTransform card, InsectData insect)
        {
            var row = Row(card);
            EmptyPlateSlot(row.transform);
            var line = MakeText(row.transform, string.Empty, 17, TextAnchor.MiddleLeft, Ink2, _serif);
            FlexibleWidth(line.gameObject, 1f);
            var marks = MarkRow(row.transform, insect.sketches, SketchMark);

            var captured = insect;
            _liveUpdaters.Add(() =>
            {
                // Sketch counts are NOT in the page's structure signature — a
                // portion arriving is a number moving, not a card appearing —
                // so this is one of the few things here that has to be live.
                var sketches = Insects.SketchCount(_loop.State, captured.id);
                line.text = sketches > 0
                    ? "<i>a shape half-caught</i>"
                    : "<i>… something not yet caught …</i>";
                for (var index = 0; index < marks.Length; index++)
                {
                    marks[index].color = index < sketches ? Ink : RulePaper;
                }
            });
        }

        /// <summary>
        /// The room a plate would take, ruled and empty — the pencilled box the
        /// warden hasn't drawn in yet. A blank space of the same width would
        /// have been honest about the layout and said nothing about the page.
        /// </summary>
        private void EmptyPlateSlot(Transform row)
        {
            var go = MakePanel("EmptyPlate", (RectTransform)row, DeepPaper);
            AddBorder(go, RulePaper);
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = RowPlate;
            element.preferredWidth = RowPlate;
            element.minHeight = RowPlate;
            element.preferredHeight = RowPlate;
            go.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>
        /// The deep amber at the foot of the Deep Pages (design §6): the one
        /// find the land lets the warden keep, surfaced piece by authored
        /// piece at its own zone's observation site. Hidden until that site has been
        /// walked this run — unless the carried journal already holds a piece,
        /// because the record crosses every fold.
        /// </summary>
        private void BuildDeepAmberEntries(RectTransform card)
        {
            if (!Sim.DeepAmber.Configured(_loop.Data))
            {
                return;
            }

            var amber = _loop.Data.deepAmber;
            var siteWalked = false;
            foreach (var site in _loop.State.digSites)
            {
                if (site.zoneId == amber.zoneId)
                {
                    siteWalked = true;
                    break;
                }
            }

            if (!siteWalked && Sim.DeepAmber.FoundCount(_loop.State) == 0)
            {
                return;
            }

            // Ruled off the insect plates above: without the line, its own
            // plate and title read as a seventh insect. The title carries the
            // plate's name, so the rule is all the head it needs.
            MakeHairline(card);
            var art = ArtLibrary.ForLine("deep-amber");
            if (art != null)
            {
                PlateImage(card, art, 200f);
            }

            var title = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            var body = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2, _serif);
            _liveUpdaters.Add(() =>
            {
                var found = Sim.DeepAmber.FoundCount(_loop.State);
                var complete = Sim.DeepAmber.IsComplete(_loop.State, _loop.Data);
                title.text = amber.plateName + "  " + SizeOpen(15)
                             + (complete
                                 ? "<color=" + MossDeepHex + ">recorded: it outlives every Migration</color>"
                                 : "<color=" + Ink2Hex + ">" + found + " of " + amber.pieces.Count + " surfaced</color>")
                             + "</size>";

                var lines = new List<string>();
                foreach (var piece in Sim.DeepAmber.FoundPieces(_loop.State, _loop.Data))
                {
                    lines.Add("<b>" + piece.displayName + "</b>: <i>" + piece.lore + "</i>");
                }

                lines.Add(complete
                    ? "<i>" + amber.completedLore + "</i>"
                    : "<i>… the resin holds more …</i>");
                body.text = string.Join("\n", lines);
            });
        }

        /// <summary>
        /// The final waystones, kept directly under the deep amber (design §7):
        /// the reveal is assembled from those four notes and these four stones
        /// and nowhere else, so the two halves belong on one page where they can
        /// be read against each other. Hidden until the first stone is taken —
        /// an empty heading here would advertise the ending.
        /// </summary>
        private void BuildFinalWaystoneEntries(RectTransform card)
        {
            if (!Narrative.FinalWaystonesConfigured(_loop.Data) || _loop.State.finalWaystonesRead == 0)
            {
                return;
            }

            MakeHairline(card);
            var stone = ArtLibrary.ForJournal("waystone");
            if (stone != null)
            {
                PlateImage(card, stone, 200f);
            }

            var title = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            var body = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2, _serif);
            _liveUpdaters.Add(() =>
            {
                var read = _loop.State.finalWaystonesRead;
                var total = Narrative.FinalWaystoneCount(_loop.Data);
                var complete = Narrative.AreFinalWaystonesComplete(_loop.State, _loop.Data);
                title.text = "The Final Waystones  " + SizeOpen(15)
                             + (complete
                                 ? "<color=" + MossDeepHex + ">read: all of it, and it does not unsay</color>"
                                 : "<color=" + Ink2Hex + ">" + read + " of " + total + " read</color>")
                             + "</size>";

                var lines = new List<string>();
                foreach (var entry in Narrative.ReadFinalWaystones(_loop.State, _loop.Data))
                {
                    lines.Add("<i>“" + entry.text + "”</i>");
                }

                if (!complete)
                {
                    // Says the cadence, not the count of seasons left: the stone
                    // arrives on a fold, and the player should climb expecting it.
                    lines.Add("<i>… the next one waits for the next season …</i>");
                }

                body.text = string.Join("\n", lines);
            });
        }

        // ── The Wheel ─────────────────────────────────────────────────────

        /// <summary>
        /// The Wheel's shelf (design §15): what the warden has kept of the
        /// eight, said as a count and the names that answer it.
        /// <para>
        /// The shelf has worn both of the other shapes. Eight identical
        /// "unkept"s read as a checklist of things already missed; the eight
        /// dated and ordered by what comes next read as a calendar, which is a
        /// thing to act on and so belongs where a player acts — the events
        /// rail, the Trail's head, both of which carry the next tide's
        /// countdown. What the Record holds is what was done, so what is left
        /// here is the keeping and nothing around it.
        /// </para>
        /// </summary>
        private void BuildWheelCard()
        {
            var wheel = _loop.Data.wheel;
            if (wheel?.sabbats == null || wheel.sabbats.Count == 0)
            {
                return;
            }

            var kept = new List<SabbatData>();
            foreach (var sabbat in wheel.sabbats)
            {
                if (Keeping.KeptYears(_loop.State, sabbat.id).Count > 0)
                {
                    kept.Add(sabbat);
                }
            }

            // The reckoning names the card. It governs the whole wheel rather
            // than any one line of it, and the inside cover, where it is
            // changed, is the place that explains it.
            var card = FoldingCard(JournalCardFolds.Wheel,
                _loop.State.hemisphere == Wheel.HemisphereSouth ? "THE SOUTHERN WHEEL" : "THE NORTHERN WHEEL",
                kept.Count + " of " + wheel.sabbats.Count + " kept", out var open, out var heading);
            // The wheel itself, not a sabbat off it: the card governs the whole
            // turn of the year, and a mark wearing one festival's plate would
            // pick a winner out of the eight the card exists to hold together.
            AddHeadingMark(heading, ArtLibrary.ForJournal("wheel"));
            if (!open)
            {
                return;
            }

            // What a sabbat IS, which nothing else on any page says: the card
            // named eight festivals and then counted them, and a player who
            // does not already keep the old year had no way in.
            MakeText(card, "<i>the eight turnings of the warden's year: the solstices, the equinoxes, and the"
                           + " fire festivals between them. each opens a tide, and a tide kept is written"
                           + " here for good.</i>",
                15, TextAnchor.UpperLeft, Ink2, _serif);

            // Authored order, which is the wheel's own turn: the shelf no
            // longer says when anything falls, so there is nothing for a
            // soonest-first order to tell a reader.
            MakeText(card, kept.Count + " of " + wheel.sabbats.Count + " kept",
                20, TextAnchor.MiddleCenter, Ink, _serif);
            foreach (var sabbat in kept)
            {
                BuildWheelShelfRow(card, sabbat);
            }
        }

        /// <summary>One kept sabbat: the plate the keeping earned (design §15), and its name.</summary>
        private void BuildWheelShelfRow(RectTransform card, SabbatData sabbat)
        {
            var plate = ArtLibrary.ForJournal("sabbat-" + sabbat.id);
            if (plate != null)
            {
                PlateImage(card, plate, 140f);
            }

            MakeText(card, "<b>" + sabbat.displayName + "</b>", 18, TextAnchor.MiddleCenter, Ink);
        }

        // ── The practical matter ──────────────────────────────────────────

        /// <summary>
        /// The way in to the inside cover: the keeping of the book, what it
        /// tells anyone, what was bought, the colophon, and starting again.
        /// Last card on the last page, where a book puts its practical matter —
        /// the colophon held this spot alone until the rest of it arrived, and
        /// it is still the licence's reason for the card existing at all (five
        /// source works are CC BY, and a credit sitting in a repo file is not
        /// carried by the build).
        /// </summary>
        private void BuildInsideCoverCard()
        {
            var card = Card("THE INSIDE COVER");
            MakeText(card, "Where the book is kept, what it tells us, what was bought, "
                           + "and the hands that drew its plates.",
                16, TextAnchor.UpperLeft, Ink2, _serif);

            var row = Row(card);
            var label = MakeText(row.transform, "the keeping of the book",
                17, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            Button(row.transform, "Open", 160, () => _hud.Sheets.OpenInsideCoverSheet());
        }

        private void BuildStandingCard()
        {
            var card = Card("THE STANDING");
            var reading = MakeText(card, string.Empty, 18, TextAnchor.MiddleCenter, Ink2);

            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 17, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            // The board lives behind Play Games, so a signed-out tap offers the
            // sign-in rather than swallowing itself. Signed in, we read the
            // scores and draw them in the journal — Play's own overlay never
            // opens on a modern target SDK, and a board in the book beats a
            // Google sheet thrown over the top of it.
            Button view = null;
            view = Button(row.transform, "View", 160, () =>
            {
                if (_loop.GameServices.IsSignedIn)
                {
                    ReadTheBoard(view);
                    return;
                }

                Flash(view, "asking Play Games", true);
                _loop.GameServices.SignInInteractive(signedIn =>
                {
                    if (signedIn)
                    {
                        ReadTheBoard(view);
                        return;
                    }

                    SetNote("Play Games didn't answer. the board stays shut.");
                });
            });

            _liveUpdaters.Add(() =>
            {
                // The camp count came off the page header's eyebrow, which only
                // restated the lit tab; the record of how many folds you've
                // walked belongs with the rest of your standing.
                var camp = _loop.State.migrationCount > 0
                    ? "  ·  Camp " + (_loop.State.migrationCount + 1)
                    : string.Empty;
                reading.text = "Renown  " + NumberFormat.Short(_loop.State.renown) + camp;
                var signedIn = _loop.GameServices.IsSignedIn;
                label.text = signedIn
                    ? "how you stand among the folk"
                    : "how you stand among the folk\n" + SizeOpen(15) + "<color=" + Ink2Hex
                      + ">Play Games isn't signed in</color></size>";
                SetButtonLabel(view, signedIn ? "View" : "Sign in");
            });
        }

        // How much of the ladder the Standing sheet shows. Ten is what fits the
        // sheet without scrolling and what a player actually reads.
        private const int StandingRows = 10;

        /// <summary>
        /// Fetch the Renown board and raise the Standing sheet. The read is a
        /// network round trip, so the button says it is working — without that
        /// the tap looks dead for the second or so it takes.
        /// </summary>
        private void ReadTheBoard(Button view)
        {
            Flash(view, "reading the board", true);
            _loop.GameServices.LoadLeaderboard(Services.LeaderboardIds.Renown, StandingRows, entries =>
            {
                // The read is a network round trip, and the pump can have raised
                // a sheet while it was away. Opening over it would close it
                // unread — and the welcome-back sheet offers the offline haul
                // exactly once — so the board is the thing that gives way.
                if (_sheet != null)
                {
                    SetNote("the board answered while the page was busy. ask again.");
                    return;
                }

                if (entries == null)
                {
                    SetNote("the board wouldn't be read. Play Games kept it shut.");
                }

                // A null set still opens the sheet, which says so itself: a tap
                // that resolves to nothing at all is the thing being fixed here.
                _hud.Sheets.OpenStandingSheet(entries);
            });
        }

        // ── The Almanac ───────────────────────────────────────────────────

        /// <summary>
        /// The long song — the one card on these pages with a currency to spend,
        /// which is why it leads them. The Stores page made the same call for
        /// the same reason: the thing that can be USED goes above the things
        /// that are read, or it drifts further out of reach with every entry the
        /// record gains.
        /// <para>
        /// It FOLDS, from 2026-08-14, and is the one card on the page that
        /// arrives open. Leading the page and being unfoldable had been the same
        /// fact by accident: a warden with nothing unspent had a tree of learned
        /// lines nailed to the top of the back pages and no head to press. Shut,
        /// its head carries the two things the card is opened to find out — how
        /// much of the song is sung, and whether there is anything to spend.
        /// </para>
        /// </summary>
        private void BuildAlmanacCard()
        {
            if (_loop.State.verdurePoints <= 0.0 && _loop.State.almanacNodeIds.Count == 0)
            {
                return;
            }

            var card = FoldingCard(JournalCardFolds.Almanac, "THE ALMANAC", AlmanacTally(),
                out var open, out var heading);
            AddHeadingMark(heading, ArtLibrary.ForJournal("almanac"));
            if (!open)
            {
                // The unspent figure moves on its own — a fold lands, a line is
                // learned — so the shut head is rewritten on the cadence rather
                // than written once at the build, the way the keeping's is.
                var shutLabel = heading.GetComponentInChildren<Text>();
                if (shutLabel != null)
                {
                    _liveUpdaters.Add(() => shutLabel.text = FoldingCardLabel("THE ALMANAC", AlmanacTally()));
                }

                return;
            }

            var header = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                header.text = Mathf.FloorToInt((float)_loop.AvailableVerdure()) + " Verdure unspent";
            });

            foreach (var node in _loop.Data.almanac)
            {
                // What a line does is fixed data — settle it once per rebuild.
                // Nothing else on the row told the player, so the names alone
                // asked for Verdure on trust.
                var gives = AlmanacGives(node);
                var givesLine = gives.Length > 0
                    ? "\n" + SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>"
                    : string.Empty;

                // An endless line is never "learned" — it wears its level
                // instead, and the price on the tap climbs with it.
                if (node.repeatable)
                {
                    var heldNow = Almanac.Levels(_loop.State, node.id);
                    if (heldNow == 0 && !Almanac.PrerequisiteMet(_loop.State, _loop.Data, node))
                    {
                        continue;
                    }

                    var endlessNode = node;
                    var endlessRow = Row(card);
                    var endlessLabel = MakeText(endlessRow.transform, node.displayName + givesLine, 18, TextAnchor.MiddleLeft, Ink);
                    FlexibleWidth(endlessLabel.gameObject, 1f);
                    Button take = null;
                    take = Button(endlessRow.transform, string.Empty, 300, () =>
                    {
                        if (_loop.BuyAlmanacNode(endlessNode))
                        {
                            Flash(take, "sung", true);
                            SetNote("the long song takes another verse. it crosses the fold.");
                            _dirty = true;
                        }
                    });

                    _liveUpdaters.Add(() =>
                    {
                        var held = Almanac.Levels(_loop.State, endlessNode.id);
                        var cost = Mathf.CeilToInt((float)Almanac.NextCost(_loop.State, _loop.Data, endlessNode));
                        endlessLabel.text = node.displayName
                                            + (held > 0 ? "  <color=" + MossDeepHex + ">verse " + held + "</color>" : string.Empty)
                                            + givesLine;
                        SetButtonLabel(take, "Sing · " + cost + " Verdure");
                        var ok = Almanac.CanBuy(_loop.State, _loop.Data, endlessNode);
                        take.interactable = ok;
                        SetButtonTint(take, ok);
                    });
                    continue;
                }

                if (_loop.State.almanacNodeIds.Contains(node.id))
                {
                    MakeText(card, node.displayName + "  <color=" + MossDeepHex + ">learned</color>" + givesLine,
                        18, TextAnchor.MiddleLeft, Ink);
                    continue;
                }

                // A later tier is not a choice yet — the line below it has to be
                // learned first. Listing it anyway priced a tap that Almanac
                // would refuse, so the tree reveals itself one tier at a time.
                if (!Almanac.PrerequisiteMet(_loop.State, _loop.Data, node))
                {
                    continue;
                }

                var captured = node;
                var row = Row(card);
                var label = MakeText(row.transform, node.displayName + givesLine, 18, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button buy = null;
                // Cost on the plate, the way the sheets price a save: the row's
                // own line is spent naming what the line gives, and the price of
                // a tap belongs on the tap.
                buy = Button(row.transform, "Learn · " + Mathf.CeilToInt((float)node.costVerdure) + " Verdure", 300, () =>
                {
                    if (_loop.BuyAlmanacNode(captured))
                    {
                        Flash(buy, "learned", true);
                        SetNote("the almanac takes a new line. it crosses the fold.");
                        _dirty = true;
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    var ok = Almanac.CanBuy(_loop.State, _loop.Data, captured);
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }

        /// <summary>
        /// The song as one line, for the head that stands where the card is
        /// folded away: how much of it is sung, and — in the journal's
        /// invitation ink — whether there is Verdure waiting to be spent. That
        /// second clause is the whole of what a shut Almanac owes the player,
        /// and it is the rule the keeping's head and a folded ground both keep.
        /// <para>
        /// Verses count with lines, not apart from them. An endless line is
        /// sung over and over (<c>almanacLevels</c>) while the rest are learned
        /// once (<c>almanacNodeIds</c>), and a head that counted only the second
        /// would read "0 lines learned" at a warden four verses into the long
        /// song.
        /// </para>
        /// </summary>
        private string AlmanacTally()
        {
            var learned = _loop.State.almanacNodeIds.Count;
            foreach (var pair in _loop.State.almanacLevels)
            {
                learned += pair.Value;
            }

            var line = learned + (learned == 1 ? " line sung" : " lines sung");
            var unspent = Mathf.FloorToInt((float)_loop.AvailableVerdure());
            if (unspent <= 0)
            {
                return line;
            }

            return line + " · <color=" + MossDeepHex + ">" + unspent + " Verdure unspent</color>";
        }

        /// <summary>
        /// What learning a line actually gets you. Effects cover most of the
        /// tree, but The Old Friend carries none at all — its whole payload is a
        /// bond, which lives in bonds.json — so effects alone would ask 12
        /// Verdure for a bare name.
        /// </summary>
        private string AlmanacGives(AlmanacNodeData node)
        {
            var parts = new List<string>();
            var effects = EffectsLabel(node.effects);
            if (effects.Length > 0)
            {
                // On an endless line the numbers are what ONE more verse adds,
                // not the whole line — saying so is the difference between a
                // price that looks poor and one that reads as a choice.
                parts.Add(node.repeatable ? effects + ", every verse" : effects);
            }

            var bond = Bonds.BondForSource(_loop.Data, "almanacNode", node.id);
            if (bond != null)
            {
                parts.Add(bond.displayName + " the " + SpeciesName(bond.species)
                          + " bonds, a companion who walks every fold with you");
            }

            return string.Join(" · ", parts);
        }
    }
}
