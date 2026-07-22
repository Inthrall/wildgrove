using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Warden page — the worn kit, the unlocked crafts and their progress,
    /// and this run's summary. The warden's own identity, kept apart from the
    /// land (Trail) and the camp.
    /// </summary>
    internal sealed class WardenPage : JournalSection
    {
        internal WardenPage(GameHud hud) : base(hud) { }

        internal void BuildWardenPage()
        {
            BuildKitCard();
            BuildCraftsCard();
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
