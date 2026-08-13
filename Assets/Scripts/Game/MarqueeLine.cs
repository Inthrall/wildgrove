using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// A label held to ONE line, whatever it says: the lane reports a single
    /// line's height and never a second one, and a label too long for it is
    /// clipped by the lane's mask and walked across instead of wrapped.
    /// <para>
    /// This is <see cref="HeightSettledElement"/>'s problem taken one step
    /// further. A settling row holds still once it has seen its wordiest state,
    /// which is the right trade for a row inside the page — but the margin note
    /// is pinned ABOVE the page, so every line it gained pushed the whole
    /// journal down and every line it lost pulled it back up, under a thumb that
    /// was reading it. A note is one sentence of aside; it is not worth a line of
    /// the page, and it is certainly not worth two intermittently.
    /// </para>
    /// <para>
    /// The walk holds at the START first, and returns there rather than
    /// wrapping around: a note is prose, so the beginning is the part that has
    /// to be readable the instant it appears, and a ticker that loops through
    /// the join would put the tail of a sentence in front of its head. Most
    /// notes are read and gone inside the first hold (they revert after
    /// <c>GameHud.NoteRevertSeconds</c>), which is the point — the walk is for
    /// the long ones, not a permanent animation.
    /// </para>
    /// <para>
    /// The label must be set to <see cref="HorizontalWrapMode.Overflow"/> and
    /// the lane must carry a <see cref="RectMask2D"/>; without the first it
    /// wraps anyway, and without the second it draws straight out through the
    /// page margins. <see cref="GameHud.BuildChrome"/> wires both.
    /// </para>
    /// </summary>
    public sealed class MarqueeLine : MonoBehaviour, ILayoutElement
    {
        /// <summary>The words. Sized to its own unwrapped measure, not to the lane.</summary>
        public Text label;

        /// <summary>How fast the words walk, in canvas units a second.</summary>
        public float travelSpeed = 90f;

        /// <summary>How long each end of the walk is held still, so both can be read.</summary>
        public float holdSeconds = 1.6f;

        private string _walking;
        private float _measured;
        private float _elapsed;

        public float minWidth => -1f;
        public float preferredWidth => -1f;
        public float flexibleWidth => -1f;
        public float minHeight => preferredHeight;
        public float flexibleHeight => -1f;
        public int layoutPriority => 2;

        /// <summary>
        /// One line, always. The label overflows rather than wrapping, so its
        /// own preferred height IS a single line whatever it holds — the floor
        /// is only for the empty string, which measures nothing and would
        /// otherwise collapse the lane on the frame a note is cleared.
        /// </summary>
        public float preferredHeight
        {
            get
            {
                if (label == null)
                {
                    return -1f;
                }

                return Mathf.Max(label.preferredHeight, label.fontSize * 1.25f);
            }
        }

        private void OnEnable()
        {
            // A note that comes back saying what it said last time still starts
            // at its own beginning — the lane was empty in between.
            _elapsed = 0f;
        }

        private void LateUpdate()
        {
            if (label == null)
            {
                return;
            }

            var line = (RectTransform)label.transform;
            if (!string.Equals(_walking, label.text))
            {
                _walking = label.text;
                _measured = label.preferredWidth;
                _elapsed = 0f;
                line.sizeDelta = new Vector2(_measured, 0f);
            }

            // Measured once per sentence, but the lane narrows on a rotation and
            // widens on a fold, so how far there is to walk is asked every frame.
            var over = _measured - ((RectTransform)transform).rect.width;
            if (over <= 0.5f)
            {
                line.anchoredPosition = Vector2.zero;
                return;
            }

            _elapsed += Time.deltaTime;
            line.anchoredPosition = new Vector2(-Shift(over), 0f);
        }

        /// <summary>
        /// How far along the sentence the lane is showing: held at the start,
        /// walked out to the tail, held there, and walked back. Public and
        /// static because the shape of the walk is the testable part — the
        /// component around it is a rect and a clock.
        /// </summary>
        public static float Shift(float over, float elapsed, float travelSeconds, float holdSeconds)
        {
            if (over <= 0f)
            {
                return 0f;
            }

            // A walk of no duration would divide by zero below, and a hold of
            // none is a legitimate setting: both leave the tail showing.
            if (travelSeconds <= 0f)
            {
                return holdSeconds <= 0f ? over : (Mathf.Repeat(elapsed, holdSeconds * 2f) < holdSeconds ? 0f : over);
            }

            var leg = holdSeconds + travelSeconds;
            var step = Mathf.Repeat(elapsed, leg * 2f);
            if (step < holdSeconds)
            {
                return 0f;
            }

            if (step < leg)
            {
                return (step - holdSeconds) / travelSeconds * over;
            }

            if (step < leg + holdSeconds)
            {
                return over;
            }

            return (1f - (step - leg - holdSeconds) / travelSeconds) * over;
        }

        private float Shift(float over)
        {
            return Shift(over, _elapsed, over / Mathf.Max(1f, travelSpeed), holdSeconds);
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }
    }
}
