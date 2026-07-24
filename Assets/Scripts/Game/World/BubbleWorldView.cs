using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One windfall bubble adrift in the node strip: a resource-tinted disc
    /// with a small highlight, risen from a worked node, caught with a tap
    /// for a burst of that node's goods (see <see cref="Wildgrove.Sim.Bubbles"/>).
    /// Purely ephemeral — nothing here persists. <see cref="WorldView"/> owns
    /// spawn timing, the float path, expiry and the hit test; this is just
    /// the sprite.
    /// </summary>
    public sealed class BubbleWorldView : MonoBehaviour
    {
        private const float SkinAlpha = 0.8f;
        private static readonly Color ShineColour = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>The node this bubble rose from — what a catch pays out in.</summary>
        public NodeState Node { get; private set; }

        /// <summary>When the bubble spawned (Time.time) — WorldView ages it from here.</summary>
        public float SpawnTime { get; private set; }

        /// <summary>Per-bubble wobble phase so simultaneous bubbles don't drift in lockstep.</summary>
        public float Seed { get; private set; }

        /// <summary>Last placement's screen position — the tap hit test point.</summary>
        public Vector2 ScreenPosition { get; private set; }

        /// <summary>Last placement's screen radius — the tap hit test circle.</summary>
        public float ScreenRadius { get; private set; }

        private SpriteRenderer _skin;
        private SpriteRenderer _shine;
        private Color _colour;

        public static BubbleWorldView Create(Transform parent, NodeState node, Color colour, float spawnTime, float seed)
        {
            var go = new GameObject("Bubble_" + node.resourceId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BubbleWorldView>();
            view.Node = node;
            view.SpawnTime = spawnTime;
            view.Seed = seed;
            view._colour = colour;

            view._skin = CreateSprite(go.transform, "Skin", colour, 6);

            // A soft off-centre highlight is what makes a tinted disc read as
            // a bubble rather than another node.
            view._shine = CreateSprite(go.transform, "Shine", ShineColour, 7);
            view._shine.transform.localScale = Vector3.one * 0.28f;
            view._shine.transform.localPosition = new Vector3(-0.22f, 0.24f, 0f);

            return view;
        }

        /// <summary>Place the bubble for this frame; <paramref name="fade"/> is 1 fully drawn, 0 gone.</summary>
        public void SetPlacement(Vector3 worldPosition, float worldDiameter, Vector2 screenPosition, float screenRadius, float fade)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * worldDiameter;
            ScreenPosition = screenPosition;
            ScreenRadius = screenRadius;

            var skin = _colour;
            skin.a = SkinAlpha * fade;
            _skin.color = skin;
            var shine = ShineColour;
            shine.a = ShineColour.a * fade;
            _shine.color = shine;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Color colour, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderArt.Disc;
            renderer.color = colour;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
