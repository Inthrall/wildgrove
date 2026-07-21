using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One gathering node's world-space sprite: a resource-coloured disc, an
    /// accent ring behind it while the node is selected, a gentle scale pulse
    /// while a Tending burst is live, a golden halo while the post-tend
    /// Pristine window runs, a small dot per working familiar arced beneath
    /// it (so a glance shows where the flock is), and a diamond marker when a
    /// bonded companion works here. Dimmed while nothing works it. Placement
    /// and per-frame refresh are driven by <see cref="WorldView"/>.
    /// </summary>
    public sealed class NodeWorldView : MonoBehaviour
    {
        private const float RingScale = 1.3f;
        private const float HaloScale = 1.16f;
        private const float PulseAmount = 0.08f;
        private const float PulseSpeed = 8f;
        private const float HaloPulseSpeed = 3f;
        private const float IdleAlpha = 0.55f;

        // Beyond this the dots stop being countable anyway; the HUD row
        // carries the exact number.
        private const int MaxFlockDots = 8;
        private const float FlockDotScale = 0.14f;
        private const float FlockArcY = -0.72f;
        private const float FlockArcHalfWidth = 0.45f;

        private static readonly Color HaloColour = new Color(1f, 0.78f, 0.25f, 0.5f);
        private static readonly Color BondedColour = new Color(1f, 0.72f, 0.2f, 1f);
        private static readonly Color WardenColour = new Color(0.95f, 0.92f, 0.83f, 1f);
        private static readonly Color LabelColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2

        public NodeState Node { get; private set; }

        private SpriteRenderer _disc;
        private SpriteRenderer _ring;
        private SpriteRenderer _halo;
        private SpriteRenderer _bondedMarker;
        private SpriteRenderer _wardenMarker;
        private SpriteRenderer[] _flockDots;
        private TextMesh _label;
        private Color _colour;
        private float _diameter = 1f;

        public static NodeWorldView Create(Transform parent, NodeState node, Color colour, Color ringColour, Font labelFont)
        {
            var go = new GameObject("Node_" + node.resourceId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<NodeWorldView>();
            view.Node = node;
            view._colour = colour;

            // The resource name under the disc — the strip's shapes and the
            // FIG. plates below name the same thing, so a glance connects them.
            view._label = PlaceholderArt.CreateLabel(go.transform, node.resourceId, labelFont, LabelColour);

            view._ring = CreateSprite(go.transform, "Ring", PlaceholderArt.Disc, ringColour, 0);
            view._ring.transform.localScale = Vector3.one * RingScale;

            view._halo = CreateSprite(go.transform, "Halo", PlaceholderArt.Disc, HaloColour, 1);
            view._halo.transform.localScale = Vector3.one * HaloScale;

            view._disc = CreateSprite(go.transform, "Disc", PlaceholderArt.Disc, colour, 2);

            // The flock: one small dot per working familiar, arced under the
            // disc — a darker shade of the resource so it reads as "at" the node.
            var dotColour = Color.Lerp(colour, Color.black, 0.4f);
            view._flockDots = new SpriteRenderer[MaxFlockDots];
            for (var i = 0; i < MaxFlockDots; i++)
            {
                var dot = CreateSprite(go.transform, "Flock_" + i, PlaceholderArt.Disc, dotColour, 3);
                dot.transform.localScale = Vector3.one * FlockDotScale;
                dot.enabled = false;
                view._flockDots[i] = dot;
            }

            view._bondedMarker = CreateSprite(go.transform, "Bonded", PlaceholderArt.Diamond, BondedColour, 3);
            view._bondedMarker.transform.localScale = Vector3.one * 0.32f;
            view._bondedMarker.transform.localPosition = new Vector3(0.42f, 0.42f, 0f);
            view._bondedMarker.enabled = false;

            // The warden's post — a parchment-cream triangle (a little tent)
            // above the node they stand at.
            view._wardenMarker = CreateSprite(go.transform, "Warden", PlaceholderArt.Triangle, WardenColour, 3);
            view._wardenMarker.transform.localScale = Vector3.one * 0.34f;
            view._wardenMarker.transform.localPosition = new Vector3(-0.42f, 0.46f, 0f);
            view._wardenMarker.enabled = false;

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
        }

        public void Refresh(bool selected, float time, int workingCount, bool hasBonded, bool wardenPosted)
        {
            _wardenMarker.enabled = wardenPosted;

            var pulse = Node.tendBurstRemaining > 0.0
                ? 1f + PulseAmount * Mathf.Sin(time * PulseSpeed)
                : 1f;
            transform.localScale = Vector3.one * (_diameter * pulse);

            _ring.enabled = selected;

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

            // The warden counts as somebody working it — a bare node at the
            // post shouldn't read as idle.
            var working = workingCount > 0 || wardenPosted;
            var colour = _colour;
            colour.a = working ? 1f : IdleAlpha;
            _disc.color = colour;

            // One dot per familiar stationed here (design §2) — a glance shows
            // where the kith stands.
            var dots = Mathf.Min(workingCount, MaxFlockDots);
            for (var i = 0; i < _flockDots.Length; i++)
            {
                _flockDots[i].enabled = i < dots;
                if (i < dots)
                {
                    // Spread the visible dots evenly across the arc.
                    var t = dots == 1 ? 0.5f : i / (float)(dots - 1);
                    _flockDots[i].transform.localPosition = new Vector3(
                        Mathf.Lerp(-FlockArcHalfWidth, FlockArcHalfWidth, t), FlockArcY, 0f);
                }
            }

            _bondedMarker.enabled = hasBonded;
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
