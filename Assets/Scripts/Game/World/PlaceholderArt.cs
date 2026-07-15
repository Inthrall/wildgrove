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
