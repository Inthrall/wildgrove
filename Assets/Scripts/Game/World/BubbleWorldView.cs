using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One windfall adrift in the node strip: the resource's own naturalist
    /// plate, risen from a worked node on a soft parchment mount and turning
    /// slowly as it goes — a pressed cutting carried on the wind. Caught with
    /// a tap for a burst of that node's goods (see <see cref="Wildgrove.Sim.Bubbles"/>).
    /// Purely ephemeral — nothing here persists. <see cref="WorldView"/> owns
    /// spawn timing, the float path, expiry and the hit test; this is just
    /// the sprite.
    /// </summary>
    public sealed class BubbleWorldView : MonoBehaviour
    {
        // The plate's longest side in local units before the parent's
        // per-diameter scale, leaving the mount showing as a halo around it
        // (the same fit NodeWorldView gives a node's face).
        private const float PlateFit = 1f;
        private const float MountAlpha = 0.4f;
        private const float SkinAlpha = 0.8f;
        private const float SwayDegrees = 7f;
        private const float SwaySpeed = 1.1f;

        // Warm parchment: a windfall is the land handing something over, so it
        // carries the journal's own paper light rather than a resource hue.
        private static readonly Color MountColour = new Color(1f, 0.93f, 0.74f, MountAlpha);
        private static readonly Color ShineColour = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>The node this windfall rose from — what a catch pays out in.</summary>
        public NodeState Node { get; private set; }

        /// <summary>When it spawned (Time.time) — WorldView ages it from here.</summary>
        public float SpawnTime { get; private set; }

        /// <summary>Per-windfall wobble phase so simultaneous ones don't drift in lockstep.</summary>
        public float Seed { get; private set; }

        /// <summary>Last placement's screen position — the tap hit test point.</summary>
        public Vector2 ScreenPosition { get; private set; }

        /// <summary>Last placement's screen radius — the tap hit test circle.</summary>
        public float ScreenRadius { get; private set; }

        private SpriteRenderer _mount;
        private SpriteRenderer _plate;
        private SpriteRenderer _skin;
        private SpriteRenderer _shine;
        private Color _colour;

        public static BubbleWorldView Create(Transform parent, NodeState node, Color colour, Sprite face, float spawnTime, float seed)
        {
            var go = new GameObject("Windfall_" + node.resourceId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BubbleWorldView>();
            view.Node = node;
            view.SpawnTime = spawnTime;
            view.Seed = seed;
            view._colour = colour;

            if (face != null)
            {
                // A soft paper glow behind the cutting so it reads against the
                // strip without borrowing the Pristine window's gold halo.
                view._mount = CreateSprite(go.transform, "Mount", PlaceholderArt.Disc, MountColour, 6);

                view._plate = CreateSprite(go.transform, "Plate", face, Color.white, 7);
                var longest = Mathf.Max(face.bounds.size.x, face.bounds.size.y);
                view._plate.transform.localScale = Vector3.one * (longest > 0f ? PlateFit / longest : 1f);

                return view;
            }

            // No plate for this resource: fall back to a tinted disc with an
            // off-centre highlight, which reads as a drifting bead rather than
            // another node.
            view._skin = CreateSprite(go.transform, "Skin", PlaceholderArt.Disc, colour, 6);
            view._shine = CreateSprite(go.transform, "Shine", PlaceholderArt.Disc, ShineColour, 7);
            view._shine.transform.localScale = Vector3.one * 0.28f;
            view._shine.transform.localPosition = new Vector3(-0.22f, 0.24f, 0f);

            return view;
        }

        /// <summary>Place the windfall for this frame; <paramref name="fade"/> is 1 fully drawn, 0 gone.</summary>
        public void SetPlacement(Vector3 worldPosition, float worldDiameter, Vector2 screenPosition, float screenRadius, float fade)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * worldDiameter;

            // Turning as it rises — the wind has hold of it.
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * SwaySpeed + Seed) * SwayDegrees);

            ScreenPosition = screenPosition;
            ScreenRadius = screenRadius;

            SetAlpha(_mount, MountColour, MountColour.a * fade);
            SetAlpha(_plate, Color.white, fade);
            SetAlpha(_skin, _colour, SkinAlpha * fade);
            SetAlpha(_shine, ShineColour, ShineColour.a * fade);
        }

        private static void SetAlpha(SpriteRenderer renderer, Color colour, float alpha)
        {
            if (renderer == null)
            {
                return;
            }

            colour.a = alpha;
            renderer.color = colour;
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
