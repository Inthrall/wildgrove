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
    /// </summary>
    internal sealed class RecordPage : JournalSection
    {
        internal RecordPage(GameHud hud) : base(hud) { }

        internal void BuildRecordPage()
        {
            BuildWrittenCard();
            BuildCompendiumCard();
            BuildFolioCard();
            BuildDeepPagesCard();
            BuildAlmanacCard();
            BuildStandingCard();
            BuildInsideCoverCard();
        }

        /// <summary>
        /// How much of the book is written — one figure for the whole pane,
        /// first thing on it. Every card below counts its own kind (entries
        /// recorded, specimens pressed, plates drawn), so the page could say how
        /// each collection was going without ever answering the question a
        /// collector actually asks.
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

                    SetNote("Play Games didn't answer, so the board stays shut for now.");
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
                if (entries == null)
                {
                    SetNote("the board wouldn't be read. Play Games kept it shut.");
                }

                // A null set still opens the sheet, which says so itself: a tap
                // that resolves to nothing at all is the thing being fixed here.
                _hud.Sheets.OpenStandingSheet(entries);
            });
        }

        private void BuildCompendiumCard()
        {
            var card = Card("THE COMPENDIUM");
            MakeText(card, Compendium.DiscoveredCount(_loop.State, _loop.Data) + " of "
                           + Compendium.TotalEntries(_loop.Data) + " recorded", 18, TextAnchor.MiddleCenter, Ink2);

            // What's missing pulls harder than what's held — the Deep Pages
            // already tease "— something not yet caught —", and skipping the
            // undiscovered here hid the collection entirely. A mystery line
            // per resource ON THE TRAIL (its node is reachable now); the rest
            // fold into one count so far-zone entries neither leak nor spam.
            var onTheTrail = new HashSet<string>();
            foreach (var node in _loop.State.nodes)
            {
                onTheTrail.Add(node.resourceId);
            }

            var beyondTheTrail = 0;
            foreach (var resource in _loop.Data.resources)
            {
                if (!Compendium.IsResourceDiscovered(_loop.State, resource.id))
                {
                    if (onTheTrail.Contains(resource.id))
                    {
                        MakeText(card, "<i>… something on the trail, not yet gathered …</i>", 18, TextAnchor.MiddleLeft, Ink2);
                    }
                    else
                    {
                        beyondTheTrail++;
                    }

                    continue;
                }

                var captured = resource;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    // What the camp holds now, then what it has ever gathered.
                    // The held figure used to run in the chrome ledger, a line
                    // that grew a wrap every zone; here each number sits beside
                    // the entry it belongs to and costs the page nothing.
                    var held = _loop.State.GetResource(captured.id);
                    var stock = held > BigDouble.Zero
                        ? "<b>" + NumberFormat.Short(held) + "</b> held  ·  "
                        : string.Empty;
                    // Fine finds are banked apart from the camp stock — 3.5%
                    // of every haul landed where no page counted it, so the
                    // pool read as goods quietly going missing.
                    var fine = _loop.State.GetFine(captured.id);
                    var fineHeld = fine > BigDouble.Zero
                        ? "<b>" + NumberFormat.Short(fine) + "</b> fine  ·  "
                        : string.Empty;
                    // Pristine's own pool, on the same footing as fine — it was
                    // counted for a lifetime and shown nowhere, so the rarest
                    // thing the ground gives had no tally of its own.
                    var pristine = _loop.State.GetPristine(captured.id);
                    var pristineHeld = pristine > BigDouble.Zero
                        ? "<b>" + NumberFormat.Short(pristine) + "</b> pristine  ·  "
                        : string.Empty;
                    var everPristine = Compendium.LifetimePristine(_loop.State, captured.id);
                    var pristineEver = everPristine > BigDouble.Zero
                        ? ", " + NumberFormat.Short(everPristine) + " of them pristine"
                        : string.Empty;
                    line.text = captured.id + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + stock + fineHeld + pristineHeld + "lifetime "
                                + NumberFormat.Short(Compendium.LifetimeGathered(_loop.State, captured.id))
                                + pristineEver + "</color></size>";
                });
            }

            if (beyondTheTrail > 0)
            {
                MakeText(card, "<i>…and " + beyondTheTrail + " more beyond the trail.</i>", 18, TextAnchor.MiddleLeft, Ink2);
            }

            // What "fine" is for, said once at the card's foot — the pool's
            // only exit is a verse asking for a fine find, and without the
            // note the tally above is a number with no door.
            var fineNote = MakeText(card, "<i>fine finds are kept apart from the stores; a verse sometimes asks for one</i>",
                16, TextAnchor.MiddleLeft, Ink2, _serif);
            _liveUpdaters.Add(() =>
            {
                var anyFine = false;
                foreach (var pair in _loop.State.fineResources)
                {
                    if (pair.Value > BigDouble.Zero)
                    {
                        anyFine = true;
                        break;
                    }
                }

                fineNote.gameObject.SetActive(anyFine);
            });

            BuildCompendiumCrafts(card);
        }

        /// <summary>
        /// The Compendium's second page — what the warden has ever made. The
        /// card's head counts recipes among its entries (see
        /// <see cref="Compendium.TotalEntries"/>), but only the gatherables were
        /// ever listed, so a third of the tally had no page: lifetimeCrafted
        /// climbed for the whole life of a save and fed nothing but achievements
        /// and Game Stats. Undiscovered recipes fold into one count, the same
        /// way the finds above do, so a far-zone recipe name can't leak.
        /// </summary>
        private void BuildCompendiumCrafts(RectTransform card)
        {
            if (_loop.Data.recipes == null || _loop.Data.recipes.Count == 0)
            {
                return;
            }

            MakeHairline(card);
            MakeText(card, "THE CRAFTS", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);

            var unmade = 0;
            foreach (var recipe in _loop.Data.recipes)
            {
                if (!Compendium.IsRecipeDiscovered(_loop.State, recipe.id))
                {
                    unmade++;
                    continue;
                }

                var captured = recipe;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    // Batches, not units — a batch is the thing the station
                    // finishes, and it's what the skill gates are counted in
                    // (forgecraft 40 is ~1770 of them).
                    var batches = Compendium.LifetimeCrafted(_loop.State, captured.id);
                    line.text = GoodName(captured.id) + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                + NumberFormat.Short(new BigDouble(batches))
                                + (batches == 1.0 ? " batch made" : " batches made") + "</color></size>";
                });
            }

            if (unmade > 0)
            {
                MakeText(card, "<i>…and " + unmade + " never yet made.</i>", 18, TextAnchor.MiddleLeft, Ink2);
            }
        }

        private void BuildFolioCard()
        {
            if (_loop.Data.folioSpreads == null || _loop.Data.folioSpreads.Count == 0)
            {
                return;
            }

            var card = Card("THE FOLIO");
            MakeText(card, "<i>Press finds to keep a permanent record</i>", 16, TextAnchor.MiddleCenter, Ink2, _serif);

            foreach (var spread in _loop.Data.folioSpreads)
            {
                var captured = spread;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    var done = Folio.IsSpreadComplete(_loop.State, captured);
                    var progress = done
                        ? "<color=" + MossDeepHex + ">complete</color>"
                        : Folio.FixedEntryCount(_loop.State, captured) + " of " + captured.entries.Count + " pressed";
                    line.text = captured.displayName + "  " + SizeOpen(15) + progress + "</size>";
                });
            }

            foreach (var pair in _loop.State.pristineResources)
            {
                if (pair.Value <= BigDouble.Zero)
                {
                    continue;
                }

                BuildPristineEntry(card, pair.Key);
            }
        }

        /// <summary>
        /// One held Pristine specimen. A specimen the page will take gets a row
        /// with the Press button on it; one it won't — already pressed, or no
        /// spread asks for it — is a line of text like the spreads above. It used
        /// to be a row either way, and a row is built around a 48dp button: with
        /// the button switched off, every settled specimen sat in a fingertip of
        /// blank paper, which read as an empty line between the entries.
        ///
        /// Which of the two it is settles at build time. Both a press and a
        /// newly-held specimen move the structure signature, so the page is
        /// rebuilt on either.
        /// </summary>
        private void BuildPristineEntry(RectTransform card, string resourceId)
        {
            var isFixed = Folio.IsFixed(_loop.State, resourceId);
            var wanted = false;
            foreach (var spread in _loop.Data.folioSpreads)
            {
                if (spread.entries.Contains(resourceId))
                {
                    wanted = true;
                    break;
                }
            }

            if (isFixed || !wanted)
            {
                // A dead Press button explains nothing, so there isn't one —
                // the line says instead why the page is done with it.
                var note = isFixed
                    ? "  ·  <color=" + MossDeepHex + ">pressed</color>"
                    : "  ·  <color=" + Ink2Hex + ">no spread asks for it</color>";
                var settled = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() => settled.text = PristineLine(resourceId, note));
                return;
            }

            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            Button fix = null;
            fix = Button(row.transform, "Press", 140, () =>
            {
                if (_loop.FixSpecimen(resourceId))
                {
                    Flash(fix, "pressed to the page", true);
                    SetNote("pressed it between these pages, where it will outlast the camp.");
                    _dirty = true;
                }
            });

            _liveUpdaters.Add(() =>
            {
                label.text = PristineLine(resourceId, string.Empty);
                var ok = Folio.CanFix(_loop.State, _loop.Data, resourceId);
                fix.interactable = ok;
                SetButtonTint(fix, ok);
            });
        }

        /// <summary>"Pristine glow-moss  3 held", plus whatever the page has to say about it.</summary>
        private string PristineLine(string resourceId, string note)
        {
            return "Pristine " + resourceId + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                   + NumberFormat.Short(_loop.State.GetPristine(resourceId)) + " held</color>" + note + "</size>";
        }

        private void BuildDeepPagesCard()
        {
            if (_loop.Data.insects == null || _loop.Data.insects.Count == 0)
            {
                return;
            }

            var card = Card("THE DEEP PAGES");
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
            }

            BuildUncaughtEntries(card);
            BuildDeepAmberEntries(card);
            BuildFinalWaystoneEntries(card);
        }

        /// <summary>
        /// What the book hasn't caught yet, gathered below the plates rather
        /// than shuffled in among them. A page not yet earned keeps its secret —
        /// no name, no haunt, just the sense of something. Only sketching
        /// resolves it, and even then the name waits for the full record.
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
                var captured = insect;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
                _liveUpdaters.Add(() =>
                {
                    var sketches = Insects.SketchCount(_loop.State, captured.id);
                    line.text = sketches > 0
                        ? "<i>a shape half-caught, " + sketches + " of " + captured.sketches + " sketched</i>"
                        : "<i>… something not yet caught …</i>";
                });
            }
        }

        /// <summary>
        /// The deep amber at the foot of the Deep Pages (design §6): the one
        /// find the land lets the warden keep, surfaced piece by authored
        /// piece at its own zone's watch site. Hidden until that site has been
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

        private void BuildAlmanacCard()
        {
            if (_loop.State.verdurePoints <= 0.0 && _loop.State.almanacNodeIds.Count == 0)
            {
                return;
            }

            var card = Card("THE ALMANAC");
            var header = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                header.text = Mathf.FloorToInt((float)_loop.AvailableVerdure()) + " Verdure unspent";
            });

            var tree = ArtLibrary.ForJournal("almanac");
            if (tree != null)
            {
                PlateImage(card, tree, 240f);
            }

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
                            SetNote("the long song takes another verse. it crosses every fold with you.");
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
                        SetNote("the almanac takes a new line. it crosses every fold with you.");
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
