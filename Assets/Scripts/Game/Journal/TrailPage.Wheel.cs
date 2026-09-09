using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalSprites;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Wheel's presence on the trail: the keeping card, folded shut behind
    /// its own tally, with a row per slot. Nothing else — the seasons run back
    /// to back, so a tide line at the head of this page would be a line that is
    /// never not there.
    /// <para>
    /// The sign stood above the card until 2026-09-09, the last of three things
    /// this page said about the Wheel. It was a margin note about what day it
    /// is, and once a day became a six-week season that was the same scrawl
    /// over the plates on every visit; it reads once and on purpose on the
    /// tide's own sheet instead. (The touch label and the fallow weeks'
    /// countdown were the two before it, both 2026-08-13.)
    /// </para>
    /// </summary>
    internal sealed partial class TrailPage
    {
        /// <summary>
        /// The keeping's page (design §15): the season's own offering slots,
        /// beside the Rite and never of it — no verse count, no gift pile, no
        /// ground moves with it. Tiers pay a little Amber; the first writes
        /// the year's claim. Nothing here where the calendar does not reach.
        /// <para>
        /// It FOLDS, and folds shut by default, from 2026-08-13 — the one card
        /// in the book that does so while carrying buttons (see
        /// <see cref="JournalCardFolds"/> for why it earns the exception). A
        /// plate, a standing line and five slot rows was close to a whole phone
        /// viewport, and it stands at the head of the Trail every day of the
        /// year: opening the page put no gathering plate on screen at all. The
        /// plate came off the card and into the head on 2026-08-14, which takes
        /// 200 units off the open card as well and gives the shut one a face.
        /// </para>
        /// <para>
        /// From 2026-09-09 the head carries the tally open as well as shut, and
        /// the card's own standing line is gone with it. The two said the same
        /// thing a finger's width apart — how it is kept, how far it has got,
        /// how long is left — and the head is the half that is on screen either
        /// way, so a player who folds the card away loses nothing and one who
        /// opens it is not told twice.
        /// </para>
        /// </summary>
        private void BuildKeepingCard()
        {
            var keeping = _loop.CurrentKeeping();
            var tide = _loop.OpenTide();
            if (keeping == null || tide == null)
            {
                return;
            }

            var head = "THE KEEPING · " + tide.displayName.ToUpperInvariant() + "-TIDE";
            var card = FoldingCard(JournalCardFolds.Keeping, head, KeepingTally(), out var open, out var heading,
                tallyWhenOpen: true);
            // The sabbat's plate rides the heading's right margin, opposite the
            // chevron — the same place a ground's keystone stands, and for the
            // same reason: on the card it cost 200 units of the head this page
            // is trying to keep short, and it only drew while the card was open,
            // so the season the head is named for had no face for most of its run.
            // (Null until the art pass paints it; then the card is words alone.)
            AddHeadingMark(heading, ArtLibrary.ForJournal("sabbat-" + tide.id));
            // The tracker's tide row and the tide sheet's button both deep-link
            // here, and both open the fold on the way (GameHud.ScrollToOnTrail).
            _firstKeepingCard = card;

            // Every word of the head moves: the tier as slots are answered, the
            // clock as the season runs down, the moss as the stores rise to a
            // slot. Written once at the build it would be a day stale by the
            // evening, which is the one thing a countdown may not be.
            var label = heading.GetComponentInChildren<Text>();
            if (label != null)
            {
                _liveUpdaters.Add(() => label.text = FoldingCardLabel(head, KeepingTally()));
            }

            if (!open)
            {
                return;
            }

            for (var i = 0; i < keeping.slots.Count; i++)
            {
                BuildKeepingSlotRow(card, i);
            }
        }

        /// <summary>
        /// The keeping as one line, under the head open or shut: how it is
        /// kept, how much of it is answered, who takes the wheel next and when
        /// — and, in the journal's invitation ink, whether the stores can answer
        /// a slot this minute. That last clause is the whole of what a shut card
        /// owes the player, and it is the Trail's own rule for a folded ground,
        /// which says "no one sketches here" the same way.
        /// </summary>
        private string KeepingTally()
        {
            var keeping = _loop.CurrentKeeping();
            if (keeping == null)
            {
                return "the season has turned; the fire keeps what it was given";
            }

            var line = KeepingWord() + " · " + Keeping.CompletedSlotCount(keeping)
                       + " of " + keeping.slots.Count + " set down · " + KeepingCloseWord();
            for (var i = 0; i < keeping.slots.Count; i++)
            {
                if (!Keeping.IsSlotComplete(keeping.slots[i]) && _loop.CanOfferKeeping(i))
                {
                    return line + " · <color=" + MossDeepHex + ">something to set down</color>";
                }
            }

            return line;
        }

        private string KeepingWord()
        {
            switch (_loop.KeepingTierReached())
            {
                case 1: return "kept the eve";
                case 2: return "kept the day";
                case 3: return "kept the wheel";
                default: return "unkept yet";
            }
        }

        private string KeepingCloseWord()
        {
            var days = (long)System.Math.Ceiling((_loop.OpenTideCloseMs() - _loop.NowUnixMs()) / 86400000.0);
            return TideCloseWord(_loop.NextTide(out _), days);
        }

        private void BuildKeepingSlotRow(RectTransform card, int slotIndex)
        {
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            Button offer = null;
            offer = Button(row.transform, "Set down", 180, () =>
            {
                if (_loop.OfferKeeping(slotIndex))
                {
                    Flash(offer, "set down", true);
                    SetNote("set it down at the fire. the day is more kept.");
                }
                else
                {
                    Flash(offer, "not the whole offering", false);
                    SetNote("the whole offering or none. the stores are short.");
                }
            });

            _liveUpdaters.Add(() =>
            {
                var keeping = _loop.CurrentKeeping();
                if (keeping == null || slotIndex >= keeping.slots.Count)
                {
                    row.gameObject.SetActive(false);
                    return;
                }

                row.gameObject.SetActive(true);
                var slot = keeping.slots[slotIndex];
                var specimen = slot.kind == KeepingSlotState.SpecimenKind;
                var name = specimen ? "a Decent find" : (slot.goodsId ?? string.Empty).Replace('-', ' ');
                if (Keeping.IsSlotComplete(slot))
                {
                    label.text = "<color=" + MossDeepHex + ">" + name + ", set down</color>";
                }
                else
                {
                    var held = specimen ? DecentFindsInHand() : _loop.State.GetResource(slot.goodsId).ToDouble();
                    var inHand = System.Math.Min(held, slot.target);
                    label.text = name + "  <color=" + Ink2Hex + ">" + NumberFormat.ShortFloor(System.Math.Floor(inHand))
                                 + " / " + NumberFormat.Short(slot.target) + "</color>";
                }

                var open = !Keeping.IsSlotComplete(slot);
                offer.gameObject.SetActive(open);
                if (open)
                {
                    var ok = _loop.CanOfferKeeping(slotIndex);
                    offer.interactable = ok;
                    SetButtonTint(offer, ok);
                }
            });
        }

        private double DecentFindsInHand()
        {
            var total = 0.0;
            foreach (var pair in _loop.State.decentResources)
            {
                total += pair.Value.ToDouble();
            }

            return total;
        }
    }
}
