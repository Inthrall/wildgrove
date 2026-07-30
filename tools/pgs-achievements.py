#!/usr/bin/env python3
"""Push the achievement ladder to Play Games Services, and generate its ids.

The console is a bad home for forty-five achievements: nothing about them is
reviewable, a renamed one leaves no trace, and the encoded ids have to be
hand-copied out of games-ids.xml into C#. So `store/play-games/achievements.json`
is the ladder, this pushes it through the Play Games Services Publishing API, and
the ids in code are written from the console's own replies rather than pasted.

    python tools/pgs-achievements.py                 # validate + read + plan, no writes
    python tools/pgs-achievements.py --apply         # insert what's missing
    python tools/pgs-achievements.py --apply --update  # also push drift onto existing
    python tools/pgs-achievements.py --validate      # manifest checks only, no network

Writes nothing to the console without --apply, because an achievement is a
public artifact: a draft can be deleted, but a published one carries player
data and stays.

Two things this deliberately cannot do, both confirmed against the current docs:

  * **Icons.** `imageConfigurations.upload` is deprecated with no replacement
    endpoint, and it is not merely discouraged — probed 2026-07-30, it answers
    **503 Service Unavailable** to every variant (both documented URL shapes,
    `uploadType=media` and `uploadType=resumable`) while a control GET on the
    configuration API returns 200 with the same token. The endpoint is gone.
    Icons are a manual console upload; there is no point writing a flag for it.
    Generate them with tools/make-store-art.py first — they are clipped to a
    circle, so the plate needs an inset.
  * **Publishing.** `published` is read-only on the resource and there is no
    publish method. This writes drafts; publishing the Play Games Services
    configuration stays a click in the console, and an unpublished achievement
    silently never unlocks on a device.

Auth is a personal Google access token holding the androidpublisher scope, read
from a file outside the repo (default Documents/Wildgrove-secrets/pgs-token.txt)
or the PGS_TOKEN environment variable. It is never printed. A token from the
OAuth Playground lasts an hour, which is long enough for the whole ladder.
"""
import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "store" / "play-games" / "achievements.json"
# Generated beside the manifest rather than into Assets/: the class it declares
# replaces the AchievementIds in ServiceIds.cs, and dropping a second copy into a
# live editor's compile would be CS0101 in someone's running session. Moving it
# into Assets/ is a deliberate step in the same change that removes the old one.
GENERATED = ROOT / "store" / "play-games" / "AchievementIds.g.cs"
DEFAULT_TOKEN = Path.home() / "Documents" / "Wildgrove-secrets" / "pgs-token.txt"

BASE = "https://www.googleapis.com/games/v1configuration"

# Play's rules for an achievement, worth failing on before a request rather than
# after forty-five of them: point values are multiples of 5 and never above 200,
# a game's total is capped (the current doc says 2000, older ones 1000 — hold to
# the smaller so the ladder is valid under either), names run to 100 characters
# and descriptions to 500.
MAX_POINTS_PER = 200
MAX_POINTS_TOTAL = 1000
MAX_NAME = 100
MAX_DESCRIPTION = 500


class ApiError(Exception):
    """A non-2xx from the Publishing API, with the body Google sent back."""


def load_manifest():
    with MANIFEST.open(encoding="utf-8") as handle:
        return json.load(handle)


def validate(manifest):
    """Every check that can be made without touching the network."""
    problems = []
    entries = manifest["achievements"]
    seen_slugs, seen_names = set(), set()
    total = 0

    for entry in entries:
        slug = entry.get("slug", "<no slug>")
        name = entry.get("name", "")
        description = entry.get("description", "")
        points = entry.get("points")

        if slug in seen_slugs:
            problems.append(f"{slug}: duplicate slug")
        seen_slugs.add(slug)

        # Names are how this script recognises an achievement it already made —
        # the console mints the id, so the name is the only stable key we own.
        if name in seen_names:
            problems.append(f"{slug}: duplicate name {name!r}, which would make matching ambiguous")
        seen_names.add(name)

        if not re.fullmatch(r"[a-z0-9-]+", slug or ""):
            problems.append(f"{slug}: slug must be lower-case kebab-case")
        if not name or len(name) > MAX_NAME:
            problems.append(f"{slug}: name must be 1-{MAX_NAME} characters")
        if not description or len(description) > MAX_DESCRIPTION:
            problems.append(f"{slug}: description must be 1-{MAX_DESCRIPTION} characters")
        if not isinstance(points, int) or points < 5 or points > MAX_POINTS_PER or points % 5:
            problems.append(f"{slug}: points must be a multiple of 5 from 5 to {MAX_POINTS_PER}")
        else:
            total += points

        if entry.get("type") not in ("STANDARD", "INCREMENTAL"):
            problems.append(f"{slug}: type must be STANDARD or INCREMENTAL")
        if entry.get("state") not in ("REVEALED", "HIDDEN", "UNLOCKED"):
            problems.append(f"{slug}: state must be REVEALED, HIDDEN or UNLOCKED")

        steps = entry.get("steps")
        if entry.get("type") == "INCREMENTAL":
            if not isinstance(steps, int) or steps < 2:
                problems.append(f"{slug}: an incremental achievement needs steps >= 2")
        elif steps is not None:
            problems.append(f"{slug}: steps is only meaningful on an INCREMENTAL achievement")

    if total > MAX_POINTS_TOTAL:
        problems.append(f"total point value {total} exceeds the {MAX_POINTS_TOTAL} budget")

    first_hour = [e for e in entries if e.get("firstHour")]
    if len(first_hour) < 4:
        problems.append(
            f"only {len(first_hour)} achievements are marked firstHour; Quest eligibility needs 4"
        )
    if len(entries) < 10:
        problems.append(f"only {len(entries)} achievements; the Level Up minimum is 10")

    return problems, total


def read_token(path):
    if os.environ.get("PGS_TOKEN"):
        return os.environ["PGS_TOKEN"].strip()
    if not path.exists():
        raise SystemExit(
            f"No access token. Put one in {path} or set PGS_TOKEN.\n"
            "Get one from https://developers.google.com/oauthplayground with the scope\n"
            "https://www.googleapis.com/auth/androidpublisher"
        )
    token = path.read_text(encoding="utf-8").strip()
    if not token:
        raise SystemExit(f"{path} is empty.")
    return token


def request(token, method, url, body=None):
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bearer {token}")
    if data:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as response:
            payload = response.read().decode("utf-8")
            return json.loads(payload) if payload else {}
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")
        raise ApiError(f"{error.code} {error.reason} on {method} {url}\n{detail}") from None


def fetch_existing(token, application_id):
    """Every achievement configuration the console already holds, by name."""
    by_name, page = {}, None
    while True:
        url = f"{BASE}/applications/{application_id}/achievements?maxResults=200"
        if page:
            url += f"&pageToken={page}"
        result = request(token, "GET", url)
        for item in result.get("items", []):
            by_name[localized(item.get("draft", {}).get("name"))] = item
        page = result.get("nextPageToken")
        if not page:
            return by_name


def localized(bundle):
    if not bundle:
        return ""
    translations = bundle.get("translations") or []
    return translations[0].get("value", "") if translations else ""


def body_for(entry, locale):
    body = {
        "achievementType": entry["type"],
        "initialState": entry["state"],
        "draft": {
            "name": {"translations": [{"locale": locale, "value": entry["name"]}]},
            "description": {
                "translations": [{"locale": locale, "value": entry["description"]}]
            },
            "pointValue": entry["points"],
        },
    }
    if entry["type"] == "INCREMENTAL":
        body["stepsToUnlock"] = entry["steps"]
    return body


def drift(entry, existing):
    """What differs between the manifest and the console's draft."""
    draft = existing.get("draft", {})
    differences = []
    if localized(draft.get("description")) != entry["description"]:
        differences.append("description")
    if draft.get("pointValue") != entry["points"]:
        differences.append(f"points ({draft.get('pointValue')} -> {entry['points']})")
    if existing.get("achievementType") != entry["type"]:
        differences.append(f"type ({existing.get('achievementType')} -> {entry['type']})")
    if existing.get("initialState") != entry["state"]:
        differences.append(f"state ({existing.get('initialState')} -> {entry['state']})")
    if entry["type"] == "INCREMENTAL" and existing.get("stepsToUnlock") != entry["steps"]:
        differences.append(f"steps ({existing.get('stepsToUnlock')} -> {entry['steps']})")
    return differences


def constant_name(slug):
    return "".join(part.capitalize() for part in slug.split("-"))


def emit_ids(manifest, ids):
    """Write the C# id constants from what the console actually holds."""
    lines = [
        "// Generated by tools/pgs-achievements.py from store/play-games/achievements.json",
        "// and the ids the Play Console replied with. Do not edit by hand: run the tool.",
        "",
        "namespace Wildgrove.Game.Services",
        "{",
        "    /// <summary>",
        "    /// Play Games Services achievement IDs — the encoded console ids, not the",
        "    /// resource names. Every constant here must be evaluated by",
        "    /// <see cref=\"Achievements\"/>; a test fails if one is not.",
        "    /// </summary>",
        "    public static class AchievementIds",
        "    {",
    ]
    listed = []
    for entry in manifest["achievements"]:
        identifier = ids.get(entry["name"]) or entry.get("id")
        if not identifier:
            continue
        name = constant_name(entry["slug"])
        listed.append(name)
        lines.append(f"        /// <summary>\"{entry['name']}\" — {entry['description']}</summary>")
        lines.append(f"        public const string {name} = \"{identifier}\";")
        lines.append("")
    lines.append("        /// <summary>Every achievement this build knows about.</summary>")
    lines.append("        public static readonly string[] All =")
    lines.append("        {")
    for name in listed:
        lines.append(f"            {name},")
    lines.append("        };")
    lines.append("    }")
    lines.append("}")
    GENERATED.parent.mkdir(parents=True, exist_ok=True)
    GENERATED.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return len(listed)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--apply", action="store_true", help="insert achievements that are missing")
    parser.add_argument(
        "--update",
        action="store_true",
        help="with --apply, also push manifest changes onto achievements that already exist",
    )
    parser.add_argument(
        "--include-existing",
        action="store_true",
        help="allow --update to touch entries marked existing:true (First kith was made by hand)",
    )
    parser.add_argument("--validate", action="store_true", help="manifest checks only, no network")
    parser.add_argument(
        "--delete-unpublished",
        action="store_true",
        help="delete every manifest achievement that the console holds as a draft only. "
             "Refuses anything carrying a published detail, so an achievement players "
             "can already unlock cannot be removed by this. Needs --apply.",
    )
    parser.add_argument("--token-file", type=Path, default=DEFAULT_TOKEN)
    args = parser.parse_args()

    manifest = load_manifest()
    problems, total = validate(manifest)
    entries = manifest["achievements"]
    print(f"manifest: {len(entries)} achievements, {total} points, "
          f"{len([e for e in entries if e.get('firstHour')])} marked first-hour")
    if problems:
        print("\nmanifest problems:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("manifest checks passed")
    if args.validate:
        return 0

    token = read_token(args.token_file)
    application_id = manifest["applicationId"]
    locale = manifest["locale"]

    try:
        existing = fetch_existing(token, application_id)
    except ApiError as error:
        print(f"\ncould not read the console:\n{error}")
        return 1
    print(f"console: {len(existing)} achievements already configured")

    ids = {name: item["id"] for name, item in existing.items()}
    inserted = updated = 0

    if args.delete_unpublished:
        # Bulk import is not an upsert: it rejects every name the configuration
        # already holds, so clearing the drafts is the only way to import a
        # ladder that was first pushed through this API. A published achievement
        # is never in scope — players may already hold it.
        deleted = 0
        for entry in entries:
            item = existing.get(entry["name"])
            if item is None:
                continue
            if item.get("published"):
                print(f"  . {entry['name']}: published, left alone")
                continue
            if not args.apply:
                print(f"  - {entry['name']}: would delete")
                continue
            try:
                request(token, "DELETE", f"{BASE}/achievements/{item['id']}")
            except ApiError as error:
                print(f"  ! {entry['name']}: delete failed\n{error}")
                return 1
            ids.pop(entry["name"], None)
            deleted += 1
            print(f"  - {entry['name']}: deleted")
        print(f"\n{deleted} deleted"
              + ("" if args.apply else "  (no writes — re-run with --apply)"))
        return 0

    for entry in entries:
        name = entry["name"]
        if name in existing:
            differences = drift(entry, existing[name])
            if not differences:
                print(f"  = {name}")
                continue
            locked = entry.get("existing") and not args.include_existing
            if args.apply and args.update and not locked:
                try:
                    result = request(
                        token, "PUT", f"{BASE}/achievements/{existing[name]['id']}",
                        body_for(entry, locale),
                    )
                except ApiError as error:
                    print(f"  ! {name}: update failed\n{error}")
                    return 1
                ids[name] = result["id"]
                updated += 1
                print(f"  ~ {name}: updated ({', '.join(differences)})")
            else:
                why = " [pass --include-existing to touch]" if locked else ""
                print(f"  ~ {name}: drift in {', '.join(differences)}{why}")
            continue

        if not args.apply:
            print(f"  + {name}: would insert")
            continue

        try:
            result = request(
                token, "POST", f"{BASE}/applications/{application_id}/achievements",
                body_for(entry, locale),
            )
        except ApiError as error:
            print(f"  ! {name}: insert failed\n{error}")
            return 1
        ids[name] = result["id"]
        inserted += 1
        print(f"  + {name}: {result['id']}")

    written = emit_ids(manifest, ids)
    print(f"\n{inserted} inserted, {updated} updated, {written} ids written to "
          f"{GENERATED.relative_to(ROOT)}")
    if not args.apply:
        print("(no writes — re-run with --apply)")
    else:
        print("Still manual: upload the icons, then publish the Play Games Services "
              "configuration in the console.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
