using UnityEngine;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// Pure layout and hit-test maths for the node strip — the horizontal band
    /// of node sprites shown in the gap the HUD leaves open. Everything is in
    /// screen pixels: the HUD reports the gap as a screen rect, taps arrive as
    /// screen points, and <see cref="WorldView"/> converts to world space only
    /// when placing the sprites. Kept free of scene state so the maths is
    /// EditMode-testable.
    /// </summary>
    public static class WorldStrip
    {
        /// <summary>Node centres spread evenly along the strip's horizontal middle.</summary>
        public static Vector2[] LayoutCentres(Rect strip, int count)
        {
            var centres = new Vector2[Mathf.Max(0, count)];
            for (var i = 0; i < centres.Length; i++)
            {
                var x = strip.xMin + strip.width * (i + 1) / (count + 1);
                centres[i] = new Vector2(x, strip.center.y);
            }

            return centres;
        }

        /// <summary>
        /// Sprite diameter (same units as the strip): as big as fits both the
        /// strip's height and an even horizontal spread, never negative.
        /// </summary>
        public static float Diameter(Rect strip, int count)
        {
            if (count <= 0)
            {
                return 0f;
            }

            var byHeight = strip.height * 0.6f;
            var byWidth = strip.width / (count + 1) * 0.7f;
            return Mathf.Max(0f, Mathf.Min(byHeight, byWidth));
        }

        /// <summary>Index of the centre nearest <paramref name="point"/> within <paramref name="radius"/>, or -1 for a miss.</summary>
        public static int HitIndex(Vector2[] centres, float radius, Vector2 point)
        {
            var best = -1;
            var bestSqr = radius * radius;
            for (var i = 0; i < centres.Length; i++)
            {
                var sqr = (centres[i] - point).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    best = i;
                    bestSqr = sqr;
                }
            }

            return best;
        }
    }
}
