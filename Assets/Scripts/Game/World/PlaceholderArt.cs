using UnityEngine;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// Runtime-generated programmer art for the world layer: one anti-aliased
    /// white disc sprite (tinted per use) and a deterministic muted colour per
    /// resource id.
    ///
    /// It was written to be replaced wholesale by the naturalist plates, and
    /// wasn't. The plates landed and every node draws one via
    /// <see cref="ArtLibrary.ForResource"/>, but this stayed as three things
    /// they don't cover: the per-resource tint the plates are mounted against,
    /// the label builder, and the fallback for an id with no plate.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int DiscTexSize = 128;
        private const int PappusTexSize = 64;

        // A clock's hairs. Enough that the rim reads as down rather than as a
        // wheel, few enough that they stay separate at a windfall's size.
        private const int SeedheadHairs = 34;
        private const int PappusHairs = 9;

        private static Sprite _disc;
        private static Sprite _diamond;
        private static Sprite _triangle;
        private static Sprite _seedhead;
        private static Sprite _pappus;

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
        /// A dandelion clock — a seeded receptacle with pappus hairs radiating
        /// out to a rim of tufts. The windfall's mount: what drifts through the
        /// grove is a seedhead carrying a cutting, and what it leaves behind
        /// when it's caught is seed.
        /// </summary>
        public static Sprite Seedhead
        {
            get
            {
                if (_seedhead == null)
                {
                    _seedhead = CreateSeedhead();
                }

                return _seedhead;
            }
        }

        /// <summary>
        /// One dandelion seed — the fruit, its stalk, and the little parachute
        /// above it. Scattered from a caught windfall, and drifted up the paper
        /// behind the journal's celebrations.
        /// </summary>
        public static Sprite Pappus
        {
            get
            {
                if (_pappus == null)
                {
                    _pappus = CreatePappus();
                }

                return _pappus;
            }
        }

        /// <summary>
        /// A small world-space caption under a placeholder sprite — it ties
        /// the strip's anonymous shapes to the journal plates naming the same
        /// resource, and doubles as the "this is a thing" tap affordance.
        /// Sized in the parent's local units, so it scales with the sprite.
        /// </summary>
        public static TextMesh CreateLabel(Transform parent, string text, Font font, Color colour)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -1.02f, 0f);
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.font = font;
            label.fontSize = 64;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center;
            label.color = colour;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.material = font.material;
            renderer.sortingOrder = 4;
            return label;
        }

        /// <summary>
        /// Re-run a label's mesh generation. Dynamic font atlases rebuild when
        /// new glyphs arrive (the HUD shares these fonts at many sizes), and a
        /// TextMesh caught out by a rebuild renders garbage until poked.
        /// </summary>
        public static void RefreshLabel(TextMesh label)
        {
            if (label == null)
            {
                return;
            }

            var text = label.text;
            label.text = string.Empty;
            label.text = text;
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

        private static Sprite CreateSeedhead()
        {
            var texture = new Texture2D(DiscTexSize, DiscTexSize, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderSeedhead",
                hideFlags = HideFlags.HideAndDontSave,
            };

            var centre = (DiscTexSize - 1) * 0.5f;
            var radius = DiscTexSize * 0.5f - 1f;
            var coreRadius = radius * 0.13f;
            var tipRadius = radius * 0.86f;
            var step = Mathf.PI * 2f / SeedheadHairs;

            var pixels = new Color32[DiscTexSize * DiscTexSize];
            for (var y = 0; y < DiscTexSize; y++)
            {
                for (var x = 0; x < DiscTexSize; x++)
                {
                    var dx = x - centre;
                    var dy = y - centre;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // The receptacle the seeds are set into — the one part of
                    // the head that isn't hair.
                    var alpha = Mathf.Clamp01(coreRadius - distance) * 0.75f;

                    if (distance > 0.5f && distance <= radius)
                    {
                        // Two rings, the inner one shorter and offset into the
                        // gaps of the outer. One ring alone draws a wheel; the
                        // second fills it in without closing it to a disc.
                        alpha = Mathf.Max(alpha, SeedheadRay(dx, dy, distance,
                            0f, tipRadius, 0.5f, 0.4f, radius * 0.085f));
                        alpha = Mathf.Max(alpha, SeedheadRay(dx, dy, distance,
                            step * 0.5f, tipRadius * 0.62f, 0.45f, 0.28f, radius * 0.07f));
                    }

                    pixels[y * DiscTexSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, DiscTexSize, DiscTexSize),
                new Vector2(0.5f, 0.5f), DiscTexSize);
        }

        /// <summary>
        /// One ring of the clock's hair, at the pixel <paramref name="dx"/>,
        /// <paramref name="dy"/> from the middle: a shaft out to
        /// <paramref name="length"/> and a soft tuft at the end of it. Measured
        /// ACROSS the nearest hair rather than as an angle to it, so a hair
        /// keeps one width the whole way out instead of fanning into a wedge.
        /// </summary>
        private static float SeedheadRay(float dx, float dy, float distance, float phase,
            float length, float width, float ink, float tuftRadius)
        {
            var step = Mathf.PI * 2f / SeedheadHairs;
            var offset = (Mathf.Atan2(dy, dx) - phase) / step;
            var nearest = Mathf.Round(offset);
            var across = Mathf.Abs(offset - nearest) * step * distance;

            var alpha = distance <= length ? Mathf.Clamp01(width - across) * ink : 0f;

            // Feathered, not a bead on a spoke: a clock's rim is down.
            var tipAngle = nearest * step + phase;
            var toTipX = dx - Mathf.Cos(tipAngle) * length;
            var toTipY = dy - Mathf.Sin(tipAngle) * length;
            var toTip = Mathf.Sqrt(toTipX * toTipX + toTipY * toTipY);
            return Mathf.Max(alpha, Mathf.Clamp01(1f - toTip / tuftRadius) * ink * 0.9f);
        }

        private static Sprite CreatePappus()
        {
            var texture = new Texture2D(PappusTexSize, PappusTexSize, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderPappus",
                hideFlags = HideFlags.HideAndDontSave,
            };

            // Drawn upright — the fruit low, the parachute above it — so a
            // seed's rotation is its own and reads as tumbling.
            var midX = (PappusTexSize - 1) * 0.5f;
            var fruit = new Vector2(midX, PappusTexSize * 0.13f);
            var crown = new Vector2(midX, PappusTexSize * 0.55f);
            var fruitRadius = PappusTexSize * 0.06f;
            var hairLength = PappusTexSize * 0.4f;

            var pixels = new Color32[PappusTexSize * PappusTexSize];
            for (var y = 0; y < PappusTexSize; y++)
            {
                for (var x = 0; x < PappusTexSize; x++)
                {
                    var point = new Vector2(x, y);
                    var alpha = Mathf.Clamp01(fruitRadius - Vector2.Distance(point, fruit));
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1f - SegmentDistance(point, fruit, crown)) * 0.9f);

                    for (var i = 0; i < PappusHairs; i++)
                    {
                        // Splayed evenly either side of straight up.
                        var spread = Mathf.Lerp(-1.15f, 1.15f, i / (PappusHairs - 1f));
                        var end = crown + new Vector2(Mathf.Sin(spread), Mathf.Cos(spread)) * hairLength;
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(0.9f - SegmentDistance(point, crown, end)) * 0.85f);
                    }

                    pixels[y * PappusTexSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, PappusTexSize, PappusTexSize),
                new Vector2(0.5f, 0.5f), PappusTexSize);
        }

        /// <summary>Shortest distance from <paramref name="point"/> to the segment a→b.</summary>
        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            var lengthSquared = edge.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, a);
            }

            var along = Mathf.Clamp01(Vector2.Dot(point - a, edge) / lengthSquared);
            return Vector2.Distance(point, a + edge * along);
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
