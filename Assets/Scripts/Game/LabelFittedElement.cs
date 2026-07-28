using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// Layout element for a control whose label decides its height: reports the
    /// label's wrapped height plus padding, never below a floor (the 48dp touch
    /// minimum a button owes a fingertip).
    ///
    /// A journal button's label is stretched across it and set to overflow
    /// vertically, so a two-line label that wraps to three — "Raise Timber
    /// Frame / 20 timber (have 582.69K)" at a runtime-sized number — drew
    /// straight out through the plate's own border and over whatever card sat
    /// above and below it. Growing the plate is the fix rather than clipping the
    /// text: the cost line is the point of the label.
    ///
    /// Priority 2 so this beats the LayoutElement's fixed height on the same
    /// object; heights are read in the vertical layout pass, by which point the
    /// label's width is settled, so the wrap is measured at the real width.
    /// </summary>
    public sealed class LabelFittedElement : MonoBehaviour, ILayoutElement
    {
        public Text label;

        /// <summary>The touch-target floor a short label still has to fill.</summary>
        public float floorHeight = 120f;

        /// <summary>Breathing room between the text and the plate's border.</summary>
        public float padding = 16f;

        public float minWidth => -1f;
        public float preferredWidth => -1f;
        public float flexibleWidth => -1f;
        public float minHeight => preferredHeight;
        public float flexibleHeight => -1f;
        public int layoutPriority => 2;

        public float preferredHeight => label == null
            ? -1f
            : Mathf.Max(floorHeight, LayoutUtility.GetPreferredHeight((RectTransform)label.transform) + padding);

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }
    }
}
