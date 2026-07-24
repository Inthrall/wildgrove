using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// The small assignment badge pinned under every strip post (node, trail,
    /// wander): a tiny icon of whoever holds the post — the warden's tent, a
    /// familiar's portrait, or a tinted disc with the familiar's initial when
    /// its species has no plate yet — and a dashed-feeling "+" mark while the
    /// post stands empty. Tapping the badge is the assign/unassign gesture
    /// (<see cref="WorldView.StationAtScreenPoint"/> does the hit test; this
    /// is just the visuals).
    /// </summary>
    public sealed class AssignBadge
    {
        private static readonly Color WardenColour = new Color(0.95f, 0.92f, 0.83f, 1f);
        private static readonly Color VacantBack = new Color(0.9f, 0.86f, 0.76f, 0.45f);
        private static readonly Color OccupiedBack = new Color(0.98f, 0.95f, 0.88f, 1f);
        private static readonly Color BondedColour = new Color(1f, 0.72f, 0.2f, 1f);
        private static readonly Color MarkColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2

        private readonly SpriteRenderer _back;
        private readonly SpriteRenderer _icon;
        private readonly SpriteRenderer _bondedPip;
        private readonly TextMesh _mark;

        /// <summary>Local-units offset of the badge centre below the post sprite (the parent is scaled to one diameter).</summary>
        public const float OffsetY = WorldStrip.BadgeOffsetFactor;

        public AssignBadge(Transform parent, Font markFont)
        {
            var root = new GameObject("Badge");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, OffsetY, 0f);

            _back = CreateSprite(root.transform, "Back", PlaceholderArt.Disc, VacantBack, 3);
            _back.transform.localScale = Vector3.one * (WorldStrip.BadgeRadiusFactor * 2f);

            _icon = CreateSprite(root.transform, "Icon", null, Color.white, 4);
            _icon.enabled = false;

            // The bonded companion's gold diamond, riding the badge's shoulder.
            _bondedPip = CreateSprite(root.transform, "Bonded", PlaceholderArt.Diamond, BondedColour, 5);
            _bondedPip.transform.localScale = Vector3.one * 0.12f;
            _bondedPip.transform.localPosition = new Vector3(WorldStrip.BadgeRadiusFactor * 0.9f, WorldStrip.BadgeRadiusFactor * 0.9f, 0f);
            _bondedPip.enabled = false;

            // One TextMesh serves both the vacant "+" and the no-plate initial.
            var markGo = new GameObject("Mark");
            markGo.transform.SetParent(root.transform, false);
            _mark = markGo.AddComponent<TextMesh>();
            _mark.font = markFont;
            _mark.fontSize = 64;
            _mark.characterSize = 0.09f;
            _mark.anchor = TextAnchor.MiddleCenter;
            _mark.alignment = TextAlignment.Center;
            _mark.color = MarkColour;
            var renderer = markGo.GetComponent<MeshRenderer>();
            renderer.material = markFont.material;
            renderer.sortingOrder = 5;
        }

        /// <summary>Re-run the mark's mesh after a dynamic font atlas rebuild.</summary>
        public void RefreshMark()
        {
            PlaceholderArt.RefreshLabel(_mark);
        }

        /// <summary>Draw the post's holder: the warden, a familiar (with its plate or initial), or the vacant "+".</summary>
        public void Refresh(bool wardenPosted, Familiar occupant, Sprite occupantIcon)
        {
            if (wardenPosted)
            {
                _back.enabled = true;
                _back.color = OccupiedBack;
                ShowIcon(PlaceholderArt.Triangle, WardenColour, 0.36f);
                SetMark(string.Empty);
                _bondedPip.enabled = false;
                return;
            }

            if (occupant != null)
            {
                _back.enabled = true;
                _bondedPip.enabled = occupant.bonded;
                if (occupantIcon != null)
                {
                    _back.color = OccupiedBack;
                    ShowIcon(occupantIcon, Color.white, WorldStrip.BadgeRadiusFactor * 2f * 0.92f);
                    SetMark(string.Empty);
                }
                else
                {
                    // No plate for this species yet — a tinted disc wearing the
                    // familiar's initial keeps the badge tellable at a glance.
                    _back.color = PlaceholderArt.ResourceColour(occupant.speciesId ?? string.Empty);
                    _icon.enabled = false;
                    SetMark(Initial(occupant));
                }

                return;
            }

            // A vacant post shows nothing — tapping the plate itself is the
            // assign gesture now, so the old "+" invitation would just repeat
            // what the idle-dimmed plate already says.
            _back.enabled = false;
            _icon.enabled = false;
            _bondedPip.enabled = false;
            SetMark(string.Empty);
        }

        private void ShowIcon(Sprite sprite, Color colour, float fit)
        {
            _icon.sprite = sprite;
            _icon.color = colour;
            _icon.enabled = sprite != null;
            if (sprite == null)
            {
                return;
            }

            var longest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            _icon.transform.localScale = Vector3.one * (longest > 0f ? fit / longest : 1f);
        }

        private void SetMark(string text)
        {
            if (_mark.text != text)
            {
                _mark.text = text;
            }
        }

        private static string Initial(Familiar familiar)
        {
            var name = string.IsNullOrEmpty(familiar.name) ? familiar.speciesId : familiar.name;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
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
