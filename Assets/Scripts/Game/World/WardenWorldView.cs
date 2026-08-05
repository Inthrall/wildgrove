using UnityEngine;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// The warden's place on the strip while they stand at camp — an EMPTY
    /// GROUND wearing their badge, rather than a plate of the warden themselves.
    /// <para>
    /// Third shape for this, and the reasoning is worth keeping. The warden was
    /// drawn only as a badge under whichever node they stood at, so a warden at
    /// camp was nowhere on the board at all — the tap that puts the PLAYER to
    /// work had no home on the surface that exists to hand out work. A plate of
    /// their own at the head of the strip (2026-08-05) fixed the absence and
    /// broke the grammar: every plate on the strip is a ground, so a body among
    /// them read as a node you could gather from, captioned with a name where
    /// its neighbours carried crops. It came off again 2026-08-06.
    /// </para>
    /// <para>
    /// So the strip keeps its grammar and this slot obeys it. The plate is a
    /// ground with nothing on it — a moss (+) where the crop would be, the same
    /// mark the (+) closing the strip wears — and the warden is in the BADGE
    /// beneath, which is where every body on this board is drawn. The caption
    /// names why the ground is empty, never the warden: a name-captioned plate is
    /// what ran into its neighbour's caption before.
    /// </para>
    /// <para>
    /// Camp only. A wandering warden holds the wander post, which is no single
    /// node and so has no plate here either — but drawing them on an empty
    /// ground would say they had no work, when roaming IS the work
    /// (decided 2026-08-06: one slot meaning one thing).
    /// </para>
    /// Placement is driven by <see cref="WorldView"/>; nothing here changes
    /// frame to frame, so there is no per-frame refresh.
    /// </summary>
    public sealed class WardenWorldView : MonoBehaviour
    {
        private static readonly Color LabelColour = new Color(0.431f, 0.376f, 0.278f, 1f); // GameHud's Ink2
        private static readonly Color MossColour = new Color(0.333f, 0.392f, 0.247f, 1f);  // JournalTheme's MossDeep

        private TextMesh _plus;
        private TextMesh _label;
        private AssignBadge _badge;

        public static WardenWorldView Create(Transform parent, Font labelFont)
        {
            var go = new GameObject("WardenPlace");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<WardenWorldView>();

            view._label = PlaceholderArt.CreateLabel(go.transform, "at camp", labelFont, LabelColour);

            // The (+) an empty ground wears, drawn at the size and colour of the
            // one closing the strip — the two mean the same thing: a place
            // waiting to be filled.
            var plusGo = new GameObject("Plus");
            plusGo.transform.SetParent(go.transform, false);
            view._plus = plusGo.AddComponent<TextMesh>();
            view._plus.font = labelFont;
            view._plus.fontSize = 64;
            view._plus.characterSize = 0.09f;
            view._plus.anchor = TextAnchor.MiddleCenter;
            view._plus.alignment = TextAlignment.Center;
            view._plus.color = MossColour;
            view._plus.text = "+";
            plusGo.GetComponent<MeshRenderer>().material = labelFont.material;
            plusGo.GetComponent<MeshRenderer>().sortingOrder = 5;

            // The warden rides in the badge, like every other body on the strip.
            // Drawn once: the badge's warden branch is their silhouette on
            // occupied paper — a body without a ground, not a vacancy.
            view._badge = new AssignBadge(go.transform, labelFont);
            view._badge.Refresh(true, null, null);

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
            PlaceholderArt.RefreshLabel(_plus);
            _badge.RefreshMark();
        }

        /// <summary>Move the badge and toggle the caption for the strip's current row layout (see <see cref="NodeWorldView.SetStripLayout"/>).</summary>
        public void SetStripLayout(float badgeOffsetY, bool showCaption)
        {
            _badge.SetOffset(badgeOffsetY);
            if (_label != null && _label.gameObject.activeSelf != showCaption)
            {
                _label.gameObject.SetActive(showCaption);
            }
        }
    }
}
