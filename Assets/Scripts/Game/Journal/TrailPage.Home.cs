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
    /// The head of the page: the recruit bar that appears when a companion will
    /// answer a pile.
    /// <para>
    /// The trail-home line stood here until 2026-08-13 — "the trail home" on the
    /// left, a dashed rule with the delivery batch and the fell pony walking it,
    /// a line of status on the right. It went because it was 100 units of pinned
    /// height at the top of the page, and by the end it was pure presentation:
    /// deliveries had become automatic and lossless, the carrier post left with
    /// the hauling system, and nothing on the bar was postable or even
    /// tappable. It was an animation of a thing that could not go wrong, drawn
    /// above the plates that can.
    /// </para>
    /// <para>
    /// The carrier went with it for good. The fell pony is the open question —
    /// she still reads on the roster ("walks her own lane"), so what she has
    /// lost is the one place she was a body moving rather than a line of text.
    /// If that is wanted back it belongs in the world strip, where ambient
    /// motion already lives and costs the page nothing; not here.
    /// </para>
    /// </summary>
    internal sealed partial class TrailPage
    {
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
                        ? "something said yes to the " + target.resourceId + ". it rests at camp, unposted."
                        : "something said yes to the " + target.resourceId + ".");
                    _dirty = true;
                }
                else if (_loop.GiftWaitsOnAmber(target))
                {
                    SetNote("the pile is ready. the asking is "
                            + NumberFormat.Short(_loop.GiftCallingCost()) + " amber.");
                }
                else
                {
                    SetNote("not enough " + target.resourceId + " for a pile. keep picking.");
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
