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
        // two. (Three zones ≈ 10 nodes; four zones plus the trail and wander
        // posts ≈ 15.)
        public const int MaxPerRow = 8;

        /// <summary>The assignment badge's centre, in diameters below a post sprite's centre.</summary>
        public const float BadgeOffsetFactor = -0.72f;

        /// <summary>The badge's drawn radius, in diameters (its hit circle is a little forgiving — see <see cref="BadgeHitIndex"/>).</summary>
        public const float BadgeRadiusFactor = 0.22f;

        // Fingers are blunter than the badge is drawn — the hit circle gets
        // slop, like the plates' HitSlop, without colliding into the plate
        // circle above.
        private const float BadgeHitSlop = 1.5f;

        /// <summary>
        /// Index of the post whose assignment badge is under
        /// <paramref name="point"/>, or -1 for a miss. Badges hang
        /// <see cref="BadgeOffsetFactor"/> below each centre; callers check
        /// this BEFORE the plate hit so the badge wins the overlap band.
        /// </summary>
        public static int BadgeHitIndex(Vector2[] centres, float diameter, Vector2 point)
        {
            var offset = new Vector2(0f, BadgeOffsetFactor * diameter);
            var radius = BadgeRadiusFactor * diameter * BadgeHitSlop;
            var best = -1;
            var bestSqr = radius * radius;
            for (var i = 0; i < centres.Length; i++)
            {
                var sqr = (centres[i] + offset - point).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    best = i;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        /// <summary>
        /// Resolve a tap against plates and badges TOGETHER, so a top-row
        /// badge's hit circle can no longer steal a tap meant for the
        /// bottom-row plate it overlaps. Circles differ in size, so ties are
        /// broken by RADIUS-NORMALISED distance — the circle the tap is
        /// proportionally deepest inside wins (a direct badge tap still beats
        /// the plate whose edge it grazes). Badge circles only answer inside
        /// the strip (they hang low enough to spill into the chrome below the
        /// band), and a badge that draws nothing (<paramref name="badgeVisible"/>
        /// false — the vacant post) hits nothing: the plate is the assign
        /// gesture there.
        /// </summary>
        public static int ResolveHit(Rect strip, Vector2[] centres, float plateRadius, float diameter, bool[] badgeVisible, Vector2 point)
        {
            var best = -1;
            var bestDepth = 1f;
            var plateSqr = plateRadius * plateRadius;
            for (var i = 0; i < centres.Length; i++)
            {
                var depth = plateSqr > 0f ? (centres[i] - point).sqrMagnitude / plateSqr : float.MaxValue;
                if (depth <= bestDepth)
                {
                    best = i;
                    bestDepth = depth;
                }
            }

            if (strip.Contains(point))
            {
                var offset = new Vector2(0f, BadgeOffsetFactor * diameter);
                var badgeRadius = BadgeRadiusFactor * diameter * BadgeHitSlop;
                var badgeSqr = badgeRadius * badgeRadius;
                for (var i = 0; i < centres.Length; i++)
                {
                    if (badgeVisible != null && (i >= badgeVisible.Length || !badgeVisible[i]))
                    {
                        continue;
                    }

                    var depth = badgeSqr > 0f ? (centres[i] + offset - point).sqrMagnitude / badgeSqr : float.MaxValue;
                    if (depth <= bestDepth)
                    {
                        best = i;
                        bestDepth = depth;
                    }
                }
            }

            return best;
        }

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

        // A windfall is a moving finger target, so it is sized from the BAND
        // rather than from the node plates: plates shrink every time a zone
        // unlocks (more sprites share the row), and a catch must not get
        // harder as the camp grows. Floored at a full node plate so a nearly
        // empty strip still floats something substantial.
        private const float BubbleBandShare = 0.42f;
        private const float BubbleWidthShare = 0.2f;
        private const float BubbleNodeShare = 1f;

        /// <summary>
        /// A windfall's diameter (same units as the strip): a fixed share of
        /// the band, never narrower than one node plate.
        /// </summary>
        public static float BubbleDiameter(Rect strip, int count)
        {
            var byBand = Mathf.Min(strip.height * BubbleBandShare, strip.width * BubbleWidthShare);
            return Mathf.Max(0f, Mathf.Max(Diameter(strip, count) * BubbleNodeShare, byBand));
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
