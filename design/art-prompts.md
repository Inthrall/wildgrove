# Art prompts — brand & store images

Prompts for an image AI, one per image the app and its Play listing need.
Written against the design doc's art direction (§3): hand-drawn naturalist
field-guide plates — fine ink linework, muted watercolour and sepia washes,
aged parchment. Generate the **App icon (framed mark)** first, then feed it
back as a style/subject reference for every other prompt so the mark stays
identical across the set.

## The shared style block

Prepend this to every prompt:

> Hand-drawn 19th-century naturalist field-guide illustration. Fine
> black-brown ink linework with slightly varying, hand-inked line weight;
> muted watercolour washes in moss green (#4A6B44) and sage (#728D5F) with
> darker pooling at the edges of each wash; warm aged parchment ground
> (cream #EEE4CC) with subtle paper grain and a few faint sepia foxing
> spots. Quiet, precise, a little tender — a field guide someone loved.
> Flat, even lighting. No photorealism, no 3D render, no gloss, no neon,
> no drop shadows, no gradients.

The mark itself, referenced throughout: **a single young sprig — one gently
curved stem, one upright terminal leaf, two small opposed leaflets springing
from the stem — inside a thin hand-inked sepia (#8A7350) double ring, like a
numbered figure frame on a botanical plate.**

## The images

### 1. App icon — framed mark (legacy/round icon + Play Store icon)
- **Deliver:** 1024×1024 PNG, opaque (Play Store rejects transparency).
- **Prompt:** The sprig mark centred inside its hand-inked double ring on
  aged parchment, composed comfortably inside the square with generous
  margin; faint foxing near the corners only. No text, no border beyond the
  ring itself.

### 2. App icon — adaptive foreground
- **Deliver:** 1024×1024 PNG, **transparent background**. The sprig only —
  **no ring** — occupying no more than the central 60% of the canvas
  (Android masks the outer third of an adaptive icon to a circle/squircle,
  so anything outside the safe zone gets cut).
- **Prompt:** The sprig alone, isolated on a fully transparent background,
  centred and small within the frame, inked and washed exactly as in the
  reference. No ring, no ground, no text, no shadow.

### 3. App icon — adaptive background
- **Deliver:** 1024×1024 PNG, opaque, edge-to-edge (any part may be shown
  under any mask shape — keep it uniform, nothing composed).
- **Prompt:** A plain square of warm aged parchment paper, cream #EEE4CC,
  subtle fibrous paper grain, very gentle mottling, imperceptibly darker
  toward the corners. No marks, no objects, no text — a background texture
  only.

### 4. Splash logo
- **Deliver:** ~2048×1024 PNG, **transparent background** (it sits on the
  parchment splash colour the app already sets).
- **Prompt:** The sprig mark in its double ring, with WILDGROVE hand-lettered
  beneath in small capitals — 19th-century botanical-plate caption
  lettering, slightly irregular letterpress ink in near-black sepia, modest
  letter-spacing. Mark above, wordmark below, generous clear space around
  both. Transparent background, nothing else.

### 5. Play Store feature graphic
- **Deliver:** exactly **1024×500** PNG/JPG, opaque (required for the store
  listing; shown behind the play button).
- **Prompt:** A wide botanical-plate composition on aged parchment: the
  sprig mark in its ring sitting left-of-centre, WILDGROVE hand-lettered
  large to its right in plate-caption capitals, and around the margins the
  quiet furniture of a field-guide page — a faint partial ruled border, a
  small "Fig. 1." style caption under the ring, one or two lines of
  illegible hand-written pencil marginalia. Muted moss green and sepia
  against cream; calm, spacious, nothing crowded.

### 6. Play Store screenshots frame (optional, later)
- **Deliver:** 1080×1920 PNG, opaque, one reusable frame.
- **Prompt:** An empty full-page plate from the same field guide: parchment
  ground, a fine single-ruled ink border with a small figure-caption box at
  the bottom centre, light foxing. The middle of the page stays empty (a
  gameplay screenshot will be composited there). No text beyond the empty
  caption box, no objects.

## Store product icons

The three one-time purchases (`remove_ads`, `starter_bundle`, `kith_slot`)
shown in the in-game store rows. Prepend the shared style block to each.

- **Deliver (all three):** 512×512 PNG, **transparent background**, a single
  centred subject with generous margin. They render small (~56 px on the card),
  so keep one bold, legible silhouette — no fine detail that dissolves at that
  size, no text, no ring, no ground plane.
- **A note on colour:** these three may introduce one warm accent — **amber /
  honey (#B07B36)** for the resin — over the usual moss-and-sage washes, since
  amber is the currency they trade in. Everything else stays in the field-guide
  palette.
- **Wiring:** drop the PNGs under `Assets/Resources/Art/Store/` and map each
  product id in `ArtLibrary` (a `Product` map), then show them with
  `IconImage(...)` on the store rows — additive, so a missing plate just falls
  back to the text row.

### 7. remove_ads — "the ads step aside"
- **Prompt:** A single small songbird (a lark or linnet) caught mid-lift, wings
  raised, rising off a bare twig — the quiet returning to the grove. Inked and
  washed in moss green and sage exactly as the reference; the twig minimal, the
  bird the whole subject. Nothing crossed-out or modern; the idea is calm
  restored, not a prohibition symbol.

### 8. starter_bundle — a slot and a pile of amber
- **Prompt:** A small field-guide still life: a cloth travelling bundle knotted
  with cord (the pack you set out with), a warm translucent amber nugget with a
  tiny insect sealed inside resting against it, and a single downy feather laid
  alongside — the three read together as "a companion's place and a pile of
  amber to start with." Amber rendered in warm honey #B07B36; cloth and feather
  in the muted washes. Composed as a tidy little group, generous margin.

### 9. kith_slot — room for one more
- **Prompt:** A single animal track — one clean paw print pressed into soft
  earth — sitting inside a faint hand-inked dotted oval, like an empty labelled
  space on a plate waiting to be filled. The print in black-brown ink with a
  soft sage wash, the dotted "slot" outline in thin sepia #8A7350. The quiet
  idea: a place kept for a friend not yet arrived.

## Working notes

- **Consistency:** generate #1 first; pass it as an image/style reference
  for #2, #4, #5 and #6, asking the model to redraw the same sprig, not a
  new plant.
- **Upscaling:** ask for the largest output the tool gives and downscale —
  ink linework survives downscaling far better than upscaling.
- **Transparency:** many models fake transparency with a checkerboard or
  white — for #2 and #4 verify the alpha channel is real before importing,
  or ask for the mark on solid #FF00FF and key it out.
- **What Unity needs:** icons wire in via Player Settings → Icon (adaptive
  foreground/background, round, legacy); the splash logo becomes a Sprite
  assigned in Player Settings → Splash Image. `SplashBranding.Apply`
  (Wildgrove menu) already sets the parchment background + dark Unity logo.
- **Later (the in-game plate pass, ~60 plates):** the same style block works
  per specimen — swap the subject line for the gatherable/creature/fossil
  and add "numbered figure, thin ruled plate border, hand-written margin
  annotation". Keep one reference plate as the anchor for the whole set.
