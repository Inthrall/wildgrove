using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Game.World;

namespace Wildgrove.Game
{
    /// <summary>
    /// A drift of dandelion seed up the face of a sheet — the journal's whole
    /// vocabulary for celebrating. It borrows the windfall's own seed (see
    /// <see cref="PlaceholderArt.Pappus"/>) so a good moment always looks like
    /// the same thing: something the wind has taken up.
    ///
    /// A burst, not a loop. It settles after one pass and takes itself away —
    /// a sheet that asks the player to type a name must not keep moving behind
    /// the field while they do it.
    /// </summary>
    internal sealed class Seedfall : MonoBehaviour
    {
        private const float LifeSeconds = 2.4f;
        private const float StaggerSeconds = 0.9f;
        private const float SeedPixels = 30f;

        private RectTransform[] _seeds;
        private Image[] _images;
        private float[] _delay;
        private float[] _swayAmplitude;
        private float[] _swayPhase;
        private float[] _spin;
        private float _elapsed;
        private float _last;
        private Color _tint;

        /// <summary>
        /// Sow a burst across the sheet <paramref name="content"/> belongs to.
        /// Parented to the paper panel rather than the scrolling content, and
        /// drawn first — so the seed passes over the page and under the words,
        /// and scrolling the sheet doesn't drag the drift with it.
        /// </summary>
        internal static void Sow(Transform content, int count, Color tint)
        {
            var panel = content != null && content.parent != null ? content.parent.parent : null;
            if (panel == null || count <= 0)
            {
                return;
            }

            var host = new GameObject("Seedfall", typeof(RectTransform), typeof(RectMask2D), typeof(Seedfall));
            host.transform.SetParent(panel, false);

            var rect = (RectTransform)host.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // The panel lays its children out in a column; this one is weather,
            // not a row, so it opts out and covers the whole card.
            host.AddComponent<LayoutElement>().ignoreLayout = true;
            host.transform.SetAsFirstSibling();

            host.GetComponent<Seedfall>().Build(count, tint);
        }

        private void Build(int count, Color tint)
        {
            _tint = tint;
            _seeds = new RectTransform[count];
            _images = new Image[count];
            _delay = new float[count];
            _swayAmplitude = new float[count];
            _swayPhase = new float[count];
            _spin = new float[count];

            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Seed", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);

                var image = go.GetComponent<Image>();
                image.sprite = PlaceholderArt.Pappus;
                image.color = new Color(tint.r, tint.g, tint.b, 0f);
                // Nothing on this layer is touchable — the sheet's own buttons
                // sit above it and must keep every tap that lands on them.
                image.raycastTarget = false;

                var rect = (RectTransform)go.transform;
                // Spread across the width, kept off both margins.
                var across = Mathf.Lerp(0.08f, 0.92f, count == 1 ? 0.5f : i / (count - 1f))
                             + Random.Range(-0.04f, 0.04f);
                rect.anchorMin = new Vector2(across, 0f);
                rect.anchorMax = new Vector2(across, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var size = SeedPixels * Random.Range(0.7f, 1.25f);
                rect.sizeDelta = new Vector2(size, size);

                _seeds[i] = rect;
                _images[i] = image;
                // Staggered, or the burst reads as one line of seed rising in
                // formation rather than as a drift.
                _delay[i] = Random.Range(0f, StaggerSeconds);
                _swayAmplitude[i] = Random.Range(14f, 42f);
                _swayPhase[i] = Random.Range(0f, Mathf.PI * 2f);
                _spin[i] = Random.Range(-140f, 140f);
                _last = Mathf.Max(_last, _delay[i]);
            }
        }

        private void Update()
        {
            // Unscaled: a sheet is up while the grove ticks on, and a celebration
            // must not slow down or speed up with anything the sim is doing.
            _elapsed += Time.unscaledDeltaTime;

            var height = ((RectTransform)transform).rect.height;
            for (var i = 0; i < _seeds.Length; i++)
            {
                var t = (_elapsed - _delay[i]) / LifeSeconds;
                if (t <= 0f || t >= 1f)
                {
                    _images[i].color = new Color(_tint.r, _tint.g, _tint.b, 0f);
                    continue;
                }

                var sway = Mathf.Sin(t * 4f + _swayPhase[i]) * _swayAmplitude[i];
                _seeds[i].anchoredPosition = new Vector2(sway, Mathf.Lerp(-SeedPixels, height + SeedPixels, t));
                _seeds[i].localRotation = Quaternion.Euler(0f, 0f, _spin[i] * t);

                // In quickly, out slowly, and never at full strength: this is
                // paper the player is reading through.
                var fade = Mathf.Min(t / 0.12f, (1f - t) / 0.35f);
                _images[i].color = new Color(_tint.r, _tint.g, _tint.b, Mathf.Clamp01(fade) * _tint.a);
            }

            if (_elapsed > _last + LifeSeconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
