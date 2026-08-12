#!/usr/bin/env python3
"""Draw the cache chest — the events rail's weekly-cache mark.

An authored ink drawing rather than a sourced plate, in the hand the journal's
other furniture already uses (`building-store.png`'s barrel, `ui-cairn.png`):
warm near-black strokes on transparency, so the cell's paper shows through the
picture rather than a white card sitting on it. Nothing is owed for it, and a
painted plate can land over the same file any time.

    python tools/make-chest-plate.py            # write the plate
    python tools/make-chest-plate.py --check    # verify size/mode, write nothing

Requires Pillow. Everything below is drawn from these numbers, so a change is a
matter of moving one of them and running it again — the file is generated, and
hand-editing the PNG would be lost the next time anyone does.
"""
import argparse
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Resources" / "Art" / "UI" / "Journal" / "ui-chest.png"

# The ink the other line drawings are inked in, sampled off building-store.png.
INK = (34, 29, 23, 255)

W, H = 900, 760
SS = 4  # supersample, so the strokes keep their edge when the plate is cut down

# The chest, in the finished image's own units.
LID_Y = 300.0          # where the dome meets the rim
RIM_Y = 344.0          # the rim band's lower edge — the body starts here
BODY_X0, BODY_X1 = 100.0, 800.0
BODY_Y1 = 650.0        # the body's foot, where the plinth starts
PLINTH_Y = 696.0
FOOT_Y = 730.0
OVERHANG = 16.0        # how far the lid and the plinth stand proud of the body
DOME = 190.0           # the lid's rise above LID_Y

STRAP = 46.0           # an iron band's width
STRAP_X = (250.0, 650.0)
LOCK_X0, LOCK_X1 = 408.0, 492.0
LOCK_Y0, LOCK_Y1 = 296.0, 396.0

STROKE = 10.0          # the pen, in finished units
HAIR = 6.5             # the lighter pen, for the staves' seams and the grain

# One seed, so the wobble is the same drawing every run — a plate that redraws
# itself differently each time is a diff nobody can read.
RNG = random.Random(20260812)


def wobble(points, amount=1.6):
    """Walk a drawn line off true, the way a nib does. Ends stay put."""
    out = []
    for index, (x, y) in enumerate(points):
        if index == 0 or index == len(points) - 1:
            out.append((x, y))
            continue
        out.append((x + RNG.uniform(-amount, amount), y + RNG.uniform(-amount, amount)))
    return out


def line(draw, points, width=STROKE, amount=1.6):
    scaled = [(x * SS, y * SS) for x, y in wobble(points, amount)]
    draw.line(scaled, fill=INK, width=int(round(width * SS)), joint="curve")


def dome_y(x, span_x0=BODY_X0 - OVERHANG, span_x1=BODY_X1 + OVERHANG, rise=DOME):
    """The lid's height above LID_Y at x — a half-ellipse over the rim."""
    centre = (span_x0 + span_x1) * 0.5
    half = (span_x1 - span_x0) * 0.5
    ratio = max(-1.0, min(1.0, (x - centre) / half))
    return LID_Y - rise * math.sqrt(max(0.0, 1.0 - ratio * ratio))


def dome_points(x0, x1, steps=48):
    return [(x0 + (x1 - x0) * i / steps, dome_y(x0 + (x1 - x0) * i / steps)) for i in range(steps + 1)]


def draw_chest(draw):
    lid_x0, lid_x1 = BODY_X0 - OVERHANG, BODY_X1 + OVERHANG

    # The lid, and the rim band under it.
    line(draw, dome_points(lid_x0, lid_x1))
    line(draw, [(lid_x0, LID_Y), (lid_x1, LID_Y)])
    line(draw, [(lid_x0, RIM_Y), (lid_x1, RIM_Y)])
    line(draw, [(lid_x0, LID_Y), (lid_x0, RIM_Y)])
    line(draw, [(lid_x1, LID_Y), (lid_x1, RIM_Y)])

    # The body, standing in from the lid's overhang.
    line(draw, [(BODY_X0, RIM_Y), (BODY_X0, BODY_Y1)])
    line(draw, [(BODY_X1, RIM_Y), (BODY_X1, BODY_Y1)])
    line(draw, [(BODY_X0, BODY_Y1), (BODY_X1, BODY_Y1)])

    # The plinth and the two feet.
    line(draw, [(lid_x0, BODY_Y1), (lid_x0, PLINTH_Y), (lid_x1, PLINTH_Y), (lid_x1, BODY_Y1)])
    for foot_x0, foot_x1 in ((lid_x0 + 30, lid_x0 + 110), (lid_x1 - 110, lid_x1 - 30)):
        line(draw, [(foot_x0, PLINTH_Y), (foot_x0, FOOT_Y), (foot_x1, FOOT_Y), (foot_x1, PLINTH_Y)])

    # Two iron bands, each carried over the lid and down the body.
    for strap_centre in STRAP_X:
        for edge in (strap_centre - STRAP * 0.5, strap_centre + STRAP * 0.5):
            line(draw, [(edge, dome_y(edge))] + [(edge, y) for y in (LID_Y, RIM_Y, BODY_Y1, PLINTH_Y)])

    # The seam between the two boards the front is cut from. ONE, though a
    # chest this deep would have three: at the size the rail draws it, a body
    # ruled into equal courses reads as a chest of drawers.
    for seam_y in (500.0,):
        for x0, x1 in ((BODY_X0, STRAP_X[0] - STRAP * 0.5),
                       (STRAP_X[0] + STRAP * 0.5, STRAP_X[1] - STRAP * 0.5),
                       (STRAP_X[1] + STRAP * 0.5, BODY_X1)):
            line(draw, [(x0, seam_y), (x1, seam_y)], width=HAIR, amount=2.2)

    # The lock plate, hung off the rim, and its keyhole.
    line(draw, [(LOCK_X0, LOCK_Y0), (LOCK_X0, LOCK_Y1), (LOCK_X1, LOCK_Y1), (LOCK_X1, LOCK_Y0)])
    line(draw, [(LOCK_X0 - 6, LOCK_Y0), (LOCK_X1 + 6, LOCK_Y0)])
    keyhole_x = (LOCK_X0 + LOCK_X1) * 0.5
    draw.ellipse(
        [((keyhole_x - 13) * SS, (LOCK_Y0 + 34) * SS), ((keyhole_x + 13) * SS, (LOCK_Y0 + 60) * SS)],
        outline=INK, width=int(round(HAIR * SS)))
    line(draw, [(keyhole_x, LOCK_Y0 + 58), (keyhole_x, LOCK_Y1 - 16)], width=HAIR + 2)

    # The hasp: the lid's own tongue, reaching down over the lock. Closed
    # across the top, or the two sides read as a strap floating on the lid.
    hasp, under = 30.0, 30.0
    crown = [(x, dome_y(x) + under) for x, _ in dome_points(keyhole_x - hasp, keyhole_x + hasp, steps=8)]
    line(draw, [(keyhole_x - hasp, LOCK_Y0 - 4)] + crown + [(keyhole_x + hasp, LOCK_Y0 - 4)], amount=1.0)

    # Grain: a few short marks on the boards, the way the barrel wears its.
    for _ in range(9):
        x = RNG.uniform(BODY_X0 + 26, BODY_X1 - 26)
        y = RNG.uniform(RIM_Y + 30, BODY_Y1 - 26)
        if any(abs(x - centre) < STRAP * 0.5 + 14 for centre in STRAP_X):
            continue
        length = RNG.uniform(18, 46)
        line(draw, [(x, y), (x + length, y + RNG.uniform(-3, 3))], width=HAIR - 1.5, amount=1.0)


def build():
    canvas = Image.new("RGBA", (W * SS, H * SS), (INK[0], INK[1], INK[2], 0))
    draw_chest(ImageDraw.Draw(canvas))
    return canvas.resize((W, H), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="verify the committed plate, write nothing")
    args = parser.parse_args()

    if args.check:
        if not OUT.exists():
            print("missing: " + str(OUT))
            return 1
        with Image.open(OUT) as plate:
            if plate.size != (W, H) or plate.mode != "RGBA":
                print("unexpected " + str(plate.size) + " " + plate.mode + ": " + str(OUT))
                return 1
        print("ok: " + str(OUT))
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    build().save(OUT)
    print("wrote " + str(OUT))
    return 0


if __name__ == "__main__":
    sys.exit(main())
