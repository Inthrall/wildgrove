using UnityEngine;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// The warden's own plate — the strip's first, and the only one always on
    /// it. The warden is a body like any other (design §2: one body per post),
    /// but the one body the player IS, and the strip drew them only as a badge
    /// under whichever node they stood at: a warden at camp was nowhere on the
    /// board at all, so the tap that puts the PLAYER to work had no home on the
    /// surface that exists to hand out work.
    /// <para>
    /// It carries the warden's mark while they hold a post, and a moss (+)
    /// while they stand at camp — the strip's own invitation mark, so an idle
    /// warden reads exactly the way an unfilled slot does. The caption names
    /// the slot either way, so the leading plate is always recognisably theirs.
    /// Placement and per-frame refresh are driven by <see cref="WorldView"/>.
    /// </para>
    /// </summary>
    public sealed class WardenWorldView : MonoBehaviour
    {
        // The mark's longest side in local units, before the parent's
        // per-diameter scale — a shade under the node plates' 1.05, because a
        // figure reads bigger than a specimen at the same width.
        private const float FaceFit = 0.95f;

        private static readonly Color LabelColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2
        private static readonly Color MarkColour = new Color(0.333f, 0.392f, 0.247f, 1f);  // JournalTheme's MossDeep
        private static readonly Color FallbackColour = new Color(0.95f, 0.92f, 0.83f, 1f); // tints the placeholder only

        private SpriteRenderer _disc;
        private SpriteRenderer _face;
        private TextMesh _mark;
        private TextMesh _label;
        private bool _hasPlate;

        public static WardenWorldView Create(Transform parent, string caption, Font labelFont)
        {
            var go = new GameObject("Warden");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<WardenWorldView>();

            view._label = PlaceholderArt.CreateLabel(go.transform, caption, labelFont, LabelColour);

            // The mount the placeholder silhouette stands on. A real mark needs
            // none — like a node's plate, it is the face itself.
            view._disc = CreateSprite(go.transform, "Disc", PlaceholderArt.Disc,
                PlaceholderArt.DigSiteColour("warden"), 2);

            var plate = ArtLibrary.ForWarden();
            view._hasPlate = plate != null;
            view._face = view._hasPlate
                ? CreateSprite(go.transform, "Face", plate, Color.white, 3)
                : CreateSprite(go.transform, "Face", PlaceholderArt.Triangle, FallbackColour, 3);
            var faceSprite = view._face.sprite;
            var longest = Mathf.Max(faceSprite.bounds.size.x, faceSprite.bounds.size.y);
            var fit = view._hasPlate ? FaceFit : FaceFit * 0.55f;
            view._face.transform.localScale = Vector3.one * (longest > 0f ? fit / longest : 1f);

            // The (+) the plate wears at camp, drawn at the size and colour of
            // the one closing the strip — the two mean the same thing.
            var markGo = new GameObject("Mark");
            markGo.transform.SetParent(go.transform, false);
            view._mark = markGo.AddComponent<TextMesh>();
            view._mark.font = labelFont;
            view._mark.fontSize = 64;
            view._mark.characterSize = 0.09f;
            view._mark.anchor = TextAnchor.MiddleCenter;
            view._mark.alignment = TextAlignment.Center;
            view._mark.color = MarkColour;
            var renderer = markGo.GetComponent<MeshRenderer>();
            renderer.material = labelFont.material;
            renderer.sortingOrder = 5;

            return view;
        }

        public void SetPlacement(Vector3 worldPosition, float worldDiameter)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * worldDiameter;
        }

        /// <summary>Re-run the label and mark meshes after a dynamic font atlas rebuild.</summary>
        public void RefreshLabel()
        {
            PlaceholderArt.RefreshLabel(_label);
            PlaceholderArt.RefreshLabel(_mark);
        }

        /// <summary>Toggle the caption for the strip's current row layout — in two rows it would hang over the row beneath (see NodeWorldView.SetStripLayout).</summary>
        public void SetStripLayout(bool showCaption)
        {
            if (_label != null && _label.gameObject.activeSelf != showCaption)
            {
                _label.gameObject.SetActive(showCaption);
            }
        }

        /// <summary>
        /// Draw the warden as posted (their mark) or at camp (the moss (+)).
        /// <paramref name="posted"/> covers the wander post too: roaming is
        /// holding a post, so the mark stands either way.
        /// </summary>
        public void Refresh(bool posted)
        {
            _face.enabled = posted;
            _disc.enabled = posted && !_hasPlate;
            var text = posted ? string.Empty : "+";
            if (_mark.text != text)
            {
                _mark.text = text;
            }
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
