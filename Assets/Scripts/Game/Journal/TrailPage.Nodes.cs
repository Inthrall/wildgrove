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
    /// A ground's node plates: what each one yields, the mastery line under it,
    /// the mark saying who works it, and the planter actions it offers.
    /// </summary>
    internal sealed partial class TrailPage
    {
        /// <summary>
        /// The post plate and the picture it wears. Narrower than the worded
        /// buttons it replaces (190 at a node, 220 at a site) because a
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

                var rate = Simulation.YieldPerSecond(captured, state, _loop.Data, _loop.Data.economy);
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
    }
}
