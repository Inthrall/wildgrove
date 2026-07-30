using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the journal row's height rule: grow to whatever the contents ask
    /// for, never give the height back when they ask for less — while the
    /// row's width holds — and stay out of the layout pass entirely until the
    /// component is wired up. A height measured at one width says nothing
    /// about another, so a width change lets the settle go: the first reads of
    /// a freshly built row arrive at its creation size, and a label wrapped at
    /// that width once locked every busy row open at ~700px.
    /// </summary>
    public class HeightSettledElementTests
    {
        [Test]
        public void SettledHeight_ContentTallerThanSettled_Grows()
        {
            Assert.That(HeightSettledElement.SettledHeight(120f, 190f, 76f), Is.EqualTo(190f));
        }

        [Test]
        public void SettledHeight_ContentShorterThanSettled_HoldsTheTallerHeight()
        {
            // The whole point: a label that drops a line must not move the page.
            Assert.That(HeightSettledElement.SettledHeight(190f, 120f, 76f), Is.EqualTo(190f));
        }

        [Test]
        public void SettledHeight_NothingMeasuredYet_ReportsTheFloor()
        {
            Assert.That(HeightSettledElement.SettledHeight(0f, 0f, 76f), Is.EqualTo(76f));
        }

        [Test]
        public void SettledHeight_ContentUnderTheFloor_ReportsTheFloor()
        {
            Assert.That(HeightSettledElement.SettledHeight(0f, 40f, 76f), Is.EqualTo(76f));
        }

        [Test]
        public void SameWidth_FloatNoise_HoldsTheSettle()
        {
            // Half a pixel of slack: layout must not re-measure over FP dust,
            // or the bounce this component stops comes straight back.
            Assert.That(HeightSettledElement.SameWidth(900.4f, 900f), Is.True);
        }

        [Test]
        public void SameWidth_AGenuineMove_LetsTheSettleGo()
        {
            Assert.That(HeightSettledElement.SameWidth(100f, 900f), Is.False);
        }

        [Test]
        public void PreferredHeight_BeforeWiring_OptsOutOfLayout()
        {
            var go = new GameObject("settled", typeof(RectTransform));
            try
            {
                var settled = go.AddComponent<HeightSettledElement>();

                // -1 is uGUI's "no opinion" — an unwired element must leave the
                // row's own LayoutElement and layout group to size it.
                Assert.That(settled.preferredHeight, Is.EqualTo(-1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreferredHeight_OnceWired_KeepsTheTallestItHasSeen()
        {
            var go = new GameObject("row", typeof(RectTransform));
            try
            {
                var group = go.AddComponent<HorizontalLayoutGroup>();
                group.childControlHeight = true;
                var settled = go.AddComponent<HeightSettledElement>();
                settled.source = group;
                settled.floorHeight = 76f;

                var child = new GameObject("tall", typeof(RectTransform));
                child.transform.SetParent(go.transform, false);
                var element = child.AddComponent<LayoutElement>();
                element.preferredHeight = 200f;

                Measure(group);
                Assert.That(settled.preferredHeight, Is.EqualTo(200f), "grows to the content");

                element.preferredHeight = 90f;
                Measure(group);
                Assert.That(settled.preferredHeight, Is.EqualTo(200f), "and holds it when the content shrinks");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreferredHeight_RowWidens_MeasuresAfresh()
        {
            var go = new GameObject("row", typeof(RectTransform));
            try
            {
                // The screenshot bug: a label measured while the row still had
                // its creation width wraps enormously tall, and a settle with
                // no memory of the width kept that height at every width after.
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(100f, 0f);

                var group = go.AddComponent<HorizontalLayoutGroup>();
                group.childControlHeight = true;
                var settled = go.AddComponent<HeightSettledElement>();
                settled.source = group;
                settled.floorHeight = 76f;

                var child = new GameObject("label", typeof(RectTransform));
                child.transform.SetParent(go.transform, false);
                var element = child.AddComponent<LayoutElement>();
                element.preferredHeight = 600f;

                Measure(group);
                Assert.That(settled.preferredHeight, Is.EqualTo(600f), "the narrow measurement settles at the narrow width");

                rect.sizeDelta = new Vector2(900f, 0f);
                element.preferredHeight = 150f;
                Measure(group);
                Assert.That(settled.preferredHeight, Is.EqualTo(150f),
                    "a new width owes nothing to the old one's height");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// One measuring sweep by hand. The horizontal pass is not optional:
        /// it's where a layout group collects its children, so the vertical
        /// pass measures nothing without it.
        /// </summary>
        private static void Measure(LayoutGroup group)
        {
            group.CalculateLayoutInputHorizontal();
            group.CalculateLayoutInputVertical();
        }
    }
}
