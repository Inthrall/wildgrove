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
import csv
import json
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

# One plate per achievement, because the Play Console refuses to publish a
# configuration in which two achievements share an icon — a generic card for all
# of them is not an option, whatever the bulk-import docs imply about icons being
# optional. Keyed by the slug in store/play-games/achievements.json; every slug
# there must appear here or the run fails, so the ladder can't grow an
# achievement that has nothing to wear. Plates are chosen for what the
# achievement is about rather than assigned in order — the resource you first
# gather, the building you first light, the bone-beds you find in The Hollows.
ACHIEVEMENT_PLATES = {
    # First hour
    "first-harvest": "Resources/res-berries",
    "first-friends": "Familiars/familiar-squirrel",
    "off-the-beaten-path": "Resources/res-nuts",
    "fire-and-fruit": "Buildings/building-fire",
    "green-hands": "Goods/goods-seedling",
    # Gathering
    "a-full-basket": "Goods/goods-basket",
    "the-long-haul": "Gear/gear-pack",
    "laden-trails": "Familiars/familiar-raven",
    "the-grove-gives": "Resources/res-wildflowers",
    # Crafting
    "the-whole-camp-working": "Buildings/building-bench",
    "steady-hands": "Goods/goods-tools",
    "stores-overflowing": "Buildings/building-store",
    # Kith — first-kith keeps the vole card made by hand before this table
    "first-kith": "Familiars/familiar-vole",
    "a-second-bond": "Familiars/familiar-hedgehog",
    "six-at-post": "Buildings/building-roosts",
    "well-known": "Familiars/familiar-hare",
    "inseparable": "Familiars/familiar-owl",
    "the-whole-wood": "Familiars/familiar-weasel",
    # Compendium and Folio
    "choice": "Resources/res-crystals",
    "fixed-in-ink": "Resources/res-rare-herbs",
    "a-spread-complete": "Resources/res-herbs",
    "half-the-folio": "Resources/res-mushrooms",
    "the-whole-folio": "Zones/keystone-aurora-bloom",
    "the-full-cabinet": "Resources/res-eggs",
    # Observation
    "first-sketch": "Insects/insect-silver-skimmer",
    "a-plate-recorded": "Insects/insect-stags-herald",
    "all-five-plates": "Insects/insect-those-who-sow",
    "something-older": "Resources/res-amber",
    "the-deep-pages": "Insects/insect-deep-amber",
    # The Rite
    "first-verse": "Zones/keystone-lantern-firefly",
    "the-rite-complete": "Zones/keystone-echo-geode",
    "five-verses": "Resources/res-reeds",
    "twenty-five-verses": "Resources/res-sky-blossoms",
    # Migration — the tarp because folding the camp is what a fold is
    "the-first-fold": "Gear/gear-tarp",
    "three-folds": "Familiars/familiar-songbird",
    "ten-folds": "Zones/keystone-cloudfleece-ram",
    "verdant": "Goods/goods-trellis",
    # Trails and waystones
    "reader-of-stones": "Resources/res-flint",
    "every-stone-read": "Zones/keystone-amber-snail",
    "into-the-hollows": "Resources/res-bone",
    "cloudreach": "Resources/res-glacier-ice",
    # Camp and Almanac
    "steel-in-hand": "Goods/goods-ingot",
    "the-almanac-opens": "Goods/goods-planks",
    "the-almanac-complete": "Zones/keystone-moonscale-trout",
    "a-fuller-grove": "Resources/res-timber",
}


# One plate per Game Stat, on the same rule the achievements keep: Play asks for
# "a unique icon representing the stat", and there is no reason to find out the
# hard way whether it enforces that as strictly. Keyed by the Stat Id in the two
# config CSVs, so a stat cannot be added without an icon to wear. None of these
# plates is used by an achievement card either — both sets are read on the same
# gamer profile, and a stat wearing an achievement's picture would look like a
# mistake even where it is allowed.
GAMESTAT_PLATES = {
    # What the flock carried home, so the creel rather than the basket the
    # "a-full-basket" achievement already wears.
    "resources_gathered": "Gear/gear-creel",
    # The forge is the one crafting building no achievement took.
    "goods_crafted": "Buildings/building-forge",
    # Lights caught in flight, for the windfall that drifts off if it isn't.
    # (`insect-windborne` is the better word and the wrong file: it has no alpha
    # at all, so it lands as a white box on the parchment. See the warning in
    # build() — it is not the only plate cut that way.)
    "windfalls_caught": "Insects/insect-lantern-bearers",
    # The specimen fixed into the Folio, which is a court gone quiet.
    # (`insect-parchment-wings` names the idea exactly and is the same white box.)
    "specimens_fixed": "Insects/insect-quiet-court",
    # A verse is answered by ground opening, which is what a keystone is.
    "verses_sung": "Zones/keystone-sunburst-poppy",
    # The camp set down again further on: a seed, not a tarp.
    "migrations": "Zones/keystone-ancient-acorn",
    # Ground stood in, told the way ground tells it: lichen is what grows on the
    # waystones a warden has already read. (`gear-torch` was the first choice and
    # renders as a lamp whose chimney is cropped flat by its own frame, which the
    # elliptical fade cannot reach on a plate that tall.)
    "trails_walked": "Resources/res-lichen",
}

GAMESTATS = ROOT / "store" / "play-games" / "gamestats"


def gamestat_cards():
    """A card per stat in the console configs, named by the CSV's own icon column.

    Google's icon rules here are stricter than the store's: **exactly** 512x512,
    PNG or JPEG, at most 1 MB, and the files sit in the root of the uploaded ZIP
    beside the two config CSVs, which is where these are written. They keep the
    achievements' circular inset and vignette because nothing published says what
    shape a stat icon is displayed in, and an inset ring is harmless on a square
    card while a sliced-off rule is not.
    """
    stats = []
    for name in ("RepetitiveStatsConfig.csv", "ProgressionStatConfig.csv"):
        with (GAMESTATS / name).open(encoding="utf-8", newline="") as handle:
            for row in csv.DictReader(handle):
                stats.append((row["Stat Id"], row["Icon File Name"]))

    missing = [stat for stat, _ in stats if stat not in GAMESTAT_PLATES]
    if missing:
        raise SystemExit(
            "No plate assigned for stat: " + ", ".join(missing)
            + "\nAdd them to GAMESTAT_PLATES — every stat in the config needs its own icon."
        )

    plates = [GAMESTAT_PLATES[stat] for stat, _ in stats]
    clashes = {plate for plate in plates if plates.count(plate) > 1}
    if clashes:
        raise SystemExit(
            "Plate used by more than one stat: " + ", ".join(sorted(clashes))
            + "\nPlay asks for a unique icon per stat."
        )

    return [Card("play-games/gamestats/" + icon, GAMESTAT_PLATES[stat],
                 size=512, plate_fraction=0.50, inset=0.08, vignette=True)
            for stat, icon in stats]


def achievement_cards():
    """A card per achievement in the manifest, in its order."""
    manifest = json.loads(
        (ROOT / "store" / "play-games" / "achievements.json").read_text(encoding="utf-8")
    )
    slugs = [entry["slug"] for entry in manifest["achievements"]]

    missing = [s for s in slugs if s not in ACHIEVEMENT_PLATES]
    if missing:
        raise SystemExit(
            "No plate assigned for: " + ", ".join(missing)
            + "\nAdd them to ACHIEVEMENT_PLATES — Play will not publish a duplicate icon."
        )
    plates = [ACHIEVEMENT_PLATES[s] for s in slugs]
    clashes = {p for p in plates if plates.count(p) > 1}
    if clashes:
        raise SystemExit(
            "Plate used by more than one achievement: " + ", ".join(sorted(clashes))
            + "\nPlay rejects a configuration where two achievements share an icon."
        )

    cards = []
    for slug in slugs:
        # "First kith" was drawn by hand before this table existed and is already
        # uploaded; regenerating it would only churn the file.
        if slug == "first-kith":
            continue
        cards.append(Card(f"play-games/achievement-{slug}-512.png", ACHIEVEMENT_PLATES[slug],
                          size=512, plate_fraction=0.50, inset=0.08, vignette=True))
    return cards


CARDS += achievement_cards()
CARDS += gamestat_cards()


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


def opaque_edged(plate):
    """True when the plate's own border is solid, i.e. it was cut without alpha."""
    alpha = plate.getchannel("A")
    width, height = plate.size
    px = alpha.load()
    edge = ([px[x, 0] for x in range(width)] + [px[x, height - 1] for x in range(width)]
            + [px[0, y] for y in range(height)] + [px[width - 1, y] for y in range(height)])
    return sum(1 for value in edge if value > 200) > len(edge) // 2


def build(card, check):
    src = PLATES / (card.plate + ".png")
    if not src.exists():
        print("MISSING PLATE  %s  (wanted by %s)" % (src.relative_to(ROOT), card.out))
        return False

    plate = Image.open(src).convert("RGBA")
    if card.vignette:
        # soften() fades the alpha it is given, so a plate cut with no alpha at
        # all cannot be dissolved: it lands as a white rectangle on the parchment
        # with softened corners, which is worse than a straight edge because it
        # looks deliberate. Three achievement plates are cut that way and are
        # already published under those cards, so this warns rather than fails —
        # replacing a published achievement icon is a console decision, and Play
        # will not accept two achievements sharing one.
        if opaque_edged(plate):
            print("WHITE BOX  %s  (%s has no alpha to fade)" % (card.out, card.plate))
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
