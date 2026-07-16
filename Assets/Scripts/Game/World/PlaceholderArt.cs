using UnityEngine;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// Runtime-generated programmer art for the world layer, so Phase 1 ships
    /// zero image assets: one anti-aliased white disc sprite (tinted per use)
    /// and a deterministic muted colour per resource id. Replaced wholesale by
    /// the hand-drawn naturalist plates when the art direction lands.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int DiscTexSize = 128;

        private static Sprite _disc;
        private static Sprite _diamond;
        private static Sprite _triangle;

        /// <summary>A white disc, 1 world unit across at scale 1 — tint via SpriteRenderer.color.</summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc == null)
                {
                    _disc = CreateDisc();
                }

                return _disc;
            }
        }

        /// <summary>A white diamond (rotated square) — the bonded-companion marker shape.</summary>
        public static Sprite Diamond
        {
            get
            {
                if (_diamond == null)
                {
                    _diamond = CreateDiamond();
                }

                return _diamond;
            }
        }

        /// <summary>A white upward triangle — the dig-site shape (a spoil heap).</summary>
        public static Sprite Triangle
        {
            get
            {
                if (_triangle == null)
                {
                    _triangle = CreateTriangle();
                }

                return _triangle;
            }
        }

        /// <summary>
        /// A stable, muted colour for a resource id — same id, same colour,
        /// every run — so the placeholder nodes are tellable apart without any
        /// authored palette data.
        /// </summary>
        public static Color ResourceColour(string id)
        {
            var hue = Fnv1A(id) % 360u / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.8f);
        }

        /// <summary>
        /// A stable, earthier colour for a dig site's zone — lower saturation
        /// and value than the node palette so turned soil reads as ground, not
        /// another gatherable.
        /// </summary>
        public static Color DigSiteColour(string zoneId)
        {
            var hue = Fnv1A(zoneId) % 360u / 360f;
            return Color.HSVToRGB(hue, 0.28f, 0.6f);
        }

        private static Sprite CreateDisc()
        {
            var texture = new Texture2D(DiscTexSize, DiscTexSize, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderDisc",
                hideFlags = HideFlags.HideAndDontSave,
            };

            var centre = (DiscTexSize - 1) * 0.5f;
            var radius = DiscTexSize * 0.5f - 1f;
            var pixels = new Color32[DiscTexSize * DiscTexSize];
            for (var y = 0; y < DiscTexSize; y++)
            {
                for (var x = 0; x < DiscTexSize; x++)
                {
                    var distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    var alpha = Mathf.Clamp01(radius - distance); // one-pixel anti-aliased edge
                    pixels[y * DiscTexSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, DiscTexSize, DiscTexSize),
                new Vector2(0.5f, 0.5f), DiscTexSize);
        }

        private static Sprite CreateDiamond()
        {
            // |dx| + |dy| ≤ radius is a diamond; the clamp gives the same
            // one-pixel anti-aliased edge as the disc.
            return CreateShape("PlaceholderDiamond", (x, y, centre, radius) =>
                radius - (Mathf.Abs(x - centre) + Mathf.Abs(y - centre)));
        }

        private static Sprite CreateTriangle()
        {
            return CreateShape("PlaceholderTriangle", (x, y, centre, radius) =>
            {
                // An upward triangle: apex at the top, base near the bottom.
                // Alpha is the distance inside the nearest edge (negative
                // outside), giving the anti-aliased rim.
                var apex = new Vector2(centre, centre + radius);
                var left = new Vector2(centre - radius * 0.95f, centre - radius * 0.75f);
                var right = new Vector2(centre + radius * 0.95f, centre - radius * 0.75f);
                var point = new Vector2(x, y);
                return Mathf.Min(
                    InnerEdgeDistance(point, apex, left),
                    Mathf.Min(
                        InnerEdgeDistance(point, left, right),
                        InnerEdgeDistance(point, right, apex)));
            });
        }

        /// <summary>Distance of <paramref name="point"/> inside the edge a→b (negative when outside), for counter-clockwise wound shapes.</summary>
        private static float InnerEdgeDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            var toPoint = point - a;
            return (edge.x * toPoint.y - edge.y * toPoint.x) / edge.magnitude;
        }

        private static Sprite CreateShape(string name, System.Func<float, float, float, float, float> innerDistance)
        {
            var texture = new Texture2D(DiscTexSize, DiscTexSize, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var centre = (DiscTexSize - 1) * 0.5f;
            var radius = DiscTexSize * 0.5f - 1f;
            var pixels = new Color32[DiscTexSize * DiscTexSize];
            for (var y = 0; y < DiscTexSize; y++)
            {
                for (var x = 0; x < DiscTexSize; x++)
                {
                    var alpha = Mathf.Clamp01(innerDistance(x, y, centre, radius));
                    pixels[y * DiscTexSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, DiscTexSize, DiscTexSize),
                new Vector2(0.5f, 0.5f), DiscTexSize);
        }

        private static uint Fnv1A(string text)
        {
            // string.GetHashCode isn't guaranteed stable across runtimes; FNV-1a is.
            var hash = 2166136261u;
            foreach (var c in text ?? string.Empty)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }
    }
}
