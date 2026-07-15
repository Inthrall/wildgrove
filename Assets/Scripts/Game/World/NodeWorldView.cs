using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One gathering node's world-space sprite: a resource-coloured disc, an
    /// accent ring behind it while the node is selected, a gentle scale pulse
    /// while a Tending burst is live, and dimmed while no familiar works it.
    /// Placement and per-frame refresh are driven by <see cref="WorldView"/>.
    /// </summary>
    public sealed class NodeWorldView : MonoBehaviour
    {
        private const float RingScale = 1.3f;
        private const float PulseAmount = 0.08f;
        private const float PulseSpeed = 8f;
        private const float IdleAlpha = 0.55f;

        public NodeState Node { get; private set; }

        private SpriteRenderer _disc;
        private SpriteRenderer _ring;
        private Color _colour;
        private float _diameter = 1f;

        public static NodeWorldView Create(Transform parent, NodeState node, Color colour, Color ringColour)
        {
            var go = new GameObject("Node_" + node.resourceId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<NodeWorldView>();
            view.Node = node;
            view._colour = colour;

            var ringGo = new GameObject("Ring", typeof(SpriteRenderer));
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localScale = Vector3.one * RingScale;
            view._ring = ringGo.GetComponent<SpriteRenderer>();
            view._ring.sprite = PlaceholderArt.Disc;
            view._ring.color = ringColour;
            view._ring.sortingOrder = 0;

            var discGo = new GameObject("Disc", typeof(SpriteRenderer));
            discGo.transform.SetParent(go.transform, false);
            view._disc = discGo.GetComponent<SpriteRenderer>();
            view._disc.sprite = PlaceholderArt.Disc;
            view._disc.color = colour;
            view._disc.sortingOrder = 1;

            return view;
        }

        public void SetPlacement(Vector3 worldPosition, float worldDiameter)
        {
            transform.position = worldPosition;
            _diameter = worldDiameter;
        }

        public void Refresh(bool selected, float time)
        {
            var pulse = Node.tendBurstRemaining > 0.0
                ? 1f + PulseAmount * Mathf.Sin(time * PulseSpeed)
                : 1f;
            transform.localScale = Vector3.one * (_diameter * pulse);

            _ring.enabled = selected;

            var colour = _colour;
            colour.a = Node.familiarCount > 0 ? 1f : IdleAlpha;
            _disc.color = colour;
        }
    }
}
