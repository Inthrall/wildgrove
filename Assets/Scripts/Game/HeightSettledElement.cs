using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// Layout element for a row whose contents keep re-measuring: reports the
    /// tallest height the row has ever needed, and never a shorter one.
    /// <para>
    /// A journal row is sized by its label, and the busy pages rewrite their
    /// labels four times a second — a percentage gains a digit, a status clause
    /// appears, a have-count crosses 1.0K. Any of those can re-wrap a line, and
    /// a line gained or lost moves every card below it. Which line moved is
    /// invisible: the lower half of the page simply lurches while you are
    /// trying to tap something on it.
    /// </para>
    /// <para>
    /// Growing-only is the fix. A row settles within a few seconds of play at
    /// the height of its wordiest state and then holds still for good. The cost
    /// is a little whitespace on a row that had one tall moment, which is the
    /// better half of the trade — and the page rebuilds whenever its structure
    /// changes (see <see cref="GameHud"/>), so each rebuild measures afresh.
    /// </para>
    /// <para>
    /// Reads its height from a sibling layout group rather than measuring its
    /// own rect, because it sits ON that rect: asking uGUI for the rect's
    /// preferred height would ask this component, not the group. Priority 2 so
    /// it beats the group (0) and a plain LayoutElement (1) on the same object.
    /// </para>
    /// </summary>
    public sealed class HeightSettledElement : MonoBehaviour, ILayoutElement
    {
        /// <summary>The group that actually measures the row's contents.</summary>
        public LayoutGroup source;

        /// <summary>The height a sparse row still fills — the row's touch floor.</summary>
        public float floorHeight = 76f;

        private float _settled;

        public float minWidth => -1f;
        public float preferredWidth => -1f;
        public float flexibleWidth => -1f;
        public float minHeight => preferredHeight;
        public float flexibleHeight => -1f;
        public int layoutPriority => 2;

        /// <summary>
        /// The high-water mark is taken here rather than in
        /// <see cref="CalculateLayoutInputVertical"/> because both run in the
        /// same sweep and component order decides which goes first, whereas
        /// every read of this property is guaranteed to come after the sweep
        /// the group measured itself in.
        /// </summary>
        public float preferredHeight
        {
            get
            {
                if (source == null)
                {
                    return -1f;
                }

                _settled = SettledHeight(_settled, source.preferredHeight, floorHeight);
                return _settled;
            }
        }

        /// <summary>The tallest of: where the row has settled, what it needs now, and its floor.</summary>
        public static float SettledHeight(float settled, float measured, float floorHeight)
        {
            return Mathf.Max(settled, Mathf.Max(measured, floorHeight));
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }
    }
}
