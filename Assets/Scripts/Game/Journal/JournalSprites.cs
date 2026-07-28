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
        private static Sprite _grainSprite;
        private static Sprite _dashSprite;
        private static Sprite _dashAcrossSprite;
        private static Sprite _quillSprite;

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
