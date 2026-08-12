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
    /// The head of the page: the trail home, and the recruit bar that appears
    /// when a companion will answer a pile.
    /// </summary>
    internal sealed partial class TrailPage
    {
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
    }
}
