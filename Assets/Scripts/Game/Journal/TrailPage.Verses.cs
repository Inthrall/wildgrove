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
    /// The Rite on the trail page: a card per verse in play, the sealed ones
    /// behind them, and a row per slot asking to be filled.
    /// </summary>
    internal sealed partial class TrailPage
    {
        private void BuildVerseCards()
        {
            // Only the verses this run is walking (design §8's fold gate): a
            // verse whose trail does not exist yet is not a sealed verse the
            // warden could reach by working, so drawing it would set the whole
            // page to a task that cannot be started. The numerals count the
            // verses in play, so an early run reads I, II, III with no gaps —
            // the deep verses are not yet part of this Rite to be missing from.
            var verses = Rite.VersesInPlay(_loop.State, _loop.Data);
            if (verses.Count == 0)
            {
                return;
            }

            // Every fold past the first re-casts the Rite (RiteGenerator), and
            // rotates which crafts stand in its light — those ask for less,
            // the ones out of it for more. Unsaid, a warden who remembers last
            // run's verse reads the new numbers as the grove moving the mark.
            if (_loop.State.migrationCount > 0)
            {
                MakeText(_body, "<i>each fold re-casts the Rite: the crafts in this fold's light are asked less of, those out of it more</i>",
                    15, TextAnchor.MiddleCenter, Ink2);
            }

            // Open verses keep their full cards; verses already sung collapse
            // into one summary so the Trail page doesn't grow without bound — a
            // finished verse is a memory, not a worklist. A verse whose site
            // the trail has reached before its turn (verses are sung in
            // order) gets one quiet sealed card; anything past it stays
            // unwritten.
            List<RiteVerseData> sung = null;
            var sealedShown = false;
            for (var i = 0; i < verses.Count; i++)
            {
                var verse = verses[i];
                // Verses are sung in order; their number is their place in the
                // rite, shown as a numeral so a verse can be spoken of by name.
                var number = i + 1;
                if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse))
                {
                    // One quiet card for the next verse that isn't open yet,
                    // whichever holds it — its turn, or a trail that hasn't
                    // reached its site. Gate the card on the site being reached
                    // and a run with every reachable verse sung shows nothing
                    // but SUNG VERSES, leaving the rite's last verse — the whole
                    // Migration gate — unmentioned on every page in the book.
                    if (!sealedShown)
                    {
                        BuildSealedVerseCard(verses, verse, number);
                        sealedShown = true;
                    }

                    continue;
                }

                if (Rite.IsVerseComplete(_loop.State, _loop.Data, verse))
                {
                    (sung ?? (sung = new List<RiteVerseData>())).Add(verse);
                    continue;
                }

                var card = BuildVerseCard(verse, number);
                if (_firstVerseCard == null)
                {
                    // The tracker's deep-link lands on the first verse still open.
                    _firstVerseCard = card;
                }
            }

            if (sung != null)
            {
                // Every verse in play is sung: the rite is complete, and the
                // next verse belongs to the camp after this one. Without a
                // card in the current-verse slot the page would end on SUNG
                // VERSES alone, with nothing saying where the rite goes next.
                if (sung.Count == verses.Count)
                {
                    BuildRiteSungCard();
                }

                BuildSungVersesCard(verses, sung);
            }
        }

        /// <summary>
        /// The card standing where the next verse would — every verse in play
        /// is sung, so the only verse left to sing waits beyond the fold.
        /// </summary>
        private void BuildRiteSungCard()
        {
            var card = Card("THE NEXT VERSE");
            MakeText(card, "<i>the rite is sung entire. the next verse will be written after the fold</i>",
                19, TextAnchor.MiddleCenter, Ink2, _serif);
            if (_firstVerseCard == null)
            {
                _firstVerseCard = card;
            }
        }

        /// <summary>
        /// The collapsed record of verses already answered — one line each, so a
        /// long-lived rite doesn't bury the open verses under finished ones.
        /// </summary>
        private void BuildSungVersesCard(List<RiteVerseData> inPlay, List<RiteVerseData> verses)
        {
            var card = Card("SUNG VERSES");
            foreach (var verse in verses)
            {
                var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
                var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
                MakeText(card, Roman(inPlay.IndexOf(verse) + 1) + " · " + ZoneName(verse.zone) + "  " + SizeOpen(15) + "<color=" + MossDeepHex
                               + ">" + site + ", sung</color></size>", 18, TextAnchor.MiddleLeft, Ink);
            }
        }

        /// <summary>
        /// The card for the next verse that isn't open yet — it names what
        /// holds it (an earlier verse still unsung, its turn, or a trail that
        /// hasn't reached its site) and asks nothing.
        /// </summary>
        private void BuildSealedVerseCard(List<RiteVerseData> inPlay, RiteVerseData verse, int number)
        {
            string barring = null;
            foreach (var earlier in inPlay)
            {
                if (earlier.id == verse.id)
                {
                    break;
                }

                if (!Rite.IsVerseComplete(_loop.State, _loop.Data, earlier))
                {
                    barring = ZoneName(earlier.zone);
                    break;
                }
            }

            var card = Card("VERSE " + Roman(number) + " · " + ZoneName(verse.zone).ToUpperInvariant());
            var line = barring != null
                ? "the verse of " + barring + " is still unsung"
                : Upgrades.UnlockedZoneIds(_loop.State, _loop.Data).Contains(verse.zone)
                    ? "its turn has not come"
                    // Every earlier verse is sung and the site is still beyond
                    // the trail: buying that zone's map IS the rite's next step,
                    // so the card says so rather than leaving the gate mute.
                    : "the trail has not reached " + ZoneName(verse.zone);
            MakeText(card, "<i>the cairn keeps its silence: " + line + "</i>",
                19, TextAnchor.MiddleCenter, Ink2, _serif);
            if (_firstVerseCard == null)
            {
                _firstVerseCard = card;
            }
        }

        private RectTransform BuildVerseCard(RiteVerseData verse, int number)
        {
            var capturedVerse = verse;
            var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
            var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
            var card = Card("VERSE " + Roman(number) + " · " + ZoneName(verse.zone).ToUpperInvariant());

            var siteLine = MakeText(card, site, 23, TextAnchor.MiddleCenter, Ink, _serif);
            siteLine.gameObject.name = "Site";
            var cairn = ArtLibrary.ForJournal("cairn");
            if (cairn != null)
            {
                PlateImage(card, cairn, 200f);
            }

            var verseLine = Narrative.VerseLine(_loop.Data, verse.zone);
            if (!string.IsNullOrEmpty(verseLine))
            {
                MakeText(card, "<i>“" + verseLine + "”</i>", 19, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            var progress = MakeText(card, string.Empty, 17, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                // Clamp for saves that over-delivered before expiry was
                // enforced — "5 of 3" reads broken, and any three IS answered.
                var need = Rite.RequiredSlots(_loop.State, _loop.Data, capturedVerse);
                var answered = Mathf.Min(Rite.CompletedSlotCount(_loop.State, capturedVerse), need);
                progress.text = Rite.IsVerseComplete(_loop.State, _loop.Data, capturedVerse)
                    ? "<color=" + MossDeepHex + ">answered: the verse is sung</color>"
                    : "answered " + answered + " of " + need;
            });

            for (var i = 0; i < verse.slots.Count; i++)
            {
                BuildSlotRow(card, capturedVerse, i);
            }

            return card;
        }

        private void BuildSlotRow(RectTransform card, RiteVerseData verse, int slotIndex)
        {
            var slot = verse.slots[slotIndex];
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            Button offer = null;
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    offer = Button(row.transform, "Set down", 180, () =>
                    {
                        var given = _loop.OfferResource(verse, slotIndex);
                        if (given > BigDouble.Zero)
                        {
                            var units = PlainNumber(System.Math.Floor(given.ToDouble()));
                            Flash(offer, "set down " + units + " " + slot.resource, true);
                            SetNote("set down " + units + " " + slot.resource + ". all of it. no answer. not yet.");
                        }
                        else
                        {
                            Flash(offer, "not the whole offering", false);
                            SetNote("the whole offering, or none at all. the stores are short.");
                        }
                    });
                    break;
                case RiteSlotType.Specimen:
                    offer = Button(row.transform, slot.count > 1 ? "Offer " + slot.count : "Offer one", 180, () =>
                    {
                        if (_loop.OfferSpecimen(verse, slotIndex))
                        {
                            Flash(offer, "set down", true);
                            SetNote(slot.count > 1
                                ? "set the perfect ones down. they deserved better than a page, maybe."
                                : "set the perfect one down. it deserved better than a page, maybe.");
                        }
                        else
                        {
                            Flash(offer, slot.count > 1 ? "too few such finds in hand" : "no such find in hand", false);
                            SetNote(slot.count > 1
                                ? "the whole offering, or none at all. the drawer is short."
                                : "no such find in hand. the site is patient.");
                        }
                    });
                    break;
                case RiteSlotType.Sketch:
                    offer = Button(row.transform, "Offer a sketch", 220, () =>
                    {
                        if (_loop.OfferSketch(verse, slotIndex))
                        {
                            Flash(offer, "page torn out", true);
                            SetNote("tore the page out for them. that portion must be watched again.");
                        }
                        else
                        {
                            Flash(offer, "no finished sketch", false);
                            SetNote("no finished sketch to give.");
                        }
                    });
                    break;
            }

            _liveUpdaters.Add(() =>
            {
                var delivered = Rite.SlotDelivered(_loop.State, verse, slotIndex);
                var target = Rite.SlotTarget(slot);
                var done = delivered >= target;
                var expired = !done && Rite.IsVerseComplete(_loop.State, _loop.Data, verse);
                var name = SlotName(slot);
                if (done)
                {
                    label.text = "<color=" + MossDeepHex + ">" + name + ", set down</color>";
                }
                else if (expired)
                {
                    // Any three answer the verse; the rest expire (§8) — the
                    // spirits stopped listening to this one.
                    label.text = "<color=" + Ink2Hex + "><i>" + name + ", unasked now</i></color>";
                }
                else
                {
                    // A deed slot has no button — it is earned at the nodes,
                    // never pressed — so the row has to say so, or its count
                    // reads as a delivery the player can't make.
                    var deedTail = slot.type == RiteSlotType.Deed
                        ? "  <color=" + Ink2Hex + "><i>counted as the work is done</i></color>"
                        : string.Empty;
                    // Have against asked, not delivered against asked: the ask is
                    // whole, so there is no part-delivery left to report and the
                    // only useful number is how close the stores are to answering
                    // it. Abbreviated like every other quantity in the book —
                    // a late verse asks in five and six figures, and "18437 /
                    // 20000" is a number nobody reads, only counts the digits of.
                    var inHand = Rite.SlotInHand(_loop.State, _loop.Data, verse, slotIndex);
                    label.text = name + "  <color=" + Ink2Hex + ">" + NumberFormat.ShortFloor(System.Math.Floor(inHand))
                                 + " / " + NumberFormat.Short(target) + "</color>" + deedTail;
                }

                if (offer != null)
                {
                    offer.gameObject.SetActive(!done && !expired);
                    var ok = _loop.CanOffer(verse, slotIndex);
                    offer.interactable = ok;
                    SetButtonTint(offer, ok);
                }
            });
        }
    }
}
