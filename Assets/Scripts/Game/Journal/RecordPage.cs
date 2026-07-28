using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
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
            BuildCompendiumCard();
            BuildFolioCard();
            BuildDeepPagesCard();
            BuildAlmanacCard();
            BuildStandingCard();
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

                    SetNote("Play Games didn't answer — the board stays shut for now.");
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

            // TEMP diagnostics: the Play Games call under test is the one this
            // card makes, and the launch popup has long since been dismissed by
            // the time you tap it — so keep the status lines one tap away from
            // the button under test. Remove with the Diag sink.
            var diagRow = Row(card);
            var diagLabel = MakeText(diagRow.transform, "Play Games status", 15, TextAnchor.MiddleLeft, Ink2);
            FlexibleWidth(diagLabel.gameObject, 1f);
            Button(diagRow.transform, "Show", 160, _hud.ShowDiagnostics);
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
                    SetNote("the board wouldn't be read — Play Games kept it shut.");
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
                        MakeText(card, "<i>— something on the trail, not yet gathered —</i>", 18, TextAnchor.MiddleLeft, Ink2);
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
                    line.text = captured.id + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + stock + "lifetime "
                                + NumberFormat.Short(Compendium.LifetimeGathered(_loop.State, captured.id)) + "</color></size>";
                });
            }

            if (beyondTheTrail > 0)
            {
                MakeText(card, "<i>…and " + beyondTheTrail + " more beyond the trail.</i>", 18, TextAnchor.MiddleLeft, Ink2);
            }
        }

        private void BuildFolioCard()
        {
            if (_loop.Data.folioSpreads == null || _loop.Data.folioSpreads.Count == 0)
            {
                return;
            }

            var card = Card("THE FOLIO");
            foreach (var spread in _loop.Data.folioSpreads)
            {
                var captured = spread;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    var done = Folio.IsSpreadComplete(_loop.State, captured);
                    var progress = done
                        ? "<color=" + MossDeepHex + ">complete — it outlives every Migration</color>"
                        : Folio.FixedEntryCount(_loop.State, captured) + " of " + captured.entries.Count + " pressed";
                    line.text = captured.displayName + "  " + SizeOpen(15) + progress + "</size>";
                });
            }

            var anyPristine = false;
            foreach (var pair in _loop.State.pristineResources)
            {
                if (pair.Value <= BigDouble.Zero)
                {
                    continue;
                }

                anyPristine = true;
                var resourceId = pair.Key;
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

                    // A dead Fix button explains nothing — say why the page
                    // won't take it, and only offer the button when it might.
                    var hint = isFixed
                        ? "  ·  <color=" + MossDeepHex + ">pressed — the page keeps it</color>"
                        : !wanted ? "  ·  <color=" + Ink2Hex + ">no spread asks for it</color>" : string.Empty;
                    label.text = "Pristine " + resourceId + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                 + NumberFormat.Short(_loop.State.GetPristine(resourceId)) + " held</color>" + hint + "</size>";
                    fix.gameObject.SetActive(!isFixed && wanted);
                    var ok = Folio.CanFix(_loop.State, _loop.Data, resourceId);
                    fix.interactable = ok;
                    SetButtonTint(fix, ok);
                });
            }

            if (anyPristine)
            {
                MakeText(card, "<i>pressing consumes the find — the page keeps it instead of you.</i>", 16, TextAnchor.MiddleCenter, Ink2, _serif);
            }
        }

        private void BuildDeepPagesCard()
        {
            if (_loop.Data.insects == null || _loop.Data.insects.Count == 0)
            {
                return;
            }

            var card = Card("THE DEEP PAGES");
            foreach (var insect in _loop.Data.insects)
            {
                var captured = insect;
                if (Insects.IsRecorded(_loop.State, captured))
                {
                    // A recorded specimen shows its deep-page plate; unrecorded
                    // ones stay a mystery (text only), per the design.
                    var art = ArtLibrary.ForInsect(captured.id);
                    if (art != null)
                    {
                        PlateImage(card, art, 260f);
                    }

                    var plate = Narrative.InsectPlate(_loop.Data, captured.id);
                    MakeText(card, captured.displayName + "  <color=" + MossDeepHex + ">recorded</color>"
                                   + (string.IsNullOrEmpty(plate) ? string.Empty : "\n" + SizeOpen(15) + "<i>" + plate + "</i></size>"),
                        18, TextAnchor.MiddleLeft, Ink);
                    continue;
                }

                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
                _liveUpdaters.Add(() =>
                {
                    // A page not yet earned keeps its secret — no name, no haunt,
                    // just the sense of something. Only sketching resolves it,
                    // and even then the name waits for the full record.
                    var sketches = Insects.SketchCount(_loop.State, captured.id);
                    line.text = sketches > 0
                        ? "<i>a shape half-caught — " + sketches + " of " + captured.sketches + " sketched</i>"
                        : "<i>— something not yet caught —</i>";
                });
            }

            MakeText(card, "<i>nothing you sketch is ever taken — the creature is let go, and only these pages remain.</i>",
                16, TextAnchor.MiddleCenter, Ink2, _serif);
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
                parts.Add(effects);
            }

            var bond = Bonds.BondForSource(_loop.Data, "almanacNode", node.id);
            if (bond != null)
            {
                parts.Add(bond.displayName + " the " + SpeciesName(bond.species)
                          + " bonds — a companion who walks every fold with you");
            }

            return string.Join(" · ", parts);
        }
    }
}
