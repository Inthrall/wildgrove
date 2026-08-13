using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One post's world-space sprite: a coloured disc under its plate, and the
    /// assignment badge beneath — the tiny icon of whoever holds the post (one
    /// body per post), which is also the tap target for posting. Dimmed while
    /// nothing works it. Placement and per-frame refresh are driven by
    /// <see cref="WorldView"/>.
    /// <para>
    /// Two kinds of post wear this view, and the difference is only what the
    /// plate shows: a gathering node (<see cref="Create"/>), which keeps its
    /// <see cref="Node"/>, and an observation site's sketching post
    /// (<see cref="CreateSketching"/>), whose <see cref="Node"/> is null because
    /// a site is no node. <see cref="PostId"/> is the station id either way, and
    /// it is what everything about STANDING should ask — <see cref="Node"/> is
    /// for the things that are really about a node, chiefly which resource a
    /// windfall pays out in.
    /// </para>
    /// (The selection ring is gone — selection stopped doing anything once
    /// taps opened sheets directly, and its near-paper colour never read.
    /// The scale pulse that rode the Tending burst is gone too — a caught
    /// windfall already plays its own send-off over the node, so the plate
    /// twitching underneath only added noise. The golden halo that breathed
    /// while the Choice window ran went the same way 2026-08-06: it lit for
    /// half a minute after every catch, which is most of the time on a worked
    /// node, so the strip's brightest mark was also its least informative.)
    /// </summary>
    public sealed class NodeWorldView : MonoBehaviour
    {
        private const float IdleAlpha = 0.55f;

        // The node plate's longest side, in local units before the parent's
        // per-diameter scale — a shade over the disc so the specimen fills
        // its mount.
        private const float PlateFit = 1.05f;

        private static readonly Color LabelColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2

        /// <summary>The gathering node this plate is, or null at a sketching post — a site is no node.</summary>
        public NodeState Node { get; private set; }

        /// <summary>The station id a body stands at here: the node's own id, or the site's sketching post.</summary>
        public string PostId { get; private set; }

        private SpriteRenderer _disc;
        private SpriteRenderer _plate;
        private AssignBadge _badge;
        private TextMesh _label;
        private Color _colour;
        private float _diameter = 1f;

        public static NodeWorldView Create(Transform parent, NodeState node, Color colour, Font labelFont, Sprite face)
        {
            // The resource name under the disc — the strip's shapes and the
            // FIG. plates below name the same thing, so a glance connects them.
            var view = Build(parent, "Node_" + node.resourceId, node.resourceId, colour, labelFont, face);
            view.Node = node;
            view.PostId = node.id;
            return view;
        }

        /// <summary>
        /// An observation site's sketching post, drawn as a ground like any
        /// other: the sketching plate over a disc in that site's own colour, and
        /// the badge of whoever draws there (design §6 — one post per site,
        /// warden or familiar).
        /// <para>
        /// It had no plate here at all until 2026-08-13, on the reasoning that a
        /// site is no node and so has no place among the grounds — which left the
        /// one post the player could fill and never see, and put a body doing
        /// real work off the assignment board entirely while they did it. The
        /// caption carries the zone because two sketching plates otherwise read
        /// as the same place twice.
        /// </para>
        /// </summary>
        public static NodeWorldView CreateSketching(Transform parent, string zoneId, string stationId,
            Color colour, Font labelFont, Sprite face)
        {
            var view = Build(parent, "Sketching_" + zoneId, zoneId + " sketching", colour, labelFont, face);
            view.PostId = stationId;
            return view;
        }

        private static NodeWorldView Build(Transform parent, string name, string caption, Color colour,
            Font labelFont, Sprite face)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<NodeWorldView>();
            view._colour = colour;

            view._label = PlaceholderArt.CreateLabel(go.transform, caption, labelFont, LabelColour,
                StripLayers.NodeCaption);

            view._disc = CreateSprite(go.transform, "Disc", PlaceholderArt.Disc, colour, StripLayers.NodeDisc);

            // The resource's naturalist plate, pinned over the disc — the disc's
            // resource colour peeks around the portrait as a card mount. Scaled
            // so its longest side ≈ one node diameter (the plate is authored at
            // 100 px/unit, so a ~1200 px plate is ~12 units before this fit).
            if (face != null)
            {
                view._plate = CreateSprite(go.transform, "Plate", face, Color.white, StripLayers.NodePlate);
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
        public void Refresh(bool wardenPosted, Familiar occupant, Sprite occupantIcon, bool dimIdle)
        {
            transform.localScale = Vector3.one * _diameter;

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
