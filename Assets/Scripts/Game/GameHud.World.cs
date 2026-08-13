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
            if (_worldGap == null)
            {
                return;
            }

            // Null for the camera: the HUD's canvas is an overlay, where the
            // gap's world corners ARE screen pixels.
            _worldGap.GetWorldCorners(Corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);

            // The band, once, for both readers of it.
            var band = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);

            // The rail reads it from here too — it left the HUD's canvas for
            // one of its own, so the gap's rect no longer positions it.
            PlaceEventRail(band);

            if (_world == null)
            {
                return;
            }

            // Windfalls freeze under a sheet — they're ephemeral presentation,
            // and burning their lifetime behind a modal punished opening one.
            _world.Frozen = _sheet != null;
            _world.CatchHintPending = !_hintCatchDone;

            // The WHOLE band, rail or no rail. The strip's plates are spread
            // across it at width * (i + 1) / (count + 1), so insetting the rect
            // off the rail's right edge moved every plate over and shrank them
            // all: the rail was paid for out of the plates rather than out of
            // the margin it stands in. At the portrait cap of three a row the
            // leftmost centre sits at a quarter of the band, which clears the
            // rail's 152 units by a plate's radius over, and the tap guard in
            // HandleWorldTap covers the rest. A landscape band seating five or
            // six a row would bring that centre in toward the rail, and the
            // answer then is a shorter rail, not a narrower strip.
            _world.StripScreenRect = band;
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
                // A fresh gesture: whatever the last one left on the rail's
                // one-shot is spent (see _railTapCaught). Cleared for the
                // non-positional confirms too — Space and pad South are Submit
                // on a marked cell, and a stale flag would eat one of those.
                _railTapCaught = false;

                if (screenPosition.HasValue)
                {
                    // A drifting bubble floats over everything — the catch wins
                    // before any plate or badge underneath it, and (since the
                    // rail draws under the windfalls) before a rail cell too.
                    var overRail = PointerOverEventRail(screenPosition.Value);
                    var caught = _world != null ? _world.PopBubbleAt(screenPosition.Value) : null;
                    if (caught != null)
                    {
                        // The cell's own click is spoken for: this press caught
                        // the thing drawn in front of it, and the release must
                        // not ALSO open the cell's sheet.
                        _railTapCaught = overRail;
                        CollectBubble(caught);
                        return;
                    }

                    // Nothing caught, and the finger is on a cell: the cell's
                    // own Button takes it from here (this path knows nothing
                    // about uGUI), and the near-miss nudge below would only
                    // shake the strip under an unrelated press.
                    if (overRail)
                    {
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
                SetNote("the windfall bursts over the " + node.resourceId + ", empty.");
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
