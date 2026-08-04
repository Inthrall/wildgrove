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
        // The fewest plates a row ever carries before wrapping to two — the
        // phone band's answer, and the floor under PerRowCap. Three is
        // deliberately early there: that band is wide but shallow, and a row of
        // four already sizes each plate off the width rather than the height,
        // so the specimens shrink instead of filling the band. Two rows of
        // three keep the plates big enough to read as portraits.
        // (Three zones ≈ 10 nodes; four zones plus the open-slots mark ≈ 15.)
        public const int MaxPerRow = 3;

        // How much of the band a row of plates is allowed to use: 0.6 of the
        // row's height, and 0.7 of each plate's share of the width. Diameter
        // sizes by these, so PerRowCap must reason with the same two numbers or
        // the wrap and the sizing disagree.
        private const float RowHeightShare = 0.6f;
        private const float RowWidthShare = 0.7f;

        // The captioned plate, measured in diameters, top to bottom: half a
        // diameter of plate above its centre, then the caption hanging under it
        // — PlaceholderArt.CreateLabel drops the label 1.02 diameters below the
        // centre and its glyphs run about 0.3 further. (The assignment badge
        // hangs in that same space, shallower, so the caption is the deepest
        // thing a plate carries.)
        private const float CaptionDrop = 1.02f;
        private const float CaptionGlyphs = 0.3f;

        /// <summary>
        /// A captioned plate's full height in diameters — what a single row
        /// actually occupies, as against the disc alone.
        /// </summary>
        public const float CaptionedPlate = 0.5f + CaptionDrop + CaptionGlyphs;

        /// <summary>
        /// How many plates a single row of THIS band can carry: as many as the
        /// width can seat at the size the band's height already allows.
        /// <para>
        /// <see cref="MaxPerRow"/> alone was a portrait answer to a question
        /// about the band's shape, and on a spread it read as one: a landscape
        /// band is twice as broad and — because height is what landscape is
        /// short of — barely half as deep, so five plates that would sit
        /// comfortably in one row wrapped into two rows of specks in a band
        /// with room to spare either side. Never fewer than
        /// <see cref="MaxPerRow"/>, so a column's band wraps exactly where it
        /// always did.
        /// </para>
        /// </summary>
        public static int PerRowCap(Rect strip)
        {
            if (strip.width <= 0f || strip.height <= 0f)
            {
                return MaxPerRow;
            }

            // Solving width / (n + 1) * RowWidthShare >= height / CaptionedPlate
            // for n: the largest row that is still sized by the band's height.
            // (The single row's height budget — the same one Diameter uses.)
            var byShape = Mathf.FloorToInt(strip.width * RowWidthShare * CaptionedPlate / strip.height) - 1;
            return Mathf.Max(MaxPerRow, byShape);
        }

        /// <summary>The assignment badge's centre, in diameters below a post sprite's centre (single-row; two rows clamp to the row pitch — see <see cref="BadgeOffset"/>).</summary>
        public const float BadgeOffsetFactor = -0.72f;

        /// <summary>
        /// The badge's vertical offset in strip units. In two-row layout the
        /// full -0.72 diameters would hang a top-row badge over the bottom
        /// row's plates, so the drop is clamped to a share of the row pitch.
        /// </summary>
        public static float BadgeOffset(Rect strip, int count, float diameter)
        {
            var drop = -BadgeOffsetFactor * diameter;
            if (Rows(strip, count) == 2)
            {
                var pitch = strip.height * (0.68f - 0.32f);
                drop = Mathf.Min(drop, pitch * 0.4f);
            }

            return -drop;
        }

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
                var offset = new Vector2(0f, BadgeOffset(strip, centres.Length, diameter));
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

        /// <summary>Rows the strip lays out in — one up to <see cref="PerRowCap"/>, then two.</summary>
        public static int Rows(Rect strip, int count)
        {
            return count <= PerRowCap(strip) ? 1 : 2;
        }

        /// <summary>
        /// True while the plates name themselves. Only in a single row: in two
        /// the captions would hang over the row beneath. <see cref="WorldView"/>
        /// draws them, and the single-row sizing below reserves their space.
        /// </summary>
        public static bool ShowsCaptions(Rect strip, int count)
        {
            return Rows(strip, count) == 1;
        }

        /// <summary>
        /// How far above the band's middle a single row of plates rides, so that
        /// the plate AND its hanging caption sit inside the band rather than
        /// spilling out of the bottom of it.
        /// <para>
        /// The captions always hung below the band — which was harmless while a
        /// pinned bar sat under the strip to hang into, and stopped being
        /// harmless when that bar moved onto the page: on a spread the herbs
        /// plate captioned itself straight across the facing page's running
        /// head. Centring the captioned plate rather than the disc puts the
        /// whole thing back inside the band it belongs to.
        /// </para>
        /// </summary>
        public static float CaptionLift(float diameter)
        {
            return diameter * (CaptionedPlate * 0.5f - 0.5f);
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
            if (Rows(strip, count) == 1)
            {
                // Lifted so the caption below each plate lands inside the band.
                var y = strip.center.y + CaptionLift(Diameter(strip, count));
                for (var i = 0; i < centres.Length; i++)
                {
                    var x = strip.xMin + strip.width * (i + 1) / (count + 1);
                    centres[i] = new Vector2(x, y);
                }

                return;
            }

            // Split down the middle, the odd one out riding on top: four goes
            // 2/2 rather than filling the top row to its cap and stranding a
            // single plate underneath, and seven — which cannot go 3/3 — goes
            // 4/3.
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

            var rows = Rows(strip, count);
            var perRow = (count + rows - 1) / rows;
            // A single row carries captions, so its height budget is the whole
            // captioned plate, not the disc — otherwise the disc fills the band
            // and the caption has to hang out of it.
            var byHeight = rows == 1
                ? strip.height / CaptionedPlate
                : strip.height / rows * RowHeightShare;
            var byWidth = strip.width / (perRow + 1) * RowWidthShare;
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
