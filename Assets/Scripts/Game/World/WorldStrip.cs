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
        // Beyond this many sprites a single row turns into confetti — wrap to
        // two. (Three zones ≈ 10 nodes; four zones plus dig sites ≈ 15.)
        public const int MaxPerRow = 8;

        /// <summary>Rows the strip lays out in — one until crowded, then two.</summary>
        public static int Rows(int count)
        {
            return count <= MaxPerRow ? 1 : 2;
        }

        /// <summary>
        /// Node centres spread evenly along the strip's horizontal middle —
        /// splitting into two bands once a single row would be too crowded.
        /// </summary>
        public static Vector2[] LayoutCentres(Rect strip, int count)
        {
            var centres = new Vector2[Mathf.Max(0, count)];
            LayoutCentresInto(strip, count, centres);
            return centres;
        }

        /// <summary>
        /// Allocation-free variant for the per-frame caller: writes into
        /// <paramref name="centres"/> (sized to <paramref name="count"/>).
        /// </summary>
        public static void LayoutCentresInto(Rect strip, int count, Vector2[] centres)
        {
            if (Rows(count) == 1)
            {
                for (var i = 0; i < centres.Length; i++)
                {
                    var x = strip.xMin + strip.width * (i + 1) / (count + 1);
                    centres[i] = new Vector2(x, strip.center.y);
                }

                return;
            }

            var topCount = (count + 1) / 2;
            var bottomCount = count - topCount;
            var topY = strip.yMin + strip.height * 0.68f;
            var bottomY = strip.yMin + strip.height * 0.32f;
            for (var i = 0; i < topCount; i++)
            {
                var x = strip.xMin + strip.width * (i + 1) / (topCount + 1);
                centres[i] = new Vector2(x, topY);
            }

            for (var i = 0; i < bottomCount; i++)
            {
                var x = strip.xMin + strip.width * (i + 1) / (bottomCount + 1);
                centres[topCount + i] = new Vector2(x, bottomY);
            }
        }

        /// <summary>
        /// Sprite diameter (same units as the strip): as big as fits the
        /// per-row height and an even horizontal spread, never negative.
        /// </summary>
        public static float Diameter(Rect strip, int count)
        {
            if (count <= 0)
            {
                return 0f;
            }

            var rows = Rows(count);
            var perRow = (count + rows - 1) / rows;
            var byHeight = strip.height / rows * 0.6f;
            var byWidth = strip.width / (perRow + 1) * 0.7f;
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
