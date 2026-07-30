using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the button plate's height rule: fit the label's wrap plus padding,
    /// never below the touch floor, hold the tallest wrap seen while the
    /// label's width holds — and let the settle go when the width moves, so a
    /// wrap measured at the label's creation size can't hold the plate open
    /// at every width after.
    /// </summary>
    public class LabelFittedElementTests
    {
        private const string LongLabel =
            "Raise Timber Frame with 20 timber, 12 copper ingots and 6 charcoal, have 582.69K of each";

        private GameObject _plate;
        private Text _label;
        private LabelFittedElement _fitted;

        [SetUp]
        public void SetUp()
        {
            _plate = new GameObject("plate", typeof(RectTransform));
            var labelGo = new GameObject("label", typeof(Text));
            labelGo.transform.SetParent(_plate.transform, false);
            _label = labelGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 30;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.text = LongLabel;

            _fitted = _plate.AddComponent<LabelFittedElement>();
            _fitted.label = _label;
            _fitted.floorHeight = 120f;
            _fitted.padding = 16f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_plate);
        }

        [Test]
        public void PreferredHeight_ShortLabelAtTheSameWidth_HoldsTheTallerWrap()
        {
            SetLabelWidth(300f);
            var tall = _fitted.preferredHeight;
            Assert.That(tall, Is.GreaterThan(120f), "the long label wraps past the floor");

            _label.text = "Craft";
            Assert.That(_fitted.preferredHeight, Is.EqualTo(tall),
                "a relabel at the same width must not rock the plate");
        }

        [Test]
        public void PreferredHeight_LabelWidens_MeasuresAfresh()
        {
            SetLabelWidth(100f);
            Assert.That(_fitted.preferredHeight, Is.GreaterThan(120f),
                "wrapped at a fingertip, the label measures far past the floor");

            SetLabelWidth(2000f);
            Assert.That(_fitted.preferredHeight, Is.EqualTo(120f),
                "on one line the label is under the floor, and the narrow wrap is let go");
        }

        private void SetLabelWidth(float width)
        {
            ((RectTransform)_label.transform).sizeDelta = new Vector2(width, 0f);
        }
    }
}
