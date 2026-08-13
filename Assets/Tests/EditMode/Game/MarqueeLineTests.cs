using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the margin note's walk. The cases that matter are the two ends —
    /// a sentence that fits must not move at all, and one that doesn't must
    /// show its BEGINNING first and come back to it rather than looping through
    /// the join — because a note is prose, and a ticker that wrapped around
    /// would put the tail of a sentence in front of its head.
    /// </summary>
    public class MarqueeLineTests
    {
        private const float Over = 300f;
        private const float Travel = 3f;
        private const float Hold = 1.5f;

        [Test]
        public void Shift_ASentenceThatFits_NeverMoves()
        {
            Assert.That(MarqueeLine.Shift(0f, 0f, Travel, Hold), Is.EqualTo(0f));
            Assert.That(MarqueeLine.Shift(-40f, 12f, Travel, Hold), Is.EqualTo(0f),
                "a lane wider than the words has nothing to walk");
        }

        [Test]
        public void Shift_TheFirstBeat_HoldsAtTheStart()
        {
            // The part that has to be readable the instant the note appears —
            // and most notes are read and gone inside this hold.
            Assert.That(MarqueeLine.Shift(Over, 0f, Travel, Hold), Is.EqualTo(0f));
            Assert.That(MarqueeLine.Shift(Over, Hold * 0.99f, Travel, Hold), Is.EqualTo(0f));
        }

        [Test]
        public void Shift_MidWalk_IsPartWayAcross()
        {
            var half = MarqueeLine.Shift(Over, Hold + Travel * 0.5f, Travel, Hold);

            Assert.That(half, Is.EqualTo(Over * 0.5f).Within(0.01f));
        }

        [Test]
        public void Shift_TheEndOfTheWalk_HoldsAtTheTail()
        {
            Assert.That(MarqueeLine.Shift(Over, Hold + Travel, Travel, Hold), Is.EqualTo(Over).Within(0.01f),
                "the walk stops where the last word is, not past it");
            Assert.That(MarqueeLine.Shift(Over, Hold + Travel + Hold * 0.5f, Travel, Hold),
                Is.EqualTo(Over).Within(0.01f));
        }

        [Test]
        public void Shift_AfterTheTail_WalksBackRatherThanWrapping()
        {
            var leg = Hold + Travel;
            var returning = MarqueeLine.Shift(Over, leg + Hold + Travel * 0.5f, Travel, Hold);

            Assert.That(returning, Is.EqualTo(Over * 0.5f).Within(0.01f),
                "halfway back is the same place as halfway out — the walk is a there-and-back");
        }

        [Test]
        public void Shift_AWholeCycleLater_IsBackAtTheStart()
        {
            var cycle = (Hold + Travel) * 2f;

            Assert.That(MarqueeLine.Shift(Over, cycle, Travel, Hold), Is.EqualTo(0f).Within(0.01f));
            Assert.That(MarqueeLine.Shift(Over, cycle * 4f, Travel, Hold), Is.EqualTo(0f).Within(0.01f),
                "and stays a there-and-back however long the note is left up");
        }

        [Test]
        public void Shift_NoTravelTime_DoesNotDivideByZero()
        {
            // travelSpeed is a public field, so a zero is reachable from the
            // inspector — and a lane exactly as wide as its words gets here too.
            Assert.That(MarqueeLine.Shift(Over, 0f, 0f, Hold), Is.EqualTo(0f));
            Assert.That(MarqueeLine.Shift(Over, Hold + 0.1f, 0f, Hold), Is.EqualTo(Over));
            Assert.That(MarqueeLine.Shift(Over, 5f, 0f, 0f), Is.EqualTo(Over));
        }

        [Test]
        public void PreferredHeight_BeforeWiring_OptsOutOfLayout()
        {
            var go = new GameObject("lane", typeof(RectTransform));
            try
            {
                var marquee = go.AddComponent<MarqueeLine>();

                Assert.That(marquee.preferredHeight, Is.EqualTo(-1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreferredHeight_ALongSentenceInANarrowLane_IsStillOneLine()
        {
            // The whole reason the component exists: this note is several lanes
            // wide, and the lane it stands in must be exactly as tall as the
            // short one below it. Two lines here is the page-bump being fixed.
            var lane = BuildLane(240f, out var label, out var marquee);
            try
            {
                label.text = "set down";
                var oneLine = marquee.preferredHeight;

                label.text = "the whole offering or none. the stores are short.";

                Assert.That(marquee.preferredHeight, Is.EqualTo(oneLine),
                    "the label overflows its lane rather than wrapping inside it");
            }
            finally
            {
                Object.DestroyImmediate(lane);
            }
        }

        [Test]
        public void PreferredHeight_AnEmptyNote_StillReportsOneLine()
        {
            // The frame a note is cleared on: the label measures nothing, and a
            // lane that reported zero would collapse and shove the page up.
            var lane = BuildLane(240f, out var label, out var marquee);
            try
            {
                label.text = string.Empty;

                Assert.That(marquee.preferredHeight, Is.GreaterThanOrEqualTo(label.fontSize));
            }
            finally
            {
                Object.DestroyImmediate(lane);
            }
        }

        /// <summary>
        /// A lane the width of a phone's margin note, with the label set up the
        /// way <c>GameHud.BuildChrome</c> sets it up — Overflow, because the wrap
        /// is the thing being prevented.
        /// </summary>
        private static GameObject BuildLane(float width, out Text label, out MarqueeLine marquee)
        {
            var go = new GameObject("lane", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 0f);

            var labelGo = new GameObject("note", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            marquee = go.AddComponent<MarqueeLine>();
            marquee.label = label;
            return go;
        }
    }
}
