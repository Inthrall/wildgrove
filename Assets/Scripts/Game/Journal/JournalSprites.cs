using UnityEngine;
using static Wildgrove.Game.JournalTheme;

namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's generated art — procedurally built, cached sprites for the
    /// ruled borders, paper grain, and dashed rules. Kept out of the widget
    /// factory so the pixel-poking stays in one place.
    /// </summary>
    internal static class JournalSprites
    {
        private static Sprite _borderSprite;
        private static Sprite _gradeBorderSprite;
        private static Sprite _grainSprite;
        private static Sprite _dashSprite;
        private static Sprite _dashAcrossSprite;
        private static Sprite _quillSprite;
        private static Sprite _crossSprite;
        private static Sprite _plusSprite;
        private static Sprite _foldArrowSprite;
        private static Sprite _rerollSprite;
        private static Sprite _discSprite;

        /// <summary>
        /// A plain filled circle, drawn white so a caller tints it. It backs a
        /// mark laid over a picture — a tile's corner mark, where a crop glyph
        /// dropped straight onto a portrait reads as something the animal is
        /// holding rather than as a mark about it. The world strip's assignment
        /// badge has always sat on a disc for the same reason; this is the
        /// journal's own, so the paper tones come from
        /// <see cref="JournalTheme"/> rather than the world's palette.
        /// </summary>
        internal static Sprite DiscSprite()
        {
            if (_discSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var centre = (size - 1) * 0.5f;
                var radius = size * 0.5f - 1f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        // A one-pixel feather at the rim: a hard-edged circle at
                        // this size renders as a cogwheel against paper.
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                        var alpha = Mathf.Clamp01(radius - distance);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _discSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _discSprite;
        }

        /// <summary>
        /// The fold arrow — a pen-drawn chevron beside a ground's name, down
        /// when the ground is open and turned a quarter to the right when it is
        /// shut. Drawn rather than typed for the same reason as the cross and
        /// the quill: none of the journal's four faces carries a triangle or an
        /// arrow, so a "▾" would render as a missing glyph on the one heading
        /// whose whole job is to say which way it opens.
        /// <para>
        /// Points DOWN as drawn; callers rotate it +90° on the Z axis for shut.
        /// </para>
        /// </summary>
        internal static Sprite FoldArrowSprite()
        {
            if (_foldArrowSprite == null)
            {
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                // Two strokes meeting at the point, each thickest where the nib
                // starts and tapering into the join — the same hand as the
                // cross, so the mark belongs to the page rather than an icon set.
                var point = new Vector2(size * 0.5f, 9f);
                var strokes = new[]
                {
                    (from: new Vector2(8f, 22f), to: point),
                    (from: new Vector2(size - 8f, 22f), to: point),
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var pixel = new Vector2(x + 0.5f, y + 0.5f);
                        var alpha = 0f;
                        foreach (var stroke in strokes)
                        {
                            var along = stroke.to - stroke.from;
                            var t = Mathf.Clamp01(Vector2.Dot(pixel - stroke.from, along) / along.sqrMagnitude);
                            var distance = Vector2.Distance(pixel, stroke.from + (along * t));
                            var halfWidth = Mathf.Lerp(1.7f, 1.0f, t);
                            alpha = Mathf.Max(alpha, Mathf.Clamp01(halfWidth - distance + 0.5f));
                        }

                        texture.SetPixel(x, y, alpha <= 0f
                            ? Color.clear
                            : new Color(Ink2.r, Ink2.g, Ink2.b, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _foldArrowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _foldArrowSprite;
        }

        /// <summary>
        /// Two circular arrows chasing each other: the re-deal, on the
        /// caravan's consideration button. The turn is the whole message a
        /// reroll needs, and every game a player has met says it with this
        /// mark, so the exchange card spends a glyph on it instead of the
        /// sentence it used to.
        /// <para>
        /// Drawn white, like <see cref="DiscSprite"/> and unlike the other
        /// glyphs here: the button dims with the rest of its plate when the
        /// amber isn't there, and a baked ink can't be walked back to Ink2.
        /// </para>
        /// </summary>
        internal static Sprite RerollSprite()
        {
            if (_rerollSprite == null)
            {
                const int size = 64;
                const float radius = 18f;
                // A little over a third of the turn each, which leaves the two
                // gaps the arrowheads stand in; a pair of arcs any closer
                // reads as one broken circle.
                const float sweep = 140f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var centre = new Vector2(size * 0.5f, size * 0.5f);
                var starts = new[] { 170f, -10f };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var offset = point - centre;
                        var fromCentre = offset.magnitude;
                        var bearing = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                        var alpha = 0f;
                        foreach (var start in starts)
                        {
                            // Both arcs run clockwise, which is falling bearing,
                            // so how far along its arc a pixel lies is simply the
                            // turn from that arc's start.
                            var travelled = Mathf.Repeat(start - bearing, 360f);
                            if (travelled <= sweep)
                            {
                                // Light where the stroke sets out and heaviest
                                // where it runs into the head, the same nib the
                                // quill and the cross are drawn with.
                                var halfWidth = Mathf.Lerp(1.5f, 2.6f, travelled / sweep);
                                alpha = Mathf.Max(alpha,
                                    Mathf.Clamp01(halfWidth - Mathf.Abs(fromCentre - radius) + 0.5f));
                            }

                            // The head at the arc's end: a filled triangle, not
                            // two strokes. A tapered chevron this small closed up
                            // into a smudge, and an arrow that doesn't read as an
                            // arrow leaves the mark as a plain broken ring.
                            var head = (start - sweep) * Mathf.Deg2Rad;
                            var outward = new Vector2(Mathf.Cos(head), Mathf.Sin(head));
                            var onward = new Vector2(outward.y, -outward.x);
                            var seat = centre + (outward * radius);
                            // The base sits a pixel and a half back down the arc
                            // rather than on the seat: flush, the stroke's last
                            // pixel and the head's first met at a half-covered
                            // seam that read as a nick in the arrow.
                            var seam = seat - (onward * 1.5f);
                            alpha = Mathf.Max(alpha, TriangleAlpha(point,
                                seat + (onward * 8f),
                                seam + (outward * 5.2f),
                                seam - (outward * 5.2f)));
                        }

                        texture.SetPixel(x, y, alpha <= 0f ? Color.clear : new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _rerollSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _rerollSprite;
        }

        /// <summary>How solidly a pixel sits inside a triangle, with the half-pixel feather every mark here is drawn with.</summary>
        private static float TriangleAlpha(Vector2 point, Vector2 first, Vector2 second, Vector2 third)
        {
            var inside = Mathf.Min(InsideEdge(point, first, second, third),
                Mathf.Min(InsideEdge(point, second, third, first), InsideEdge(point, third, first, second)));
            return Mathf.Clamp01(inside + 0.5f);
        }

        /// <summary>
        /// How far a point lies on the inner side of one edge, taking the side
        /// the triangle's third corner sits on as the inside, so the caller
        /// need not name its corners in any particular winding.
        /// </summary>
        private static float InsideEdge(Vector2 point, Vector2 from, Vector2 to, Vector2 opposite)
        {
            var along = to - from;
            var normal = new Vector2(-along.y, along.x).normalized;
            if (Vector2.Dot(opposite - from, normal) < 0f)
            {
                normal = -normal;
            }

            return Vector2.Dot(point - from, normal);
        }

        /// <summary>
        /// A cross — the sheets' close affordance, in the corner where a hand
        /// expects it. Two tapering strokes rather than even bars: the same
        /// pen-drawn logic as the quill, so it reads as written in the journal
        /// and not pasted in from an icon set (there is none to draw from).
        /// </summary>
        internal static Sprite CrossSprite()
        {
            if (_crossSprite == null)
            {
                const int size = 32;
                const float margin = 8.5f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var strokes = new[]
                {
                    (from: new Vector2(margin, margin), to: new Vector2(size - margin, size - margin)),
                    (from: new Vector2(margin, size - margin), to: new Vector2(size - margin, margin)),
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var alpha = 0f;
                        foreach (var stroke in strokes)
                        {
                            var along = stroke.to - stroke.from;
                            var t = Mathf.Clamp01(Vector2.Dot(point - stroke.from, along) / along.sqrMagnitude);
                            var distance = Vector2.Distance(point, stroke.from + (along * t));
                            // Thickest at the middle, tapering to both ends —
                            // a stroke a nib would leave crossing itself.
                            var halfWidth = Mathf.Lerp(0.9f, 1.9f, 1f - Mathf.Abs((t * 2f) - 1f));
                            alpha = Mathf.Max(alpha, Mathf.Clamp01(halfWidth - distance + 0.5f));
                        }

                        texture.SetPixel(x, y, alpha <= 0f
                            ? Color.clear
                            : new Color(Ink.r, Ink.g, Ink.b, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _crossSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _crossSprite;
        }

        /// <summary>
        /// The invitation mark — the moss (+) the strip's empty slot and the
        /// warden's own plate at camp wear, drawn here in ink for the journal's
        /// picture buttons so an unheld post says "someone could stand here" in
        /// the one glyph the game already uses for it.
        /// <para>
        /// Drawn rather than typed, unlike the recruit bar's "+": a picture
        /// button carries a Sprite, and a plate that switched between a Text and
        /// an Image would have two channels to keep in step. Moss is baked in
        /// (every other mark in this file bakes its ink) — it is the grove's
        /// invitation colour, never ochre, which belongs to costs and halts.
        /// </para>
        /// <para>
        /// Twice the cross's texture: the post plates wear this at 84 units,
        /// where a 32-pixel glyph goes soft.
        /// </para>
        /// </summary>
        internal static Sprite PlusSprite()
        {
            if (_plusSprite == null)
            {
                const int size = 64;
                const float margin = 14f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var strokes = new[]
                {
                    (from: new Vector2(margin, size * 0.5f), to: new Vector2(size - margin, size * 0.5f)),
                    (from: new Vector2(size * 0.5f, margin), to: new Vector2(size * 0.5f, size - margin)),
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var alpha = 0f;
                        foreach (var stroke in strokes)
                        {
                            var along = stroke.to - stroke.from;
                            var t = Mathf.Clamp01(Vector2.Dot(point - stroke.from, along) / along.sqrMagnitude);
                            var distance = Vector2.Distance(point, stroke.from + (along * t));
                            // Thickest where the strokes cross, tapering to all
                            // four ends — the same nib as the cross, at the same
                            // relative weight.
                            var halfWidth = Mathf.Lerp(1.8f, 3.8f, 1f - Mathf.Abs((t * 2f) - 1f));
                            alpha = Mathf.Max(alpha, Mathf.Clamp01(halfWidth - distance + 0.5f));
                        }

                        texture.SetPixel(x, y, alpha <= 0f
                            ? Color.clear
                            : new Color(MossDeep.r, MossDeep.g, MossDeep.b, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _plusSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _plusSprite;
        }

        /// <summary>
        /// A quill — the rename affordance beside a familiar's name. Drawn as a
        /// stroke that tapers to a nib, because a bar of even width reads as a
        /// slash and a nib is what says "you may write here". Code-drawn like
        /// every other glyph in this file; there is no icon set to draw from,
        /// and the journal's own fonts carry no pen character.
        /// </summary>
        internal static Sprite QuillSprite()
        {
            if (_quillSprite == null)
            {
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                // Nib at the lower left, feather end at the upper right — the
                // angle a right-handed pen rests at, which is what makes the
                // shape legible at a glyph's size.
                var nib = new Vector2(6.5f, 6.5f);
                var feather = new Vector2(24.5f, 24.5f);
                var along = feather - nib;
                var lengthSq = along.sqrMagnitude;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var t = Mathf.Clamp01(Vector2.Dot(point - nib, along) / lengthSq);
                        var distance = Vector2.Distance(point, nib + (along * t));
                        var halfWidth = Mathf.Lerp(0.55f, 3.1f, t);
                        // Alpha from the distance rather than a hard cut: the
                        // stroke is diagonal, and stair-stepped edges look like
                        // a mistake next to type this soft.
                        var alpha = Mathf.Clamp01(halfWidth - distance + 0.5f);
                        texture.SetPixel(x, y, alpha <= 0f
                            ? Color.clear
                            : new Color(Ink.r, Ink.g, Ink.b, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _quillSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _quillSprite;
        }

        /// <summary>A sliced border-only sprite — the journal's ruled ink outlines.</summary>
        internal static Sprite BorderSprite()
        {
            if (_borderSprite == null)
            {
                const int size = 8;
                const int thickness = 2;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var edge = x < thickness || y < thickness || x >= size - thickness || y >= size - thickness;
                        texture.SetPixel(x, y, edge ? Color.white : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _borderSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
            }

            return _borderSprite;
        }

        /// <summary>
        /// The Stores drawer's grade rule — the same ruled outline as
        /// <see cref="BorderSprite"/>, drawn eight times thicker. The card
        /// rule is a 2-unit hairline, which is right for a border that only
        /// has to separate two sheets of paper and wrong for one that has to
        /// carry meaning: at hairline weight the chalk grade was invisible
        /// against the tile and the moss one read as a drawing error.
        /// </summary>
        internal static Sprite GradeBorderSprite()
        {
            if (_gradeBorderSprite == null)
            {
                const int size = 24;
                const int thickness = 8;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var edge = x < thickness || y < thickness || x >= size - thickness || y >= size - thickness;
                        texture.SetPixel(x, y, edge ? Color.white : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _gradeBorderSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(thickness, thickness, thickness, thickness));
            }

            return _gradeBorderSprite;
        }

        /// <summary>The page's paper-grain noise — the mock's fractal-noise overlay, seeded for a stable look.</summary>
        internal static Sprite GrainSprite()
        {
            if (_grainSprite == null)
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var rng = new System.Random(1897);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var alpha = rng.NextDouble() < 0.5 ? 0f : (float)rng.NextDouble() * 0.08f;
                        texture.SetPixel(x, y, new Color(Ink.r, Ink.g, Ink.b, alpha));
                    }
                }

                texture.Apply();
                _grainSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            return _grainSprite;
        }

        /// <summary>A vertical dash pattern — the trail row's dotted rule.</summary>
        internal static Sprite DashSprite()
        {
            if (_dashSprite == null)
            {
                const int width = 4;
                const int height = 24;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        texture.SetPixel(x, y, y < height / 2 ? new Color(Ink2.r, Ink2.g, Ink2.b, 0.45f) : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _dashSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            return _dashSprite;
        }

        /// <summary>A horizontal dash pattern — the dashed outlines' top and bottom rules.</summary>
        internal static Sprite DashAcrossSprite()
        {
            if (_dashAcrossSprite == null)
            {
                const int width = 24;
                const int height = 4;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        texture.SetPixel(x, y, x < width / 2 ? new Color(Ink2.r, Ink2.g, Ink2.b, 0.45f) : Color.clear);
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _dashAcrossSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            return _dashAcrossSprite;
        }
    }
}
