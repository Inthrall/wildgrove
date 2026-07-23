using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// A non-gathering post's world-space sprite — the trail (the carrier's
    /// haul lane) and the wander post (roaming every node and watch site).
    /// A face sprite over a muted disc, the caption beneath, and the same
    /// assignment badge the nodes wear. There is nothing to tend here, so a
    /// tap anywhere on it opens the posting sheet. Dimmed while the post
    /// stands empty. Placement and per-frame refresh are driven by
    /// <see cref="WorldView"/>.
    /// </summary>
    public sealed class StationWorldView : MonoBehaviour
    {
        private const float IdleAlpha = 0.55f;
        private const float FaceFit = 1.0f;

        private static readonly Color LabelColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2

        /// <summary>The station id this post assigns to ("trail" / "wander").</summary>
        public string StationId { get; private set; }

        private SpriteRenderer _disc;
        private SpriteRenderer _face;
        private AssignBadge _badge;
        private TextMesh _label;
        private Color _colour;

        public static StationWorldView Create(Transform parent, string stationId, string caption, Color colour, Font labelFont, Sprite face)
        {
            var go = new GameObject("Station_" + stationId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<StationWorldView>();
            view.StationId = stationId;
            view._colour = colour;

            view._label = PlaceholderArt.CreateLabel(go.transform, caption, labelFont, LabelColour);

            view._disc = CreateSprite(go.transform, "Disc", PlaceholderArt.Disc, colour, 2);

            if (face != null)
            {
                view._face = CreateSprite(go.transform, "Face", face, Color.white, 3);
                var longest = Mathf.Max(face.bounds.size.x, face.bounds.size.y);
                view._face.transform.localScale = Vector3.one * (longest > 0f ? FaceFit / longest : 1f);
                view._disc.enabled = false;
            }

            view._badge = new AssignBadge(go.transform, labelFont);
            return view;
        }

        public void SetPlacement(Vector3 worldPosition, float worldDiameter)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * worldDiameter;
        }

        /// <summary>Re-run the label's mesh after a dynamic font atlas rebuild.</summary>
        public void RefreshLabel()
        {
            PlaceholderArt.RefreshLabel(_label);
            _badge.RefreshMark();
        }

        public void Refresh(Familiar occupant, Sprite occupantIcon)
        {
            var working = occupant != null;
            var colour = _colour;
            colour.a = working ? 1f : IdleAlpha;
            _disc.color = colour;
            if (_face != null)
            {
                _face.color = new Color(1f, 1f, 1f, working ? 1f : IdleAlpha);
            }

            _badge.Refresh(false, occupant, occupantIcon);
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
