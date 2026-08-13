#!/usr/bin/env python3
"""Draw the sand glass, the events rail's time-skip mark.

An authored ink drawing rather than a sourced plate, in the hand the journal's
other furniture already uses (`ui-chest.png`, `ui-cairn.png`): warm near-black
strokes on transparency, so the cell's paper shows through the picture rather
than a white card sitting on it. Nothing is owed for it, and a painted plate can
land over the same file any time.

A sand glass and not a clock face: the warden times an observation with one, and
the rail's cell is about hours the land works through rather than an hour of the
day. It is drawn part run, upper bulb half empty and a mound standing in the
lower, because a full glass reads as a thing not started and an empty one as a
thing finished, and this cell is neither.

    python tools/make-glass-plate.py            # write the plate
    python tools/make-glass-plate.py --check    # verify size/mode, write nothing

Requires Pillow. Everything below is drawn from these numbers, so a change is a
matter of moving one of them and running it again. The file is generated, and
hand-editing the PNG would be lost the next time anyone does.
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Resources" / "Art" / "UI" / "Journal" / "ui-glass.png"

# The ink the other line drawings are inked in, sampled off building-store.png.
INK = (34, 29, 23, 255)

W, H = 800, 900
SS = 4  # supersample, so the strokes keep their edge when the plate is cut down

MID = W * 0.5

# The frame: a plate at each end, and a post down each side between them.
PLATE_X0, PLATE_X1 = 96.0, 704.0
CAP_Y0, CAP_Y1 = 62.0, 128.0
BASE_Y0, BASE_Y1 = 772.0, 838.0
POST_X = (168.0, 632.0)

# The glass itself: the mouth each bulb opens to, and the waist they meet at.
# The waist is the midpoint between the two plates on purpose: the lower bulb is
# the upper one mirrored through it, so anywhere else and the mirror lands short
# of the base plate or drawn through it.
GLASS_X0, GLASS_X1 = 224.0, 576.0
WAIST_Y = (CAP_Y1 + BASE_Y0) * 0.5
WAIST_HALF = 24.0
# How hard the wall sweeps in. Above 1 it hugs the mouth and drops away late,
# which is the bulb's shape; at 1 it would be a plain funnel.
SWEEP = 1.7

# Where the sand stands. The upper level is a straight line across a bulb half
# run; the lower mound peaks under the stream.
UPPER_LEVEL_Y = 286.0
MOUND_PEAK_Y = 596.0

STROKE = 11.0  # the pen, in finished units
HAIR = 6.5     # the lighter pen, for the sand's hatching and the stream


def line(draw, points, width=STROKE):
    """One inked stroke. No wobble: at 72 units the whole plate is 64 across,
    and the nib waver the chest wears at 900 turns into a ragged edge here."""
    scaled = [(x * SS, y * SS) for x, y in points]
    draw.line(scaled, fill=INK, width=int(round(width * SS)), joint="curve")


def wall(y0, y1, mouth_x, steps=40):
    """One side of one bulb, from its mouth at y0 to the waist at y1."""
    points = []
    waist_x = MID - WAIST_HALF if mouth_x < MID else MID + WAIST_HALF
    for index in range(steps + 1):
        ratio = index / steps
        y = y0 + (y1 - y0) * ratio
        x = mouth_x + (waist_x - mouth_x) * ratio ** SWEEP
        points.append((x, y))
    return points


def wall_x(y, mouth_y, mouth_x, steps=40):
    """Where a wall stands at height y, for fitting the sand inside the glass."""
    span = WAIST_Y - mouth_y
    if abs(span) < 1e-6:
        return mouth_x
    ratio = min(1.0, max(0.0, (y - mouth_y) / span))
    waist_x = MID - WAIST_HALF if mouth_x < MID else MID + WAIST_HALF
    return mouth_x + (waist_x - mouth_x) * ratio ** SWEEP


def draw_glass(draw):
    # The end plates, each with the second rule that gives it its thickness.
    for y0, y1 in ((CAP_Y0, CAP_Y1), (BASE_Y0, BASE_Y1)):
        line(draw, [(PLATE_X0, y0), (PLATE_X1, y0)])
        line(draw, [(PLATE_X0, y1), (PLATE_X1, y1)])
        line(draw, [(PLATE_X0, y0), (PLATE_X0, y1)])
        line(draw, [(PLATE_X1, y0), (PLATE_X1, y1)])

    # The posts, standing between the plates.
    for post in POST_X:
        line(draw, [(post, CAP_Y1), (post, BASE_Y0)])

    # The four walls of the glass, drawn as two continuous strokes through the
    # waist so the neck is one line rather than four ends meeting in a knot.
    for mouth_x in (GLASS_X0, GLASS_X1):
        upper = wall(CAP_Y1, WAIST_Y, mouth_x)
        lower = [(x, WAIST_Y + (WAIST_Y - y)) for x, y in reversed(upper)]
        line(draw, upper + lower)

    # The sand still to fall: a level line, and hatching under it to the waist.
    left = wall_x(UPPER_LEVEL_Y, CAP_Y1, GLASS_X0)
    right = wall_x(UPPER_LEVEL_Y, CAP_Y1, GLASS_X1)
    line(draw, [(left + 6, UPPER_LEVEL_Y), (right - 6, UPPER_LEVEL_Y)])
    # Two marks under it, not five. The plate is 64 units across in the cell and
    # a bulb ruled into courses greys over into a smudge at that size.
    for step in (1, 2):
        y = UPPER_LEVEL_Y + (WAIST_Y - UPPER_LEVEL_Y) * step / 3.0
        inset = 12.0 + step * 4.0
        line(draw, [(wall_x(y, CAP_Y1, GLASS_X0) + inset, y),
                    (wall_x(y, CAP_Y1, GLASS_X1) - inset, y)], width=HAIR)

    # The stream, from the waist down onto the mound.
    line(draw, [(MID, WAIST_Y + 12), (MID, MOUND_PEAK_Y - 8)], width=HAIR - 1.5)

    # The mound it has made, and hatching down to the base.
    foot = BASE_Y0 - 8.0
    mound = []
    for step in range(41):
        ratio = step / 40.0
        x = wall_x(foot, BASE_Y0, GLASS_X0) + (wall_x(foot, BASE_Y0, GLASS_X1)
                                              - wall_x(foot, BASE_Y0, GLASS_X0)) * ratio
        # A cone, flattened at the tip the way poured sand stands.
        rise = (1.0 - abs(ratio - 0.5) * 2.0) ** 0.7
        mound.append((x, foot - (foot - MOUND_PEAK_Y) * rise))
    line(draw, mound)
    for step in (1, 2):
        y = MOUND_PEAK_Y + (foot - MOUND_PEAK_Y) * (step + 0.4) / 3.0
        half = (foot - y) / (foot - MOUND_PEAK_Y)
        span = (wall_x(y, BASE_Y0, GLASS_X1) - wall_x(y, BASE_Y0, GLASS_X0)) * 0.5 * (1.0 - half * 0.5)
        line(draw, [(MID - span + 8, y), (MID + span - 8, y)], width=HAIR)


def build():
    canvas = Image.new("RGBA", (W * SS, H * SS), (INK[0], INK[1], INK[2], 0))
    draw_glass(ImageDraw.Draw(canvas))
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
