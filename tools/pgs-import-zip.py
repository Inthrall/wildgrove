#!/usr/bin/env python3
"""Build the Play Console bulk-import archive for the achievement ladder.

Play Console takes the whole ladder as one ZIP — Grow > Play Games Services >
Setup and management > Achievements > Import achievements — which is the only way
to set forty-five icons without forty-five trips through the edit form. The
`imageConfigurations.upload` API method that would have done it is dead (503; see
tools/pgs-achievements.py), so this is the bulk path that remains.

    python tools/pgs-import-zip.py            # build store/play-games/import/achievements-import.zip

Everything comes from store/play-games/achievements.json and the cards
tools/make-store-art.py generates, so the imported set, the API-pushed set and
the ids in code all descend from one manifest.

Play's rules for the archive, each of which this enforces rather than trusts:
  * exact filenames — `AchievementsMetadata.csv` and
    `AchievementsIconsMappings.csv` (Icons plural, *not* the help page's
    "AchievementsIconMappings") — and **no header row** on either;
  * `AchievementsLocalizations.csv` is for other languages only: a row in the
    game's default locale is rejected outright, so an English-only ladder must
    omit the file rather than restate itself in it;
  * AchievementsMetadata.csv is seven values per row — Name, Description,
    Incremental value (True/False), Steps Needed, Initial State (Hidden/Revealed),
    Points, List Order — and blanks where a value does not apply;
  * **no commas in a Name or a Description**, there being no quoting to escape
    them with;
  * icons 512x512 PNG/JPEG, each file under 1 MB, under 203 files, 200 MB total;
  * a flat archive — no subdirectories, and nothing in it but CSVs and images.

⚠️ **Import is not an upsert.** It refuses every name the configuration already
holds — "Duplicate names — remove or rename the duplicate names for the
following achievements", listing all of them — so a ladder that was first pushed
through the API has to be cleared before it can be imported
(`pgs-achievements.py --delete-unpublished --apply`, which cannot touch anything
published). Entries marked `existing` in the manifest stay out of the archive for
that reason.
"""
import argparse
import csv
import io
import json
import shutil
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "store" / "play-games" / "achievements.json"
CARDS = ROOT / "store" / "play-games"
OUT_DIR = ROOT / "store" / "play-games" / "import"
ARCHIVE = OUT_DIR / "achievements-import.zip"

MAX_FILE_BYTES = 1_000_000
MAX_FILES = 203
MAX_TOTAL_BYTES = 200 * 1024 * 1024


def rows(manifest):
    """The CSVs, as lists of already-validated rows.

    No AchievementsLocalizations.csv: that file carries *translations*, and the
    console rejects a row whose locale is the game's own default ("Wrong locale —
    choose a locale that is different than the game's default"). The default text
    already travels in AchievementsMetadata.csv, and the ladder is English only,
    so the file has nothing legal to hold. Add it back the day a second language
    exists, listing only that language.
    """
    metadata, icons = [], []
    problems = []

    for order, entry in enumerate(manifest["achievements"], start=1):
        # Import refuses any name the configuration already holds — it is not an
        # upsert. "First kith" was made by hand and is *published*, carrying real
        # unlocks, so it is never in the archive: it keeps its own card and the
        # import works around it.
        if entry.get("existing"):
            continue
        name, description = entry["name"], entry["description"]
        if "," in name or "," in description:
            problems.append(f"{entry['slug']}: a comma in the name or description")
        incremental = entry["type"] == "INCREMENTAL"
        icon = f"achievement-{entry['slug']}-512.png"
        if not (CARDS / icon).exists():
            problems.append(f"{entry['slug']}: no card at {icon} — run tools/make-store-art.py")

        metadata.append([
            name,
            description,
            "True" if incremental else "False",
            entry["steps"] if incremental else "",
            "Hidden" if entry["state"] == "HIDDEN" else "Revealed",
            entry["points"],
            order,
        ])
        icons.append([name, icon])

    return metadata, icons, problems


def write_csv(path, table):
    # Written with \r\n and no header, which is what the console's own exported
    # files use; csv.writer quotes nothing here because nothing needs quoting —
    # commas were rejected upstream.
    buffer = io.StringIO()
    csv.writer(buffer, lineterminator="\r\n").writerows(table)
    # newline="" or Windows turns each \r\n into \r\r\n, and every second line of
    # the file is then blank — which the console reads as a malformed row.
    path.write_text(buffer.getvalue(), encoding="utf-8", newline="")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--keep-staging", action="store_true",
                        help="leave the unzipped staging directory in place for inspection")
    args = parser.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    metadata, icons, problems = rows(manifest)
    if problems:
        print("cannot build the archive:")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    staging = OUT_DIR / "staging"
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)

    write_csv(staging / "AchievementsMetadata.csv", metadata)
    # "AchievementsIconsMappings" — Icons plural. The help page calls this file
    # AchievementsIconMappings and the console rejects that spelling outright
    # ("Incorrect CSV file name"), so the console's error text is the spec here.
    write_csv(staging / "AchievementsIconsMappings.csv", icons)
    for _, icon in icons:
        shutil.copy2(CARDS / icon, staging / icon)

    files = sorted(staging.iterdir())
    total = sum(f.stat().st_size for f in files)
    oversized = [f.name for f in files if f.stat().st_size > MAX_FILE_BYTES]
    if oversized:
        print(f"files over 1 MB: {', '.join(oversized)}")
        return 1
    if len(files) > MAX_FILES:
        print(f"{len(files)} files, over the {MAX_FILES} limit")
        return 1
    if total > MAX_TOTAL_BYTES:
        print(f"{total} bytes, over the 200 MB limit")
        return 1

    if ARCHIVE.exists():
        ARCHIVE.unlink()
    with zipfile.ZipFile(ARCHIVE, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in files:
            # arcname is the bare filename: the archive must be flat.
            archive.write(path, arcname=path.name)

    if not args.keep_staging:
        shutil.rmtree(staging)

    print(f"{ARCHIVE.relative_to(ROOT)}")
    print(f"  {len(metadata)} achievements, {len(files)} files, "
          f"{total / 1024 / 1024:.1f} MB unpacked, {ARCHIVE.stat().st_size / 1024 / 1024:.1f} MB zipped")
    print(f"  {sum(1 for r in metadata if r[2] == 'True')} incremental, "
          f"{sum(1 for r in metadata if r[4] == 'Hidden')} hidden, "
          f"{sum(r[5] for r in metadata)} points")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
