using UnityEngine;
using Wildgrove.Sim;

namespace Wildgrove.Game.World
{
    /// <summary>
    /// One windfall adrift in the node strip: the resource's own naturalist
    /// plate, mounted on a page and risen from a worked node on a dandelion
    /// clock, turning slowly as it goes — a pressed cutting carried on the
    /// wind. Caught with a tap for a burst of that node's goods, and the clock
    /// lets its seed go as it does (see <see cref="Wildgrove.Sim.Bubbles"/>).
    /// Purely ephemeral — nothing here persists. <see cref="WorldView"/> owns
    /// spawn timing, the float path, expiry and the hit test; this is just
    /// the sprite.
    /// </summary>
    public sealed class BubbleWorldView : MonoBehaviour
    {
        // The plate's longest side in local units before the parent's
        // per-diameter scale, leaving the mount showing as a halo around it
        // (the same fit NodeWorldView gives a node's face).
        // The cutting rides in the MIDDLE of the clock rather than filling it:
        // a plate fitted to the full diameter left the tuft ring sitting under
        // its own edge, which is a mount again and not a dandelion.
        private const float PlateFit = 0.75f;
        private const float MountFit = 1.3f;
        private const float MountAlpha = 0.7f;
        private const float SkinAlpha = 0.8f;
        private const float SwayDegrees = 7f;
        private const float SwaySpeed = 1.1f;

        // The clock carries a page, and the cutting is mounted on that. Without
        // it the plate is a transparency held up against whatever the drift is
        // crossing — a node's own plate, a badge, a rock — and at the moment it
        // passes one, neither reads. Sized to reach the tuft ring (the hairs
        // stop at 0.86 of the mount, so 1.12 across) and drawn UNDER the clock,
        // so the hairs radiate over the page rather than being buried by it:
        // an opaque disc over the mount is a coin with a fringe, not a
        // dandelion.
        private const float PaperFit = 1.06f;

        // Sepia ink, NOT paper. The world camera clears to the journal's own
        // page colour, so a warm parchment differs from the background by about
        // three percent — survivable for a solid disc's glow, invisible for
        // hair. A clock is a line drawing; draw it in the colour the plates are
        // drawn in.
        private static readonly Color MountColour = new Color(0.404f, 0.345f, 0.259f, MountAlpha);
        private static readonly Color ShineColour = new Color(1f, 1f, 1f, 0.55f);

        // The page the camera itself clears to (JournalTheme.PagePaper), so it
        // vanishes against the empty band and only does its one job: hide the
        // strip behind the cutting. Not quite opaque — a hard disc drifting
        // over a plate reads as a hole punched in the page.
        private static readonly Color PaperColour = new Color(0.949f, 0.918f, 0.839f, 0.94f);

        /// <summary>The node this windfall rose from — what a catch pays out in.</summary>
        public NodeState Node { get; private set; }

        /// <summary>When it spawned (Time.time) — WorldView ages it from here.</summary>
        public float SpawnTime { get; private set; }

        /// <summary>Per-windfall wobble phase so simultaneous ones don't drift in lockstep.</summary>
        public float Seed { get; private set; }

        /// <summary>Last placement's screen position — the tap hit test point.</summary>
        public Vector2 ScreenPosition { get; private set; }

        /// <summary>Last placement's screen radius — the tap hit test circle.</summary>
        public float ScreenRadius { get; private set; }

        // A caught windfall's send-off: a real catch swells and fades gold-lit,
        // an empty one deflates grey — the difference must live at the finger.
        private const float BurstSeconds = 0.45f;
        private const float NudgeSeconds = 0.3f;

        // The seed outlives the head that let it go — the clock is gone in
        // under half a second, the seed is still drifting a second later.
        private const float SeedSeconds = 1.15f;
        private const int RewardedSeeds = 9;
        private const int EmptySeeds = 3;
        private const float SeedScale = 0.24f;

        // Same trap as the clock: near-white seed on a parchment sky is no seed
        // at all. A real catch throws ochre — the journal's own warm accent —
        // and an empty one a washed-out grey that still has to be seen to read
        // as the lesser answer.
        private static readonly Color SeedColour = new Color(0.627f, 0.353f, 0.212f, 1f);
        private static readonly Color SpentSeedColour = new Color(0.5f, 0.47f, 0.42f, 1f);

        private SpriteRenderer _mount;
        private SpriteRenderer _paper;
        private SpriteRenderer _plate;
        private SpriteRenderer _skin;
        private SpriteRenderer _shine;
        private TextMesh _hint;
        private Color _colour;
        private float _burstAt = -1f;
        private bool _burstRewarded;
        private float _nudgedAt = -10f;
        private float _placedDiameter = 1f;

        private SpriteRenderer[] _seeds;
        private Vector2[] _seedDrift;
        private float[] _seedSpin;
        private Color _seedColour;
        private bool _seedsSown;

        public static BubbleWorldView Create(Transform parent, NodeState node, Color colour, Sprite face, float spawnTime, float seed, Font hintFont = null)
        {
            var go = new GameObject("Windfall_" + node.resourceId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BubbleWorldView>();
            view.Node = node;
            view.SpawnTime = spawnTime;
            view.Seed = seed;
            view._colour = colour;

            if (hintFont != null)
            {
                // First-run only: nothing else marks the drifting plate as the
                // game's most valuable target.
                view._hint = PlaceholderArt.CreateLabel(go.transform,
                    Wildgrove.Game.Input.DeviceForm.PressVerb + " to catch", hintFont,
                    new Color(0.431f, 0.376f, 0.278f, 1f), StripLayers.WindfallHint);
                view._hint.transform.localPosition = new Vector3(0f, -0.62f, 0f);
                view._hint.characterSize = 0.06f;
            }

            // The page the windfall carries, and the clock it rides on — in
            // that order, bottom of the stack up.
            view._paper = CreateSprite(go.transform, "Paper", PlaceholderArt.Disc, PaperColour, StripLayers.WindfallPaper);
            view._paper.transform.localScale = Vector3.one * PaperFit;

            view._mount = CreateSprite(go.transform, "Seedhead", PlaceholderArt.Seedhead, MountColour, StripLayers.WindfallMount);
            view._mount.transform.localScale = Vector3.one * MountFit;

            if (face != null)
            {
                view._plate = CreateSprite(go.transform, "Plate", face, Color.white, StripLayers.WindfallPlate);
                var longest = Mathf.Max(face.bounds.size.x, face.bounds.size.y);
                view._plate.transform.localScale = Vector3.one * (longest > 0f ? PlateFit / longest : 1f);

                return view;
            }

            // No plate for this resource: fall back to a tinted disc with an
            // off-centre highlight, which reads as a drifting bead rather than
            // another node.
            // Sized to the plate it stands in for, so the clock rings the bead
            // exactly as it rings a cutting.
            view._skin = CreateSprite(go.transform, "Skin", PlaceholderArt.Disc, colour, StripLayers.WindfallSkin);
            view._skin.transform.localScale = Vector3.one * PlateFit;
            view._shine = CreateSprite(go.transform, "Shine", PlaceholderArt.Disc, ShineColour, StripLayers.WindfallShine);
            view._shine.transform.localScale = Vector3.one * (0.28f * PlateFit);
            view._shine.transform.localPosition = new Vector3(-0.22f, 0.24f, 0f) * PlateFit;

            return view;
        }

        /// <summary>Place the windfall for this frame; <paramref name="fade"/> is 1 fully drawn, 0 gone.</summary>
        public void SetPlacement(Vector3 worldPosition, float worldDiameter, Vector2 screenPosition, float screenRadius, float fade)
        {
            transform.position = worldPosition;
            _placedDiameter = worldDiameter;

            var now = Time.time;
            // A near miss shivers it briefly; the first-run hint beckons with
            // a slow pulse so the plate reads as "alive", not scenery.
            var nudge = now - _nudgedAt < NudgeSeconds
                ? 1f - 0.08f * Mathf.Sin((now - _nudgedAt) / NudgeSeconds * Mathf.PI)
                : 1f;
            var beckon = _hint != null ? 1f + 0.05f * Mathf.Sin(now * 3f + Seed) : 1f;
            transform.localScale = Vector3.one * (worldDiameter * nudge * beckon);

            // Turning as it rises — the wind has hold of it.
            var shiver = now - _nudgedAt < NudgeSeconds ? Mathf.Sin(now * 40f) * 4f : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * SwaySpeed + Seed) * SwayDegrees + shiver);

            ScreenPosition = screenPosition;
            ScreenRadius = screenRadius;

            SetAlpha(_mount, MountColour, MountColour.a * fade);
            SetAlpha(_paper, PaperColour, PaperColour.a * fade);
            SetAlpha(_plate, Color.white, fade);
            SetAlpha(_skin, _colour, SkinAlpha * fade);
            SetAlpha(_shine, ShineColour, ShineColour.a * fade);
            if (_hint != null)
            {
                var hintColour = _hint.color;
                hintColour.a = fade;
                _hint.color = hintColour;
            }
        }

        /// <summary>A tap landed near but not on it — shiver so the miss reads as a miss.</summary>
        public void Nudge(float now)
        {
            _nudgedAt = now;
        }

        /// <summary>Shift every timestamp forward after a freeze (a sheet covered the strip), so the drift resumes where it stopped.</summary>
        public void ShiftTime(float seconds)
        {
            SpawnTime += seconds;
            if (_burstAt >= 0f)
            {
                _burstAt += seconds;
            }
        }

        /// <summary>Start the send-off. Restyleable until the first AnimateBurst tick — the catch is taken before the sim knows what it paid.</summary>
        public void BeginBurst(float now, bool rewarded)
        {
            if (_burstAt < 0f)
            {
                _burstAt = now;
            }

            _burstRewarded = rewarded;
            if (_hint != null)
            {
                _hint.gameObject.SetActive(false);
            }
        }

        /// <summary>Advance the send-off; false once it has played out and the object can go.</summary>
        public bool AnimateBurst(float now)
        {
            var elapsed = now - _burstAt;
            if (!_seedsSown)
            {
                // Sown on the first tick and not in BeginBurst: the catch is
                // taken before the sim knows what it paid, and the seed carries
                // that answer's colour.
                SowSeeds();
                _seedsSown = true;
            }

            AnimateSeeds(elapsed);

            var t = Mathf.Clamp01(elapsed / BurstSeconds);
            if (t >= 1f)
            {
                // The head is spent, but the seed it let go is still in the air
                // — hold the object open until the last of it has drifted out.
                return elapsed < SeedSeconds;
            }

            var scale = _burstRewarded ? 1f + 0.4f * t : 1f - 0.35f * t;
            transform.localScale = Vector3.one * (_placedDiameter * scale);
            var fade = 1f - t * t;
            if (_burstRewarded)
            {
                // Ochre rather than pale gold, for the same reason the clock is
                // drawn in ink: a lit flash has to out-read the paper behind it.
                SetAlpha(_mount, SeedColour, (MountAlpha + 0.35f) * fade);
                SetAlpha(_paper, PaperColour, PaperColour.a * fade);
                SetAlpha(_plate, Color.white, fade);
                SetAlpha(_skin, _colour, SkinAlpha * fade);
                SetAlpha(_shine, ShineColour, ShineColour.a * fade);
            }
            else
            {
                // Empty: grey out and deflate — visibly not a jackpot.
                var grey = new Color(0.6f, 0.58f, 0.53f, 1f);
                SetAlpha(_mount, grey, MountAlpha * fade);
                SetAlpha(_paper, Color.Lerp(PaperColour, grey, t * 0.5f), PaperColour.a * fade);
                SetAlpha(_plate, Color.Lerp(Color.white, grey, t), fade * 0.8f);
                SetAlpha(_skin, Color.Lerp(_colour, grey, t), SkinAlpha * fade * 0.8f);
                SetAlpha(_shine, grey, 0.2f * fade);
            }

            return true;
        }

        /// <summary>
        /// Let the clock go. A full catch scatters a headful, an empty one
        /// three grey ones that barely leave — the send-off already says which
        /// it was, and the seed says it again a beat later.
        /// </summary>
        private void SowSeeds()
        {
            var count = _burstRewarded ? RewardedSeeds : EmptySeeds;
            _seedColour = _burstRewarded ? SeedColour : SpentSeedColour;
            _seeds = new SpriteRenderer[count];
            _seedDrift = new Vector2[count];
            _seedSpin = new float[count];

            for (var i = 0; i < count; i++)
            {
                // Fanned evenly and offset by the windfall's own phase, so the
                // scatter is neither a starburst nor a different lottery each
                // time a plate is caught.
                var angle = (i + 0.5f) / count * Mathf.PI * 2f + Seed;
                var reach = _burstRewarded ? 1f : 0.45f;

                // Outward, and upward on top of that: seed leaves on the wind
                // rather than falling away from the head.
                _seedDrift[i] = new Vector2(Mathf.Cos(angle) * 0.6f, Mathf.Sin(angle) * 0.35f + 0.7f) * reach;
                _seedSpin[i] = Mathf.Sin(angle * 3f) * 90f;

                var seed = CreateSprite(transform, "Seed", PlaceholderArt.Pappus, _seedColour, StripLayers.WindfallSeed);
                seed.transform.localScale = Vector3.one * SeedScale;
                _seeds[i] = seed;
            }
        }

        private void AnimateSeeds(float elapsed)
        {
            if (_seeds == null)
            {
                return;
            }

            var t = Mathf.Clamp01(elapsed / SeedSeconds);
            // Out-quad: away from the head at once, then coasting — the shape
            // of something the wind has taken rather than something thrown.
            var eased = 1f - (1f - t) * (1f - t);
            for (var i = 0; i < _seeds.Length; i++)
            {
                var seed = _seeds[i];
                if (seed == null)
                {
                    continue;
                }

                var drift = _seedDrift[i];
                var sway = Mathf.Sin(elapsed * 5f + i) * 0.05f * eased;
                seed.transform.localPosition = new Vector3(drift.x * eased + sway, drift.y * eased, 0f);
                seed.transform.localRotation = Quaternion.Euler(0f, 0f, _seedSpin[i] * eased);
                // Whole until the last third, then gone — a seed that starts
                // fading the moment it leaves never reads as having left.
                SetAlpha(seed, _seedColour, Mathf.Clamp01((1f - t) * 3f));
            }
        }

        private static void SetAlpha(SpriteRenderer renderer, Color colour, float alpha)
        {
            if (renderer == null)
            {
                return;
            }

            colour.a = alpha;
            renderer.color = colour;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Color colour, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
