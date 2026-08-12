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
    /// The caravan exchange: what the camp will trade, at what quality, for how
    /// much of what it holds.
    /// <para>
    /// Amounts are fractions rather than numbers so the choice still means
    /// something at eight berries and at eight million, and trading the whole
    /// stock is the one amount that asks first, because it is the one that cannot
    /// be walked back.
    /// </para>
    /// </summary>
    internal sealed partial class CampPage
    {
        // How much of the give-good a trade spends, as a fraction of what's
        // held. Half by default: the whole stock is the one amount that can't
        // be walked back, so it isn't what a stray tap reaches for — and it
        // alone asks first.
        private double _exchangeFraction = 0.5;

        // The amounts the caravan deals in. Fractions rather than numbers so
        // the choice still means something at 8 berries and at 8 million.
        private static readonly (string Label, double Fraction)[] ExchangeAmounts =
        {
            ("a quarter", 0.25),
            ("half", 0.5),
            ("all", 1.0),
        };

        // The quality tiers a deal is answered at, in row order.
        private static readonly QualityTier[] ExchangeTiers =
        {
            QualityTier.Poor,
            QualityTier.Decent,
            QualityTier.Choice,
        };

        /// <summary>
        /// The portion chips, 200 apiece (was 170). MEASURED against
        /// IMFellEnglishSC at the 38px uGUI renders a 19pt label: "a quarter" is
        /// 158 where a 170 plate leaves 150 usable, so it wrapped to two lines
        /// beside a one-line "half" (71) and "all" (50) and the row sat at three
        /// different heights.
        /// <para>
        /// Worse, it wrapped ON SELECTION. SetButtonChosen switches the label to
        /// bold, which takes "a quarter" from 158 to 164, so the row could change
        /// height as the player tapped between the three. 184 is the floor that
        /// holds the widest chosen label; 200 leaves a little air. Three at 200
        /// plus spacing is 616, inside both a portrait card (~906) and a 4:3
        /// spread column (~748).
        /// </para>
        /// </summary>
        private const float ExchangeChipPlate = 200f;

        /// <summary>
        /// The trade plates run the card's full measure — this is the primary act
        /// and it earns the width.
        /// </summary>
        private const float ExchangeTradePlate = 800f;

        /// <summary>
        /// The consideration's plate, and the arrows on it — a square mark, not
        /// a line of the card. Every width this had before (800, then 320) was
        /// still a plate in a row of its own at the foot of the card, which gave
        /// a 5-amber re-draw a whole line of the measure the trades are asking
        /// for, and ended the card on a purchase rather than on the deal.
        /// </summary>
        private const float ExchangeConsiderationPlate = 120f;

        private const float ExchangeConsiderationGlyph = 56f;

        /// <summary>
        /// The corner the consideration stands in: the plate, and the price on
        /// the line under it. 160 wide so "25 amber" in small caps at 13 keeps
        /// one line beneath a 120 plate; the inset holds the pair off the top
        /// right of the caravan's own plate, so the mark reads as pinned to the
        /// picture rather than balanced on its edge.
        /// </summary>
        private const float ExchangeConsiderationMeasure = 160f;

        private const float ExchangeConsiderationInset = 10f;

        /// <summary>The traded goods' own plates, either side of the arrow on the deal row.</summary>
        private const float ExchangeGoodGlyph = 72f;

        /// <summary>
        /// The caravan (design §9): goods for goods, but the deal is the
        /// caravan's to name now — one give-good for one take-good, drawn from
        /// the wall-clock window and turning every few minutes. The player
        /// chooses only how much to answer with, at whichever quality tiers
        /// the camp holds of the asked good: Decent and Choice trade in at
        /// their §5 value multipliers and are always paid out in plain goods,
        /// which is finally an exit for the windfall pools.
        /// </summary>
        private void BuildExchangeCard()
        {
            var card = Card("THE EXCHANGE");
            MakeText(card, "<i>a caravan idles at the camp edge. it trades; it does not sell.</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);
            var caravan = ArtLibrary.ForJournal("caravan");
            // The plate is kept hold of because the consideration is pinned into
            // its corner — a 200-deep band of art with nothing else in it is the
            // one place on this card a mark can stand without landing on a line
            // of words. ArtLibraryTests pins the caravan, so the null arm is for
            // art not yet drawn rather than a state the game reaches.
            var caravanPlate = caravan != null ? PlateImage(card, caravan, 200f) : null;

            if (!Exchange.Configured(_loop.Data))
            {
                MakeText(card, "the caravan has not come this way.", 18, TextAnchor.MiddleCenter, Ink2);
                return;
            }

            var deal = MakeText(card, string.Empty, 21, TextAnchor.MiddleCenter, Ink, _serif);

            // The trade itself, in pictures: the asked good, the arrow, the paid
            // good. The card named the pair twice in words (the deal line above
            // and the rate line's "reeds → clay" below) and showed neither, while
            // the only picture on it was the caravan — decoration, the same
            // whatever is being traded. Both plates are guaranteed: the caravan
            // only ever offers a discovered resource or a "trade" recipe output
            // (Exchange.TradeableGoods), and ArtLibraryTests pins a plate to
            // every one of both. The sprites still swap on the refresh cadence,
            // because the deal turns on its own every few minutes.
            var goodsRow = Row(card);
            goodsRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var fromPlate = IconImage(goodsRow.transform, null, ExchangeGoodGlyph, Color.white)
                .GetComponent<Image>();
            var arrow = MakeText(goodsRow.transform, "→", 26, TextAnchor.MiddleCenter, Ink2, _serif);
            var toPlate = IconImage(goodsRow.transform, null, ExchangeGoodGlyph, Color.white)
                .GetComponent<Image>();

            var rate = MakeText(card, string.Empty, 16, TextAnchor.MiddleCenter, Ink2);

            System.Action refresh = null;
            var amountRow = Row(card);
            amountRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var chips = new List<(Button Plate, double Fraction)>();
            foreach (var amount in ExchangeAmounts)
            {
                var fraction = amount.Fraction;
                chips.Add((Button(amountRow.transform, amount.Label, ExchangeChipPlate, () =>
                {
                    _exchangeFraction = fraction;
                    refresh();
                }), fraction));
            }

            // One trade row per quality tier; a row only shows while the camp
            // holds that tier of the asked good, so most of the time this is
            // the one plain row it always was.
            var tierRows = new List<(QualityTier Quality, GameObject Row, Button Trade)>();
            foreach (var tier in ExchangeTiers)
            {
                var captured = tier;
                var row = Row(card);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                Button trade = null;
                trade = Button(row.transform, "Trade", ExchangeTradePlate, () => OfferTrade(trade, captured));
                if (captured == QualityTier.Poor)
                {
                    KeyAction(trade);
                }

                tierRows.Add((captured, row, trade));
            }

            var idle = MakeText(card, string.Empty, 18, TextAnchor.MiddleCenter, Ink2);

            // A consideration for the drover (design §9's sink slate): a
            // little amber and the deal re-draws now, never repeating itself.
            // The corner hides while the sink is unconfigured or no deal stands.
            var considerationCost = Mathf.FloorToInt((float)_loop.ConsiderationCost());
            GameObject considerationCorner = null;
            Button press = null;
            Image pressGlyph = null;
            Text pressCost = null;
            if (considerationCost > 0)
            {
                // Pinned into the top-right of the caravan's plate rather than
                // laid out with the rows: a reroll is a mark ON the deal, and
                // the card's own lines are the deal, the pair, the rate, the
                // portions and the trades. Anchored by hand the way the tile's
                // corner mark and the focus ring are; ignoreLayout is what keeps
                // it out of the flow in the plateless fallback, where the card
                // itself is the host.
                var cornerHost = caravanPlate != null ? (RectTransform)caravanPlate.transform : card;
                considerationCorner = MakeRect("Consideration", cornerHost).gameObject;
                considerationCorner.AddComponent<LayoutElement>().ignoreLayout = true;
                var cornerRect = (RectTransform)considerationCorner.transform;
                cornerRect.anchorMin = Vector2.one;
                cornerRect.anchorMax = Vector2.one;
                cornerRect.pivot = Vector2.one;
                cornerRect.anchoredPosition = new Vector2(-ExchangeConsiderationInset, -ExchangeConsiderationInset);
                cornerRect.sizeDelta = new Vector2(ExchangeConsiderationMeasure, 0f);
                var cornerLayout = considerationCorner.AddComponent<VerticalLayoutGroup>();
                cornerLayout.childControlWidth = true;
                cornerLayout.childControlHeight = true;
                cornerLayout.childForceExpandWidth = false;
                cornerLayout.childForceExpandHeight = false;
                cornerLayout.childAlignment = TextAnchor.UpperCenter;
                cornerLayout.spacing = 2;
                // Nothing outside sizes this rect — anchored to a point, its
                // height is its own to state, so the plate and the price both
                // have somewhere to stand.
                var cornerFitter = considerationCorner.AddComponent<ContentSizeFitter>();
                cornerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Two circular arrows, where a line of prose and a "Press" plate
                // used to sit. A reroll is the one idiom every player already
                // reads at a glance, and the words belong to the confirm sheet
                // anyway: that is the one that has to be sure before amber leaves
                // the pouch.
                press = GlyphButton(considerationCorner.transform, JournalSprites.RerollSprite(),
                    "Turn", ExchangeConsiderationPlate, ExchangeConsiderationGlyph, () =>
                {
                    // Amber is premium and hard-won — never spend it on a stray tap.
                    _hud.Sheets.OpenConfirmSheet(
                        "Spend " + considerationCost + " amber",
                        "Press a consideration on the drover, and the deal turns now?",
                        "Spend " + considerationCost + " amber",
                        () =>
                        {
                            var redealt = _loop.PressConsideration();
                            if (redealt != null)
                            {
                                SetNote("the drover pockets the resin and names another deal.");
                                _dirty = true;
                            }
                        });
                });

                // The arrows carry the dead-plate ink as well. SetButtonTint
                // reaches a plate, its rule and its label; a glyph left at full
                // strength is the one channel that would still read live.
                pressGlyph = press.transform.Find("Glyph").GetComponent<Image>();
                pressGlyph.color = Ink;

                // The price on the line after the arrows, outside the plate: it
                // is what the mark costs, not what it says. Ochre is the cost
                // ink every amber line on this page already wears — and it is
                // outside the button, so SetButtonTint never reaches it.
                pressCost = MakeText(considerationCorner.transform, considerationCost + " amber",
                    13, TextAnchor.MiddleCenter, Ochre, SmallCapsFont);
            }

            refresh = () =>
            {
                var offer = _loop.CurrentExchangeOffer();
                var open = offer != null;
                deal.gameObject.SetActive(open);
                goodsRow.SetActive(open);
                rate.gameObject.SetActive(open);
                amountRow.SetActive(open);
                if (considerationCorner != null)
                {
                    considerationCorner.SetActive(open);
                    if (open)
                    {
                        var canPress = _loop.CanPressConsideration();
                        press.interactable = canPress;
                        SetButtonTint(press, canPress);
                        pressGlyph.color = canPress ? Ink : Ink2;
                        pressCost.color = canPress ? Ochre : Ink2;
                    }
                }

                if (!open)
                {
                    foreach (var tierRow in tierRows)
                    {
                        tierRow.Row.SetActive(false);
                    }

                    idle.gameObject.SetActive(true);
                    idle.text = "gather more before the caravan will barter.";
                    return;
                }

                deal.text = "the caravan asks " + GoodName(offer.from) + ", and pays in " + GoodName(offer.to);

                // A plate with no sprite draws a plain white square, so the
                // picture is switched off rather than emptied — the same guard
                // the post plates use for content added ahead of its art.
                var fromArt = ArtLibrary.ForGood(offer.from);
                var toArt = ArtLibrary.ForGood(offer.to);
                fromPlate.sprite = fromArt;
                fromPlate.enabled = fromArt != null;
                toPlate.sprite = toArt;
                toPlate.enabled = toArt != null;
                arrow.gameObject.SetActive(fromArt != null || toArt != null);

                // Per-unit, the caravan's cut, and the deal's clock — this card
                // is the game's only price signal, and a deal that turns on its
                // own must say when.
                //
                // Two lines, and both changes are about a measure that would not
                // hold. The good names left it because the plates above and the
                // deal line already say the pair twice; that alone took the line
                // from 1207px to 882 against a ~906 portrait measure, which is
                // 2px of slack at the widest real values ("123.46 each · the
                // caravan keeps 15% · a new deal in 12m 34s" is 904) — a line
                // that wraps for SOME deals and not others, which reads worse
                // than one that always did. So the clock, the only fact here
                // that moves every second, takes the second line: 543 and 318 at
                // their widest, inside a 4:3 spread column's ~748 as well.
                rate.text = NumberFormat.Rate(_loop.ExchangeRate(offer.from, offer.to)) + " each"
                            + "  ·  <color=" + OchreInkHex + ">the caravan keeps "
                            + Percent(_loop.Data.exchange.spread) + "</color>"
                            + "\n" + "a new deal in " + NumberFormat.Duration(_loop.ExchangeOfferSecondsRemaining());

                foreach (var chip in chips)
                {
                    SetButtonChosen(chip.Plate, System.Math.Abs(chip.Fraction - _exchangeFraction) < 0.001);
                }

                var anyHeld = false;
                foreach (var (quality, row, trade) in tierRows)
                {
                    var held = Exchange.Held(_loop.State, offer.from, quality);

                    // A whole unit is the smallest thing the caravan will take,
                    // so a sub-unit crumb shows no row at all rather than one
                    // reading "0" beside a dead button.
                    var show = held >= BigDouble.One;
                    row.SetActive(show);
                    if (!show)
                    {
                        continue;
                    }

                    anyHeld = true;
                    var spend = Exchange.Portion(held, _exchangeFraction);
                    var got = _loop.ExchangeQuote(offer.from, offer.to, spend, quality);

                    // And a whole unit is the smallest thing it will pay: a
                    // deal that comes back under one would read as "→ 0".
                    var live = got >= BigDouble.One;
                    trade.interactable = live;
                    SetButtonTint(trade, live, true);
                    SetButtonLabel(trade, "Trade " + ExchangeDeal(offer, quality, spend, got)
                                          + (quality == QualityTier.Poor
                                              ? string.Empty
                                              : "\n" + SizeOpen(14) + TierName(quality).TrimEnd() + " trades in at ×"
                                                + PlainNumber(Exchange.QualityValueMultiplier(_loop.Data, quality)) + "</size>"));
                }

                idle.gameObject.SetActive(!anyHeld);
                idle.text = "no " + GoodName(offer.from) + " to give. the deal turns on its own; wait it out.";
            };

            refresh();
            _liveUpdaters.Add(refresh);
        }

        /// <summary>"decent " / "choice " — the tier as the deal speaks it; plain goods go unmarked.</summary>
        private static string TierName(QualityTier quality)
        {
            switch (quality)
            {
                case QualityTier.Decent:
                    return "decent ";
                case QualityTier.Choice:
                    return "choice ";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// "120 decent berries → 18 wildflowers". Whole units on both sides,
        /// like every other resource readout in the journal — the caravan is the
        /// one card whose rates would otherwise speak in halves and thirds, and
        /// a choice pile trading in at ×2.5 makes a meal of it.
        /// </summary>
        private string ExchangeDeal(ExchangeOffer offer, QualityTier quality, BigDouble spend, BigDouble got)
        {
            return NumberFormat.Short(spend) + " " + TierName(quality) + GoodName(offer.from)
                   + " → " + NumberFormat.Short(got) + " " + GoodName(offer.to);
        }

        /// <summary>
        /// Strike the deal — or, for the whole stock, ask first. Amber asks
        /// before it's spent for the same reason: emptying a good the camp has
        /// been gathering all session is not a thing to do by accident.
        /// </summary>
        private void OfferTrade(Button trade, QualityTier quality)
        {
            var offer = _loop.CurrentExchangeOffer();
            if (offer == null)
            {
                return;
            }

            var spend = Exchange.Portion(Exchange.Held(_loop.State, offer.from, quality), _exchangeFraction);
            var got = _loop.ExchangeQuote(offer.from, offer.to, spend, quality);
            if (got < BigDouble.One)
            {
                return;
            }

            if (_exchangeFraction < 1.0)
            {
                CommitTrade(trade, offer, quality, spend);
                return;
            }

            // Choice finds have two other suitors — the folio's pages and the
            // rite's specimen slots — so emptying that pool warns of both.
            var caution = quality == QualityTier.Choice
                ? " the folio and the rite ask for choice finds too."
                : string.Empty;
            _hud.Sheets.OpenConfirmSheet(
                "Trade all your " + TierName(quality) + GoodName(offer.from) + "?",
                ExchangeDeal(offer, quality, spend, got) + ", and the trail starts that pile again." + caution,
                "Trade all",
                () => CommitTrade(trade, offer, quality, spend));
        }

        private void CommitTrade(Button trade, ExchangeOffer offer, QualityTier quality, BigDouble spend)
        {
            // Nothing captured when the confirm opened is taken on trust here.
            // The sheet can sit open while the caravan turns its deal over and the
            // camp goes on working, and TryTrade checks the pile but never the
            // standing offer — so an expired pair would still be traded, and a
            // spend larger than the pile just answers zero, which read as a
            // confirmed trade that quietly did nothing.
            var current = _loop.CurrentExchangeOffer();
            if (current == null || current.from != offer.from || current.to != offer.to)
            {
                SetNote("the caravan had already turned that deal over. nothing traded.");
                _dirty = true;
                return;
            }

            // Never more than was quoted (the pile may have grown since), never
            // nothing merely because it shrank.
            var held = Exchange.Held(_loop.State, offer.from, quality);
            if (held < spend)
            {
                spend = held;
            }

            // Quoted before it is struck: a pile that shrank far enough for the
            // deal to come back under a whole unit is refused outright, rather
            // than traded away for a flash reading "+0".
            var got = _loop.ExchangeQuote(offer.from, offer.to, spend, quality) >= BigDouble.One
                ? _loop.TradeAtExchange(offer.from, offer.to, spend, quality)
                : BigDouble.Zero;
            if (got > BigDouble.Zero)
            {
                Flash(trade, "+" + NumberFormat.Short(got) + " " + GoodName(offer.to), true);
                SetNote("traded " + TierName(quality) + GoodName(offer.from) + " for " + GoodName(offer.to)
                        + ". a nod. gone before the count.");
            }
            else
            {
                SetNote("that pile was spoken for before the deal was struck. nothing traded.");
            }

            _dirty = true;
        }
    }
}
