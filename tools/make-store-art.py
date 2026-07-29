#!/usr/bin/env python3
"""Build the Play Console artwork from the committed naturalist plates.

Every card is the same journal furniture — parchment ground, a doubled ochre
rule, faint foxing — with one plate centred on it, so the store reads as part of
the book rather than a separate thing. The template values below were measured
off the hand-made `store/iap/kith_slot-icon.png` rather than guessed, which is
why the generated cards sit in the same set as the five that predate this tool.

    python tools/make-store-art.py           # write every card
    python tools/make-store-art.py --check    # verify sizes/mode, write nothing
    python tools/make-store-art.py amber      # only cards whose name matches

Requires Pillow. Sources are all committed plates under Assets/Resources/Art/,
so a clone can reproduce the set exactly.

Play's rules for these cards, worth not breaking:
  * no text and no numerals in the artwork — a store icon carries its words in
    the product's name field, never burnt into the picture;
  * no promotional call-outs or badges;
  * 1:1, 32-bit PNG, sRGB, between 512 and 1080 px;
  * not a white ground, or the card's own border disappears.
The parchment is deliberately off-white for that last reason.
"""
import argparse
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
PLATES = ROOT / "Assets" / "Resources" / "Art" / "Plates"
STORE = ROOT / "store"

PARCHMENT = (238, 228, 204, 255)
RULE = (146, 124, 90, 255)
FOXING = [(230, 219, 194), (234, 224, 199), (232, 221, 197)]

# Rule radii as a fraction of the card, from the measured 464/445 of 1024.
OUTER_R, OUTER_W = 464 / 1024, 5 / 1024
INNER_R, INNER_W = 445 / 1024, 3 / 1024
SS = 4  # supersample, so the thin rules stay smooth


class Card:
    """One piece of console artwork."""

    def __init__(self, out, plate, size=1024, plate_fraction=0.547, centre=0.5, inset=0.0,
                 vignette=False):
        self.out = out
        self.plate = plate
        self.size = size
        # Some plates were cut with the subject running off-frame, leaving a
        # straight edge that reads as a torn-off rectangle on a card — and worse
        # on an achievement, which Play clips to a circle. A soft elliptical
        # falloff dissolves it without touching the plate the game loads.
        self.vignette = vignette
        # Google asks the main artwork to sit near half the card; 0.547 is the
        # measured 560/1024 of the hand-made cards.
        self.plate_fraction = plate_fraction
        self.centre = centre
        # Achievement cards are displayed clipped to a circle, so their rules
        # pull in or the border is sliced off.
        self.inset = inset


CARDS = [
    # ── In-app products ────────────────────────────────────────────────────
    Card("iap/reward_drovers_halter-icon.png", "Familiars/familiar-pony", centre=0.493),
    Card("iap/reward_weekly_amber_cache-icon.png", "Insects/insect-deep-amber", plate_fraction=0.561),
    # The awarded plate wears its own moth, because the reward *is* a plate —
    # anything else on this card would advertise the wrong thing. Cut from
    # Scott, "Australian Lepidoptera and their Transformations" (1864) Plate 4,
    # drawn from nature by Helena Scott: a hand that really did walk somewhere
    # first, which is the fiction the reward is built on.
    Card("iap/reward_wayfarers_plate-icon.png", "Insects/insect-wayfarers-plate"),
    # ── Play Games Services ───────────────────────────────────────────────
    # "First kith": the vole is the seed familiar and the first friend the
    # fiction gives you, so it is the one that belongs on this card.
    Card("play-games/achievement-first-kith-512.png", "Familiars/familiar-vole",
         size=512, plate_fraction=0.50, inset=0.08, vignette=True),
]


def foxing(size, rng):
    spots = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(spots)
    for _ in range(45):
        x, y = rng.randrange(size), rng.randrange(size)
        r = rng.randint(max(2, size // 256), max(5, size // 79))
        draw.ellipse((x - r, y - r, x + r, y + r),
                     fill=rng.choice(FOXING) + (rng.randint(18, 38),))
    return spots.filter(ImageFilter.GaussianBlur(size / 170))


def ground(size, inset):
    # Seeded per card size so a re-run reproduces the same card byte for byte.
    im = Image.new("RGBA", (size, size), PARCHMENT)
    im = Image.alpha_composite(im, foxing(size, random.Random(1910)))

    rings = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(rings)
    c = size * SS // 2
    for r_frac, w_frac in ((OUTER_R, OUTER_W), (INNER_R, INNER_W)):
        r = int((r_frac - inset) * size * SS)
        draw.ellipse((c - r, c - r, c + r, c + r), outline=RULE,
                     width=max(SS, int(w_frac * size * SS)))
    return Image.alpha_composite(im, rings.resize((size, size), Image.LANCZOS))


def soften(plate):
    """Fade a plate's edges so a straight crop line stops reading as a tear."""
    from math import sqrt
    w, h = plate.size
    alpha = plate.getchannel("A")
    px = alpha.load()
    cx, cy, rx, ry = w * 0.55, h * 0.45, w * 0.66, h * 0.62
    for y in range(h):
        for x in range(w):
            if not px[x, y]:
                continue
            d = sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)
            k = min(1.0, max(0.0, (1.0 - d) / 0.26))
            px[x, y] = int(px[x, y] * k)
    plate = plate.copy()
    plate.putalpha(alpha.filter(ImageFilter.GaussianBlur(max(1, w // 220))))
    return plate


def build(card, check):
    src = PLATES / (card.plate + ".png")
    if not src.exists():
        print("MISSING PLATE  %s  (wanted by %s)" % (src.relative_to(ROOT), card.out))
        return False

    plate = Image.open(src).convert("RGBA")
    if card.vignette:
        plate = soften(plate)
    longest = card.plate_fraction * card.size
    scale = longest / max(plate.size)
    plate = plate.resize((max(1, round(plate.width * scale)), max(1, round(plate.height * scale))),
                         Image.LANCZOS)

    im = ground(card.size, card.inset)
    im.alpha_composite(plate, ((card.size - plate.width) // 2,
                               round(card.centre * card.size) - plate.height // 2))

    dest = STORE / card.out
    if check:
        if not dest.exists():
            print("ABSENT   %s" % card.out)
            return False
        have = Image.open(dest)
        ok = have.size == (card.size, card.size) and have.mode == "RGBA"
        print(("OK       " if ok else "WRONG    ") + "%s  %sx%s %s"
              % (card.out, have.size[0], have.size[1], have.mode))
        return ok

    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest)
    print("wrote    %s  %sx%s  plate %sx%s" % (card.out, im.size[0], im.size[1],
                                               plate.size[0], plate.size[1]))
    return True


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("match", nargs="?", help="only cards whose output path contains this")
    ap.add_argument("--check", action="store_true", help="verify what is on disk, write nothing")
    args = ap.parse_args()

    cards = [c for c in CARDS if not args.match or args.match in c.out]
    if not cards:
        print("no cards match %r" % args.match)
        return 1

    return 0 if all(build(c, args.check) for c in cards) else 1


if __name__ == "__main__":
    sys.exit(main())
