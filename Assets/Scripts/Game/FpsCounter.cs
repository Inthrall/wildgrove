using UnityEngine;
using UnityEngine.UI;

namespace Wildgrove.Game
{
    /// <summary>
    /// A small always-on framerate readout in the top-right corner: average
    /// fps over the last half second, plus the worst single frame in that
    /// window (the number that exposes hitches an average hides). It lives on
    /// its own canvas so its own text updates never dirty the HUD canvas —
    /// a profiler readout must not become the thing it measures.
    /// </summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        private const float SampleWindow = 0.5f;

        private Text _label;
        private int _frames;
        private float _elapsed;
        private float _worstFrame;
        private string _lastShown = string.Empty;

        private void Update()
        {
            // Built lazily and rebuilt after a Play-mode recompile, the same
            // way GameHud recovers (non-serialised fields reset on reload).
            if (_label == null)
            {
                BuildUi();
            }

            var dt = Time.unscaledDeltaTime;
            _frames++;
            _elapsed += dt;
            if (dt > _worstFrame)
            {
                _worstFrame = dt;
            }

            if (_elapsed < SampleWindow)
            {
                return;
            }

            var fps = Mathf.RoundToInt(_frames / _elapsed);
            var worstMs = Mathf.RoundToInt(_worstFrame * 1000f);
            // The version leads so "which build am I actually running" is
            // never a question a Play-internal rollout can leave open.
            var shown = "v" + Application.version + "  " + fps + " fps  " + worstMs + " ms";
            if (shown != _lastShown)
            {
                _label.text = shown;
                _lastShown = shown;
            }

            _frames = 0;
            _elapsed = 0f;
            _worstFrame = 0f;
        }

        private void BuildUi()
        {
            var stale = transform.Find("FpsCanvas");
            if (stale != null)
            {
                Destroy(stale.gameObject);
            }

            var canvasGo = new GameObject("FpsCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD and its modal sheets — a diagnostic never hides.
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvasGo.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, -8f);
            rect.sizeDelta = new Vector2(320f, 40f);

            _label = go.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 26;
            _label.alignment = TextAnchor.UpperRight;
            _label.color = new Color(0.95f, 0.95f, 0.9f, 0.75f);
            _label.raycastTarget = false;
            _lastShown = string.Empty;
        }
    }
}
