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
    /// The Wheel's presence on the trail: the open tide, the next sabbat coming,
    /// and the keeping card with a row per slot.
    /// </summary>
    internal sealed partial class TrailPage
    {
        /// <summary>
        /// The tide's line (design §15): while a sabbat's tide is open, the
        /// warden's margin names it at the head of the Trail — the calendar is
        /// the warden's, the land's answer belongs on the land's page.
        /// <para>
        /// Through the fallow weeks it counts the next one down instead of
        /// showing nothing. Showing nothing was the whole of the Wheel's
        /// presence outside a tide: the tracker row, this line and the keeping
        /// card all stand down together, and the only surface left naming a
        /// sabbat was the fold sheet — which a player has no reason to open.
        /// The countdown is a margin aside, not a card: a tide that is not open
        /// is not something to act on.
        /// </para>
        /// </summary>
        private void BuildTideLine()
        {
            var tide = _loop.OpenTide();
            if (tide == null)
            {
                BuildNextSabbatLine();
                return;
            }

            MakeText(_body, "<i>" + tide.sign + "</i>", 17, TextAnchor.MiddleCenter, Ink2, _hand);

            // What the tide actually does — said out loud, so the lean doesn't
            // read as a bug when it lapses at the fire. And the half that
            // matters after the drawn season's DemandWeight retired (design
            // §8): the verse's asks are NOT scaled by it — keeping the sabbat
            // never raises the Rite's price.
            var gives = EffectsLabel(tide.touch);
            if (gives.Length > 0)
            {
                MakeText(_body, gives + " · while " + tide.displayName + "-tide holds — the Rite's verses ask none of it",
                    15, TextAnchor.MiddleCenter, Ink2);
            }
        }

        /// <summary>
        /// The fallow weeks' one line: which sabbat is coming, and how long
        /// until its tide opens. Live-updated, because a countdown that only
        /// moves when the page is rebuilt is a countdown that reads as stuck.
        /// </summary>
        private void BuildNextSabbatLine()
        {
            if (_loop.NextSabbat(out _) == null)
            {
                return;
            }

            var line = MakeText(_body, string.Empty, 17, TextAnchor.MiddleCenter, Ink2, _hand);
            void Reread()
            {
                // Re-read the sabbat, not just the clock: a tide that opens
                // while this page is up makes the whole line wrong, and the
                // rebuild that replaces it with the warden's sign is a beat
                // behind the cadence that notices.
                var ahead = _loop.NextSabbat(out _);
                if (ahead == null || _loop.OpenTide() != null)
                {
                    line.gameObject.SetActive(false);
                    return;
                }

                line.gameObject.SetActive(true);
                var away = _loop.NextNightOf(ahead, out _, out var opensMs)
                    ? (opensMs - _loop.NowUnixMs()) / 1000.0
                    : 0.0;
                line.text = away > 0
                    ? "<i>" + ahead.displayName + " is coming — the tide opens in " + NumberFormat.Countdown(away) + ".</i>"
                    : "<i>" + ahead.displayName + " is coming.</i>";
            }

            // Drawn once now rather than a quarter-second later: the page must
            // never appear with a blank line standing where this one goes.
            Reread();
            _liveUpdaters.Add(Reread);
        }

        /// <summary>
        /// The keeping's page (design §15): the tide's own offering slots,
        /// beside the Rite and never of it — no verse count, no gift pile, no
        /// ground moves with it. Tiers pay a little Amber; the first writes
        /// the year's claim. Nothing here through the fallow weeks.
        /// </summary>
        private void BuildKeepingCard()
        {
            var keeping = _loop.CurrentKeeping();
            var tide = _loop.OpenTide();
            if (keeping == null || tide == null)
            {
                return;
            }

            var card = Card("THE KEEPING · " + tide.displayName.ToUpperInvariant() + "-TIDE");
            // The tracker's tide row deep-links here.
            _firstKeepingCard = card;

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

                var closeMs = _loop.OpenTideCloseMs();
                var days = (long)System.Math.Ceiling((closeMs - _loop.NowUnixMs()) / 86400000.0);
                var tier = _loop.KeepingTierReached();
                var word = tier == 1 ? "kept the eve" : tier == 2 ? "kept the day" : tier == 3 ? "kept the wheel" : "unkept yet";
                standing.text = word + " · " + (days <= 1 ? "closes at the fire tonight" : "closes in " + days + " days");
            });

            for (var i = 0; i < keeping.slots.Count; i++)
            {
                BuildKeepingSlotRow(card, i);
            }
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
