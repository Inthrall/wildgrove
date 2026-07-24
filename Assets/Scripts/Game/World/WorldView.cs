using System.Collections.Generic;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// The world layer: spawns a <see cref="NodeWorldView"/> per gathering node
    /// plus a <see cref="StationWorldView"/> for the wander post (the trail post
    /// moved to the HUD's trail-home line in the band above the strip),
    /// and lays them out in the screen gap the HUD leaves open (the HUD reports
    /// that gap via <see cref="StripScreenRect"/> each frame). The strip IS the
    /// assignment board (design §2: one body per post): every post wears a
    /// badge of its holder, and this class answers both tap questions — which
    /// node a tap tends, and which post's badge a tap assigns. Placeholder
    /// tier: shapes in a strip now; a real region scene replaces the layout
    /// when the art lands, but the camera/world seam and hit-testing stay.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class WorldView : MonoBehaviour
    {
        private const float HitSlop = 1.15f;

        // Bubbles are smaller than the plates (~45% of a node) — a blunter
        // finger circle keeps the catch satisfying rather than fiddly.
        private const float BubbleHitSlop = 1.6f;
        // Soft white — the gold accents belong to the Pristine window halo and
        // the bonded pip on a badge, so selection reads as its own thing.
        private static readonly Color RingColour = new Color(0.94f, 0.97f, 0.92f, 0.95f);

        /// <summary>The HUD's free gap, in screen pixels — where the node strip lives.</summary>
        public Rect StripScreenRect { get; set; }

        /// <summary>The HUD's selected node, mirrored here so its sprite wears the ring.</summary>
        public NodeState SelectedNode { get; set; }

        private GameLoop _loop;
        private Camera _camera;
        private GameState _builtFor;
        private Transform _container;
        private Font _labelFont;
        private readonly List<NodeWorldView> _views = new List<NodeWorldView>();
        private readonly List<BubbleWorldView> _bubbles = new List<BubbleWorldView>();
        private StationWorldView _wanderView;
        private Vector2[] _centres = new Vector2[0];
        private float _radiusPx;
        private float _diameterPx;
        private float _nextBubbleAt;
        private int _bubbleCursor;

        private void OnEnable()
        {
            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        private void OnDisable()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
        }

        // The labels share the HUD's dynamic fonts; an atlas rebuild (a new
        // glyph/size requested anywhere) leaves stale TextMesh geometry unless
        // each label re-generates.
        private void OnFontTextureRebuilt(Font font)
        {
            if (font != _labelFont)
            {
                return;
            }

            foreach (var view in _views)
            {
                view.RefreshLabel();
            }

            _wanderView?.RefreshLabel();
        }

        /// <summary>
        /// The node whose plate is under the screen point, or null for a miss —
        /// the tend hit. The trail/wander posts share the strip but aren't
        /// tendable, and a badge tap resolves through
        /// <see cref="StationAtScreenPoint"/> instead.
        /// </summary>
        public NodeState NodeAtScreenPoint(Vector2 screenPoint)
        {
            var index = WorldStrip.HitIndex(_centres, _radiusPx, screenPoint);
            return index >= 0 && index < _views.Count ? _views[index].Node : null;
        }

        /// <summary>
        /// The station whose assignment affordance is under the screen point,
        /// or null: any post's badge, or the trail/wander plates themselves
        /// (no tend there — the whole sprite is the assign gesture). Callers
        /// check this BEFORE the tend hit so badges win the overlap band.
        /// </summary>
        public string StationAtScreenPoint(Vector2 screenPoint)
        {
            var badge = WorldStrip.BadgeHitIndex(_centres, _diameterPx, screenPoint);
            if (badge >= 0)
            {
                return StationIdAt(badge);
            }

            var plate = WorldStrip.HitIndex(_centres, _radiusPx, screenPoint);
            return plate >= _views.Count ? StationIdAt(plate) : null;
        }

        private string StationIdAt(int index)
        {
            if (index < 0 || index >= _centres.Length)
            {
                return null;
            }

            if (index < _views.Count)
            {
                return _views[index].Node.id;
            }

            return Familiar.WanderStation;
        }

        private void LateUpdate()
        {
            if (_loop == null)
            {
                _loop = GetComponent<GameLoop>();
            }

            if (_loop == null || _loop.State == null)
            {
                return;
            }

            // Rebuild on a new run/state object, and when the strip's population
            // grows mid-run (a trail map unlocked a zone's nodes).
            if (_builtFor != _loop.State || _views.Count != _loop.State.nodes.Count)
            {
                Rebuild();
            }

            if (_camera == null && !EnsureCamera())
            {
                return;
            }

            Layout();

            var state = _loop.State;
            var postNodeId = Warden.PostNodeId(state);
            foreach (var view in _views)
            {
                var occupant = Stationing.OccupantOf(state, view.Node.id);
                view.Refresh(view.Node == SelectedNode, Time.time,
                    view.Node.id == postNodeId, occupant, IconFor(occupant));
            }

            var wanderer = Stationing.OccupantOf(state, Familiar.WanderStation);
            _wanderView.Refresh(Warden.IsWandering(state), wanderer, IconFor(wanderer));

            UpdateBubbles(state);
        }

        // ─────────────────────── Windfall bubbles ────────────────────────
        // A worked node drifts a bubble up the strip now and then; catching
        // it pays a burst of that node's goods (Sim.Bubbles). All ephemeral —
        // spawn timing, float path and expiry live here, nothing persists.

        /// <summary>
        /// The bubble under the screen point, removed and returned as its
        /// node (what the catch pays out in), or null for a miss. Checked
        /// before every other strip hit — a bubble floats over the plates.
        /// </summary>
        public NodeState PopBubbleAt(Vector2 screenPoint)
        {
            for (var i = 0; i < _bubbles.Count; i++)
            {
                var bubble = _bubbles[i];
                var radius = bubble.ScreenRadius * BubbleHitSlop;
                if ((bubble.ScreenPosition - screenPoint).sqrMagnitude <= radius * radius)
                {
                    return TakeBubble(i);
                }
            }

            return null;
        }

        /// <summary>The longest-adrift bubble, removed and returned as its node — the keyboard/gamepad catch. Null when none float.</summary>
        public NodeState PopOldestBubble()
        {
            return _bubbles.Count > 0 ? TakeBubble(0) : null;
        }

        private NodeState TakeBubble(int index)
        {
            var bubble = _bubbles[index];
            _bubbles.RemoveAt(index);
            Destroy(bubble.gameObject);
            return bubble.Node;
        }

        private void UpdateBubbles(GameState state)
        {
            var config = _loop.Data?.economy?.bubbles;
            if (config == null || !Bubbles.Configured(_loop.Data))
            {
                return;
            }

            var now = Time.time;

            // Expired bubbles drift off the top and go.
            for (var i = _bubbles.Count - 1; i >= 0; i--)
            {
                if (now - _bubbles[i].SpawnTime >= config.lifetimeSec)
                {
                    Destroy(_bubbles[i].gameObject);
                    _bubbles.RemoveAt(i);
                }
            }

            MaybeSpawnBubble(state, config, now);

            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            var bubbleDiameterPx = _diameterPx * 0.45f;
            foreach (var bubble in _bubbles)
            {
                var age = now - bubble.SpawnTime;
                var progress = Mathf.Clamp01(age / (float)config.lifetimeSec);
                var home = NodeCentre(bubble.Node);

                // Rise from the node toward the strip's top edge, with a
                // gentle per-bubble wobble; fade out over the last stretch.
                var y = Mathf.Lerp(home.y, StripScreenRect.yMax - bubbleDiameterPx * 0.5f, progress);
                var x = home.x + Mathf.Sin(age * 1.5f + bubble.Seed) * bubbleDiameterPx * 0.6f;
                var screen = new Vector2(x, y);
                var fade = progress > 0.8f ? Mathf.InverseLerp(1f, 0.8f, progress) : 1f;

                bubble.SetPlacement(ScreenToWorld(screen), bubbleDiameterPx * worldPerPixel,
                    screen, bubbleDiameterPx * 0.5f, fade);
            }
        }

        private void MaybeSpawnBubble(GameState state, EconomyData.BubblesData config, float now)
        {
            if (_nextBubbleAt <= 0f)
            {
                // First look at a fresh strip — let the player settle in for
                // half an interval before the first windfall drifts up.
                _nextBubbleAt = now + (float)config.spawnIntervalSec * 0.5f;
                return;
            }

            if (now < _nextBubbleAt || _bubbles.Count >= config.maxLive)
            {
                return;
            }

            // Round-robin over the nodes so every worked post gets its turn.
            for (var step = 0; step < _views.Count; step++)
            {
                var index = (_bubbleCursor + step) % _views.Count;
                var node = _views[index].Node;
                if (!Bubbles.IsEligible(state, _loop.Data, node))
                {
                    continue;
                }

                _bubbleCursor = index + 1;
                _bubbles.Add(BubbleWorldView.Create(_container, node,
                    PlaceholderArt.ResourceColour(node.resourceId), now, index * 2.1f));
                _nextBubbleAt = now + (float)config.spawnIntervalSec;
                return;
            }

            // Nothing worked anywhere — look again shortly rather than
            // banking a full interval against an empty camp.
            _nextBubbleAt = now + 2f;
        }

        private Vector2 NodeCentre(NodeState node)
        {
            for (var i = 0; i < _views.Count && i < _centres.Length; i++)
            {
                if (_views[i].Node == node)
                {
                    return _centres[i];
                }
            }

            return StripScreenRect.center;
        }

        private static Sprite IconFor(Familiar familiar)
        {
            return familiar != null ? ArtLibrary.ForSpecies(familiar.speciesId) : null;
        }

        private void Rebuild()
        {
            // A recompile during Play reloads the app domain: our list resets but
            // the spawned children survive — clear them before rebuilding, same
            // as the HUD's stale-canvas teardown.
            var stale = transform.Find("WorldNodes");
            if (stale != null)
            {
                Destroy(stale.gameObject);
            }

            _views.Clear();
            // Any bubbles adrift were children of the torn-down container.
            _bubbles.Clear();
            _container = new GameObject("WorldNodes").transform;
            _container.SetParent(transform, false);

            if (_labelFont == null)
            {
                // The HUD's chrome face; the built-in face when running bare.
                var font = Resources.Load<Font>("Fonts/IMFellEnglishSC");
                _labelFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            foreach (var node in _loop.State.nodes)
            {
                _views.Add(NodeWorldView.Create(
                    _container, node, PlaceholderArt.ResourceColour(node.resourceId), RingColour, _labelFont,
                    ArtLibrary.ForResource(node.resourceId)));
            }

            // The wander post closes the strip (roaming every node and watch
            // site). The trail post moved to the HUD's trail-home line.
            _wanderView = StationWorldView.Create(
                _container, Familiar.WanderStation, "wandering",
                PlaceholderArt.DigSiteColour(Familiar.WanderStation), _labelFont,
                ArtLibrary.ForSkill("observation"));

            _builtFor = _loop.State;
        }

        private void Layout()
        {
            // Nodes first, the wander post after — one shared strip, so the hit
            // test's "first N centres are nodes" convention holds. The buffer is
            // reused frame to frame — this runs per LateUpdate.
            var total = _views.Count + 1;
            if (_centres.Length != total)
            {
                _centres = new Vector2[total];
            }

            WorldStrip.LayoutCentresInto(StripScreenRect, total, _centres);
            _diameterPx = WorldStrip.Diameter(StripScreenRect, total);
            _radiusPx = _diameterPx * 0.5f * HitSlop;

            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            for (var i = 0; i < _views.Count; i++)
            {
                _views[i].SetPlacement(ScreenToWorld(_centres[i]), _diameterPx * worldPerPixel);
            }

            _wanderView.SetPlacement(ScreenToWorld(_centres[_views.Count]), _diameterPx * worldPerPixel);
        }

        private Vector3 ScreenToWorld(Vector2 screenPoint)
        {
            var world = _camera.ScreenToWorldPoint(
                new Vector3(screenPoint.x, screenPoint.y, -_camera.transform.position.z));
            world.z = 0f;
            return world;
        }

        private bool EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                // Zero-scene-setup fallback, matching Bootstrap's philosophy: if the
                // open scene has no camera, make a plain orthographic one.
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.orthographicSize = 5f;
                _camera.transform.position = new Vector3(0f, 0f, -10f);
            }

            // The camera's clear colour IS the journal's page paper (#F2EAD6,
            // GameHud.PagePaper) — the HUD deliberately has no backdrop image,
            // so the world strip shows through the gap it leaves open.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.949f, 0.918f, 0.839f, 1f);
            return true;
        }
    }
}
