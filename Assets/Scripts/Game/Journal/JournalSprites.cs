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
