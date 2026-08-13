#!/usr/bin/env python3
"""Draw the back pages' fold marks — one per folding card on the Record page.

Four cards fold there and none of them had a picture: the Compendium, the
Folio, the Deep Pages and the Wheel. Every other folding section in the book
wears its own mark in the heading's right margin (a ground's keystone, a
sabbat on the keeping, a building on a crafting station, the tree on the
Almanac), so folded down, the Record page alone read as a column of bare
words.

Authored ink drawings rather than sourced plates, in the hand the journal's
other furniture already uses (`ui-chest.png`, `ui-glass.png`, the sabbat
plates): warm near-black strokes on transparency, so the card's own paper
shows through the picture rather than a white tile sitting on it. Nothing is
owed for any of them, and a painted plate can land over the same file any
time.

    python tools/make-record-marks.py            # write all four
    python tools/make-record-marks.py --check    # verify size/mode, write nothing

Requires Pillow. Everything below is drawn from these numbers, so a change is a
matter of moving one of them and running it again — the files are generated,
and hand-editing a PNG would be lost the next time anyone does.

The field is 3:2 because that is the shape of the lane these stand in
(`JournalWidgets.HeadingMarkWidth/Height`, 112x76): a mark drawn square would
be fitted by height and give back a third of the width it was allowed.
"""
import argparse
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "Assets" / "Resources" / "Art" / "UI" / "Journal"

# The ink the other line drawings are inked in, sampled off building-store.png.
INK = (34, 29, 23, 255)

W, H = 900, 600
SS = 4  # supersample, so the strokes keep their edge when the plate is cut down

STROKE = 14.0  # the pen
HAIR = 8.0     # the lighter pen, for what is behind or only implied

# A mark is 112 units wide on the page — about an eighth of what is drawn here
# — so every one of these is built out of a handful of strokes and no shading.
# Anything finer greys over into a smudge at the size it is actually seen.


def line(draw, points, width=STROKE):
    """One inked stroke."""
    scaled = [(x * SS, y * SS) for x, y in points]
    draw.line(scaled, fill=INK, width=int(round(width * SS)), joint="curve")


def dotted(draw, points, width=HAIR, on=46.0, off=34.0):
    """The same stroke, laid down as dashes — a thing drawn rather than kept.

    Walked at a fixed step rather than solved per segment: the paths here are
    already polylines of thirty-odd points, so a step finer than a dash is
    exact enough and the arithmetic stays readable.
    """
    step = 4.0
    run = 0.0
    for index in range(1, len(points)):
        x0, y0 = points[index - 1]
        x1, y1 = points[index]
        span = math.hypot(x1 - x0, y1 - y0)
        if span <= 0.0:
            continue

        walked = 0.0
        while walked < span:
            ahead = min(step, span - walked)
            if run % (on + off) < on:
                ratio0 = walked / span
                ratio1 = (walked + ahead) / span
                line(draw, [(x0 + (x1 - x0) * ratio0, y0 + (y1 - y0) * ratio0),
                            (x0 + (x1 - x0) * ratio1, y0 + (y1 - y0) * ratio1)], width)

            walked += ahead
            run += ahead


def circle(draw, cx, cy, radius, width=STROKE):
    draw.ellipse(
        [((cx - radius) * SS, (cy - radius) * SS), ((cx + radius) * SS, (cy + radius) * SS)],
        outline=INK, width=int(round(width * SS)))


def leaf(cx, cy, length, half, angle, steps=28):
    """A pointed oval on a bearing — the shape every leaf here is cut from."""
    cos, sin = math.cos(angle), math.sin(angle)
    points = []
    for side in (1.0, -1.0):
        span = range(steps + 1) if side > 0 else range(steps, -1, -1)
        for index in span:
            ratio = index / steps
            along = -length * 0.5 + length * ratio
            # Pinched to a point at both ends and fullest in the middle: a sine
            # of the run along the leaf, flattened a little so it is not an oval.
            across = side * half * math.sin(math.pi * ratio) ** 0.8
            points.append((cx + along * cos - across * sin, cy + along * sin + across * cos))

    points.append(points[0])
    return points


# ── The Wheel: eight turnings, four of them the sun's ──────────────────────

WHEEL_R = 236.0
WHEEL_HUB = 46.0


def draw_wheel(draw):
    cx, cy = W * 0.5, H * 0.5
    circle(draw, cx, cy, WHEEL_R)
    # The second rule just inside it, the double rule every card in the book
    # wears — it is what makes a ring read as a wheel rather than as an O.
    circle(draw, cx, cy, WHEEL_R - 30.0, HAIR)
    circle(draw, cx, cy, WHEEL_HUB)

    # Eight spokes, and the four that are the sun's own — the solstices and the
    # equinoxes — are inked while the fire festivals between them are hairs.
    # Four and four is the whole of what the card has to say from the margin.
    for step in range(8):
        angle = math.pi * step / 4.0
        cardinal = step % 2 == 0
        inner = WHEEL_HUB + 8.0
        outer = WHEEL_R - (34.0 if cardinal else 44.0)
        line(draw,
             [(cx + math.cos(angle) * inner, cy + math.sin(angle) * inner),
              (cx + math.cos(angle) * outer, cy + math.sin(angle) * outer)],
             STROKE if cardinal else HAIR)


# ── The Compendium: the hand lens, over what it is held to ─────────────────

def draw_compendium(draw):
    cx, cy = 356.0, 226.0
    radius = 194.0

    # A sprig behind the glass, and only there: the lens is what the card is,
    # and a leaf drawn clear of it would be a second subject at this size.
    line(draw, leaf(cx + 4.0, cy + 24.0, 264.0, 68.0, math.radians(-22.0)), HAIR)
    line(draw, [(cx - 128.0, cy + 76.0), (cx + 136.0, cy - 30.0)], HAIR)

    circle(draw, cx, cy, radius)
    # The bezel, standing a little proud of the glass.
    circle(draw, cx, cy, radius + 24.0, HAIR)

    # The handle, off the lower right on the diagonal. Kept SHORT: the mark is
    # fitted whole into a 112-unit lane, so every unit of handle is a unit the
    # lens gives back, and the lens is the half anyone reads.
    angle = math.radians(46.0)
    grip0 = (cx + math.cos(angle) * (radius + 24.0), cy + math.sin(angle) * (radius + 24.0))
    grip1 = (cx + math.cos(angle) * 400.0, cy + math.sin(angle) * 400.0)
    line(draw, [grip0, grip1], STROKE + 5.0)


# ── The Folio: a sprig pressed, and the corners holding it ─────────────────

FOLIO_BOX = (196.0, 116.0, 704.0, 484.0)
FOLIO_CORNER = 88.0


def draw_folio(draw):
    x0, y0, x1, y1 = FOLIO_BOX
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5

    # ONE leaf, laid on the diagonal, and not a sprig. A stem with leaves off
    # it was three outlines crossing inside 112 units and read as a knot: at
    # the size this is actually seen, a specimen has to be one closed shape.
    angle = math.radians(-30.0)
    cos, sin = math.cos(angle), math.sin(angle)
    length, half = 408.0, 118.0
    line(draw, leaf(cx, cy, length, half, angle))

    # The stalk, out of the pinched end.
    line(draw, [(cx - cos * length * 0.5, cy - sin * length * 0.5),
                (cx - cos * (length * 0.5 + 74.0), cy - sin * (length * 0.5 + 74.0))])

    # The midrib and its veins, which is what makes it a pressed leaf rather
    # than an almond. Veins are struck off the rib alternately, out and back
    # toward the tip the way a real one runs.
    line(draw, [(cx - cos * length * 0.42, cy - sin * length * 0.42),
                (cx + cos * length * 0.46, cy + sin * length * 0.46)], HAIR)
    for step in (-0.24, -0.06, 0.12, 0.30):
        root = (cx + cos * length * step, cy + sin * length * step)
        reach = half * (1.0 - abs(step) * 1.1) * 0.86
        for side in (1.0, -1.0):
            tip = (root[0] - sin * side * reach + cos * 52.0,
                   root[1] + cos * side * reach + sin * 52.0)
            line(draw, [root, tip], HAIR - 2.0)

    # The four paper corners it is mounted under. They are the whole reason
    # this is the FOLIO and not a plate of a plant: a specimen fixed to a page.
    for corner_x, corner_y, step_x, step_y in (
            (x0, y0, 1.0, 1.0), (x1, y0, -1.0, 1.0),
            (x0, y1, 1.0, -1.0), (x1, y1, -1.0, -1.0)):
        line(draw, [(corner_x + step_x * FOLIO_CORNER, corner_y),
                    (corner_x, corner_y),
                    (corner_x, corner_y + step_y * FOLIO_CORNER)])
        line(draw, [(corner_x + step_x * FOLIO_CORNER, corner_y),
                    (corner_x, corner_y + step_y * FOLIO_CORNER)], HAIR)


# ── The Deep Pages: half drawn, and the half that was let go ───────────────

def flank(cx, cy, rx, ry, side, steps=34):
    """One side of an oval, top to bottom — the half of a body a side owns."""
    points = []
    for index in range(steps + 1):
        angle = -math.pi * 0.5 + math.pi * index / steps
        points.append((cx + side * rx * math.cos(angle), cy + ry * math.sin(angle)))

    return points


# The beetle, seen from above, in the finished image's own units: elytra, the
# pronotum over them and the head over that. A beetle and not the moth this
# started as — four wings off a thorax are four outlines meeting in one place,
# and at the 112 units a mark is actually seen they merged into a blot. A
# beetle's silhouette is one closed oval and survives any size.
# Sized so the whole creature — antenna tips to rear feet — stands clear of the
# field. The mark is fitted whole into its lane, so a leg over the edge is not
# cropped on the page: it shrinks everything else to make room for the crop.
BEETLE_BODY = (124.0, 166.0)     # the elytra
BEETLE_PRONOTUM = (88.0, 50.0)
BEETLE_HEAD = (50.0, 36.0)
BEETLE_LEGS = ((-0.52, -40.0), (0.02, 6.0), (0.54, 44.0))


def draw_deep_pages(draw):
    cx = W * 0.5
    body_y = H * 0.5 + 92.0
    pronotum_y = body_y - BEETLE_BODY[1] - BEETLE_PRONOTUM[1] + 22.0
    head_y = pronotum_y - BEETLE_PRONOTUM[1] - BEETLE_HEAD[1] + 17.0

    # Left inked, right dashed: the creature is DRAWN and then let go (design
    # §6), and the mark says so rather than showing a pinned specimen, which is
    # the one thing the Deep Pages are not.
    for side in (-1.0, 1.0):
        ink = line if side < 0 else dotted
        weight = STROKE if side < 0 else HAIR

        ink(draw, flank(cx, body_y, BEETLE_BODY[0], BEETLE_BODY[1], side), weight)
        ink(draw, flank(cx, pronotum_y, BEETLE_PRONOTUM[0], BEETLE_PRONOTUM[1], side), weight)
        ink(draw, flank(cx, head_y, BEETLE_HEAD[0], BEETLE_HEAD[1], side), weight)

        # Three legs a side, out of the pronotum and the shoulders, each with a
        # knee — a straight spar off a beetle reads as a pin, which is the one
        # thing this mark must not say.
        for along, drop in BEETLE_LEGS:
            root = (cx + side * BEETLE_BODY[0] * 0.88, body_y + BEETLE_BODY[1] * along)
            knee = (root[0] + side * 92.0, root[1] + drop * 0.4 - 34.0)
            ink(draw, [root, knee, (knee[0] + side * 74.0, knee[1] + drop + 36.0)], HAIR)

        # The antenna, out of the head the way a naturalist's plate draws one.
        horn = (cx + side * BEETLE_HEAD[0] * 0.7, head_y - BEETLE_HEAD[1] * 0.5)
        ink(draw, [horn, (horn[0] + side * 70.0, horn[1] - 44.0),
                   (horn[0] + side * 128.0, horn[1] - 56.0)], HAIR)

    # The suture down the elytra, which is what makes the oval a beetle rather
    # than a bean: it is the one line both halves share, so it is inked whole.
    line(draw, [(cx, body_y - BEETLE_BODY[1] + 8.0), (cx, body_y + BEETLE_BODY[1] - 12.0)])


# ── The Almanac: the long song, drawn as what it is ────────────────────────

# The tree, in the finished image's own units: a trunk, two limbs, and four
# boughs off those, with a node wherever it divides and one at every tip. Six
# nodes, which is a tree AND the shape of a thing learned a line at a time —
# the two readings the card wants, and the reason the sourced engraving it
# replaces (a 1657 arbor consanguinitatis) was the right subject in the wrong
# hand: photographic, portrait and white-grounded beside four line marks.
# Spread wide rather than tall: the lane is 3:2, so a canopy no broader than it
# is high is fitted by its height and gives back a third of the width it had.
ALMANAC_FORKS = ((-224.0, -116.0), (224.0, -116.0))
ALMANAC_BOUGHS = ((-146.0, -132.0), (68.0, -164.0))
ALMANAC_NODE = 28.0


def draw_almanac(draw):
    cx = W * 0.5
    foot, fork_y = 528.0, 356.0

    line(draw, [(cx, foot), (cx, fork_y)], STROKE + 6.0)
    # A rule across the foot, so the trunk stands on something rather than
    # stopping. It is the only horizontal in the mark and it settles the whole.
    line(draw, [(cx - 92.0, foot), (cx + 92.0, foot)], HAIR)

    for reach, rise in ALMANAC_FORKS:
        limb = (cx + reach, fork_y + rise)
        line(draw, [(cx, fork_y), limb])
        circle(draw, limb[0], limb[1], ALMANAC_NODE, HAIR)

        for spread, lift in ALMANAC_BOUGHS:
            # Mirrored outward, so the canopy opens away from the trunk rather
            # than the right-hand limb growing back across the left.
            side = 1.0 if reach > 0 else -1.0
            tip = (limb[0] + spread * side, limb[1] + lift)
            line(draw, [limb, tip], HAIR)
            circle(draw, tip[0], tip[1], ALMANAC_NODE, HAIR)


MARKS = (
    ("ui-wheel.png", draw_wheel),
    ("ui-compendium.png", draw_compendium),
    ("ui-folio.png", draw_folio),
    ("ui-deep-pages.png", draw_deep_pages),
    ("ui-almanac.png", draw_almanac),
)


def build(paint):
    canvas = Image.new("RGBA", (W * SS, H * SS), (INK[0], INK[1], INK[2], 0))
    paint(ImageDraw.Draw(canvas))

    # Stand the ink in the middle of the field before cutting it down. The mark
    # is fitted whole into its lane, so empty paper on one side is not trimmed
    # on the page — it shifts the drawing off the centre of the lane and shrinks
    # it to fit the emptiness. Every one of these was hand-placed to begin with
    # and every one of them was out by 30-60 units, which is half a mark.
    ink = canvas.getbbox()
    if ink is not None:
        shift = (int(round((W * SS - ink[0] - ink[2]) * 0.5)),
                 int(round((H * SS - ink[1] - ink[3]) * 0.5)))
        if shift != (0, 0):
            centred = Image.new("RGBA", canvas.size, (INK[0], INK[1], INK[2], 0))
            centred.alpha_composite(canvas, shift)
            canvas = centred

    return canvas.resize((W, H), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="verify the committed plates, write nothing")
    args = parser.parse_args()

    if args.check:
        for name, _ in MARKS:
            path = OUT_DIR / name
            if not path.exists():
                print("missing: " + str(path))
                return 1

            with Image.open(path) as plate:
                if plate.size != (W, H) or plate.mode != "RGBA":
                    print("unexpected " + str(plate.size) + " " + plate.mode + ": " + str(path))
                    return 1

            print("ok: " + str(path))

        return 0

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, paint in MARKS:
        build(paint).save(OUT_DIR / name)
        print("wrote " + str(OUT_DIR / name))

    return 0


if __name__ == "__main__":
    sys.exit(main())
