using System.Collections.Generic;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>What a tap on the strip landed on — <see cref="WorldView.TapAtScreenPoint"/>'s answer.</summary>
    public enum StripTap
    {
        /// <summary>Nothing: the tap fell between the plates.</summary>
        Miss,

        /// <summary>A post's plate or badge — the post's id comes back with it.</summary>
        Post,

        /// <summary>The (+) closing the strip: an unfilled kith slot, not a post.</summary>
        OpenSlots,
    }

    /// <summary>
    /// The world layer: spawns a <see cref="NodeWorldView"/> per gathering node
    /// and lays them out in the screen gap the HUD leaves open (the HUD reports
    /// that gap via <see cref="StripScreenRect"/> each frame). The strip IS the
    /// assignment board (design §2: one body per post): every post wears a
    /// badge of its holder, and this class answers both tap questions — which
    /// node a tap tends, and which post's badge a tap assigns. While any kith
    /// slot stands unfilled a (+) mark closes the strip; tapping it opens the
    /// picker — a ground, then a body. The wander post keeps no plate of its own
    /// here — it would wear a node's face without being one — and is assigned
    /// from the watch card, the pickers and the familiars' own sheets.
    /// Placeholder tier: shapes in a strip for now; a real region scene replaces
    /// the layout when the art lands, but the camera/world seam and hit-testing
    /// stay.
    ///
    /// Only the posts with a body on them are drawn. The band is on screen at
    /// every tab, so drawing them all spends the page's whole height on plates
    /// nobody is working — fifteen by the fourth zone, and the two the warden
    /// and the kith actually stand at lost among them. A view is built for every
    /// node regardless (<see cref="_views"/>); <see cref="_onStrip"/> is the
    /// subset laid out this frame, and posting or resting a body moves a plate
    /// in or out without a rebuild. Fallow nodes are not stranded: every one of
    /// them keeps its own card on the Trail page, wearing the same (+) this
    /// strip does until somebody stands there.
    ///
    /// Everything on the strip is a GROUND. The warden had a plate of their own
    /// at the head of it for a day (2026-08-05, removed 2026-08-06): a body
    /// among grounds read as a node you could gather from, captioned with a name
    /// where every neighbour carried a crop. What that plate was reaching for —
    /// the player's own body reading first — is now simply the order:
    /// <see cref="GatherStrip"/> leads with the ground the warden stands on. A
    /// warden at camp holds no ground and so shows nowhere here, which is what
    /// "at camp" means; walking them somewhere is asked at the post itself (the
    /// posting sheet's own warden row) or through the (+) picker.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class WorldView : MonoBehaviour
    {
        private const float HitSlop = 1.15f;

        // The strip's shared centre order: the plates on the strip, then the (+)
        // mark when it shows. One buffer serves the layout and the hit test, so
        // both read the order from here.
        private const int FirstNodeCentre = 0;

        // A windfall carries the resource's plate, so it needs enough room to
        // be recognised as that specimen — WorldStrip.BubbleDiameter sizes it
        // off the band; the finger circle is a little blunter still so the
        // catch isn't fiddly. It must also clear the seedhead the plate rides
        // on (BubbleWorldView.MountFit), or the outer tufts would be drawn
        // outside anything a tap can land on.
        private const float BubbleHitSlop = 1.4f;

        /// <summary>The HUD's free gap, in screen pixels — where the node strip lives.</summary>
        public Rect StripScreenRect { get; set; }

        /// <summary>
        /// True while a sheet covers the strip — windfalls stop aging and
        /// spawning so a modal can't silently burn their lifetime. Purely
        /// presentation state, so freezing it is free.
        /// </summary>
        public bool Frozen { get; set; }

        /// <summary>
        /// True until the player's first-ever catch — new windfalls carry a
        /// "tap to catch" tag and beckon, because nothing else marks a slowly
        /// turning plate as the game's most valuable tap target.
        /// </summary>
        public bool CatchHintPending { get; set; }

        private GameLoop _loop;
        private Camera _camera;
        private GameState _builtFor;
        private Transform _container;
        private Font _labelFont;
        // Every node's view, in the land's own order — built once per state.
        private readonly List<NodeWorldView> _views = new List<NodeWorldView>();
        // The subset actually on the strip this frame, in the same order.
        private readonly List<NodeWorldView> _onStrip = new List<NodeWorldView>();
        private readonly List<BubbleWorldView> _bubbles = new List<BubbleWorldView>();
        // Caught windfalls play out a short burst before being destroyed —
        // out of _bubbles, so they can't be caught twice or block a spawn.
        private readonly List<BubbleWorldView> _bursting = new List<BubbleWorldView>();
        private BubbleWorldView _lastCaught;
        // The (+) closing the strip while a kith slot stands unfilled — not a
        // post, an invitation to fill one (the tap opens the ground picker).
        private TextMesh _openSlotsMark;
        private bool _openSlotsShown;
        private Vector2[] _centres = new Vector2[0];
        private bool[] _badgeVisible = new bool[0];
        private float _radiusPx;
        private float _diameterPx;
        private float _nextBubbleAt;
        private int _bubbleCursor;
        private bool _wasFrozen;
        private float _frozenAt;

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
        //
        // Every reach here is guarded because this runs on a static event with
        // other subscribers behind it — uGUI's own FontUpdateTracker, which is
        // how every Text in the journal hears about the same rebuild. A throw
        // from this handler stops the rest of that invocation list, so one
        // unguarded field would leave the whole HUD drawing through the atlas
        // it just replaced. _openSlotsMark is null until the first Rebuild, and
        // the rebuild that Rebuild's own labels provoke is exactly the one that
        // arrives while it still is.
        private void OnFontTextureRebuilt(Font font)
        {
            if (font != _labelFont)
            {
                return;
            }

            foreach (var view in _views)
            {
                if (view != null)
                {
                    view.RefreshLabel();
                }
            }

            PlaceholderArt.RefreshLabel(_openSlotsMark);
        }

        /// <summary>
        /// What the screen point landed on. Plates and badges resolve together —
        /// nearest centre wins (see <see cref="WorldStrip.ResolveHit"/>) — and
        /// <paramref name="postId"/> carries the post for a
        /// <see cref="StripTap.Post"/>, null otherwise. Every plate here is a
        /// ground, the warden's own included: tapping the ground they stand on
        /// asks the same question as tapping any other post ("who walks here?").
        /// </summary>
        public StripTap TapAtScreenPoint(Vector2 screenPoint, out string postId)
        {
            postId = null;
            var index = WorldStrip.ResolveHit(StripScreenRect, _centres, _radiusPx, _diameterPx, _badgeVisible, screenPoint);
            if (index < 0 || index >= _centres.Length)
            {
                return StripTap.Miss;
            }

            var nodeIndex = index - FirstNodeCentre;
            if (nodeIndex < _onStrip.Count)
            {
                postId = _onStrip[nodeIndex].Node.id;
                return StripTap.Post;
            }

            return StripTap.OpenSlots;
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

            var state = _loop.State;
            var postNodeId = Warden.PostNodeId(state);

            // Which plates the strip carries has to be settled before the
            // layout maths runs — the spread, the plate size and the row count
            // are all read off the count.
            GatherStrip(state);
            _openSlotsShown = _loop.KithWalking() < _loop.KithSlots();
            if (_openSlotsMark.gameObject.activeSelf != _openSlotsShown)
            {
                _openSlotsMark.gameObject.SetActive(_openSlotsShown);
            }

            Layout();

            // A fresh camp with nothing posted anywhere would otherwise render
            // the whole strip at idle-dim — reading as "disabled" exactly when
            // the first tap must happen. Dim only once dim can mean something.
            var anyPosted = postNodeId != null || Warden.IsWandering(state);
            if (!anyPosted)
            {
                foreach (var familiar in state.roster)
                {
                    if (!familiar.IsResting)
                    {
                        anyPosted = true;
                        break;
                    }
                }
            }

            if (_badgeVisible.Length != _centres.Length)
            {
                _badgeVisible = new bool[_centres.Length];
            }

            for (var i = 0; i < _onStrip.Count; i++)
            {
                var view = _onStrip[i];
                var occupant = Stationing.OccupantOf(state, view.Node.id);
                var wardenHere = view.Node.id == postNodeId;
                // A vacant badge draws nothing, so it must hit nothing. Only
                // the empty-camp fallback puts a vacant plate on the strip at
                // all; every other frame each of these is someone's post.
                _badgeVisible[FirstNodeCentre + i] = wardenHere || occupant != null;
                view.Refresh(wardenHere, occupant, IconFor(occupant), anyPosted);
            }

            // The (+) is not a post either, so it wears no badge — its plate
            // circle is the whole tap target.
            if (_badgeVisible.Length > FirstNodeCentre + _onStrip.Count)
            {
                _badgeVisible[FirstNodeCentre + _onStrip.Count] = false;
            }

            UpdateBubbles(state);
        }

        /// <summary>
        /// Choose the plates the strip carries this frame — the posts with a
        /// body standing on them — and switch the rest off.
        ///
        /// "A body is here" rather than "this ground earns": a wandering body
        /// pays a share into every node at once, so a yield test would put the
        /// whole land back on the strip the moment anyone roams (see
        /// <see cref="Stationing.HasBodyAt"/>).
        /// </summary>
        private void GatherStrip(GameState state)
        {
            _onStrip.Clear();
            foreach (var view in _views)
            {
                if (Stationing.HasBodyAt(state, view.Node.id))
                {
                    _onStrip.Add(view);
                }
            }

            // A camp with no node held keeps the whole board — a wanderer-only
            // camp included, since the wander post has no plate of its own.
            // Hiding every plate would take the assignment surface away at
            // exactly the moment the first posting has to happen. The strip
            // collapses to the worked posts on the first node posting.
            if (_onStrip.Count == 0)
            {
                _onStrip.AddRange(_views);
            }

            // _onStrip still holds _views' own order here, so one cursor walks
            // both. The warden's ground is moved to the front AFTER this, or the
            // two lists would fall out of step and switch the wrong plates off.
            var cursor = 0;
            foreach (var view in _views)
            {
                var shown = cursor < _onStrip.Count && _onStrip[cursor] == view;
                if (shown)
                {
                    cursor++;
                }

                if (view.gameObject.activeSelf != shown)
                {
                    view.gameObject.SetActive(shown);
                }
            }

            LeadWithTheWarden(state);
        }

        /// <summary>
        /// Put the ground the warden stands on first. The player's own body
        /// reads at the head of the board, without being a plate of its own —
        /// the strip stays a row of grounds, and which one is theirs is said by
        /// the badge under it, as it is for every companion. A warden at camp or
        /// wandering holds no ground, so the order is the land's own.
        /// </summary>
        private void LeadWithTheWarden(GameState state)
        {
            var postNodeId = Warden.PostNodeId(state);
            if (postNodeId == null)
            {
                return;
            }

            for (var i = 1; i < _onStrip.Count; i++)
            {
                if (_onStrip[i].Node.id != postNodeId)
                {
                    continue;
                }

                var wardens = _onStrip[i];
                _onStrip.RemoveAt(i);
                _onStrip.Insert(0, wardens);
                return;
            }
        }

        /// <summary>True while <paramref name="node"/>'s plate is one of the strip's.</summary>
        private bool OnStrip(NodeState node)
        {
            foreach (var view in _onStrip)
            {
                if (view.Node == node)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// True when the tap was a near miss on a live windfall — within twice
        /// its catch circle but outside it. The caller swallows the tap (a
        /// whiff must not open the posting sheet underneath) and the windfall
        /// shivers so the miss reads as a miss, not a dead tap.
        /// </summary>
        public bool NudgeNearMiss(Vector2 screenPoint)
        {
            const float nearMissFactor = 2f;
            for (var i = 0; i < _bubbles.Count; i++)
            {
                var bubble = _bubbles[i];
                var radius = bubble.ScreenRadius * nearMissFactor;
                if ((bubble.ScreenPosition - screenPoint).sqrMagnitude <= radius * radius)
                {
                    bubble.Nudge(Time.time);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Style the just-caught windfall's send-off once the sim has decided
        /// what it paid: a golden burst with a rising "+N" for a real catch, a
        /// grey deflate for an empty one — feedback AT the finger, not only in
        /// the journal margin.
        /// </summary>
        public void ResolveCatch(string text, bool rewarded)
        {
            if (_lastCaught == null)
            {
                return;
            }

            _lastCaught.BeginBurst(Time.time, rewarded);
            SpawnCatchText(_lastCaught.ScreenPosition, text, rewarded);
            _lastCaught = null;
        }

        private NodeState TakeBubble(int index)
        {
            var bubble = _bubbles[index];
            _bubbles.RemoveAt(index);
            // Not destroyed yet — it plays out a short burst (ResolveCatch
            // styles it) and UpdateBubbles retires it when the burst ends.
            bubble.BeginBurst(Time.time, true);
            _bursting.Add(bubble);
            _lastCaught = bubble;
            return bubble.Node;
        }

        private void SpawnCatchText(Vector2 screen, string text, bool rewarded)
        {
            if (_labelFont == null || _container == null)
            {
                return;
            }

            var label = PlaceholderArt.CreateLabel(_container, text, _labelFont,
                rewarded ? new Color(0.333f, 0.392f, 0.247f, 1f) : new Color(0.431f, 0.376f, 0.278f, 1f));
            label.anchor = TextAnchor.MiddleCenter;
            label.transform.position = ScreenToWorld(screen);
            // CreateLabel sizes for a diameter-scaled parent; this one hangs
            // off the unscaled container, so size it to the strip instead.
            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            var diameter = WorldStrip.BubbleDiameter(StripScreenRect, StripTotal()) * worldPerPixel;
            label.characterSize = diameter * 0.055f;
            label.GetComponent<MeshRenderer>().sortingOrder = 9;
            StartCoroutine(RiseAndFadeLabel(label, diameter));
        }

        private System.Collections.IEnumerator RiseAndFadeLabel(TextMesh label, float diameter)
        {
            const float life = 1.1f;
            var start = label.transform.position;
            var colour = label.color;
            for (var age = 0f; age < life; age += Time.deltaTime)
            {
                if (label == null)
                {
                    yield break;
                }

                var t = age / life;
                label.transform.position = start + new Vector3(0f, diameter * 0.6f * t, 0f);
                label.color = new Color(colour.r, colour.g, colour.b, 1f - t * t);
                yield return null;
            }

            if (label != null)
            {
                Destroy(label.gameObject);
            }
        }

        private void UpdateBubbles(GameState state)
        {
            var config = _loop.Data?.economy?.bubbles;
            if (config == null || !Bubbles.Configured(_loop.Data))
            {
                return;
            }

            var now = Time.time;

            // Under a sheet nothing ages, spawns, or expires; on the way back
            // every timestamp shifts by the pause so the drift resumes exactly
            // where the sheet interrupted it.
            if (Frozen)
            {
                if (!_wasFrozen)
                {
                    _wasFrozen = true;
                    _frozenAt = now;
                }

                return;
            }

            if (_wasFrozen)
            {
                _wasFrozen = false;
                var pause = now - _frozenAt;
                foreach (var bubble in _bubbles)
                {
                    bubble.ShiftTime(pause);
                }

                foreach (var bubble in _bursting)
                {
                    bubble.ShiftTime(pause);
                }

                if (_nextBubbleAt > 0f)
                {
                    _nextBubbleAt += pause;
                }
            }

            // Caught windfalls play out their burst, then go.
            for (var i = _bursting.Count - 1; i >= 0; i--)
            {
                if (!_bursting[i].AnimateBurst(now))
                {
                    Destroy(_bursting[i].gameObject);
                    _bursting.RemoveAt(i);
                }
            }

            // Expired bubbles drift off the top and go — and so do any whose
            // post has since lost its body: the plate they rise from is off
            // the strip, so there is nowhere left for them to rise from.
            for (var i = _bubbles.Count - 1; i >= 0; i--)
            {
                if (now - _bubbles[i].SpawnTime >= config.lifetimeSec || !OnStrip(_bubbles[i].Node))
                {
                    Destroy(_bubbles[i].gameObject);
                    _bubbles.RemoveAt(i);
                }
            }

            MaybeSpawnBubble(state, config, now);

            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            var bubbleDiameterPx = WorldStrip.BubbleDiameter(StripScreenRect, StripTotal());
            var twoRows = WorldStrip.Rows(StripScreenRect, StripTotal()) == 2;
            foreach (var bubble in _bubbles)
            {
                var age = now - bubble.SpawnTime;
                var progress = Mathf.Clamp01(age / (float)config.lifetimeSec);
                var home = NodeCentre(bubble.Node);

                // Rise from the node toward the strip's top edge, with a
                // gentle per-bubble wobble; fade out over the last stretch.
                // In two rows a bottom-row windfall stops at the midline —
                // rising the full band would bulldoze across the top row.
                var ceiling = StripScreenRect.yMax - bubbleDiameterPx * 0.5f;
                if (twoRows && home.y < StripScreenRect.center.y)
                {
                    ceiling = StripScreenRect.center.y;
                }

                var y = Mathf.Lerp(home.y, ceiling, progress);
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

            // Round-robin over the strip so every worked post gets its turn.
            for (var step = 0; step < _onStrip.Count; step++)
            {
                var index = (_bubbleCursor + step) % _onStrip.Count;
                var node = _onStrip[index].Node;
                if (!Bubbles.IsEligible(state, _loop.Data, node))
                {
                    continue;
                }

                _bubbleCursor = index + 1;
                // Until the first-ever catch, each windfall wears its own
                // "tap to catch" tag — nothing else marks it as interactive.
                _bubbles.Add(BubbleWorldView.Create(_container, node,
                    PlaceholderArt.ResourceColour(node.resourceId),
                    ArtLibrary.ForResource(node.resourceId), now, index * 2.1f,
                    CatchHintPending ? _labelFont : null));
                _nextBubbleAt = now + (float)config.spawnIntervalSec;
                return;
            }

            // Nothing worked anywhere — look again shortly rather than
            // banking a full interval against an empty camp.
            _nextBubbleAt = now + 2f;
        }

        private Vector2 NodeCentre(NodeState node)
        {
            for (var i = 0; i < _onStrip.Count && FirstNodeCentre + i < _centres.Length; i++)
            {
                if (_onStrip[i].Node == node)
                {
                    return _centres[FirstNodeCentre + i];
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
            _onStrip.Clear();
            // Any bubbles adrift were children of the torn-down container.
            _bubbles.Clear();
            _bursting.Clear();
            _lastCaught = null;
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
                    _container, node, PlaceholderArt.ResourceColour(node.resourceId), _labelFont,
                    ArtLibrary.ForResource(node.resourceId)));
            }

            // The (+) closes the strip while a kith slot stands unfilled —
            // Layout sizes and places it; LateUpdate shows and hides it.
            _openSlotsMark = PlaceholderArt.CreateLabel(_container, "+", _labelFont,
                new Color(0.333f, 0.392f, 0.247f, 1f)); // JournalTheme's MossDeep — an invitation, not a cost
            _openSlotsMark.anchor = TextAnchor.MiddleCenter;
            _openSlotsMark.transform.localPosition = Vector3.zero;
            _openSlotsMark.gameObject.SetActive(false);

            _builtFor = _loop.State;
        }

        private void Layout()
        {
            // The nodes, then the (+) mark when it shows — one shared strip, so
            // the hit test's centre order (FirstNodeCentre) holds. Nodes here
            // means the ones ON the strip, in the order GatherStrip settled
            // (the warden's ground first). The buffer is reused frame to frame —
            // this runs per LateUpdate.
            var total = StripTotal();
            if (_centres.Length != total)
            {
                _centres = new Vector2[total];
            }

            WorldStrip.LayoutCentresInto(StripScreenRect, total, _centres);
            _diameterPx = WorldStrip.Diameter(StripScreenRect, total);
            _radiusPx = _diameterPx * 0.5f * HitSlop;

            // Two-row layout: captions would hang over the row beneath, and
            // the badge drop clamps to the row pitch (visuals must match the
            // hit maths in WorldStrip.BadgeOffset).
            var showCaptions = WorldStrip.ShowsCaptions(StripScreenRect, total);
            var badgeOffsetLocal = _diameterPx > 0f
                ? WorldStrip.BadgeOffset(StripScreenRect, total, _diameterPx) / _diameterPx
                : WorldStrip.BadgeOffsetFactor;

            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            for (var i = 0; i < _onStrip.Count; i++)
            {
                _onStrip[i].SetPlacement(ScreenToWorld(_centres[FirstNodeCentre + i]), _diameterPx * worldPerPixel);
                _onStrip[i].SetStripLayout(badgeOffsetLocal, showCaptions);
            }

            if (_openSlotsShown && _centres.Length > FirstNodeCentre + _onStrip.Count)
            {
                _openSlotsMark.transform.position = ScreenToWorld(_centres[FirstNodeCentre + _onStrip.Count]);
                // A TextMesh at fontSize 64 stands ~6.4 world units per unit of
                // characterSize — 0.09 of the plate diameter fills roughly the
                // 60% of the circle a plate's own art does.
                _openSlotsMark.characterSize = _diameterPx * worldPerPixel * 0.09f;
            }
        }

        /// <summary>Everything the strip lays out this frame — the worked plates, and the (+) mark when it shows.</summary>
        private int StripTotal()
        {
            return FirstNodeCentre + _onStrip.Count + (_openSlotsShown ? 1 : 0);
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
