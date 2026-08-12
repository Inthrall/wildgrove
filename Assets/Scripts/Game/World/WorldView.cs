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

        /// <summary>The warden's empty ground, while they hold none — the (+) asking where they walk.</summary>
        Warden,

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
    /// picker — a ground, then a body. A site's watch post keeps no plate of its
    /// own here — it would wear a node's face without being one — and is
    /// assigned from that zone's watch card, the pickers and the familiars' own
    /// sheets.
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
    /// Everything on the strip is a GROUND — the warden's place included. Their
    /// own plate led the strip for a day (2026-08-05, removed 2026-08-06): a body
    /// among grounds read as a node you could gather from, captioned with a name
    /// where every neighbour carried a crop. What it was reaching for is split in
    /// two now. While the warden holds a node, <see cref="GatherStrip"/> leads
    /// with that ground, and the badge under it says whose it is — the player's
    /// own body reads first without being a plate. While they stand at camp, an
    /// EMPTY ground leads instead (<see cref="WardenWorldView"/>): a (+) where
    /// the crop would be, their badge beneath it, and a tap that asks where they
    /// walk. Otherwise the one body the player IS would simply vanish from the
    /// assignment board at exactly the moment it has something to assign.
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed class WorldView : MonoBehaviour
    {
        private const float HitSlop = 1.15f;

        // The strip's shared centre order: the warden's empty ground when they
        // hold none, then the plates on the strip, then the (+) mark when it
        // shows. One buffer serves the layout and the hit test, so both read the
        // order from here — which is why this is a property and not a const:
        // the head slot comes and goes with the warden's own posting.
        private const int WardenCentre = 0;
        private int FirstNodeCentre => _wardenPlaceShown ? 1 : 0;

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
        // The warden's empty ground, leading the strip while they hold no node —
        // laid out at centre 0, so the plates run from centre 1 while it shows.
        private WardenWorldView _wardenPlace;
        private bool _wardenPlaceShown;
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
        // it just replaced. _wardenPlace and _openSlotsMark are null until the
        // first Rebuild, and the rebuild that Rebuild's own labels provoke is
        // exactly the one that arrives while they still are.
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

            if (_wardenPlace != null)
            {
                _wardenPlace.RefreshLabel();
            }

            PlaceholderArt.RefreshLabel(_openSlotsMark);
        }

        /// <summary>
        /// What the screen point landed on. Plates and badges resolve together —
        /// nearest centre wins (see <see cref="WorldStrip.ResolveHit"/>) — and
        /// <paramref name="postId"/> carries the post for a
        /// <see cref="StripTap.Post"/>, null otherwise. Every plate here is a
        /// ground: tapping the one the warden stands on asks the same question as
        /// tapping any other post ("who walks here?"), and tapping their empty
        /// ground asks the other half of it ("where do you walk?").
        /// </summary>
        public StripTap TapAtScreenPoint(Vector2 screenPoint, out string postId)
        {
            postId = null;
            var index = WorldStrip.ResolveHit(StripScreenRect, _centres, _radiusPx, _diameterPx, _badgeVisible, screenPoint);
            if (index < 0 || index >= _centres.Length)
            {
                return StripTap.Miss;
            }

            if (_wardenPlaceShown && index == WardenCentre)
            {
                return StripTap.Warden;
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

            // A warden at camp has no plate among the grounds, so their own empty
            // ground leads the strip instead. Camp only — a watching warden also
            // has no plate here (a site's watch is no node), but an empty ground
            // would say they had no work when watching IS the work.
            _wardenPlaceShown = postNodeId == null;
            if (_wardenPlace.gameObject.activeSelf != _wardenPlaceShown)
            {
                _wardenPlace.gameObject.SetActive(_wardenPlaceShown);
            }

            _openSlotsShown = _loop.KithWalking() < _loop.KithSlots();
            if (_openSlotsMark.gameObject.activeSelf != _openSlotsShown)
            {
                _openSlotsMark.gameObject.SetActive(_openSlotsShown);
            }

            Layout();

            // A fresh camp with nothing posted anywhere would otherwise render
            // the whole strip at idle-dim — reading as "disabled" exactly when
            // the first tap must happen. Dim only once dim can mean something.
            var anyPosted = postNodeId != null || Warden.IsWatching(state);
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

            // The warden's empty ground takes its tap on the whole circle, not on
            // the badge under it: the badge there is a statement of who, and the
            // plate is the question. (A badge hit would answer the same way, so
            // this only keeps the hit maths honest about which circle is live.)
            if (_wardenPlaceShown)
            {
                _badgeVisible[WardenCentre] = false;
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
        /// "A body is here" rather than "this ground earns": the strip is a
        /// statement of who stands where, and a rate can read zero for reasons
        /// that are nothing to do with standing (see
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

            // A camp with no node held keeps the whole board — a watch-only camp
            // included, since a site's watch has no plate of its own.
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
        /// keeping a watch holds no ground, so the order is the land's own.
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
        // ─────────────────────── Windfall bubbles ────────────────────────
        // Any node the run can reach drifts a bubble up into the band now and
        // then — the strip is where they are CAUGHT, not where they all come
        // from; catching one pays a burst of that node's goods (Sim.Bubbles).
        // All ephemeral — spawn timing, float path and expiry live here,
        // nothing persists.

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
                rewarded ? new Color(0.333f, 0.392f, 0.247f, 1f) : new Color(0.431f, 0.376f, 0.278f, 1f),
                StripLayers.CatchText);
            label.anchor = TextAnchor.MiddleCenter;
            label.transform.position = ScreenToWorld(screen);
            // CreateLabel sizes for a diameter-scaled parent; this one hangs
            // off the unscaled container, so size it to the strip instead.
            var worldPerPixel = (ScreenToWorld(Vector2.right) - ScreenToWorld(Vector2.zero)).magnitude;
            var diameter = WorldStrip.BubbleDiameter(StripScreenRect, StripTotal()) * worldPerPixel;
            label.characterSize = diameter * 0.055f;
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

            // Expired bubbles go, having already faded out wherever the wind
            // left them. So do any whose node the run can no longer reach — a
            // fold rebuilds the land, and a windfall holding a node from the
            // run before it would pay nothing if it were caught.
            //
            // This used to retire a windfall whose post lost its body, back
            // when the strip WAS the pool. It cannot any more: most windfalls
            // now rise from ground that was never on the strip, and a body
            // test would destroy each one on the frame it spawned.
            for (var i = _bubbles.Count - 1; i >= 0; i--)
            {
                if (now - _bubbles[i].SpawnTime >= config.lifetimeSec
                    || !Bubbles.IsEligible(state, _loop.Data, _bubbles[i].Node))
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
                var home = BubbleOrigin(bubble.Node, bubbleDiameterPx);

                // The band a windfall may wander in. In two rows each row is
                // its own band — a bottom-row windfall stops at the midline and
                // a top-row one starts there, or the drift would bulldoze
                // across the other row's plates.
                var ceiling = StripScreenRect.yMax - bubbleDiameterPx * 0.5f;
                var floor = StripScreenRect.yMin + bubbleDiameterPx * 0.5f;
                if (twoRows)
                {
                    if (home.y < StripScreenRect.center.y)
                    {
                        ceiling = StripScreenRect.center.y;
                    }
                    else
                    {
                        floor = StripScreenRect.center.y;
                    }
                }

                floor = Mathf.Min(floor, ceiling);

                // The rise from the node to the ceiling is only the spine of
                // the path; the wind is the rest. Two sines per axis, at
                // periods that don't divide into each other, so the loop never
                // closes and the seed wanders the band instead of swinging
                // through the same arc — and one on the way along, so it isn't
                // climbing at a constant rate either. This is what "blown"
                // costs: a single sine on x reads as a pendulum, which is a
                // thing on a string, not a thing on the wind.
                var wanderX = Mathf.Sin(age * 0.43f + bubble.Seed) * 0.68f
                              + Mathf.Sin(age * 1.07f + bubble.Seed * 1.7f) * 0.32f;
                var wanderY = Mathf.Sin(age * 0.61f + bubble.Seed * 2.3f) * 0.7f
                              + Mathf.Sin(age * 1.49f + bubble.Seed) * 0.3f;

                // The wind takes it further the longer it's up: a windfall
                // starts hugging the post it rose from — which is the one thing
                // its plate has to say — and only then wanders off across the
                // band. Roomy enough to cross a few plates, never so wide that
                // a narrow strip throws it against its own edges every pass.
                var freedom = Mathf.Lerp(0.3f, 1f, Mathf.Min(progress * 2.5f, 1f));
                var reach = Mathf.Min(StripScreenRect.width * 0.42f, bubbleDiameterPx * 3f) * freedom;
                var lift = Mathf.Min((ceiling - floor) * 0.3f, bubbleDiameterPx * 0.7f) * freedom;

                var x = Mathf.Clamp(home.x + wanderX * reach,
                    StripScreenRect.xMin + bubbleDiameterPx * 0.5f,
                    StripScreenRect.xMax - bubbleDiameterPx * 0.5f);
                // The spine stops a bob short of the ceiling. Aimed at the
                // ceiling itself, the last third of the drift is spent pinned
                // against the clamp with the wander flattened out of it — the
                // one stretch where the wind is meant to be most obvious.
                var top = Mathf.Max(ceiling - lift, floor);
                var y = Mathf.Clamp(Mathf.Lerp(home.y, top, progress) + wanderY * lift, floor, ceiling);
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

            if (_views.Count == 0)
            {
                return;
            }

            // Round-robin over the WHOLE land — every node the run can reach,
            // not just the posts drawn on the strip. A windfall is the land
            // handing something over; gating the pool on the strip meant every
            // one of them rose from the two or three plates already under the
            // player's eye, and an unposted camp drifted nothing at all. The
            // interval and maxLive still meter the reward, so this changes
            // WHERE a windfall comes from, not how much the run is paid.
            for (var step = 0; step < _views.Count; step++)
            {
                var index = (_bubbleCursor + step) % _views.Count;
                var node = _views[index].Node;
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

            // Nothing eligible anywhere — the run holds no reachable ground at
            // all, or the section is inert. Look again shortly rather than
            // banking a full interval against it.
            _nextBubbleAt = now + 2f;
        }

        /// <summary>
        /// Where a windfall from <paramref name="node"/> starts. A post drawn
        /// on the strip drifts up off its own plate, as it always has. Every
        /// other reachable node is ground the player cannot see, so its
        /// windfall blows in along the foot of the band instead — at a spot
        /// fixed by that node's own place in the land, so the same ground
        /// always sends from the same side of the strip. That is as much of a
        /// map as a band of plates can carry, and it beats the old fallback,
        /// which put every unseen node's windfall at the dead centre.
        /// </summary>
        private Vector2 BubbleOrigin(NodeState node, float diameterPx)
        {
            for (var i = 0; i < _onStrip.Count && FirstNodeCentre + i < _centres.Length; i++)
            {
                if (_onStrip[i].Node == node)
                {
                    return _centres[FirstNodeCentre + i];
                }
            }

            var place = 0;
            for (var i = 0; i < _views.Count; i++)
            {
                if (_views[i].Node == node)
                {
                    place = i;
                    break;
                }
            }

            // Golden-ratio stride rather than an even split: the land's order
            // groups a zone's nodes together, so an even split would send a
            // whole zone in from the same handspan of the band.
            var across = Mathf.Repeat(place * 0.618f + 0.191f, 1f);
            return new Vector2(
                Mathf.Lerp(StripScreenRect.xMin + diameterPx * 0.5f, StripScreenRect.xMax - diameterPx * 0.5f, across),
                StripScreenRect.yMin + diameterPx * 0.5f);
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

            // The warden's empty ground, leading the strip while they stand at
            // camp — Layout places it, LateUpdate shows and hides it.
            _wardenPlace = WardenWorldView.Create(_container, _labelFont);
            _wardenPlace.gameObject.SetActive(false);

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
            // The warden's empty ground when it shows, then the nodes, then the
            // (+) mark when it shows — one shared strip, so the hit test's centre
            // order (WardenCentre / FirstNodeCentre) holds. Nodes here means the
            // ones ON the strip, in the order GatherStrip settled (the warden's
            // own ground first when they hold one). The buffer is reused frame to
            // frame — this runs per LateUpdate.
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
            if (_wardenPlaceShown && _centres.Length > WardenCentre)
            {
                _wardenPlace.SetPlacement(ScreenToWorld(_centres[WardenCentre]), _diameterPx * worldPerPixel);
                _wardenPlace.SetStripLayout(badgeOffsetLocal, showCaptions);
            }

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

        /// <summary>Everything the strip lays out this frame — the warden's empty ground when it shows, the worked plates, and the (+) mark when it shows.</summary>
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
