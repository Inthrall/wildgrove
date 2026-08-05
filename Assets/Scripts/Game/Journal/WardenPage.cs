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
            BuildNameCard();
            BuildKitCard();
            BuildCraftsCard();
            BuildFavoursCard();
            BuildKithCard();
            BuildRunCard();
        }

        /// <summary>
        /// The page's own head: who this is. The Warden page is the warden's
        /// identity, and until now it opened on their luggage — the kit card —
        /// with the only body on the page unnamed. The name sits above
        /// everything it owns, with the quill beside it that buys or changes it.
        /// <para>
        /// The card says the name and nothing else. It carried the price of a
        /// name under it while the warden was unnamed (2026-08-06), which put a
        /// cost on the page every visit for an offer the quill already makes —
        /// the naming sheet is where the price belongs, and it says it there.
        /// </para>
        /// <para>
        /// The label is built WITH the name in it, not empty for a live updater
        /// to fill. An empty label in a horizontal group is measured at zero
        /// width, so the first vertical pass wrapped a ten-letter name into ten
        /// lines of 60px type and <see cref="HeightSettledElement"/> — grow-only
        /// by design — held the card at that height for good. That is what made
        /// this card a screenful of blank paper.
        /// </para>
        /// </summary>
        private void BuildNameCard()
        {
            var card = Card("THE WARDEN");

            var row = Row(card);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2;

            var name = MakeText(row.transform, _loop.WardenName(), 30, TextAnchor.MiddleCenter, Ink, _serif);
            IconButton(row.transform, JournalSprites.QuillSprite(), 40f, 120f,
                () => _hud.Sheets.OpenWardenNamingSheet());

            _liveUpdaters.Add(() => name.text = _loop.WardenName());
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
                parts.Add("+" + PlainNumber(snapshot.wardenYieldBonus * 100.0) + "% " + _loop.WardenNamePossessive() + " own hands");
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

        // The tinctures card moved to the Stores page (2026-08-04). A bottle
        // is stock — a thing counted, spent and run out of — and it was the
        // one item on this page that behaved that way; the shelf it belongs
        // on is the drawer with everything else the camp holds.

        private void BuildKitCard()
        {
            if (_loop.Data.gear == null || _loop.Data.gear.Count == 0)
            {
                return;
            }

            var slots = KitSlots();
            var card = Card("THE KIT");
            // Five pieces, three slots. Group by slot and say the arithmetic out
            // loud: listed flat in data order, the two PACK pieces sit four rows
            // apart with the slot named only as a prefix, so they read as
            // separate things to collect rather than one slot's two contenders
            // — and a player can swap between them for a while without noticing
            // they are the same decision.
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
                        Flash(action, "on " + _loop.WardenName(), true);
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

        /// <summary>The cell the roster drawer aims for — the Stores drawer's own, so the two read as the same furniture.</summary>
        private const float KithTileIdeal = 190f;

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

            if (_loop.State.roster.Count > 0)
            {
                // The drawer's own instruction, in the brews card's idiom ("tap a
                // bottle to drink it"): a plate carries a name and nothing else,
                // so the one thing a player has to be told is that tapping it
                // opens the companion whole.
                MakeText(card, "<i>tap a plate to read one, and to say where they walk</i>",
                    15, TextAnchor.MiddleCenter, Ink2, _serif);
                BuildKithGrid(card);
            }

            BuildKithLadderLines(card);
        }

        /// <summary>
        /// The roster as a drawer of plates rather than a row per companion. A
        /// row was a portrait, a two-line label, a Post button and an
        /// inscription under it — legible at the three companions the ladder
        /// starts with, and a page per companion at the eight it ends with. So
        /// the card keeps what a glance is for (who walks with you, and where
        /// each of them stands) and the tile's sheet takes everything that was
        /// words: the level and how far into the next, the honours, the trait,
        /// the freshest inscription, the posting and the rename.
        /// <para>
        /// Tiles are built once per structure change — the roster's count is in
        /// <c>StructureSignature</c>, so an arrival rebuilds the page — and then
        /// rewritten in place as names, posts and honours change. Rebuilding a
        /// grid on every refresh would destroy the tile under a finger.
        /// </para>
        /// </summary>
        private void BuildKithGrid(RectTransform card)
        {
            var grid = KithGrid(card);
            var tiles = new List<KithTile>();
            foreach (var familiar in _loop.State.roster)
            {
                var captured = familiar;
                var tile = new KithTile { Familiar = captured };
                PlateTile(grid, ArtLibrary.ForSpecies(captured.speciesId), captured.name,
                    Where(captured), () => _hud.Sheets.OpenStationPickSheet(captured),
                    out tile.Caption, out tile.Corner, out tile.Rule);
                tiles.Add(tile);
            }

            _liveUpdaters.Add(() =>
            {
                foreach (var tile in tiles)
                {
                    tile.Refresh(Where(tile.Familiar));
                }
            });
        }

        /// <summary>
        /// The ground a companion works, as its crop's plate — the corner mark
        /// both posting pickers already wear. Nothing for one resting at camp:
        /// an unmarked tile is the plainest way to say they are standing idle,
        /// and the count above the drawer says how many.
        /// </summary>
        private Sprite Where(Familiar familiar)
        {
            return familiar.IsResting ? null : StationPlate(familiar.stationId);
        }

        /// <summary>The drawer the roster is laid on — the Stores drawer's grid, at its own cell size.</summary>
        private static RectTransform KithGrid(RectTransform card)
        {
            var go = MakeRect("Grid", card).gameObject;
            var grid = go.AddComponent<SquareCellGrid>();
            grid.idealCell = KithTileIdeal;
            grid.spacing = new Vector2(8f, 8f);
            grid.childAlignment = TextAnchor.UpperLeft;
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// One roster tile, bound to one companion, holding the pieces that
        /// outlive what they show. The Stores drawer's shape: the page's live
        /// pass is a loop over tiles rather than a closure per companion per
        /// field.
        /// </summary>
        private sealed class KithTile
        {
            internal Familiar Familiar;
            internal Text Caption;
            internal Image Corner;
            internal Image Rule;

            internal void Refresh(Sprite where)
            {
                // Renaming is a tap away inside the tile's own sheet, and a
                // rename does not change the page's structure.
                Caption.text = Familiar.name;
                SetTileCorner(Corner, where);
                // Moss for a bond, ink for the rest. The row said BONDED in
                // words and a plate has no room for words, so the honour moves
                // onto the one channel a tile has spare — moss, not ochre,
                // because accolades are honours and ochre is the ink of costs
                // and halted work. No grades are drawn on this drawer, so a
                // green rule can't be misread as the Stores drawer's "decent".
                Rule.color = Familiar.bonded ? MossDeep : Ink2;
            }
        }

        /// <summary>
        /// The ladder under the roster (design §4), drawn as the marks the
        /// attunement sheet uses — every place the kith can ever hold, the held
        /// ones inked, the open one moss, the ones still to come faint. Then
        /// what widens it: the next verse-earned place, and the two the store
        /// keeps (the starter bundle first, the plain place behind it).
        /// <para>
        /// Those two were a bare line of text with a dashed rule and an
        /// invisible Button on the label — the only purchase in the game that
        /// didn't look like the amber card's rows, sitting at the foot of the
        /// longest card on the page. "I can't see where to buy a place" was the
        /// whole of the feedback. They are ordinary rows now: what it opens, its
        /// price beside it, and a Buy plate that reads as one.
        /// </para>
        /// </summary>
        private void BuildKithLadderLines(RectTransform card)
        {
            var places = BuildKithPlaces(card, 26f);
            var caption = MakeText(card, string.Empty, 15, TextAnchor.MiddleCenter, Ink2, _smallCaps);
            var verseLine = MakeText(card, string.Empty, 16, TextAnchor.MiddleCenter, Ink2);
            _liveUpdaters.Add(() =>
            {
                PaintKithPlaces(places);
                var open = _loop.KithSlots();
                var max = Kith.SlotsMax(_loop.Data);
                caption.text = open + " of " + max + " places at the fire";

                // One line under the marks, and only ever one: the verse still
                // to be sung while there is one, then nothing more to say once
                // the whole circle is open. The store's own rows speak for
                // themselves below.
                var next = _loop.NextKithVerseMilestone();
                var full = open >= max;
                verseLine.gameObject.SetActive(next > 0 || full);
                if (next > 0)
                {
                    verseLine.text = "<i>a place opens when " + next + " verses are sung: "
                                     + _loop.TotalVersesSung() + " so far</i>";
                }
                else if (full)
                {
                    verseLine.text = "<i>every place at the fire is open</i>";
                }
            });

            var bundleAmber = Mathf.FloorToInt((float)(_loop.Data.economy?.store?.starterBundleAmber ?? 0.0));
            BuildKithPlaceRow(card, StoreProductIds.StarterBundle,
                "the starter bundle: a place at the fire"
                + (bundleAmber > 0 ? ", and " + bundleAmber + " amber" : string.Empty),
                () => !_loop.Store.IsOwned(StoreProductIds.StarterBundle));

            // The plain place waits its turn behind the bundle — one offer on
            // the page at a time.
            BuildKithPlaceRow(card, StoreProductIds.KithSlot, "the ladder's last place",
                () => _loop.Store.IsOwned(StoreProductIds.StarterBundle)
                      && !_loop.Store.IsOwned(StoreProductIds.KithSlot));
        }

        /// <summary>
        /// One of the ladder's two bought places, in the amber card's row idiom:
        /// what it opens on the left with its real-money price beside it, and a
        /// Buy plate on the right. <paramref name="offered"/> decides whether
        /// the row stands on the page at all.
        /// </summary>
        private void BuildKithPlaceRow(RectTransform card, string productId, string gives, System.Func<bool> offered)
        {
            // Real money says its price on the line — the Play dialog must never
            // be where the player first learns it. Moss on what it gives: this
            // is an invitation, not a warning. The tail arrives with the (lazy)
            // catalogue fetch, so the line is kept current.
            System.Func<string> written = () => "open another place at the fire" + PriceTail(productId)
                                                + "\n" + SizeOpen(15) + "<color=" + MossDeepHex + ">"
                                                + gives + "</color></size>";

            var row = Row(card);
            // Built WITH its words, never empty for the updater to fill: an empty
            // label measures zero wide in a horizontal group, and the row's
            // settled height is grow-only (see BuildNameCard).
            var label = MakeText(row.transform, written(), 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            Button(row.transform, "Buy", 170, () => OnBuyKithProduct(productId));

            _liveUpdaters.Add(() =>
            {
                var show = offered();
                row.SetActive(show);
                if (show)
                {
                    label.text = written();
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
                        SetNote("a place opens at the fire. thank you for keeping the grove.");
                        _dirty = true;
                        break;
                    case StoreResult.Failed:
                        SetNote("that didn't go through. nothing was charged.");
                        break;
                    case StoreResult.Unavailable:
                        SetNote("the store couldn't be reached. nothing was charged — try again shortly.");
                        break;
                    case StoreResult.Deferred:
                        SetNote("the payment hasn't cleared yet. the place opens when Play finishes it.");
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
