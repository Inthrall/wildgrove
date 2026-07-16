using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// Layout element for a scroll section that should hug its content until a
    /// ceiling: reports a preferred height of "the content's preferred height,
    /// capped at a share of the canvas height". Below the cap the section
    /// behaves like a plain column (no dead space, nothing to scroll); past it
    /// the height pins and the ScrollRect takes over. uGUI has no native
    /// max-height, hence this component.
    /// </summary>
    public sealed class HeightClampedElement : MonoBehaviour, ILayoutElement
    {
        public RectTransform content;
        public RectTransform canvas;
        [Range(0f, 1f)] public float maxCanvasShare = 0.45f;

        public float minWidth => -1f;
        public float preferredWidth => -1f;
        public float flexibleWidth => -1f;
        // min stays unset so that when the whole column is squeezed (short
        // landscape screens) this section absorbs the squeeze instead of
        // pushing fixed-height siblings off screen.
        public float minHeight => -1f;
        public float flexibleHeight => -1f;
        // Above LayoutElement/Image so this is the height that wins.
        public int layoutPriority => 2;

        public float preferredHeight =>
            content == null || canvas == null
                ? -1f
                : ClampedHeight(LayoutUtility.GetPreferredHeight(content), canvas.rect.height, maxCanvasShare);

        /// <summary>Content height, but never more than share × canvas height.</summary>
        public static float ClampedHeight(float contentHeight, float canvasHeight, float maxCanvasShare)
        {
            return Mathf.Min(contentHeight, canvasHeight * maxCanvasShare);
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }
    }
}
