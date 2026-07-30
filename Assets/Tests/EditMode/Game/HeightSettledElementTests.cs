using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the journal row's height rule: grow to whatever the contents ask
    /// for, never give the height back when they ask for less, and stay out of
    /// the layout pass entirely until the component is wired up.
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
