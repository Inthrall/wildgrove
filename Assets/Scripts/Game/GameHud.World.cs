using BreakInfinity;
using UnityEngine;
using Wildgrove.Game.World;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    // The strip above the page: telling the world view what the journal is
    // doing, and turning a tap into the thing under it — a windfall caught, a
    // near miss nudged, a plate opened, the warden asked where to stand. The
    // sim side of a catch is Bubbles; this is only which node the finger meant.
    public sealed partial class GameHud
    {
        private void ReportWorldStrip()
        {
            if (_world == null)
            {
                return;
            }

            // Windfalls freeze under a sheet — they're ephemeral presentation,
            // and burning their lifetime behind a modal punished opening one.
            _world.Frozen = _sheet != null;
            _world.CatchHintPending = !_hintCatchDone;
            _worldGap.GetWorldCorners(Corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);

            // The events rail stands on the band's left edge, so the strip is
            // told about a narrower band. Everything the strip does — plate
            // sizing, the wrap to two rows, the hit circles, where the windfalls
            // drift — is a function of this one rect (see WorldStrip), so
            // insetting it here is the whole of keeping the plates out from
            // under the rail. Off the rail's own right edge rather than a
            // shared constant: this is screen pixels and that is canvas units.
            if (EventRailStanding())
            {
                _eventRail.GetWorldCorners(Corners);
                min.x = Mathf.Max(min.x, RectTransformUtility.WorldToScreenPoint(null, Corners[2]).x);
            }

            _world.StripScreenRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <param name="typing">
        /// True while a field has the keyboard (<see cref="TextEntryActive"/>).
        /// The strip's key bindings stand down — a C in a familiar's name is a
        /// letter, not a catch — but the finger and the mouse are still the
        /// player's, so the pointer paths below run either way.
        /// </param>
        private void HandleWorldTap(bool typing)
        {
            if (_sheet != null)
            {
                return;
            }

            // The catch that works whatever is focused (pad West, or C). Space
            // and pad South catch too, but only while nothing is focused —
            // there they are Submit, and belong to the marked control. Without
            // this binding the windfall, the game's one active-play reward,
            // would be out of reach for a pad player reading the journal.
            if (!typing && _input.CatchTriggered && TryCatchOldest())
            {
                return;
            }

            if (_input.TendTriggered(out var screenPosition))
            {
                if (screenPosition.HasValue)
                {
                    // The events rail's cells are ordinary uGUI Buttons and take
                    // their own clicks; this path knows nothing about them, so
                    // without the guard a tap on a cell would ALSO catch a
                    // windfall drifting over it — a free reward for opening a
                    // popup, and a windfall spent without the player seeing it.
                    if (PointerOverEventRail(screenPosition.Value))
                    {
                        return;
                    }

                    // A drifting bubble floats over everything — the catch
                    // wins before any plate or badge underneath it.
                    var caught = _world != null ? _world.PopBubbleAt(screenPosition.Value) : null;
                    if (caught != null)
                    {
                        CollectBubble(caught);
                        return;
                    }

                    // A whiffed catch near a windfall must NOT punish the miss
                    // with a full posting sheet — swallow it as a near miss.
                    if (_world != null && _world.NudgeNearMiss(screenPosition.Value))
                    {
                        return;
                    }

                    // Plates and badges resolve together, nearest centre wins —
                    // a node plate IS the assign gesture.
                    var postId = (string)null;
                    var tap = _world != null
                        ? _world.TapAtScreenPoint(screenPosition.Value, out postId)
                        : StripTap.Miss;
                    if (tap == StripTap.Post)
                    {
                        _sheets.OpenPostingSheet(postId);
                    }
                    else if (tap == StripTap.Warden)
                    {
                        // The warden's empty ground: the one body whose "where"
                        // is the open question, so it asks that directly.
                        _sheets.OpenWardenWalkSheet();
                    }
                    else if (tap == StripTap.OpenSlots)
                    {
                        // The (+) closing the strip is not a post — it is the
                        // nudge to fill one, so it asks the two halves of a
                        // posting in order: which ground, then who walks there.
                        _sheets.OpenGroundPickSheet();
                    }
                }
                else if (!typing && !FocusHasTarget)
                {
                    // Space / pad-A with nothing marked: catch the longest-adrift
                    // bubble. With a control marked these are Submit instead —
                    // pad South is both, and firing the button AND the catch off
                    // one press was the double-fire this gate had to settle.
                    TryCatchOldest();
                }
            }
        }

        /// <summary>Catch the longest-adrift windfall, if one is up. True when something was caught.</summary>
        private bool TryCatchOldest()
        {
            var caught = _world != null ? _world.PopOldestBubble() : null;
            if (caught == null)
            {
                return false;
            }

            CollectBubble(caught);
            return true;
        }

        private void CollectBubble(NodeState node)
        {
            if (!_hintCatchDone)
            {
                _hintCatchDone = true;
                PlayerPrefs.SetInt(HintCaughtKey, 1);
            }

            var gained = _loop.PopBubble(node);
            if (gained <= BigDouble.Zero)
            {
                // The ground went out from under the drift — a fold rebuilt
                // the land while this one was in the air, so the node it holds
                // is not this run's any more. (It used to mean the node went
                // fallow mid-drift; a fallow node pays like any other since
                // 2026-08-06.) It pops empty, and the strip itself says so
                // with a grey deflate, not just a sentence elsewhere.
                _world?.ResolveCatch("nothing inside", false);
                SetNote("the windfall bursts over the " + node.resourceId + ", with nothing inside.");
                return;
            }

            // The reward lands where the eye is: a burst and a rising "+N" at
            // the catch point. The journal-row flash and margin note echo it.
            _world?.ResolveCatch("+" + NumberFormat.Short(gained) + " " + node.resourceId, true);
            if (_tendFlashes.TryGetValue(node.id, out var flash))
            {
                flash.text = "+ " + NumberFormat.Short(gained) + " " + node.resourceId;
            }

            _flashAges[node.id] = 0f;
            SetNote("caught a windfall of " + NumberFormat.Short(gained) + " " + node.resourceId);
        }
    }
}
