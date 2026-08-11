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
    /// The Trail page — the trail home, a recruit bar when a companion will
    /// answer a pile, the zones' compact node plates, the watch planter cards,
    /// and the Rite's verse cards and waystone footer. Posting lives on the
    /// world strip's badges and on the trail-home line at the head of this page
    /// (one body per post), so the plates here carry only the land's own
    /// business: yields, replanting, planters. The kith roster now
    /// lives on the Warden page.
    /// </summary>
    internal sealed class TrailPage : JournalSection
    {
        internal TrailPage(GameHud hud) : base(hud) { }

        internal void BuildTrailPage()
        {
            BuildTideLine();
            BuildKeepingCard();
            BuildTrailHomeLine();
            BuildRecruitBar();

            var unlockedZones = ZonesInOrder();
            // Newest zone first — the page header names it, so the page must
            // open on it; the older grounds follow, folded shut behind their
            // names so eight zones of plates stay one readable page. Every one
            // of them is still worked whether or not its plates are drawn:
            // folding shortens the page, it doesn't rest the land.
            unlockedZones.Reverse();
            var newest = NewestZoneId();
            var figure = 1;
            foreach (var zone in unlockedZones)
            {
                // One ground and no heading to press: it must never fold, or
                // the page could be shut with nothing left to open it with.
                var open = unlockedZones.Count == 1 || JournalZones.IsOpen(_zoneOpen, zone.id, newest);
                if (unlockedZones.Count > 1)
                {
                    BuildZoneHeading(zone, open);
                }

                if (!open)
                {
                    continue;
                }

                // The zone's keystone specimen heads its section (design §3) —
                // a modest mark, not a full plate; the strip carries the art.
                var keystone = ArtLibrary.ForZone(zone.id);
                if (keystone != null)
                {
                    PlateImage(_body, keystone, 120f).name = "Keystone";
                }

                foreach (var node in _loop.State.nodes)
                {
                    if (node.zoneId == zone.id)
                    {
                        BuildNodePlate(node, figure++);
                    }
                }
            }

            foreach (var site in _loop.State.digSites)
            {
                BuildWatchPlate(site);
            }

            BuildVerseCards();
            BuildWaystoneFooter();
        }

        /// <summary>
        /// A ground's name, and the fold that opens or shuts it. Closed, the
        /// name carries what grows there — the page still reads as an index of
        /// the trail rather than a row of shut drawers, and the warden can see
        /// where the fibres are without opening anything.
        /// <para>
        /// It's the journal's button plate rather than furniture of its own so
        /// that it is a real control: focus reaches it, the pad presses it, and
        /// it answers a touch the way every other plate does. The name is set
        /// smaller than a button's usual voice — it heads a section, it doesn't
        /// ask for anything.
        /// </para>
        /// <para>
        /// A chevron in the left margin says which way the ground is folded.
        /// Without it, which way a plate opens is legible only by inference —
        /// from whether plates follow it, and from the growing-list subtitle a
        /// shut ground wears — so a ground with nothing under it reads as an
        /// unresponsive button rather than an empty open one.
        /// </para>
        /// </summary>
        private void BuildZoneHeading(ZoneData zone, bool open)
        {
            var captured = zone.id;
            var name = zone.displayName.ToUpperInvariant();
            var label = open
                ? SizeOpen(15) + name + "</size>"
                : SizeOpen(15) + name + "</size>" + SizeOpen(13) + "\n<color=" + Ink2Hex + ">"
                  + ZoneGrowth(zone) + "</color></size>";

            // The heading hands its own rect to the fold, which notes where it
            // stands in the viewport — the rebuilt page puts it back there.
            Button heading = null;
            heading = Button(_body, label, 400, () => _hud.FoldZone(captured, (RectTransform)heading.transform));
            heading.gameObject.name = "ZoneHeading";
            AddFoldArrow(heading, open);

            // The heading the page is being rebuilt around: the scroll comes
            // back to it once the fresh page has a height, so the ground the
            // player opened is still under the finger that opened it.
            if (captured == _hud.PendingZoneFold)
            {
                _hud.FoldedHeading = (RectTransform)heading.transform;
            }
        }

        /// <summary>The width of the heading's left margin, and the chevron standing in it.</summary>
        private const float FoldArrowLane = 56f;
        private const float FoldArrowGlyph = 34f;

        /// <summary>
        /// Pin the fold chevron into a heading's left margin — down while the
        /// ground is open, turned a quarter to the right while it is shut.
        /// <para>
        /// The mark ignores the layout and the name is inset by the same lane on
        /// BOTH sides, so a centred heading stays centred over its plates instead
        /// of shunting right by half an arrow.
        /// </para>
        /// </summary>
        private void AddFoldArrow(Button heading, bool open)
        {
            var label = heading.GetComponentInChildren<Text>();
            if (label != null)
            {
                var labelRect = (RectTransform)label.transform;
                labelRect.offsetMin = new Vector2(FoldArrowLane, labelRect.offsetMin.y);
                labelRect.offsetMax = new Vector2(-FoldArrowLane, labelRect.offsetMax.y);
            }

            var go = new GameObject("FoldArrow", typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(heading.transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.sprite = FoldArrowSprite();
            image.preserveAspect = true;
            // The plate takes the tap; a chevron that swallowed it would leave a
            // dead spot in the middle of the control it describes.
            image.raycastTarget = false;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(FoldArrowGlyph, FoldArrowGlyph);
            rect.anchoredPosition = new Vector2(FoldArrowLane * 0.5f, 0f);
            // Drawn pointing down; a quarter turn anticlockwise points it at the
            // name, which is where a shut ground's contents have gone.
            rect.localRotation = Quaternion.Euler(0f, 0f, open ? 0f : 90f);
        }

        /// <summary>
        /// The newest unlocked ground — the one the page opens on, and the
        /// default every unpressed fold is measured against.
        /// </summary>
        internal string NewestZoneId()
        {
            var zones = ZonesInOrder();
            return zones.Count > 0 ? zones[zones.Count - 1].id : null;
        }

        /// <summary>
        /// What a folded ground is still growing, named in its own words —
        /// the nodes' resources, in the order the page would have drawn them.
        /// </summary>
        private string ZoneGrowth(ZoneData zone)
        {
            var growing = new List<string>();
            foreach (var node in _loop.State.nodes)
            {
                if (node.zoneId == zone.id && !growing.Contains(node.resourceId))
                {
                    growing.Add(node.resourceId);
                }
            }

            return growing.Count == 0 ? "folded" : string.Join(" · ", growing.ToArray());
        }

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

        /// <summary>
        /// The trail home — "the trail home" on the left, a dotted rule with
        /// the day's deliveries walking it, and a line of status on the right.
        /// Pure presentation now: deliveries are automatic and lossless, so
        /// the dot is the delivery batch walking to camp (one per
        /// economy.delivery.batchSeconds while anything is pooled), and the
        /// fell pony keeps her half-step alongside while she's owned. Nothing
        /// here is postable any more — the carrier post left with the hauling
        /// system.
        /// </summary>
        private void BuildTrailHomeLine()
        {
            var bar = MakePanel("TrailHome", _body, CardPaper);
            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 4, 4);
            layout.spacing = 10;
            var element = bar.AddComponent<LayoutElement>();
            element.flexibleHeight = 0;
            element.minHeight = 100f;
            AddBorder(bar, Ink2);

            MakeText(bar.transform, "the trail home", 20, TextAnchor.MiddleLeft, Ink2, _hand);

            var lineGo = MakeRect("Line", (RectTransform)bar.transform).gameObject;
            var lineElement = lineGo.AddComponent<LayoutElement>();
            lineElement.flexibleWidth = 1f;
            lineElement.minHeight = 22f;

            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(lineGo.transform, false);
            var ruleImage = rule.GetComponent<Image>();
            ruleImage.sprite = DashSprite();
            ruleImage.type = Image.Type.Tiled;
            ruleImage.raycastTarget = false;
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.anchorMin = new Vector2(0f, 0.5f);
            ruleRect.anchorMax = new Vector2(1f, 0.5f);
            ruleRect.offsetMin = new Vector2(0f, -1f);
            ruleRect.offsetMax = new Vector2(0f, 1f);

            var dot = new GameObject("Carrier", typeof(Image));
            dot.transform.SetParent(lineGo.transform, false);
            var dotImage = dot.GetComponent<Image>();
            dotImage.color = MossDeep;
            dotImage.raycastTarget = false;
            var carrierDot = (RectTransform)dot.transform;
            carrierDot.sizeDelta = new Vector2(14f, 14f);

            // The pony's lane shares the one dashed line — two dots walking it
            // is the whole picture of a second lane, and it costs no layout.
            // She keeps half a step out of phase so the pair reads as two
            // bodies rather than one blurred dot.
            var ponyGo = new GameObject("Pony", typeof(Image));
            ponyGo.transform.SetParent(lineGo.transform, false);
            var ponyImage = ponyGo.GetComponent<Image>();
            ponyImage.color = MossDeep;
            ponyImage.raycastTarget = false;
            var ponyDot = (RectTransform)ponyGo.transform;
            ponyDot.sizeDelta = new Vector2(14f, 14f);

            var status = MakeText(bar.transform, string.Empty, 20, TextAnchor.MiddleRight, Ink2, _hand);

            // The dot walks per frame; the status only changes on the
            // cadence, like every other label on the page.
            _frameUpdaters.Add(() =>
            {
                var batchSeconds = _loop.Data.economy?.delivery?.batchSeconds ?? 0.0;
                var pending = false;
                foreach (var node in _loop.State.nodes)
                {
                    if (node.basket > BigDouble.Zero)
                    {
                        pending = true;
                        break;
                    }
                }

                var show = pending && batchSeconds > 0.0;
                var showPony = Stationing.OccupantOf(_loop.State, Familiar.PonyStation) != null;
                carrierDot.gameObject.SetActive(show);
                ponyDot.gameObject.SetActive(showPony);

                var fraction = show
                    ? Mathf.Clamp01((float)(_loop.State.deliveryProgress / batchSeconds))
                    : 0f;
                if (show)
                {
                    carrierDot.anchorMin = new Vector2(fraction, 0.5f);
                    carrierDot.anchorMax = new Vector2(fraction, 0.5f);
                    carrierDot.anchoredPosition = Vector2.zero;
                }

                if (showPony)
                {
                    var ponyFraction = Mathf.Repeat(fraction + 0.5f, 1f);
                    ponyDot.anchorMin = new Vector2(ponyFraction, 0.5f);
                    ponyDot.anchorMax = new Vector2(ponyFraction, 0.5f);
                    ponyDot.anchoredPosition = Vector2.zero;
                }
            });

            _liveUpdaters.Add(() =>
            {
                var pony = Stationing.OccupantOf(_loop.State, Familiar.PonyStation);
                status.text = pony != null
                    ? pony.name + " walks at " + _loop.WardenNamePossessive() + " side"
                    : "the day's pickings walk themselves home";
            });
        }

        /// <summary>
        /// The recruit bar — a bordered call-out at the top of the Trail page
        /// that only shows while a verse-earned pile waits (design §4). The next
        /// companion is set, not a choice: the first node still missing its
        /// specialist names who is coming, and one line leaves the pile of that
        /// node's resource to bring them in.
        /// </summary>
        private void BuildRecruitBar()
        {
            var card = Card("A COMPANION WILL ANSWER");
            var intro = MakeText(card, string.Empty, 18, TextAnchor.MiddleCenter, Ink2, _serif);

            // The set target, kept in a captured holder the live updater refreshes
            // and the tap reads — as recruits join, it advances to the next node.
            NodeState target = null;
            var line = MakeText(card, string.Empty, 19, TextAnchor.MiddleLeft, Ink2);
            var button = line.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (target == null)
                {
                    return;
                }

                var arrived = _loop.LeaveGift(target);
                if (arrived != null)
                {
                    SetNote(arrived.IsResting
                        ? "you left a pile of " + target.resourceId + " and stepped back. something said yes. it rests at camp until you give it a post."
                        : "you left a pile of " + target.resourceId + " and stepped back. something said yes.");
                    _dirty = true;
                }
                else if (_loop.GiftWaitsOnAmber(target))
                {
                    SetNote("the pile is ready, but the asking is " + NumberFormat.Short(_loop.GiftCallingCost())
                            + " amber. it will wait.");
                }
                else
                {
                    SetNote("not enough " + target.resourceId + " for a proper pile. keep picking.");
                }
            });
            line.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            AddDashedBorder(line.gameObject);

            _liveUpdaters.Add(() =>
            {
                target = NextGiftNode();
                var species = target != null ? _loop.GiftSpeciesFor(target) : null;
                var show = species != null;
                card.gameObject.SetActive(show);
                if (!show)
                {
                    return;
                }

                intro.text = "<i>a " + species.displayName + " is drawn to the " + target.resourceId
                             + ". leave a pile and it stays.</i>";
                var pile = NumberFormat.Short(_loop.GiftPileCost()) + " " + target.resourceId;
                if (_loop.GiftCallingCost() > 0.0)
                {
                    pile += " · " + NumberFormat.Short(_loop.GiftCallingCost()) + " amber";
                }
                if (_loop.CanLeaveGift(target))
                {
                    // Moss — an invitation; ochre stays with costs and halts.
                    line.text = "<color=" + MossDeepHex + ">+  leave a pile: " + pile + "</color>";
                }
                else
                {
                    line.text = "leave a pile: " + pile + " (not enough yet)";
                }
            });
        }

        /// <summary>
        /// The set node whose specialist is next to be called: the first node
        /// (in the land's own order) still missing its specialist while a pile
        /// waits, or null when no pile is on offer or every specialist walks.
        /// </summary>
        private NodeState NextGiftNode()
        {
            if (!_loop.GiftAvailable())
            {
                return null;
            }

            foreach (var node in _loop.State.nodes)
            {
                if (_loop.GiftSpeciesFor(node) != null)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// The post plate and the picture it wears. Narrower than the worded
        /// buttons it replaces (190 at a node, 220 at the watch) because a
        /// picture needs no room to wrap — the width goes back to the node's own
        /// line. 84 is as small as a portrait can be drawn and still read as an
        /// animal rather than a smudge, and it clears the plate's 120 of touch
        /// height either side.
        /// <para>
        /// 120 rather than 150 from 2026-08-11: once the node's line stopped
        /// naming the body standing here (the plate says it) and Plant back
        /// moved to the action strip, the plate was the only thing left holding
        /// the top row's width, and 84 + insets wants nothing near 150. Square
        /// with the 120 of touch height, which is the floor either way.
        /// </para>
        /// </summary>
        private const float PostPlate = 120f;
        private const float PostGlyph = 84f;

        /// <summary>
        /// The two purchase plates on a node's action strip, sized against each
        /// other rather than each on its own (2026-08-11). Both carry a name on
        /// the first line and their price on a 14pt second line, so the widths
        /// are set by whichever line is longest and the pair reads as one strip.
        /// <para>
        /// Both widths are MEASURED against IMFellEnglishSC at the sizes uGUI
        /// actually renders (FontScale 2, so a 19pt label is 38px and a
        /// SizeOpen(14) line is 28px), against the plate's width less Button's
        /// 10 of inset either side. They are not eyeballed, and the numbers below
        /// are the reason each is what it is.
        /// </para>
        /// <para>
        /// <see cref="PlanterPlate"/> at 420 (400 usable) holds the longest
        /// authored planter name, "Raise Mineshaft Beams", at 375px = 94%, and a
        /// single-material price, "+25% yield · 6 cordage (have 4)", at 353 = 88%.
        /// At the old 320 the name alone wrapped, the price wrapped again, and
        /// FitToLabel grew the plate to four lines — which is how an action the
        /// player could not yet afford became the tallest thing on the card,
        /// roughly 43% of it. A multi-material bundle measures 579 = 145% and
        /// still takes a second line; that is the one case worth the height.
        /// </para>
        /// <para>
        /// <see cref="ReplantPlate"/> at 260 (240 usable) holds "Plant back" at
        /// 175 = 73% over its price line. Six characters of cost fit — both
        /// "88.88K" and "888.8K → +10% yield" measure 238 = 99% — and seven do
        /// not: "123.46K → +10% yield" is 253 = 105% and wraps, since Short's
        /// "0.##" can reach a six-character mantissa. A deep run therefore grows
        /// this plate a line, the same accepted loss as the bundle above.
        /// Deliberately NOT matched to the planter's 420: two equal plates would
        /// pad this one with 160 of empty paper, and a centred strip has no row
        /// to fill.
        /// </para>
        /// <para>
        /// <b>The ceiling is the SPREAD, not the phone.</b> The Trail is the
        /// right-hand column of every wide layout (it has no tab there — see
        /// GameHud.BuildBody), so these must fit a half page, which is far
        /// tighter than portrait and easy to forget while looking at a phone.
        /// The narrowest supported spread is 4:3 landscape, design §12's own
        /// case: a 2048x1536 screen scales to ~1663 canvas units, less 2x16 of
        /// page margin and SpreadGap 78 over two columns gives a ~776 column and
        /// a ~748 card interior once Card's 14 either side is paid. The strip
        /// spends 40 on the trellis mark plus 8 of spacing either side of each
        /// plate, so 420 + 260 + 56 = 736 lands 12 inside that. Portrait is the
        /// easy case at ~906. Below JournalLayout.MinWideWidth 1200 a column's
        /// interior falls to ~517 and the plates squeeze under their preferred
        /// width, wrapping rather than clipping — the same graceful loss the
        /// worded buttons always had there. <b>Raise either number and check the
        /// 4:3 spread, not the phone.</b>
        /// </para>
        /// </summary>
        private const float PlanterPlate = 420f;
        private const float ReplantPlate = 260f;

        /// <summary>
        /// What a post's plate shows: the body standing there — a companion's
        /// own portrait, the warden's mark — and the moss (+) when the ground is
        /// nobody's. The occupant is asked first and answers alone, the same
        /// order the line beside it reads in, so plate and words can't disagree
        /// about who is on the ground.
        /// <para>
        /// Null when a posted companion's species has no plate: the button then
        /// falls back to its words (see <see cref="JournalWidgets.GlyphButton"/>)
        /// rather than showing the (+), which would say the ground was free when
        /// it is held. Shipped content can't reach that — ArtLibraryTests pins a
        /// plate to every species — but a species added ahead of its art can.
        /// </para>
        /// </summary>
        private static Sprite PostMark(Familiar occupant, bool wardenPosted)
        {
            if (occupant != null)
            {
                return ArtLibrary.ForSpecies(occupant.speciesId);
            }

            return wardenPosted ? ArtLibrary.ForWarden() : PlusSprite();
        }

        private void BuildNodePlate(NodeState node, int figure)
        {
            var captured = node;
            var card = Card(null);

            // Two bands, and the split is what the player can spend: the top row
            // is what this ground IS and who works it, and nothing there costs
            // anything; the action strip below holds the purchases. Plant back
            // sat at the top right until 2026-08-11, which put a repeatable
            // purchase among the facts and left its 190 of width out of the
            // line's reach. Giving that width back is what unwraps the mastery
            // line: the text column runs 324 → 552 on a spread's half page and
            // reaches 710 in portrait, where "mastery IV · +20% yield & worth ·
            // 99% to next" measures 562 and finally sits on one line. On a 4:3
            // spread it is still 2% over and takes two — better than the three
            // it took before, and not worth shrinking the type for. The full
            // specimen plate lives on the world strip above — no need to repeat
            // it large.
            var row = Row(card);
            var face = ArtLibrary.ForResource(captured.resourceId);
            if (face != null)
            {
                IconImage(row.transform, face, 60f, Color.white);
            }

            var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft, Ink, _serif);
            FlexibleWidth(label.gameObject, 1f);

            // The page must offer the post it describes. With posting only on
            // the world strip's plates, the node's own card can say "0.0/s"
            // without ever explaining or fixing it. The plate wears the body
            // standing here rather than a verb — see PostMark.
            var post = GlyphButton(row.transform, PlusSprite(), "Post here", PostPlate, PostGlyph,
                () => _hud.Sheets.OpenPostingSheet(captured.id));

            // The tend flash — the "+ yield" that rises and fades; sits outside
            // the layout at the plate's top edge, clear of the post plate on the
            // right. It cleared Plant back's 190 until that moved down, so this
            // tracks PostPlate now rather than a width no longer on the row.
            const float flashInset = PostPlate + 10f;
            var flash = MakeText(card, "+ yield", 22, TextAnchor.MiddleRight, MossDeep, _hand);
            flash.gameObject.name = "TendFlash";
            flash.raycastTarget = false;
            flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var flashRect = (RectTransform)flash.transform;
            flashRect.anchorMin = new Vector2(1f, 1f);
            flashRect.anchorMax = new Vector2(1f, 1f);
            flashRect.pivot = new Vector2(1f, 1f);
            flashRect.sizeDelta = new Vector2(240f, 46f);
            flashRect.anchoredPosition = new Vector2(-flashInset, -4f);
            flash.color = new Color(MossDeep.r, MossDeep.g, MossDeep.b, 0f);
            _tendFlashes[captured.id] = flash;
            _frameUpdaters.Add(() =>
            {
                if (!_flashAges.TryGetValue(captured.id, out var age) || age >= 1f)
                {
                    return;
                }

                age += Time.deltaTime;
                _flashAges[captured.id] = age;
                var t = Mathf.Clamp01(age);
                flash.color = new Color(MossDeep.r, MossDeep.g, MossDeep.b, 1f - t);
                flashRect.anchoredPosition = new Vector2(-flashInset, -4f + 20f * t);
            });

            // The strip is built whether or not planters have been unlocked,
            // because Plant back lives on it now — gating the whole row on
            // PlantersUnlocked would take the node's oldest purchase away from
            // every player who hasn't reached planters yet.
            var actions = ActionRow(card);
            if (_loop.PlantersUnlocked())
            {
                // One trellis heads the planter buttons — the structures raised
                // over the node — rather than repeating it on each.
                var trellis = ArtLibrary.ForLine("planter");
                if (trellis != null)
                {
                    IconImage(actions, trellis, 40f, Color.white);
                }

                foreach (var planter in _loop.NodePlanters())
                {
                    AddPlanterAction(actions, planter, captured.id);
                }
            }

            Button replant = null;
            replant = Button(actions, "Plant back", ReplantPlate, () =>
            {
                if (_loop.Replant(captured))
                {
                    Flash(replant, "planted back", true);
                    SetNote("planted " + captured.resourceId + " back into the ground. it earns tone, not numbers.");
                    _dirty = true;
                }
                else
                {
                    Flash(replant, "not enough " + captured.resourceId, false);
                    SetNote("not enough " + captured.resourceId + " to plant back. the land can wait.");
                }
            });

            // What one planting gives, in numbers — the margin note keeps its
            // riddle, but a repeat purchase can't hide its own effect.
            var richnessPct = Mathf.RoundToInt((float)((_loop.Data.economy?.replant?.richnessPerLevel ?? 0.0) * 100.0));
            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var rich = captured.richnessLevel > 0 ? " · richness " + Roman(captured.richnessLevel) : string.Empty;

                // Who stands here is the PLATE's answer, not the line's: one
                // familiar per species ever, so a portrait names the body as
                // exactly as "Cob posted" did, and the warden has their own
                // mark. Only the empty ground still needs a word — the moss (+)
                // reads as an invitation rather than a diagnosis, and without it
                // "0.0/s" is a riddle whose answer is a picture away.
                var occupant = Stationing.OccupantOf(state, captured.id);
                var wardenHere = Warden.PostNodeId(state) == captured.id;
                var fallow = occupant == null && !wardenHere
                    ? " · <color=" + OchreInkHex + ">fallow</color>"
                    : string.Empty;

                // The warden's own hands are part of this ground's rate — they
                // pocket theirs straight to camp rather than into the basket,
                // which is bookkeeping, not something the plate should hide.
                var rate = Simulation.TotalYieldPerSecond(captured, state, _loop.Data, _loop.Data.economy);
                var stock = state.GetResource(captured.resourceId);
                label.text = captured.resourceId + rich
                             + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + NumberFormat.Rate(rate) + "/s · "
                             + NumberFormat.Short(stock) + " at camp</color>" + fallow
                             + "</size>"
                             + MasteryLine(captured);

                SetButtonGlyph(post, PostMark(occupant, wardenHere),
                    occupant != null || wardenHere ? "Change post" : "Post here");
                // The cost drops the resource's name: the plate is inside a card
                // headed with it, beside its own picture, so "20 mushrooms" spent
                // a line's width restating the title.
                SetButtonLabel(replant, "Plant back\n" + SizeOpen(14) + NumberFormat.Short(_loop.ReplantCost(captured))
                                        + (richnessPct > 0 ? " → +" + richnessPct + "% yield" : string.Empty) + "</size>");
                var ok = _loop.CanReplant(captured);
                replant.interactable = ok;
                SetButtonTint(replant, ok);
            });
        }

        /// <summary>
        /// The node's mastery, said on its own plate (design §4's long-tail
        /// chase). It silently compounded to +495% yield and worth at cap
        /// without the page ever mentioning it — the one climbing number a
        /// collector chases, kept off the card it climbs on. Empty when the
        /// curve is unconfigured (hand-built test data), the sim's own gate.
        /// </summary>
        private string MasteryLine(NodeState node)
        {
            var economy = _loop.Data.economy;
            if (!Mastery.Configured(economy))
            {
                return string.Empty;
            }

            var level = Mastery.Level(node, economy);
            // Floored and held under 100, never rounded. ProgressToNext clamps to
            // [0, 1], so RoundToInt printed "100% to next" for anything from
            // 99.5% up and the card claimed a level the node did not have — on
            // the one line a player watches to see whether the ground is still
            // climbing. 99% is the honest ceiling: the level itself is what says
            // the rung landed.
            var toNext = Mathf.Min(99, Mathf.FloorToInt((float)(Mastery.ProgressToNext(node, economy) * 100.0)));
            string reading;
            if (level <= 0)
            {
                reading = "mastery: " + toNext + "% to the first level";
            }
            else
            {
                var bonus = "+" + Percent(level * economy.mastery.yieldBonusPerLevel) + " yield & worth";
                reading = level >= economy.mastery.maxLevel
                    ? "mastery " + Roman(level) + " · " + bonus + " · <color=" + MossDeepHex + ">the hand knows this ground</color>"
                    : "mastery " + Roman(level) + " · " + bonus + " · " + toNext + "% to next";
            }

            return "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + reading + "</color></size>";
        }

        /// <summary>
        /// The watch is not a post — the wanderer passes each site as it roams.
        /// The card carries the site's own clocks — how often a sketch comes and
        /// the pity timer that guarantees one, both load-bearing and otherwise
        /// invisible — plus the site's planters, with a one-line note on whether
        /// anyone wanders. The card also offers the wander post itself: the
        /// strip carries no wander plate, so the page describing the watching is
        /// where the watcher is sent.
        /// </summary>
        private void BuildWatchPlate(DigSiteState site)
        {
            var captured = site;
            var card = Card("THE WATCH · " + ZoneName(site.zoneId).ToUpperInvariant());
            var row = Row(card);
            var line = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
            FlexibleWidth(line.gameObject, 1f);
            var post = GlyphButton(row.transform, PlusSprite(), "Post a wanderer", PostPlate, PostGlyph,
                () => _hud.Sheets.OpenPostingSheet(Familiar.WanderStation));
            var clocks = MakeText(card, string.Empty, 16, TextAnchor.MiddleLeft, Ink2);

            if (_loop.PlantersUnlocked() && _loop.DigSitePlanters().Count > 0)
            {
                var actions = ActionRow(card);
                foreach (var planter in _loop.DigSitePlanters())
                {
                    AddPlanterAction(actions, planter, captured.zoneId);
                }
            }

            _liveUpdaters.Add(() =>
            {
                // Ask the same question Observation asks — WanderAgents, not
                // Stationing.Wandering: the warden may hold the wander post
                // too, and counting only familiars told a warden who WAS
                // wandering that nobody was.
                var watching = Stationing.WanderAgents(_loop.State, _loop.Data) > 0.0;
                line.text = watching
                    ? "the wanderer passes through, watching where the small lives cross"
                    : "<color=" + OchreInkHex + ">no one wanders, and the small lives go unrecorded.</color>";
                // The wander post takes one body like any other, so its plate
                // wears that body — the roamer's own portrait, or the warden's
                // mark when they are the one walking the run.
                SetButtonGlyph(post, PostMark(Stationing.OccupantOf(_loop.State, Familiar.WanderStation),
                        Warden.IsWandering(_loop.State)),
                    watching ? "Change post" : "Post a wanderer");

                // The clocks only run while someone watches — quoting a rate
                // to an empty site would contradict the line above it.
                var text = watching ? WatchClocks(captured) : string.Empty;
                clocks.gameObject.SetActive(text.Length > 0);
                clocks.text = text;
            });
        }

        /// <summary>
        /// The site's watched-hour clocks: how often a sketch comes at the
        /// current rates, and the pity timer's guarantee with the hours
        /// already banked toward it — the anti-starvation maths that used to
        /// run unseen. The deep amber's own slower clock joins it at the one
        /// site that surfaces the pieces.
        /// </summary>
        private string WatchClocks(DigSiteState site)
        {
            var observation = _loop.Data.economy?.observation;
            if (observation == null || observation.baseSketchesPerHour <= 0.0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var eligible = Observation.EligibleInsects(_loop.State, _loop.Data, site.zoneId);
            if (eligible.Count > 0)
            {
                var watchers = Stationing.WanderAgents(_loop.State, _loop.Data);
                var siteMult = Upgrades.DigSpeedMultiplier(_loop.State, _loop.Data)
                               * Planters.DigSpeedMultiplier(_loop.State, _loop.Data, site.zoneId);
                var totalRarity = 0.0;
                foreach (var insect in eligible)
                {
                    totalRarity += insect.rarity;
                }

                var perHour = watchers * observation.baseSketchesPerHour * siteMult * totalRarity;
                var mean = perHour > 0.0 ? "a sketch comes about every " + NumberFormat.Duration(3600.0 / perHour) + " watched" : "the sketching is stalled";
                parts.Add(mean + (observation.pityTimerHoursWatched > 0.0
                    ? ", and is certain by " + NumberFormat.Duration(observation.pityTimerHoursWatched * 3600.0)
                      + " · " + NumberFormat.Duration(site.pityHours * 3600.0) + " watched so far"
                    : string.Empty));
            }
            else
            {
                parts.Add("every plate here is recorded; the small lives go on unwatched");
            }

            var amber = _loop.Data.deepAmber;
            if (Sim.DeepAmber.Configured(_loop.Data) && amber.zoneId == site.zoneId
                && amber.pityHoursWatched > 0.0
                && !Sim.DeepAmber.IsComplete(_loop.State, _loop.Data))
            {
                parts.Add("the old resin gives up a piece by "
                          + NumberFormat.Duration(amber.pityHoursWatched * 3600.0) + " watched at the longest · "
                          + NumberFormat.Duration(_loop.State.deepAmberPityHours * 3600.0) + " banked toward it");
            }

            return string.Join("\n", parts);
        }

        private void AddPlanterAction(Transform actions, PlanterData planter, string targetId)
        {
            var capturedPlanter = planter;
            var capturedTarget = targetId;
            // The same structure wears the name of the work it serves — a trellis
            // over berries, mineshaft beams over ore, set nets over a fishing run.
            var name = PlanterDisplayName(capturedPlanter, capturedTarget);
            // What the structure is FOR. A name and a material bundle was all
            // either state carried, so "Reed Screen" asked for 20 reeds without
            // once saying it steadies the sketching at this site.
            var gives = PlanterGives(capturedPlanter);
            var givesTail = gives.Length > 0 ? gives + " · " : string.Empty;
            if (_loop.PlanterBuilt(capturedPlanter, capturedTarget))
            {
                var built = MakeText(actions, name + ": raised"
                                              + (gives.Length > 0 ? " · " + gives : string.Empty),
                    16, TextAnchor.MiddleLeft, MossDeep);
                built.gameObject.name = "PlanterBuilt";
                return;
            }

            Button build = null;
            build = Button(actions, "Raise " + name
                                        + "\n" + SizeOpen(14) + givesTail + BundleLabel(capturedPlanter.materials) + "</size>", PlanterPlate, () =>
            {
                if (_loop.BuildPlanter(capturedPlanter, capturedTarget))
                {
                    Flash(build, "raised", true);
                    SetNote("raised a " + name.ToLowerInvariant() + ". the ground holds it now.");
                    _dirty = true;
                }
            });
            _liveUpdaters.Add(() =>
            {
                SetButtonLabel(build, "Raise " + name
                                     + "\n" + SizeOpen(14) + givesTail + BundleHaveLabel(capturedPlanter.materials) + "</size>");
                var ok = _loop.CanBuildPlanter(capturedPlanter, capturedTarget);
                build.interactable = ok;
                SetButtonTint(build, ok);
            });
        }

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
                    offer = Button(row.transform, "Offer one", 180, () =>
                    {
                        if (_loop.OfferSpecimen(verse, slotIndex))
                        {
                            Flash(offer, "set down", true);
                            SetNote("set the perfect one down. it deserved better than a page, maybe.");
                        }
                        else
                        {
                            Flash(offer, "no such find in hand", false);
                            SetNote("no such find in hand. the site is patient.");
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

        private void BuildWaystoneFooter()
        {
            var zone = LatestZone();
            if (zone == null)
            {
                return;
            }

            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var stone = ArtLibrary.ForJournal("waystone");
            if (stone != null)
            {
                PlateImage(_body, stone, 200f).name = "WaystoneMark";
            }

            var footer = MakeText(_body, "<i>“" + text + "”</i>\n" + SizeOpen(14) + "WAYSTONE · " + zone.displayName.ToUpperInvariant() + "</size>",
                18, TextAnchor.MiddleCenter, Ink2, _serif);
            footer.gameObject.name = "Waystone";
        }
    }
}
