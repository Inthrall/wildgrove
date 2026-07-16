using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One dig site's world-space sprite: an earth-toned triangle (the spoil
    /// heap) with a small dot per digger arced beneath it, dimmed while no
    /// familiar turns soil. Purely visual — dig sites aren't tended, so taps
    /// on one fall through as a miss. Placement and per-frame refresh are
    /// driven by <see cref="WorldView"/>.
    /// </summary>
    public sealed class DigSiteWorldView : MonoBehaviour
    {
        private const float IdleAlpha = 0.55f;
        private const int MaxDiggerDots = 8;
        private const float DotScale = 0.14f;
        private const float ArcY = -0.72f;
        private const float ArcHalfWidth = 0.45f;

        public DigSiteState Site { get; private set; }

        private SpriteRenderer _heap;
        private SpriteRenderer[] _diggerDots;
        private Color _colour;

        public static DigSiteWorldView Create(Transform parent, DigSiteState site, Color colour)
        {
            var go = new GameObject("DigSite_" + site.zoneId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DigSiteWorldView>();
            view.Site = site;
            view._colour = colour;

            var heapGo = new GameObject("Heap", typeof(SpriteRenderer));
            heapGo.transform.SetParent(go.transform, false);
            view._heap = heapGo.GetComponent<SpriteRenderer>();
            view._heap.sprite = PlaceholderArt.Triangle;
            view._heap.color = colour;
            view._heap.sortingOrder = 2;

            var dotColour = Color.Lerp(colour, Color.black, 0.4f);
            view._diggerDots = new SpriteRenderer[MaxDiggerDots];
            for (var i = 0; i < MaxDiggerDots; i++)
            {
                var dotGo = new GameObject("Digger_" + i, typeof(SpriteRenderer));
                dotGo.transform.SetParent(go.transform, false);
                dotGo.transform.localScale = Vector3.one * DotScale;
                var dot = dotGo.GetComponent<SpriteRenderer>();
                dot.sprite = PlaceholderArt.Disc;
                dot.color = dotColour;
                dot.sortingOrder = 3;
                dot.enabled = false;
                view._diggerDots[i] = dot;
            }

            return view;
        }

        public void SetPlacement(Vector3 worldPosition, float worldDiameter)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * worldDiameter;
        }

        public void Refresh()
        {
            var colour = _colour;
            colour.a = Site.familiarCount > 0 ? 1f : IdleAlpha;
            _heap.color = colour;

            var dots = Mathf.Min(Site.familiarCount, MaxDiggerDots);
            for (var i = 0; i < _diggerDots.Length; i++)
            {
                _diggerDots[i].enabled = i < dots;
                if (i < dots)
                {
                    var t = dots == 1 ? 0.5f : i / (float)(dots - 1);
                    _diggerDots[i].transform.localPosition = new Vector3(
                        Mathf.Lerp(-ArcHalfWidth, ArcHalfWidth, t), ArcY, 0f);
                }
            }
        }
    }
}
