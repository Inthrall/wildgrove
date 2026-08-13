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
    /// The Wheel's presence on the trail: the open tide's sign, and the keeping
    /// card — folded shut behind its own tally — with a row per slot. The fallow
    /// weeks draw nothing here; that is the events rail's post, not the page's.
    /// </summary>
    internal sealed partial class TrailPage
    {
        /// <summary>
        /// The tide's line (design §15): while a sabbat's tide is open, the
        /// warden's margin names it at the head of the Trail — the calendar is
        /// the warden's, the land's answer belongs on the land's page. The sign
        /// alone; it is a mood, not a reading.
        /// <para>
        /// Two things left this line on 2026-08-13, both of them said better
        /// elsewhere and both of them costing the page height the grounds
        /// wanted. The touch label ("+20% … while Ostara-tide holds — the Rite's
        /// verses ask none of it") went to the tide's own sheet, which already
        /// carried the same sentence under <em>While it holds</em>: it is a
        /// static clause that does not change for a month, and it was two
        /// wrapped lines above the first plate for every one of those days. And
        /// the fallow weeks' countdown went entirely — it existed because
        /// showing nothing was once the whole of the Wheel's presence outside a
        /// tide, and the events rail retired that argument on 2026-08-11 by
        /// standing a coming-sabbat cell on every tab with the same
        /// <see cref="NumberFormat.Countdown"/> in its caption. The line was the
        /// cell read aloud.
        /// </para>
        /// </summary>
        private void BuildTideLine()
        {
            var tide = _loop.OpenTide();
            if (tide == null)
            {
                return;
            }

            MakeText(_body, "<i>" + tide.sign + "</i>", 17, TextAnchor.MiddleCenter, Ink2, _hand);
        }

        /// <summary>
        /// The keeping's page (design §15): the tide's own offering slots,
        /// beside the Rite and never of it — no verse count, no gift pile, no
        /// ground moves with it. Tiers pay a little Amber; the first writes
        /// the year's claim. Nothing here through the fallow weeks.
        /// <para>
        /// It FOLDS, and folds shut by default, from 2026-08-13 — the one card
        /// in the book that does so while carrying buttons (see
        /// <see cref="JournalCardFolds"/> for why it earns the exception). A
        /// plate, a standing line and five slot rows is close to a whole phone
        /// viewport, and it stood at the head of the Trail for the ~68% of the
        /// year a tide holds: opening the page put no gathering plate on screen
        /// at all. Shut, its head says everything the card's own summary said,
        /// and wears the moss when the stores can answer a slot.
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
            var card = FoldingCard(JournalCardFolds.Keeping, head, KeepingTally(), out var open, out var heading);
            // The tracker's tide row and the tide sheet's button both deep-link
            // here, and both open the fold on the way (GameHud.ScrollToOnTrail).
            _firstKeepingCard = card;

            if (!open)
            {
                // Every word of the head moves: the tier as slots are answered,
                // the clock as the tide runs down, the moss as the stores rise
                // to a slot. Written once at the build it would be a day stale
                // by the evening, which is the one thing a countdown may not be.
                var label = heading.GetComponentInChildren<Text>();
                if (label != null)
                {
                    _liveUpdaters.Add(() => label.text = FoldingCardLabel(head, KeepingTally(), false));
                }

                return;
            }

            // The sabbat's plate, the day the art pass paints it — until then
            // the card is the words alone.
            var plate = ArtLibrary.ForJournal("sabbat-" + tide.id);
            if (plate != null)
            {
                PlateImage(card, plate, 200f);
            }

            var standing = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                if (_loop.CurrentKeeping() == null)
                {
                    standing.text = "<i>the tide has closed; the fire keeps what it was given</i>";
                    return;
                }

                standing.text = KeepingWord() + " · " + KeepingCloseWord();
            });

            for (var i = 0; i < keeping.slots.Count; i++)
            {
                BuildKeepingSlotRow(card, i);
            }
        }

        /// <summary>
        /// The keeping as one line, for the head that stands where the card is
        /// folded away: how it is kept, how much of it is answered, how long is
        /// left, and — in the journal's invitation ink — whether the stores can
        /// answer a slot this minute. That last clause is the whole of what a
        /// shut card owes the player, and it is the Trail's own rule for a
        /// folded ground, which says "the watch stands empty" the same way.
        /// </summary>
        private string KeepingTally()
        {
            var keeping = _loop.CurrentKeeping();
            if (keeping == null)
            {
                return "the tide has closed";
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
            return days <= 1 ? "closes at the fire tonight" : "closes in " + days + " days";
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
                    SetNote("set it down at the fire. the day is a little more kept.");
                }
                else
                {
                    Flash(offer, "not the whole offering", false);
                    SetNote("the whole offering, or none at all. the stores are short.");
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
