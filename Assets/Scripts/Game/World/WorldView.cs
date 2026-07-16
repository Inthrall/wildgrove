using System.Collections.Generic;
using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// The world layer: spawns a <see cref="NodeWorldView"/> per gathering node
    /// (plus a <see cref="DigSiteWorldView"/> per unlocked dig site) and lays
    /// them out in the screen gap the HUD leaves open (the HUD reports that gap
    /// via <see cref="StripScreenRect"/> each frame). Also answers the
    /// positional-tend question — which node, if any, is under a tap. Placeholder
    /// tier: shapes in a strip now; a real region scene replaces the layout when
    /// the art lands, but the camera/world seam and hit-testing stay.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class WorldView : MonoBehaviour
    {
        private const float HitSlop = 1.15f;
        // Soft white — the gold accents belong to the Pristine window halo and
        // the bonded-companion marker, so selection reads as its own thing.
        private static readonly Color RingColour = new Color(0.94f, 0.97f, 0.92f, 0.95f);

        /// <summary>The HUD's free gap, in screen pixels — where the node strip lives.</summary>
        public Rect StripScreenRect { get; set; }

        /// <summary>The HUD's selected node, mirrored here so its sprite wears the ring.</summary>
        public NodeState SelectedNode { get; set; }

        private GameLoop _loop;
        private Camera _camera;
        private GameState _builtFor;
        private Transform _container;
        private readonly List<NodeWorldView> _views = new List<NodeWorldView>();
        private readonly List<DigSiteWorldView> _digViews = new List<DigSiteWorldView>();
        private Vector2[] _centres = new Vector2[0];
        private float _radiusPx;

        /// <summary>
        /// The node whose sprite is under the screen point, or null for a miss.
        /// Dig-site sprites share the strip but aren't tendable — a tap on one
        /// resolves as a miss.
        /// </summary>
        public NodeState NodeAtScreenPoint(Vector2 screenPoint)
        {
            var index = WorldStrip.HitIndex(_centres, _radiusPx, screenPoint);
            return index >= 0 && index < _views.Count ? _views[index].Node : null;
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
            // grows mid-run (a trail map unlocked a zone or opened a dig site).
            if (_builtFor != _loop.State
                || _views.Count != _loop.State.nodes.Count
                || _digViews.Count != _loop.State.digSites.Count)
            {
                Rebuild();
            }

            if (_camera == null && !EnsureCamera())
            {
                return;
            }

            Layout();

            foreach (var view in _views)
            {
                var bonded = Bonds.BondedGatherersAt(_loop.State, _loop.Data, view.Node);
                view.Refresh(view.Node == SelectedNode, Time.time, bonded);
            }

            foreach (var digView in _digViews)
            {
                digView.Refresh();
            }
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
            _digViews.Clear();
            _container = new GameObject("WorldNodes").transform;
            _container.SetParent(transform, false);

            foreach (var node in _loop.State.nodes)
            {
                _views.Add(NodeWorldView.Create(
                    _container, node, PlaceholderArt.ResourceColour(node.resourceId), RingColour));
            }

            foreach (var site in _loop.State.digSites)
            {
                _digViews.Add(DigSiteWorldView.Create(
                    _container, site, PlaceholderArt.DigSiteColour(site.zoneId)));
            }

            _builtFor = _loop.State;
        }

        private void Layout()
        {
            // Nodes first, dig sites after — one shared strip, so the hit test's
            // "first N centres are nodes" convention holds.
            var total = _views.Count + _digViews.Count;
            _centres = WorldStrip.LayoutCentres(StripScreenRect, total);
            var diameterPx = WorldStrip.Diameter(StripScreenRect, total);
            _radiusPx = diameterPx * 0.5f * HitSlop;

            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            for (var i = 0; i < _views.Count; i++)
            {
                _views[i].SetPlacement(ScreenToWorld(_centres[i]), diameterPx * worldPerPixel);
            }

            for (var i = 0; i < _digViews.Count; i++)
            {
                _digViews[i].SetPlacement(
                    ScreenToWorld(_centres[_views.Count + i]), diameterPx * worldPerPixel);
            }
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
            if (_camera != null)
            {
                return true;
            }

            // Zero-scene-setup fallback, matching Bootstrap's philosophy: if the
            // open scene has no camera, make a plain orthographic one.
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            return true;
        }
    }
}
