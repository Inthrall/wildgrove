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
        }

        private void BuildCompendiumCard()
        {
            var card = Card("THE COMPENDIUM");
            MakeText(card, Compendium.DiscoveredCount(_loop.State, _loop.Data) + " of "
                           + Compendium.TotalEntries(_loop.Data) + " recorded", 18, TextAnchor.MiddleCenter, Ink2);

            foreach (var resource in _loop.Data.resources)
            {
                if (!Compendium.IsResourceDiscovered(_loop.State, resource.id))
                {
                    continue;
                }

                var captured = resource;
                var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                _liveUpdaters.Add(() =>
                {
                    line.text = captured.id + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">lifetime "
                                + NumberFormat.Short(Compendium.LifetimeGathered(_loop.State, captured.id)) + "</color></size>";
                });
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
                if (_loop.State.almanacNodeIds.Contains(node.id))
                {
                    MakeText(card, node.displayName + "  <color=" + MossDeepHex + ">learned</color>", 18, TextAnchor.MiddleLeft, Ink);
                    continue;
                }

                var captured = node;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button buy = null;
                buy = Button(row.transform, "Learn", 160, () =>
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
                    label.text = captured.displayName + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                 + Mathf.CeilToInt((float)captured.costVerdure) + " Verdure</color></size>";
                    var ok = _loop.AvailableVerdure() >= captured.costVerdure;
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }
    }
}
