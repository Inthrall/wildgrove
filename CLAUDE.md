# CLAUDE.md

Guidance for Claude Code working in this repository. Conventions and traps only —
the *what* lives in the docs below, and the open work lives in the todo manifest.

## The repo is public, and a build push wants it that way

`Inthrall/wildgrove` is **public** (confirmed 2026-09-10), so an ordinary push needs nothing done first. It was set **private** on 2026-08-26 at Mo's request and has been restored since. **If it is ever made private again, put it back to public before pushing builds**, and treat pushing while it is private as a decision to put to Mo first rather than a default.

```bash
gh api repos/Inthrall/wildgrove --jq .private   # check rather than assume

GH_TOKEN="$(gh auth token --hostname github.com --user Inthrall)" \
  gh repo edit Inthrall/wildgrove --visibility public --accept-visibility-change-consequences
```

The `GH_TOKEN` prefix is needed because `gh`'s active account on this machine is the work one, which has no rights here. Note also that both Android workflows trigger on a push, and GitHub-hosted Actions minutes are metered against the free-plan quota on a private repo while a public one is unlimited, so the flip is a CI question as well as a disclosure one.

## Where things are written down

| Document | What it is |
|---|---|
| [docs/design-doc.md](docs/design-doc.md) | The spec — systems, economy math, narrative register, Level Up compliance, MVP phases. Decisions are marked **DECIDED \<date\>**; treat those as settled unless Mo reopens them. |
| [docs/todo.md](docs/todo.md) | **The list — the only one.** Every deliberate placeholder and deferred item, grouped by the phase that retires it. New deferrals go here, pointing at the code so they can be found and deleted when resolved. |
| [docs/dev-setup.md](docs/dev-setup.md) | Editor version, modules, Player Settings, first-run checklist. |

Mark items resolved in place (`~~struck~~ ✅ RESOLVED <date>`) rather than deleting
them — a closed item is what stops it being re-raised.

## Stack

Unity 6.5 (6000.5.x **Supported** stream, pinned in `ProjectSettings/ProjectVersion.txt`)
· URP 2D · Android, **Vulkan first** · Play Games Services v2 · AdMob · Unity IAP.
Currencies are `BigDouble` (BreakInfinity, vendored at `Assets/Plugins/BreakInfinity/`),
never `double` or `long`.

## Assembly layering — keep the sim engine-free

| Assembly | Rule |
|---|---|
| `Wildgrove.Sim` | Pure C# — the whole game's state and rules. **Do not add a `UnityEngine` dependency here**; it's what makes the sim testable without the editor. |
| `Wildgrove.Data` | Content defs + the `GameData` ScriptableObject. Sim depends on it; it depends on nothing of ours. |
| `Wildgrove.Game` | MonoBehaviours, UI, services (store, ads, PGS, saves). Depends on Sim + Data. |
| `Wildgrove.BuildTools` | Build-time helpers; `Assets/Editor/` holds the editor-only tooling. |

**That first rule is kept by hand — nothing enforces it.** `Wildgrove.Sim`'s own
files are clean, but its asmdef says `noEngineReferences: false` and cannot say
otherwise: the sim takes `GameDataAsset` in nearly every signature, that derives
from `ScriptableObject`, and the compiler needs `UnityEngine.CoreModule` to
resolve the base type. Setting the flag true fails with `CS0012` across
`Exchange`, `Folio`, `Almanac` and more (tried 2026-08-02). So a stray
`Time.deltaTime` or `UnityEngine.Random` in the sim would compile and ship, and
the determinism every file header claims would be quietly gone — read the rule
as one to uphold in review, not one the build will catch. Closing it properly
means lifting a plain-C# `GameData` out of the ScriptableObject; see
`todo.md` § Sim purity is a convention, not a constraint.

**If logic is hard to test because it lives in a MonoBehaviour, lift it into a plain
class** — that is the pattern the codebase already follows (`RunPersistence`,
`Announcements`, `SessionLog`, `Achievements`, `Leaderboards`, `GameStats`,
`RunSwap`, `StoreConnection` were all lifted out of `GameLoop` / `UnityIapStore` for
exactly this reason). The two sequences that replace the run wholesale — adopting a
cloud save (eight steps) and starting the book again (four) — are `RunSwap`, which
reaches the scene back through the `IRunHost` seam `GameLoop` implements explicitly.
**Their ordering is the risk and every way of getting it wrong is silent**, so a new
step goes into that sequence with a `RunSwapTests` assertion pinning where it sits,
never into `GameLoop`.

## Long classes are split into partials, not left to grow

The big classes are one class across several files, named `Type.Topic.cs`, and the
bare `Type.cs` keeps the entry point and a doc comment listing the parts. `GameHud`
(7 files), `GameLoop` (6), `JournalSheets` (11), `GameDataValidator` (10), `TrailPage`
(6), `CampPage` (4) and `SaveCodec` (4) all follow it.

- **Put a new member in the partial that owns its topic**, not in the bare `Type.cs`.
  That file is an index; it grows only when the thing being added really is the entry
  point.
- **The base type and the constructor live in the bare `Type.cs`** — every other
  partial declares the class name alone. Only one file may name the base, so a split
  that moves the declaration silently drops `: JournalSection` from the whole class,
  and the errors then land in whichever *other* partial happens to use an inherited
  member first.
- Nothing enforces the topic grouping. It survives by the doc comment at the head of
  each partial saying what belongs there, so update that comment when the shape of the
  file changes.
- Asmdefs glob by folder, so a new partial needs no project edit — only its `.meta`,
  which Unity writes on next editor load.

A method too long to read is a different problem and a partial does not solve it:
`SaveCodec.Restore` is 553 lines in a file of its own, and splitting it means finding
real seams in a single pass over every field of the save, not moving it.

## Content is data, not code

Resources, upgrades, recipes, gear, rites, species, dialogue and the economy are
authored as JSON in [design/data/](design/data/), validated by `GameDataValidator`, and
imported into `Assets/Resources/Data/GameData.asset` by `GameDataImporter` — auto on
editor load and build, or **Wildgrove → Import Design Data**.

- Balancing must never require a code change.
- `GameData.asset` is committed. **A JSON or schema change and its re-imported
  `GameData.asset` belong in the same commit**, or the running game and the authored
  data disagree.
- **`sourceHash` is not reproducible, so a dirty `GameData.asset` is not evidence of
  drift.** Merely opening the editor rewrites that one line while leaving every
  imported value byte-identical (seen 2026-08-13, against a JSON and asset committed
  together in `f3832eb`). Diff the file before assuming a re-import changed anything:
  if `sourceHash` is the only hunk, discard it rather than committing it as a data
  change.

## Saves

`SaveCodec` (`Assets/Scripts/Sim/Saves/`) owns the versioned save, split across
`SaveCodec.cs` (the two version constants and the JSON pair), `.Capture`, `.Restore`
and `.Migrations`. Any change to the persisted shape means all three of the latter:
bump `SaveCodec.CurrentVersion`, add the matching case to the sequential migration
`switch`, and make sure `Restore` copes with the old data (clamping, dedupe, resting
things that no longer fit). Test saves from before a migration are the cheapest way
to find what `Restore` missed.

**The ladder runs 42→55.** `CurrentVersion` is 55 and `EarliestReadableVersion` is
42: a v42 save climbs every rung — the clock ratchet (43), the warden's name (44),
the camp's name (45), the consideration pair (46), the second queue (47), the
keepsakes (48), the Wheel's hemisphere + claims (49), the keeping (50), the
keepsakes dropped again (51), the kith ladder onto named verses (52), the watch as
a place (53), the watch renamed to the sketching (54), the confirmations a
delivered reward is still owed (55) — and is read whole. Most
rungs add nothing but a version, which is the shape to copy: a migration fills in
only what its version predates, and never reaches for current content data.

A rung that fills nothing in is still worth adding (51, 53 and 55 are all this): it is
what makes an older build refuse a save this one wrote, rather than read it a field
short. 52 is the exception that shows the rule — it had to carry earned kith places
forward, so it writes the *old* milestones out as literals rather than reading the
current economy, because a migration has to hold for a save opened years after those
numbers stopped existing. 54 is the other kind of exception: it rewrites persisted
ids in place (`dig:{zone}` → `sketch:{zone}`) and is safe to, because it carries the
zone half through untouched and so consults no content data. Note what it must NOT
touch — `SavedStation.stationId` is the craft queue's workbench, sharing a name with
a post id and nothing else.

Add a rung the same way — a case, and bump `CurrentVersion` only — and **leave
`EarliestReadableVersion` where it is**. It moves again only when bottom rungs are
deliberately dropped, and raising it is a decision about whose saves stop working.

Anything below the floor is refused whole rather than half-read — a save loaded without
its migrations looks healthy and is quietly wrong.

## The clock is a ratchet

Every cooldown and the whole offline credit are read through `ClockGuard.Now`, which
never returns less than the highest reading the run has ever seen (`clockHighWaterUnixMs`,
saved and carried across the fold). Winding the device clock forward pays out once and
then buys nothing until real time catches the mark up; winding it back is not seen at
all. **Anything new that reads the wall clock must go through `GameLoop.NowUnixMs()`**
— not `IClock` directly — or it becomes the one unratcheted door into the Amber economy.

## Long absences are credited a slice per frame

`OfflineCatchUp` carries an absence of five minutes or more (`DeferThresholdSeconds`)
across frames, because the tick sub-steps at one second and the away cap reaches twelve
hours — 43,200 steps in one call is a stall. The welcome-back sheet waits on
`GameLoop.CatchingUp`, so the work happens behind the sheet rather than in front of a
locked frame. Slices are always whole seconds, which is what makes a sliced catch-up
land byte-for-byte where an unsliced one would (`OfflineCatchUpTests` pins it) — keep
that property if you touch the slicing.

`SaveFile` writes to disk with an atomic replace and sets aside `.corrupt` (unreadable),
`.newer` (from a future build) and `.legacy` (below the floor) — separate slots so one
can't overwrite another, and so a healthy save is never called corrupt. Tests that need
real disk use `SaveFile.DirectoryOverride` to point at a scratch directory — without it
the fixture overwrites the developer's own editor save.

## Tests

EditMode NUnit only, under `Assets/Tests/EditMode/{Sim,Game,BuildTools}` with one
`.asmdef` per area. Run them from the Unity Test Runner window, or in CI
(`.github/workflows/android-build.yml` and `android-release.yml`, via game-ci —
`android-release.yml` gates the signed Play build on a green suite).

Prefer a test that pins the *specific* thing over one that only proves nothing threw:
a mis-keyed sprite still loads a sprite, so `ArtLibraryTests` pins each motif to its own
plate rather than asserting "something drew".

**`Assert.Multiple` does not exist here.** Unity's bundled NUnit (`com.unity.ext.nunit`,
engine 3.5) predates it, so the grouped-assertion idiom fails to compile with
`CS0117: 'Assert' does not contain a definition for 'Multiple'`. Write sequential
`Assert.That(...)` calls, each with its own message — which is the shape every fixture
here already uses.

## Art, plates and attribution

`ArtCredits` lists the CC BY source works, each with the modification made — the licence
obliges credit by name and a statement of change, and every one of them must appear in
the shipped credits. Removing a plate from `Resources/` therefore also drops an
attribution: that's a deliberate pass with the credits screen open, never a drive-by
deletion. Note that everything under `Resources/` ships whether or not code references it.

## Third-party Android packages

The Google/Firebase tarballs in `Packages/manifest.json` are **not committed**, and
neither is `Assets/GeneratedLocalRepo/Firebase` (22 MB), the maven repo every Android
build resolves Firebase from. Both come from `tools/fetch-google-packages.sh` — run it
after cloning, and CI runs it before every Unity invocation.

The script generates that repo rather than checking a committed copy, because what
EDM4U's Force Resolve does for these three artifacts is a byte-for-byte copy of the
`.srcaar` inside each package. Generating it is what makes a version bump safe: change
`firebase_version` in the script and the matching lines in `manifest.json`, re-run, and
the repo follows. **The `.meta` files it writes are load-bearing** — an `.aar` under
`Assets/` with no `.meta` is imported as a plugin as well as resolved by Gradle, and the
build fails on duplicate classes. `FirebaseRepoGuard` fails an Android build whose repo
is missing, stale against the manifest, or has lost those metas.

`AndroidResolverRunner.ForceResolve` still exists for the resolvers that write nothing
here (Play Games, AdMob), which resolve from `maven.google.com` at build time rather than
from a local repo:

```bash
Unity -batchmode -quit -projectPath . -executeMethod Wildgrove.EditorTools.AndroidResolverRunner.ForceResolve
```

The upload keystore is never committed (`.gitignore` blocks `*.keystore` / `*.jks`);
CI signs from `ANDROID_KEYSTORE_BASE64`. Art binaries go through Git LFS.

## What the repo cannot close

Some work is console- or device-only, gives no error when undone, and looks exactly like
working code — AdMob ad units and the GDPR consent message, Play Console products, Play
Games achievement definitions, license testers, and anything needing a second physical
device. These are written out step by step in `todo.md` (§ Release blockers) rather than
left as "console visit". Don't try to fix them in code, and don't assume a clean log
means they're done.

## Commit style

Sentence-style imperative in the game's own voice, describing what changed for the
player or the code, e.g. *"Let the store say when it cannot be reached, and link the
policy in full"*. No ticket prefixes, no `Co-Authored-By` trailers. Ask before
committing.
