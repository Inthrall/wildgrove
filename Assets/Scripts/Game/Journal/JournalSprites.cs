using UnityEngine;
using static Wildgrove.Game.JournalTheme;

namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's generated art — procedurally built, cached sprites for the
    /// ruled borders, paper grain, stitched spine, and dashed rules. Kept out of
    /// the widget factory so the pixel-poking stays in one place.
    /// </summary>
    internal static class JournalSprites
    {
        private static Sprite _borderSprite;
        private static Sprite _grainSprite;
        private static Sprite _dashSprite;
        private static Sprite _dashAcrossSprite;
        private static Sprite _spineSprite;

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

        /// <summary>
        /// The stitched spine's thread — a soft-edged vertical stitch, faded
        /// at the ends and sides so the tiled column reads as sewn thread
        /// rather than aliased blocks. Separate from <see cref="DashSprite"/>:
        /// the trail row's rule samples that tile's bottom rows and would
        /// change appearance if this softening were applied there.
        /// </summary>
        internal static Sprite SpineSprite()
        {
            if (_spineSprite == null)
            {
                const int width = 6;
                const int height = 28;
                const int stitch = 14; // thread length; the rest of the tile is gap
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var along = Mathf.Clamp01(Mathf.Min(y + 1, stitch - y) / 2f);
                        var across = Mathf.Clamp01(Mathf.Min(x + 1, width - x) / 2f);
                        var alpha = y < stitch ? 0.5f * along * across : 0f;
                        texture.SetPixel(x, y, new Color(Ink2.r, Ink2.g, Ink2.b, alpha));
                    }
                }

                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _spineSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            }

            return _spineSprite;
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
