using NUnit.Framework;
using UnityEngine;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the scroll section's height rule: hug the content while it fits,
    /// stop growing at the canvas-share ceiling, and stay out of the layout
    /// pass entirely until the component is wired up.
    /// </summary>
    public class HeightClampedElementTests
    {
        [Test]
        public void ClampedHeight_ContentShorterThanCap_TracksContent()
        {
            Assert.That(HeightClampedElement.ClampedHeight(400f, 1920f, 0.45f), Is.EqualTo(400f));
        }

        [Test]
        public void ClampedHeight_ContentTallerThanCap_StopsAtTheCap()
        {
            Assert.That(HeightClampedElement.ClampedHeight(5000f, 1920f, 0.45f), Is.EqualTo(864f).Within(1e-3f));
        }

        [Test]
        public void ClampedHeight_ShortCanvas_CapShrinksWithIt()
        {
            // Landscape: the same content pins to the smaller screen's share.
            Assert.That(HeightClampedElement.ClampedHeight(5000f, 810f, 0.45f), Is.EqualTo(364.5f).Within(1e-3f));
        }

        [Test]
        public void PreferredHeight_BeforeWiring_OptsOutOfLayout()
        {
            var go = new GameObject("clamp", typeof(RectTransform));
            try
            {
                var clamp = go.AddComponent<HeightClampedElement>();

                // -1 is uGUI's "no opinion" — an unwired clamp must not zero
                // the section's height.
                Assert.That(clamp.preferredHeight, Is.EqualTo(-1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
