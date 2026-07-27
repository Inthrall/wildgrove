using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One gathering node's world-space sprite: a resource-coloured disc, a
    /// gentle scale pulse while a Tending burst is live, a golden halo while
    /// the post-tend Pristine window runs, and the assignment badge beneath
    /// it — the tiny icon of whoever holds the post (one body per node),
    /// which is also the tap target for posting. Dimmed while nothing works
    /// it. Placement and per-frame refresh are driven by <see cref="WorldView"/>.
    /// (The selection ring is gone — selection stopped doing anything once
    /// taps opened sheets directly, and its near-paper colour never read.)
    /// </summary>
    public sealed class NodeWorldView : MonoBehaviour
    {
        private const float HaloScale = 1.16f;
        private const float PulseAmount = 0.08f;
        private const float PulseSpeed = 8f;
        private const float HaloPulseSpeed = 3f;
        private const float IdleAlpha = 0.55f;

        // The node plate's longest side, in local units before the parent's
        // per-diameter scale — a shade over the disc so the specimen fills
        // its mount.
        private const float PlateFit = 1.05f;

        private static readonly Color HaloColour = new Color(1f, 0.78f, 0.25f, 0.5f);
        private static readonly Color LabelColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2

        public NodeState Node { get; private set; }

        private SpriteRenderer _disc;
        private SpriteRenderer _plate;
        private SpriteRenderer _halo;
        private AssignBadge _badge;
        private TextMesh _label;
        private Color _colour;
        private float _diameter = 1f;

        public static NodeWorldView Create(Transform parent, NodeState node, Color colour, Font labelFont, Sprite face)
        {
            var go = new GameObject("Node_" + node.resourceId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<NodeWorldView>();
            view.Node = node;
            view._colour = colour;

            // The resource name under the disc — the strip's shapes and the
            // FIG. plates below name the same thing, so a glance connects them.
            view._label = PlaceholderArt.CreateLabel(go.transform, node.resourceId, labelFont, LabelColour);

            view._halo = CreateSprite(go.transform, "Halo", PlaceholderArt.Disc, HaloColour, 1);
            view._halo.transform.localScale = Vector3.one * HaloScale;

            view._disc = CreateSprite(go.transform, "Disc", PlaceholderArt.Disc, colour, 2);

            // The resource's naturalist plate, pinned over the disc — the disc's
            // resource colour peeks around the portrait as a card mount. Scaled
            // so its longest side ≈ one node diameter (the plate is authored at
            // 100 px/unit, so a ~1200 px plate is ~12 units before this fit).
            if (face != null)
            {
                view._plate = CreateSprite(go.transform, "Plate", face, Color.white, 3);
                var longest = Mathf.Max(face.bounds.size.x, face.bounds.size.y);
                view._plate.transform.localScale = Vector3.one * (longest > 0f ? PlateFit / longest : 1f);

                // The plate is the node's face now — hide the coloured disc so it
                // doesn't peek around the specimen. Its colour still names the
                // resource elsewhere, so the id stays glanceable.
                view._disc.enabled = false;
            }

            // Who works here — the post's badge, which is also the tap target
            // for assigning (one body per node, design §2).
            view._badge = new AssignBadge(go.transform, labelFont);

            return view;
        }

        public void SetPlacement(Vector3 worldPosition, float worldDiameter)
        {
            transform.position = worldPosition;
            _diameter = worldDiameter;
        }

        /// <summary>Re-run the label's mesh after a dynamic font atlas rebuild.</summary>
        public void RefreshLabel()
        {
            PlaceholderArt.RefreshLabel(_label);
            _badge.RefreshMark();
        }

        /// <summary>
        /// Move the badge and toggle the caption for the strip's current row
        /// layout — in two rows a full badge drop and a hanging caption both
        /// draw over the row beneath.
        /// </summary>
        public void SetStripLayout(float badgeOffsetY, bool showCaption)
        {
            _badge.SetOffset(badgeOffsetY);
            if (_label != null && _label.gameObject.activeSelf != showCaption)
            {
                _label.gameObject.SetActive(showCaption);
            }
        }

        /// <summary>
        /// Per-frame state refresh. <paramref name="dimIdle"/> false suspends
        /// the idle dimming — on a fresh camp with nothing posted anywhere,
        /// dimming EVERY plate read as "disabled" exactly when the first tap
        /// (posting) had to happen; dim only once dim can mean something.
        /// </summary>
        public void Refresh(float time, bool wardenPosted, Familiar occupant, Sprite occupantIcon, bool dimIdle)
        {
            var pulse = Node.tendBurstRemaining > 0.0
                ? 1f + PulseAmount * Mathf.Sin(time * PulseSpeed)
                : 1f;
            transform.localScale = Vector3.one * (_diameter * pulse);

            // The Pristine window outlasts the yield burst — the halo breathes
            // slowly so it reads as "charged" rather than "working".
            var windowLive = Node.pristineBonusRemaining > 0.0;
            _halo.enabled = windowLive;
            if (windowLive)
            {
                var halo = HaloColour;
                halo.a = 0.35f + 0.2f * Mathf.Sin(time * HaloPulseSpeed);
                _halo.color = halo;
            }

            // One body per post: somebody standing here — warden or familiar —
            // is what "working" means now.
            var working = wardenPosted || occupant != null;
            var alpha = working || !dimIdle ? 1f : IdleAlpha;
            var colour = _colour;
            colour.a = alpha;
            _disc.color = colour;

            // The plate wears the same idle dimming so a fallow node reads as
            // fallow whether it shows a plate or the bare disc.
            if (_plate != null)
            {
                _plate.color = new Color(1f, 1f, 1f, alpha);
            }

            _badge.Refresh(wardenPosted, occupant, occupantIcon);
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Color colour, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
