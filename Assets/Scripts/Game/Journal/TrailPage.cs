using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Trail page — the zones' node plates, the trail-home carrier row,
    /// the watch planter cards, the Rite's verse cards and waystone footer,
    /// and the kith roster card. Posting lives on the world strip's badges
    /// now (one body per post), so the plates here carry only the land's own
    /// business: yields, baskets, replanting, piles, planters.
    /// </summary>
    internal sealed class TrailPage : JournalSection
    {
        internal TrailPage(GameHud hud) : base(hud) { }

        // Naming/stationing sheets live in JournalSheets; forwarded so the
        // builder bodies below read as they did on GameHud.
        private void OpenNamingSheet(Familiar familiar) => _hud.Sheets.OpenNamingSheet(familiar);
        private void Station(Familiar familiar, string stationId) => _hud.Sheets.Station(familiar, stationId);

        internal void BuildTrailPage()
        {
            var unlockedZones = ZonesInOrder();
            // Newest zone first — the page header names it, so the page must
            // open on it; older zones keep farming further down the scroll.
            unlockedZones.Reverse();
            var figure = 1;
            foreach (var zone in unlockedZones)
            {
                if (unlockedZones.Count > 1)
                {
                    var heading = MakeText(_body, zone.displayName.ToUpperInvariant(), 17, TextAnchor.MiddleCenter, Ink2, _smallCaps);
                    heading.gameObject.name = "ZoneHeading";
                }

                // The zone's keystone specimen heads its section (design §3).
                var keystone = ArtLibrary.ForZone(zone.id);
                if (keystone != null)
                {
                    PlateImage(_body, keystone, 240f).name = "Keystone";
                }

                foreach (var node in _loop.State.nodes)
                {
                    if (node.zoneId == zone.id)
                    {
                        BuildNodePlate(node, figure++);
                    }
                }
            }

            BuildTrailRow();

            foreach (var site in _loop.State.digSites)
            {
                BuildWatchPlate(site);
            }

            // Stationing lives with the land it points at — the design's
            // four-page journal gives the Trail page stationing; the Warden
            // page keeps the kit/crafts identity.
            BuildKithCard();

            BuildVerseCards();
            BuildWaystoneFooter();
        }

        private void BuildNodePlate(NodeState node, int figure)
        {
            var captured = node;
            var card = Card(null);
            var cardImage = card.GetComponent<Image>();

            // The resource's plate heads the card — the journal's FIG. figure,
            // the same specimen the world strip pins above the node.
            var face = ArtLibrary.ForResource(captured.resourceId);
            if (face != null)
            {
                PlateImage(card, face, 300f);
            }

            // Tending lives on the world plate above, so the card's top row
            // keeps just the figure label with Plant back at the right.
            var figRow = Row(card);
            var fig = MakeText(figRow.transform, "FIG. " + figure + ".", 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);
            fig.gameObject.name = "Fig";
            FlexibleWidth(fig.gameObject, 1f);
            // A seedling marks Plant back — putting the specimen back in the ground.
            var seedling = ArtLibrary.ForLine("seedling");
            if (seedling != null)
            {
                IconImage(figRow.transform, seedling, 40f, Color.white);
            }

            Button replant = null;
            replant = Button(figRow.transform, "Plant back", 230, () =>
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

            var nameLine = MakeText(card, string.Empty, 26, TextAnchor.MiddleLeft, Ink, _serif);

            // Gift piles (design §4): while a verse-earned pile waits, a node
            // whose specialist hasn't joined offers a dashed pile line — where
            // the pile is left is who answers, and which resource it costs.
            // The line vanishes for that node once its specialist walks.
            var giftLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);
            var giftButton = giftLine.gameObject.AddComponent<Button>();
            giftButton.transition = Selectable.Transition.None;
            giftButton.onClick.AddListener(() =>
            {
                var arrived = _loop.LeaveGift(captured);
                if (arrived != null)
                {
                    SetNote("you left a pile of " + captured.resourceId + " and stepped back. something said yes.");
                    _dirty = true;
                }
                else if (_loop.KithWalking() >= _loop.KithSlots())
                {
                    SetNote("something watches the pile, but every slot is walked. rest someone first.");
                }
                else
                {
                    SetNote("not enough " + captured.resourceId + " for a proper pile. keep picking.");
                }
            });
            giftLine.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            AddDashedBorder(giftLine.gameObject);

            var statsLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);

            // The tend flash — the mock's "+ yield" that rises and fades; sits
            // outside the layout at the plate's top edge, left of Plant back.
            var flash = MakeText(card, "+ yield", 24, TextAnchor.MiddleRight, MossDeep, _hand);
            flash.gameObject.name = "TendFlash";
            flash.raycastTarget = false;
            flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var flashRect = (RectTransform)flash.transform;
            flashRect.anchorMin = new Vector2(1f, 1f);
            flashRect.anchorMax = new Vector2(1f, 1f);
            flashRect.pivot = new Vector2(1f, 1f);
            flashRect.sizeDelta = new Vector2(260f, 52f);
            flashRect.anchoredPosition = new Vector2(-254f, -6f);
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
                flashRect.anchoredPosition = new Vector2(-254f, -6f + 20f * t);
            });

            // The basket bar — fill width driven by the live updater.
            var barGo = MakePanel("Basket", card, PagePaper);
            FixedHeight(barGo, 14);
            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = MossWash;
            fill.raycastTarget = false;
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            AddBorder(barGo, Ink2);

            if (_loop.PlantersUnlocked())
            {
                var actions = ActionRow(card);
                // One trellis heads the planter buttons — the structures raised
                // over the node — rather than repeating it on each.
                var trellis = ArtLibrary.ForLine("planter");
                if (trellis != null)
                {
                    IconImage(actions, trellis, 44f, Color.white);
                }

                foreach (var planter in _loop.NodePlanters())
                {
                    AddPlanterAction(actions, planter, captured.id);
                }
            }

            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var rich = captured.richnessLevel > 0 ? "  ·  richness " + Roman(captured.richnessLevel) : string.Empty;
                nameLine.text = captured.resourceId + rich;
                cardImage.color = captured == _selected ? MossWash : CardPaper;

                var caller = _loop.GiftAvailable() ? _loop.GiftSpeciesFor(captured) : null;
                if (caller != null)
                {
                    giftLine.gameObject.SetActive(true);
                    var pile = NumberFormat.Short(_loop.GiftPileCost()) + " " + captured.resourceId;
                    giftLine.text = _loop.CanLeaveGift(captured)
                        ? "<color=" + OchreInkHex + ">+  leave a pile — " + pile + " · a " + caller.displayName + " watches</color>"
                        : "leave a pile — " + pile + " · a " + caller.displayName + " watches (not yet)";
                }
                else
                {
                    giftLine.gameObject.SetActive(false);
                }

                var cap = NodeBasketCapacity(captured);
                var fraction = cap > BigDouble.Zero ? Mathf.Clamp01((float)(captured.basket / cap).ToDouble()) : 0f;
                fillRect.anchorMax = new Vector2(fraction, 1f);
                var basketFull = fraction >= 0.999f;
                fill.color = basketFull ? OchreWash : MossWash;

                var rate = Simulation.YieldPerSecond(captured, state, _loop.Data, _loop.Data.economy);
                var stock = state.GetResource(captured.resourceId);
                statsLine.text = NumberFormat.Rate(rate) + "/s  ·  " + NumberFormat.Short(stock) + " at camp"
                                 + (basketFull ? "  ·  <color=" + OchreInkHex + ">basket full — waits on a carrier</color>" : string.Empty);

                SetButtonLabel(replant, "Plant back\n" + SizeOpen(15) + NumberFormat.Short(_loop.ReplantCost(captured)) + " " + captured.resourceId + "</size>");
                var ok = _loop.CanReplant(captured);
                replant.interactable = ok;
                SetButtonTint(replant, ok);
            });
        }

        private void BuildTrailRow()
        {
            // The mock's trail line: a dotted rule with the carrier walking it,
            // "the trail home" on one side, who's hauling on the other.
            var rowGo = MakeRect("TrailRow", _body).gameObject;
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.spacing = 10;

            MakeText(rowGo.transform, "the trail home", 20, TextAnchor.MiddleLeft, Ink2, _hand);

            var lineGo = MakeRect("Line", (RectTransform)rowGo.transform).gameObject;
            var lineElement = lineGo.AddComponent<LayoutElement>();
            lineElement.flexibleWidth = 1f;
            lineElement.minHeight = 22f;

            var rule = new GameObject("Rule", typeof(Image));
            rule.transform.SetParent(lineGo.transform, false);
            var ruleImage = rule.GetComponent<Image>();
            ruleImage.sprite = JournalSprites.DashSprite();
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
            var dotRect = (RectTransform)dot.transform;
            dotRect.sizeDelta = new Vector2(14f, 14f);

            // Plain status — the carrier is posted from the strip's trail
            // badge now, so this line only reports the walk.
            var status = MakeText(rowGo.transform, string.Empty, 20, TextAnchor.MiddleRight, Ink2, _hand);

            _frameUpdaters.Add(() =>
            {
                var state = _loop.State;
                var carriers = Stationing.TrailCarriers(state, _loop.Data);
                var tripSeconds = _loop.Data.economy?.hauling?.tripSeconds ?? 0.0;
                var show = carriers > 0.0 && tripSeconds > 0.0;
                dot.SetActive(show);
                if (show)
                {
                    var interval = tripSeconds / carriers;
                    var fraction = Mathf.Clamp01((float)(state.haulTripProgress / interval));
                    dotRect.anchorMin = new Vector2(fraction, 0.5f);
                    dotRect.anchorMax = new Vector2(fraction, 0.5f);
                    dotRect.anchoredPosition = Vector2.zero;
                }
            });

            _liveUpdaters.Add(() =>
            {
                var carrier = Stationing.OccupantOf(_loop.State, Familiar.TrailStation);
                status.text = carrier != null
                    ? carrier.name + " carrying"
                    : "<color=" + OchreInkHex + ">no carrier walks</color>";
            });
        }

        /// <summary>
        /// The watch is not a post any more — the wanderer passes each site
        /// as it roams. The card survives only as the home of the site's
        /// planters, with a one-line note on whether anyone wanders.
        /// </summary>
        private void BuildWatchPlate(DigSiteState site)
        {
            if (!_loop.PlantersUnlocked() || _loop.DigSitePlanters().Count == 0)
            {
                return;
            }

            var captured = site;
            var card = Card("THE WATCH · " + ZoneName(site.zoneId).ToUpperInvariant());
            var line = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);

            var actions = ActionRow(card);
            foreach (var planter in _loop.DigSitePlanters())
            {
                AddPlanterAction(actions, planter, captured.zoneId);
            }

            _liveUpdaters.Add(() =>
            {
                line.text = Stationing.Wandering(_loop.State) > 0
                    ? "the wanderer passes through, watching where the small lives cross"
                    : "<color=" + OchreInkHex + ">no one wanders — the small lives go unrecorded</color>";
            });
        }

        private void AddPlanterAction(Transform actions, PlanterData planter, string targetId)
        {
            var capturedPlanter = planter;
            var capturedTarget = targetId;
            // The same structure wears the name of the work it serves — a trellis
            // over berries, mineshaft beams over ore, set nets over a fishing run.
            var name = PlanterDisplayName(capturedPlanter, capturedTarget);
            if (_loop.PlanterBuilt(capturedPlanter, capturedTarget))
            {
                var built = MakeText(actions, name + " — raised", 16, TextAnchor.MiddleLeft, MossDeep);
                built.gameObject.name = "PlanterBuilt";
                return;
            }

            Button build = null;
            build = Button(actions, "Raise " + name
                                        + "\n" + SizeOpen(14) + BundleLabel(capturedPlanter.materials) + "</size>", 250, () =>
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
                                     + "\n" + SizeOpen(14) + BundleHaveLabel(capturedPlanter.materials) + "</size>");
                var ok = _loop.CanBuildPlanter(capturedPlanter, capturedTarget);
                build.interactable = ok;
                SetButtonTint(build, ok);
            });
        }

        private void BuildVerseCards()
        {
            var rite = Rite.CurrentRite(_loop.State, _loop.Data);
            if (rite == null || rite.verses == null)
            {
                return;
            }

            // Open verses keep their full cards; verses already sung collapse
            // into one summary so the Trail page doesn't grow without bound — a
            // finished verse is a memory, not a worklist. A verse whose site
            // the trail has reached before its turn (verses are sung in
            // order) gets one quiet sealed card; anything past it stays
            // unwritten.
            List<RiteVerseData> sung = null;
            var sealedShown = false;
            var unlockedZones = Upgrades.UnlockedZoneIds(_loop.State, _loop.Data);
            foreach (var verse in rite.verses)
            {
                if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse))
                {
                    if (!sealedShown && unlockedZones.Contains(verse.zone)
                        && Rite.IsVerseSealed(_loop.State, _loop.Data, verse))
                    {
                        BuildSealedVerseCard(rite, verse);
                        sealedShown = true;
                    }

                    continue;
                }

                if (Rite.IsVerseComplete(_loop.State, _loop.Data, verse))
                {
                    (sung ?? (sung = new List<RiteVerseData>())).Add(verse);
                    continue;
                }

                var card = BuildVerseCard(verse);
                if (_firstVerseCard == null)
                {
                    // The tracker's deep-link lands on the first verse still open.
                    _firstVerseCard = card;
                }
            }

            if (sung != null)
            {
                BuildSungVersesCard(sung);
            }
        }

        /// <summary>
        /// The collapsed record of verses already answered — one line each, so a
        /// long-lived rite doesn't bury the open verses under finished ones.
        /// </summary>
        private void BuildSungVersesCard(List<RiteVerseData> verses)
        {
            var card = Card("SUNG VERSES");
            foreach (var verse in verses)
            {
                var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
                var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
                MakeText(card, ZoneName(verse.zone) + "  " + SizeOpen(15) + "<color=" + MossDeepHex
                               + ">" + site + " — sung</color></size>", 18, TextAnchor.MiddleLeft, Ink);
            }
        }

        /// <summary>
        /// The card for a verse whose site is reached but whose turn hasn't
        /// come — it names the verse still barring it and asks nothing.
        /// </summary>
        private void BuildSealedVerseCard(RiteData rite, RiteVerseData verse)
        {
            string barring = null;
            foreach (var earlier in rite.verses)
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

            var card = Card("VERSE OF " + ZoneName(verse.zone).ToUpperInvariant());
            MakeText(card, barring == null
                    ? "<i>the cairn keeps its silence — its turn has not come</i>"
                    : "<i>the cairn keeps its silence — the verse of " + barring + " is still unsung</i>",
                19, TextAnchor.MiddleCenter, Ink2, _serif);
        }

        private RectTransform BuildVerseCard(RiteVerseData verse)
        {
            var capturedVerse = verse;
            var zone = _loop.Data.ZonesById.TryGetValue(verse.zone ?? string.Empty, out var z) ? z : null;
            var site = zone != null && !string.IsNullOrEmpty(zone.verseSite) ? zone.verseSite : ZoneName(verse.zone);
            var card = Card("VERSE OF " + ZoneName(verse.zone).ToUpperInvariant());

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
                var answered = Mathf.Min(Rite.CompletedSlotCount(_loop.State, capturedVerse), _loop.Data.rites.chooseCount);
                progress.text = Rite.IsVerseComplete(_loop.State, _loop.Data, capturedVerse)
                    ? "<color=" + MossDeepHex + ">answered — the verse is sung</color>"
                    : "answered " + answered + " of " + _loop.Data.rites.chooseCount;
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
                            Flash(offer, "set down " + Mathf.FloorToInt((float)given.ToDouble()) + " " + slot.resource, true);
                            SetNote("set down " + Mathf.FloorToInt((float)given.ToDouble()) + " " + slot.resource + ". no answer. not yet.");
                        }
                        else
                        {
                            Flash(offer, "the stores are empty", false);
                            SetNote("nothing in the stores to set down.");
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
                    label.text = "<color=" + MossDeepHex + ">" + name + " — set down</color>";
                }
                else if (expired)
                {
                    // Any three answer the verse; the rest expire (§8) — the
                    // spirits stopped listening to this one.
                    label.text = "<color=" + Ink2Hex + "><i>" + name + " — unasked now</i></color>";
                }
                else
                {
                    label.text = name + "  <color=" + Ink2Hex + ">" + Mathf.FloorToInt((float)delivered) + " / " + Mathf.FloorToInt((float)target) + "</color>";
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

        private void BuildKithCard()
        {
            var card = Card("THE KITH · roster & posts");
            var slotsLine = MakeText(card, string.Empty, 16, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                var walking = _loop.KithWalking();
                var slots = _loop.KithSlots();
                slotsLine.text = "<i>" + walking + " of " + slots + " posts walked · "
                                 + _loop.KithCount() + " companions</i>";
            });

            // The first minutes are the warden alone — steer the empty roster
            // at the Ladder's first recruit rung (a pile of a hundred berries).
            var lonelyLine = MakeText(card, string.Empty, 18, TextAnchor.MiddleCenter, Ink2, _hand);
            _liveUpdaters.Add(() =>
            {
                var alone = _loop.KithCount() == 0;
                lonelyLine.gameObject.SetActive(alone);
                if (alone)
                {
                    lonelyLine.text = "the work is lonely. a pile of berries might tempt company — see the Ladder, at camp.";
                }
            });

            foreach (var familiar in _loop.State.roster)
            {
                var captured = familiar;
                var row = Row(card);
                var portrait = ArtLibrary.ForSpecies(captured.speciesId);
                if (portrait != null)
                {
                    IconImage(row.transform, portrait, 64f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 22, TextAnchor.MiddleLeft, Ink, _serif);
                FlexibleWidth(label.gameObject, 1f);
                var rename = Button(row.transform, "Name", 150, () => OpenNamingSheet(captured));
                // Posting moved out to the land — each node plate, the trail
                // row, and the watch plates carry their own "post someone"
                // affordance. The roster keeps only the recall.
                var rest = Button(row.transform, "Rest", 150, () => Station(captured, null));

                _liveUpdaters.Add(() =>
                {
                    var species = SpeciesName(captured.speciesId);
                    var bonded = captured.bonded ? " " + SizeOpen(15) + "<color=" + OchreHex + ">BONDED</color></size>" : string.Empty;
                    var kin = _loop.FamiliarKinship(captured) > 0
                        ? "  <color=" + OchreHex + ">KINSHIP " + Roman(_loop.FamiliarKinship(captured)) + "</color>"
                        : string.Empty;
                    var trait = _loop.FamiliarTrait(captured);
                    var traitLine = trait != null
                        ? "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + trait.displayName.ToLowerInvariant()
                          + " — " + trait.description + "</color></size>"
                        : string.Empty;
                    var progress = Mathf.RoundToInt((float)_loop.FamiliarLevelProgress(captured) * 100f);
                    label.text = captured.name + bonded + "  " + SizeOpen(16) + "<color=" + Ink2Hex + ">" + species + "</color></size>" + kin
                                 + "\n" + SizeOpen(16) + "<color=" + Ink2Hex + ">level " + Roman(_loop.FamiliarLevel(captured))
                                 + " · " + progress + "% · " + StationLabel(captured.stationId) + "</color></size>"
                                 + traitLine;
                    rest.gameObject.SetActive(!string.IsNullOrEmpty(captured.stationId));
                });
            }

            BuildKithLadderLines(card);
        }

        /// <summary>
        /// The slot ladder's open questions, under the roster (design §4): the
        /// next verse-earned slot, then the two the store keeps — the starter
        /// bundle (slot + Amber) first, the plain slot behind it.
        /// </summary>
        private void BuildKithLadderLines(RectTransform card)
        {
            var verseLine = MakeText(card, string.Empty, 16, TextAnchor.MiddleLeft, Ink2);
            _liveUpdaters.Add(() =>
            {
                var next = _loop.NextKithVerseMilestone();
                verseLine.gameObject.SetActive(next > 0);
                if (next > 0)
                {
                    verseLine.text = "<i>a slot opens when " + next + " verses are sung — "
                                     + _loop.TotalVersesSung() + " so far</i>";
                }
            });

            var bundleLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);
            var bundleButton = bundleLine.gameObject.AddComponent<Button>();
            bundleButton.transition = Selectable.Transition.None;
            bundleButton.onClick.AddListener(() => OnBuyKithProduct(StoreProductIds.StarterBundle));
            bundleLine.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            AddDashedBorder(bundleLine.gameObject);

            var slotLine = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2);
            var slotButton = slotLine.gameObject.AddComponent<Button>();
            slotButton.transition = Selectable.Transition.None;
            slotButton.onClick.AddListener(() => OnBuyKithProduct(StoreProductIds.KithSlot));
            slotLine.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            AddDashedBorder(slotLine.gameObject);

            _liveUpdaters.Add(() =>
            {
                var bundleOwned = _loop.Store.IsOwned(StoreProductIds.StarterBundle);
                bundleLine.gameObject.SetActive(!bundleOwned);
                if (!bundleOwned)
                {
                    bundleLine.text = "<color=" + OchreInkHex + ">+  open a slot — the starter bundle (a slot, and a pile of amber)</color>";
                }

                // The plain slot waits its turn behind the bundle.
                var slotOwned = _loop.Store.IsOwned(StoreProductIds.KithSlot);
                slotLine.gameObject.SetActive(bundleOwned && !slotOwned);
                if (bundleOwned && !slotOwned)
                {
                    slotLine.text = "<color=" + OchreInkHex + ">+  open the last slot</color>";
                }
            });
        }

        private void OnBuyKithProduct(string productId)
        {
            _loop.PurchaseKithProduct(productId, result =>
            {
                switch (result)
                {
                    case StoreResult.Purchased:
                    case StoreResult.AlreadyOwned:
                        SetNote("a slot opens. thank you for keeping the grove.");
                        _dirty = true;
                        break;
                    case StoreResult.Failed:
                        SetNote("that didn't go through — nothing was charged.");
                        break;
                }
            });
        }
    }
}
