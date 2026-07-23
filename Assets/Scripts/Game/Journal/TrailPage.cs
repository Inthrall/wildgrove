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
    /// The Trail page — a recruit bar when a companion will answer a pile, the
    /// zones' compact node plates, the watch planter cards, and the Rite's
    /// verse cards and waystone footer. Posting lives on the world strip's
    /// badges and the trail-home line in the band above (one body per post),
    /// so the plates here carry only the land's own business: yields, baskets,
    /// replanting, planters. The kith roster now lives on the Warden page.
    /// </summary>
    internal sealed class TrailPage : JournalSection
    {
        internal TrailPage(GameHud hud) : base(hud) { }

        internal void BuildTrailPage()
        {
            BuildRecruitBar();

            var unlockedZones = ZonesInOrder();
            // Newest zone first — the page header names it, so the page must
            // open on it; older zones keep farming further down the scroll.
            unlockedZones.Reverse();
            var figure = 1;
            foreach (var zone in unlockedZones)
            {
                if (unlockedZones.Count > 1)
                {
                    var heading = MakeText(_body, zone.displayName.ToUpperInvariant(), 12, TextAnchor.MiddleCenter, Ink2, _smallCaps);
                    heading.gameObject.name = "ZoneHeading";
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
                    SetNote("you left a pile of " + target.resourceId + " and stepped back. something said yes.");
                    _dirty = true;
                }
                else if (_loop.KithWalking() >= _loop.KithSlots())
                {
                    SetNote("something watches the pile, but every slot is walked. rest someone first.");
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
                             + " — leave a pile and it stays.</i>";
                var pile = NumberFormat.Short(_loop.GiftPileCost()) + " " + target.resourceId;
                line.text = _loop.CanLeaveGift(target)
                    ? "<color=" + OchreInkHex + ">+  leave a pile — " + pile + "</color>"
                    : "leave a pile — " + pile + " (not enough yet)";
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

        private void BuildNodePlate(NodeState node, int figure)
        {
            var captured = node;
            var card = Card(null);
            var cardImage = card.GetComponent<Image>();

            // A compact gathering row: the specimen's small mark, its name and
            // live yield on one line, Plant back at the right. The full specimen
            // plate lives on the world strip above — no need to repeat it large.
            var row = Row(card);
            var face = ArtLibrary.ForResource(captured.resourceId);
            if (face != null)
            {
                IconImage(row.transform, face, 60f, Color.white);
            }

            var label = MakeText(row.transform, string.Empty, 20, TextAnchor.MiddleLeft, Ink, _serif);
            FlexibleWidth(label.gameObject, 1f);

            Button replant = null;
            replant = Button(row.transform, "Plant back", 190, () =>
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

            // The tend flash — the "+ yield" that rises and fades; sits outside
            // the layout at the plate's top edge.
            var flash = MakeText(card, "+ yield", 22, TextAnchor.MiddleRight, MossDeep, _hand);
            flash.gameObject.name = "TendFlash";
            flash.raycastTarget = false;
            flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var flashRect = (RectTransform)flash.transform;
            flashRect.anchorMin = new Vector2(1f, 1f);
            flashRect.anchorMax = new Vector2(1f, 1f);
            flashRect.pivot = new Vector2(1f, 1f);
            flashRect.sizeDelta = new Vector2(240f, 46f);
            flashRect.anchoredPosition = new Vector2(-210f, -4f);
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
                flashRect.anchoredPosition = new Vector2(-210f, -4f + 20f * t);
            });

            // The basket bar — a thin fill driven by the live updater.
            var barGo = MakePanel("Basket", card, PagePaper);
            FixedHeight(barGo, 10);
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
                    IconImage(actions, trellis, 40f, Color.white);
                }

                foreach (var planter in _loop.NodePlanters())
                {
                    AddPlanterAction(actions, planter, captured.id);
                }
            }

            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var rich = captured.richnessLevel > 0 ? " · richness " + Roman(captured.richnessLevel) : string.Empty;
                cardImage.color = captured == _selected ? MossWash : CardPaper;

                var cap = NodeBasketCapacity(captured);
                var fraction = cap > BigDouble.Zero ? Mathf.Clamp01((float)(captured.basket / cap).ToDouble()) : 0f;
                fillRect.anchorMax = new Vector2(fraction, 1f);
                var basketFull = fraction >= 0.999f;
                fill.color = basketFull ? OchreWash : MossWash;

                var rate = Simulation.YieldPerSecond(captured, state, _loop.Data, _loop.Data.economy);
                var stock = state.GetResource(captured.resourceId);
                label.text = captured.resourceId + rich
                             + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + NumberFormat.Rate(rate) + "/s · "
                             + NumberFormat.Short(stock) + " at camp</color>"
                             + (basketFull ? " <color=" + OchreInkHex + ">· basket full — waits on a carrier</color>" : string.Empty)
                             + "</size>";

                SetButtonLabel(replant, "Plant back\n" + SizeOpen(14) + NumberFormat.Short(_loop.ReplantCost(captured)) + " " + captured.resourceId + "</size>");
                var ok = _loop.CanReplant(captured);
                replant.interactable = ok;
                SetButtonTint(replant, ok);
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
    }
}
