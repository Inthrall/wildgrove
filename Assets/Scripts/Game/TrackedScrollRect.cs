using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// A ScrollRect that says when a finger is actively dragging it —
    /// <see cref="ScrollRect.velocity"/> alone can't (a held-still drag has
    /// zero velocity). The HUD defers its heavy label pass while the scroll
    /// is live, because a mid-drag layout rebuild is a visible hitch.
    /// </summary>
    public sealed class TrackedScrollRect : ScrollRect
    {
        public bool Dragging { get; private set; }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            Dragging = true;
            base.OnBeginDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            Dragging = false;
            base.OnEndDrag(eventData);
        }

        protected override void OnDisable()
        {
            Dragging = false;
            base.OnDisable();
        }
    }
}
