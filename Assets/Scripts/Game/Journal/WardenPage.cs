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
    /// The Warden page — the worn kit, the unlocked crafts and their progress,
    /// the kith roster &amp; slots, and this run's summary. The warden's own
    /// identity, kept apart from the land (Trail) and the camp.
    /// </summary>
    internal sealed class WardenPage : JournalSection
    {
        internal WardenPage(GameHud hud) : base(hud) { }

        internal void BuildWardenPage()
        {
            BuildKitCard();
            BuildCraftsCard();
            BuildBrewsCard();
            BuildFavoursCard();
            BuildKithCard();
            BuildRunCard();
        }

        /// <summary>
        /// Every standing bonus, counted together (design §8's modifier
        /// union). The sources each label their own effect — a gear line, a
        /// plate's grant, an Almanac node — but nothing ever showed the sum,
        /// so "why is this node at 4.2/s" had no page to answer it. The card
        /// hides itself while the warden is still bare-handed.
        /// </summary>
        private void BuildFavoursCard()
        {
            var card = Card("THE FAVOURS");
            MakeText(card, "<i>gear, plates, spreads, the Almanac, the season and any live tincture, folded into one reckoning</i>",
                15, TextAnchor.MiddleCenter, Ink2, _serif);
            var lines = MakeText(card, string.Empty, 18, TextAnchor.MiddleLeft, Ink);
            _liveUpdaters.Add(() =>
            {
                var text = FavourLines();
                card.gameObject.SetActive(text.Length > 0);
                lines.text = text;
            });
        }

        /// <summary>One line per favour standing off its baseline — multipliers as ×, additive bonuses as +%.</summary>
        private string FavourLines()
        {
            var state = _loop.State;
            var data = _loop.Data;
            var parts = new List<string>();

            var verdure = data.economy?.verdure;
            if (verdure != null && verdure.yieldBonusPerPoint > 0.0 && state.verdurePoints > 0.0)
            {
                parts.Add("+" + PlainNumber(verdure.yieldBonusPerPoint * state.verdurePoints * 100.0)
                          + "% all gathering  ·  the Verdure, " + Mathf.FloorToInt((float)state.verdurePoints) + " points");
            }

            var snapshot = Modifiers.Of(state, data);
            if (snapshot.wardenYieldBonus > 0.0)
            {
                parts.Add("+" + PlainNumber(snapshot.wardenYieldBonus * 100.0) + "% the warden's own hands");
            }

            if (snapshot.craftSpeedGlobal != 1.0)
            {
                parts.Add("×" + PlainNumber(snapshot.craftSpeedGlobal) + " craft speed at every station");
            }

            foreach (var pair in snapshot.craftSpeedBySkill)
            {
                if (pair.Value != 1.0)
                {
                    parts.Add("×" + PlainNumber(pair.Value) + " " + pair.Key + " speed");
                }
            }

            if (snapshot.digSpeedMultiplier != 1.0)
            {
                parts.Add("×" + PlainNumber(snapshot.digSpeedMultiplier) + " watching at the sites");
            }

            if (snapshot.choiceChanceBonus > 0.0)
            {
                parts.Add("+" + PlainNumber(snapshot.choiceChanceBonus * 100.0) + "% to a choice find");
            }

            if (snapshot.tendingBurstBonus > 0.0)
            {
                parts.Add("+" + PlainNumber(snapshot.tendingBurstBonus * 100.0) + "% to the tending burst");
            }

            var comfort = Buildings.ComfortXpMultiplier(state, data);
            if (comfort > 1.0)
            {
                parts.Add("+" + PlainNumber((comfort - 1.0) * 100.0) + "% familiar XP while posted  ·  the roosts");
            }

            if (snapshot.offlineCapRaiseTo > 0.0)
            {
                parts.Add("the camp works " + PlainNumber(snapshot.offlineCapRaiseTo) + "h of a night away, at the least");
            }

            if (snapshot.offlineCapBonusHours > 0.0)
            {
                parts.Add("+" + PlainNumber(snapshot.offlineCapBonusHours) + "h on the night's work");
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// The tinctures card (design §5, Apothecary) — hidden until the craft
        /// is learned (the Mistfen map teaches it), like the crafting section
        /// before its first recipe. Brewing happens at the fire with the other
        /// recipes; this card is the drinking.
        /// </summary>
        private void BuildBrewsCard()
        {
            if (_loop.Data.tinctures == null || _loop.Data.tinctures.Count == 0
                || !Upgrades.UnlockedSkills(_loop.State, _loop.Data).Contains("apothecary"))
            {
                return;
            }

            var card = Card("TINCTURES · brewed at the fire");
            MakeText(card, "one at a time each: a second bottle adds time, not depth",
                15, TextAnchor.MiddleCenter, Ink2);

            foreach (var tincture in _loop.Data.tinctures)
            {
                var captured = tincture;
                var row = Row(card);
                // A tincture id is also its recipe output, so the brew's own
                // vessel is the good's plate.
                var vessel = ArtLibrary.ForGood(captured.id);
                if (vessel != null)
                {
                    IconImage(row.transform, vessel, 56f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink, _serif);
                FlexibleWidth(label.gameObject, 1f);
                var drink = Button(row.transform, "Drink", 120, () => _loop.DrinkTincture(captured));

                _liveUpdaters.Add(() =>
                {
                    var remaining = _loop.TinctureRemainingSeconds(captured);
                    var status = remaining > 0.0
                        ? "  " + SizeOpen(14) + "<color=" + MossDeepHex + ">LIVE · " + NumberFormat.Duration(remaining) + " left</color></size>"
                        : string.Empty;
                    label.text = captured.displayName + status
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + captured.description
                                 + " · have " + NumberFormat.Short(_loop.State.GetResource(captured.id)) + "</color></size>";

                    var ok = _loop.CanDrinkTincture(captured);
                    drink.interactable = ok;
                    SetButtonTint(drink, ok);
                });
            }
        }

        private void BuildKitCard()
        {
            if (_loop.Data.gear == null || _loop.Data.gear.Count == 0)
            {
                return;
            }

            var slots = KitSlots();
            var card = Card("THE KIT");
            // Five pieces, three slots. This card used to list them flat in
            // data order, so the two PACK pieces sat four rows apart with the
            // slot named only as a prefix — they read as separate things to
            // collect rather than one slot's two contenders, and a player could
            // swap between them for a while without noticing they were the same
            // decision. Group by slot, and say the arithmetic out loud.
            MakeText(card, slots.Count + " slots: " + string.Join(" · ", slots), 16, TextAnchor.MiddleCenter, Ink2);
            MakeText(card, "Worn for the run, folded at Migration", 20, TextAnchor.MiddleCenter, Ink2, _hand);

            // Skill unlocks are part of the structure signature, so a locked
            // piece's hint can be settled once per rebuild.
            var unlockedSkills = Upgrades.UnlockedSkills(_loop.State, _loop.Data);
            foreach (var slot in slots)
            {
                BuildKitSlot(card, slot, unlockedSkills);
            }
        }

        /// <summary>
        /// The kit's slots in the order the data introduces them (hands, pack,
        /// camp — design §4). Read off the gear list rather than hardcoded, so a
        /// new slot in gear.json groups itself instead of quietly appearing
        /// under the last one.
        /// </summary>
        private List<string> KitSlots()
        {
            var slots = new List<string>();
            foreach (var gear in _loop.Data.gear)
            {
                if (!string.IsNullOrEmpty(gear.slot) && !slots.Contains(gear.slot))
                {
                    slots.Add(gear.slot);
                }
            }

            return slots;
        }

        /// <summary>One slot's group: the slot head naming what's worn there, then every piece that competes for it.</summary>
        private void BuildKitSlot(RectTransform card, string slot, HashSet<string> unlockedSkills)
        {
            MakeHairline(card);
            var head = MakeText(card, string.Empty, 15, TextAnchor.MiddleLeft, Ink2, _smallCaps);
            _liveUpdaters.Add(() =>
            {
                var wornId = Gear.EquippedInSlot(_loop.State, slot);
                var worn = wornId != null && _loop.Data.GearById.TryGetValue(wornId, out var wornGear)
                    ? wornGear.displayName
                    : null;
                head.text = slot.ToUpperInvariant() + " · " + (worn != null
                    ? "worn: " + worn
                    : "<color=" + OchreInkHex + ">empty</color>");
            });

            foreach (var gear in _loop.Data.gear)
            {
                if (gear.slot == slot)
                {
                    BuildKitRow(card, gear, unlockedSkills);
                }
            }
        }

        private void BuildKitRow(RectTransform card, GearData gear, HashSet<string> unlockedSkills)
        {
            var captured = gear;
            var skillHint = string.Empty;
            if (!string.IsNullOrEmpty(captured.skill) && !unlockedSkills.Contains(captured.skill))
            {
                // Materials alone can't explain this dead button — name
                // the missing skill AND the Ladder rung that grants it.
                var source = SkillSource(captured.skill);
                skillHint = "  <color=" + OchreInkHex + "><b>needs " + captured.skill
                            + (source != null ? ", take up " + source.displayName : string.Empty)
                            + "</b></color>";
            }

            var row = Row(card);
            var kit = ArtLibrary.ForGear(captured.id);
            if (kit != null)
            {
                IconImage(row.transform, kit, 64f, Color.white);
            }

            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);

            var gives = EffectsLabel(captured.effects);
            var givesLine = gives.Length > 0
                ? "\n" + SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>"
                : string.Empty;

            // The slot name has moved to the group head, so the row is free to
            // be the piece itself. One button serves both states — Craft while
            // the piece has never been made, Wear once it's in the bag — and
            // since a swap destroys nothing, the rival needs no warning.
            Button action = null;
            action = Button(row.transform, "Craft", 160, () =>
            {
                if (Gear.IsCrafted(_loop.State, captured))
                {
                    if (_loop.WearGear(captured))
                    {
                        Flash(action, "on the warden", true);
                        SetNote("took the " + captured.displayName.ToLowerInvariant()
                                + " out of the bag. what it replaced keeps, and nothing is lost.");
                        _dirty = true;
                    }

                    return;
                }

                if (_loop.CraftGear(captured))
                {
                    Flash(action, "bound tight", true);
                    SetNote("bound the " + captured.displayName.ToLowerInvariant() + " tight. the work will mind it less.");
                    _dirty = true;
                }
            });

            _liveUpdaters.Add(() =>
            {
                var worn = Gear.IsEquipped(_loop.State, captured);
                var crafted = Gear.IsCrafted(_loop.State, captured);
                var status = worn
                    ? "  <color=" + MossDeepHex + ">worn</color>"
                    : crafted
                        ? "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">in the bag</color></size>"
                        : string.Empty;

                // A made piece shows no price — it's paid for, for the rest of
                // the run. Only an unmade one carries its materials, and its
                // skill lock if it has one.
                var costLine = crafted
                    ? string.Empty
                    : "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + BundleHaveLabel(captured.materials)
                      + "</color>" + skillHint + "</size>";

                label.text = captured.displayName + status + givesLine + costLine;

                action.gameObject.SetActive(!worn);
                if (worn)
                {
                    return;
                }

                SetButtonLabel(action, crafted ? "Wear" : "Craft");
                var ok = crafted
                    ? Gear.CanWear(_loop.State, captured)
                    : Gear.CanCraft(_loop.State, _loop.Data, captured);
                action.interactable = ok;
                SetButtonTint(action, ok);
            });
        }

        private void BuildCraftsCard()
        {
            var card = Card("CRAFTS");
            foreach (var skill in _loop.UnlockedSkills())
            {
                var captured = skill;

                // The craft's mark leads its line. The glyphs are monochrome
                // silhouettes (some white-on-transparent), so ink-tint them for
                // a coherent sepia mark on the paper; no glyph → bare line.
                var glyph = ArtLibrary.ForSkill(captured);
                Text line;
                if (glyph != null)
                {
                    var craftRow = MakeRect("CraftRow", card).gameObject;
                    var layout = craftRow.AddComponent<HorizontalLayoutGroup>();
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    layout.spacing = 10;
                    // Glyphs are baked two-tone sepia — pass white so the ink
                    // tones show true rather than being multiplied down again.
                    IconImage(craftRow.transform, glyph, 40f, Color.white);
                    line = MakeText(craftRow.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                    FlexibleWidth(line.gameObject, 1f);
                }
                else
                {
                    line = MakeText(card, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                }

                _liveUpdaters.Add(() =>
                {
                    var progress = Mathf.RoundToInt((float)_loop.SkillProgress(captured) * 100f);
                    line.text = captured + ", level " + _loop.SkillLevel(captured)
                                + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + progress + "% to next</color></size>";
                });
            }
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
                    lonelyLine.text = "the work is lonely. a pile of berries might tempt company. see the Ladder, at camp.";
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

                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink, _serif);
                FlexibleWidth(label.gameObject, 1f);
                // The card is headed "roster & posts" — so the row must offer
                // the post, not just report it. "Post" opens the where-sheet
                // (rest, every node, the trail, the wander post), and renaming
                // now lives in there too, behind a quill beside the name: it
                // belongs where the name is being read, not on a second button
                // squeezing the name on every line. 120 units is the touch
                // floor, and every unit off it goes to the name beside it.
                Button(row.transform, "Post", 120, () => _hud.Sheets.OpenStationPickSheet(captured));

                // Two lines, and the row's height is then the buttons' 120-unit
                // touch floor rather than the text. The four lines this used to
                // run — name/species/kinship, then level and post, then the
                // trait and its full description — wrapped into five in a
                // column ~344 units wide and made a page of one companion.
                // Species reads off the plate beside it (and by name in both
                // posting sheets); the trait moved to the Post sheet, where it
                // is what the decision is actually about.
                _liveUpdaters.Add(() =>
                {
                    // Moss, not ochre — accolades are honours, and ochre is
                    // the ink of costs and halted work.
                    var bonded = captured.bonded ? "  " + SizeOpen(14) + "<color=" + MossDeepHex + ">BONDED</color></size>" : string.Empty;
                    var kin = _loop.FamiliarKinship(captured) > 0
                        ? "  " + SizeOpen(14) + "<color=" + MossDeepHex + ">KINSHIP " + Roman(_loop.FamiliarKinship(captured)) + "</color></size>"
                        : string.Empty;
                    // "% to next" matches the crafts card above — the levels
                    // climb on the same idiom, so they read on it too.
                    var toNext = Mathf.RoundToInt((float)_loop.FamiliarLevelProgress(captured) * 100f);
                    label.text = captured.name + bonded + kin
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">level " + Roman(_loop.FamiliarLevel(captured))
                                 + " · " + toNext + "% to next · " + StationLabel(captured.stationId) + "</color></size>";
                });

                // The plate's newest margin line (design §7) — earned at
                // Kinship signature milestones, in the warden's hand. Older
                // lines stay on the plate's Record entry; the roster shows
                // the freshest so the card doesn't grow a paragraph per
                // companion.
                var inscription = MakeText(card, string.Empty, 17, TextAnchor.MiddleLeft, Ink2, _hand);
                _liveUpdaters.Add(() =>
                {
                    var earned = _loop.FamiliarInscriptions(captured);
                    inscription.gameObject.SetActive(earned.Count > 0);
                    if (earned.Count > 0)
                    {
                        inscription.text = "\"" + earned[earned.Count - 1] + "\"";
                    }
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
                    verseLine.text = "<i>a slot opens when " + next + " verses are sung: "
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
                    // Real money says its price on the line — the Play dialog
                    // must never be where the player first learns it. Moss:
                    // it's an invitation, not a warning.
                    bundleLine.text = "<color=" + MossDeepHex + ">+  open a slot: the starter bundle (a slot, and a pile of amber)</color>"
                                      + PriceTail(StoreProductIds.StarterBundle);
                }

                // The plain slot waits its turn behind the bundle.
                var slotOwned = _loop.Store.IsOwned(StoreProductIds.KithSlot);
                slotLine.gameObject.SetActive(bundleOwned && !slotOwned);
                if (bundleOwned && !slotOwned)
                {
                    slotLine.text = "<color=" + MossDeepHex + ">+  open the last slot</color>" + PriceTail(StoreProductIds.KithSlot);
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
                        SetNote("that didn't go through. nothing was charged.");
                        break;
                    case StoreResult.Unavailable:
                        SetNote("the store couldn't be reached. nothing was charged — try again shortly.");
                        break;
                    case StoreResult.Deferred:
                        SetNote("the payment hasn't cleared yet. the slot opens when Play finishes it.");
                        break;
                }
            });
        }

        private void BuildRunCard()
        {
            var card = Card("THIS RUN");
            var line = MakeText(card, string.Empty, 19, TextAnchor.MiddleCenter, Ink2, _serif);
            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var choice = 0;
                foreach (var pair in state.choiceResources)
                {
                    if (pair.Value > BigDouble.Zero)
                    {
                        choice++;
                    }
                }

                // Name what drives the forecast: Verdure is a curve over
                // lifetime Renown, and two folds banking 2 then 34 read as
                // arbitrary unless the next threshold is on the page.
                var next = new BigDouble(_loop.RenownForNextVerdure());
                var toNext = next > state.renown
                    ? "\n" + SizeOpen(15) + "the next Verdure asks " + NumberFormat.Short(next)
                      + " lifetime renown, " + NumberFormat.Short(next - state.renown) + " more</size>"
                    : string.Empty;
                line.text = "camp " + (state.migrationCount + 1)
                            + " · renown " + NumberFormat.Short(state.renown)
                            + " · fold forecast +" + Mathf.FloorToInt((float)System.Math.Max(0.0, _loop.VerdureAfterMigration() - state.verdurePoints)) + " Verdure"
                            + (choice > 0 ? " · Choice kinds in hand " + choice : string.Empty)
                            + toNext;
            });
        }
    }
}
