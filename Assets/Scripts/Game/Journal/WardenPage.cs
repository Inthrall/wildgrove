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

        // Naming/stationing sheets live in JournalSheets; forwarded so the kith
        // roster bodies below read as they did when they lived on GameHud.
        private void OpenNamingSheet(Familiar familiar) => _hud.Sheets.OpenNamingSheet(familiar);
        private void Station(Familiar familiar, string stationId) => _hud.Sheets.Station(familiar, stationId);

        internal void BuildWardenPage()
        {
            BuildKitCard();
            BuildCraftsCard();
            BuildKithCard();
            BuildRunCard();
        }

        private void BuildKitCard()
        {
            if (_loop.Data.gear == null || _loop.Data.gear.Count == 0)
            {
                return;
            }

            var card = Card("THE KIT");
            MakeText(card, "worn for the run, folded at Migration", 21, TextAnchor.MiddleCenter, Ink2, _hand);
            // Skill unlocks are part of the structure signature, so a locked
            // piece's hint can be settled once per rebuild.
            var unlockedSkills = Upgrades.UnlockedSkills(_loop.State, _loop.Data);
            foreach (var gear in _loop.Data.gear)
            {
                var captured = gear;
                var skillHint = string.Empty;
                if (!string.IsNullOrEmpty(captured.skill) && !unlockedSkills.Contains(captured.skill))
                {
                    // Materials alone can't explain this dead button — name
                    // the missing skill AND the Ladder rung that grants it.
                    var source = SkillSource(captured.skill);
                    skillHint = "  <color=" + OchreInkHex + "><b>needs " + captured.skill
                                + (source != null ? " — take up " + source.displayName : string.Empty)
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
                if (Gear.IsEquipped(_loop.State, captured))
                {
                    label.text = captured.slot.ToUpperInvariant() + " — " + captured.displayName
                                 + "  <color=" + MossDeepHex + ">worn</color>" + givesLine;
                    continue;
                }

                Button craft = null;
                craft = Button(row.transform, "Craft", 160, () =>
                {
                    if (_loop.CraftGear(captured))
                    {
                        Flash(craft, "bound tight", true);
                        SetNote("bound the " + captured.displayName.ToLowerInvariant() + " tight. the work will mind it less.");
                        _dirty = true;
                    }
                });
                _liveUpdaters.Add(() =>
                {
                    label.text = captured.slot.ToUpperInvariant() + " — " + captured.displayName + givesLine
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + BundleHaveLabel(captured.materials) + "</color>" + skillHint + "</size>";
                    var ok = Gear.CanCraft(_loop.State, _loop.Data, captured);
                    craft.interactable = ok;
                    SetButtonTint(craft, ok);
                });
            }
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
                    line.text = captured + " — level " + _loop.SkillLevel(captured)
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
                var rename = Button(row.transform, "Rename", 150, () => OpenNamingSheet(captured));
                // Posting moved out to the land — each node plate, the trail
                // line, and the watch plates carry their own "post someone"
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

        private void BuildRunCard()
        {
            var card = Card("THIS RUN");
            var line = MakeText(card, string.Empty, 19, TextAnchor.MiddleCenter, Ink2, _serif);
            _liveUpdaters.Add(() =>
            {
                var state = _loop.State;
                var pristine = 0;
                foreach (var pair in state.pristineResources)
                {
                    if (pair.Value > BigDouble.Zero)
                    {
                        pristine++;
                    }
                }

                line.text = "camp " + (state.migrationCount + 1)
                            + " · renown " + NumberFormat.Short(state.renown)
                            + " · fold forecast +" + Mathf.FloorToInt((float)System.Math.Max(0.0, _loop.VerdureAfterMigration() - state.verdurePoints)) + " Verdure"
                            + (pristine > 0 ? " · Pristine kinds in hand " + pristine : string.Empty);
            });
        }
    }
}
