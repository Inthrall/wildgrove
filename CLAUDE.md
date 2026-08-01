# CLAUDE.md

Guidance for Claude Code working in this repository. Conventions and traps only —
the *what* lives in the docs below, and the open work lives in the todo manifest.

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
| `Wildgrove.Sim` | `noEngineReferences: true`. Pure C# — the whole game's state and rules. **Do not add a `UnityEngine` dependency here**; it's what makes the sim testable without the editor. |
| `Wildgrove.Data` | Content defs + the `GameData` ScriptableObject. Sim depends on it; it depends on nothing of ours. |
| `Wildgrove.Game` | MonoBehaviours, UI, services (store, ads, PGS, saves). Depends on Sim + Data. |
| `Wildgrove.BuildTools` | Build-time helpers; `Assets/Editor/` holds the editor-only tooling. |

**If logic is hard to test because it lives in a MonoBehaviour, lift it into a plain
class** — that is the pattern the codebase already follows (`RunPersistence`,
`Announcements`, `SessionLog`, `Achievements`, `Leaderboards`, `GameStats`,
`StoreConnection` were all lifted out of `GameLoop` / `UnityIapStore` for exactly this
reason). `GameLoop` still holds multi-step sequences whose *ordering* is the risk —
`AdoptCloudRun` (eight steps) and `StartAgain` (four). Getting one out of order fails
silently, so extend them with care and prefer lifting the sequence out over adding to it.

## Content is data, not code

Resources, upgrades, recipes, gear, rites, species, dialogue and the economy are
authored as JSON in [design/data/](design/data/), validated by `GameDataValidator`, and
imported into `Assets/Resources/Data/GameData.asset` by `GameDataImporter` — auto on
editor load and build, or **Wildgrove → Import Design Data**.

- Balancing must never require a code change.
- `GameData.asset` is committed. **A JSON or schema change and its re-imported
  `GameData.asset` belong in the same commit**, or the running game and the authored
  data disagree.

## Saves

`SaveCodec` (`Assets/Scripts/Sim/Saves/`) owns the versioned save. Any change to the
persisted shape means: bump `SaveCodec.CurrentVersion`, add the matching case to the
sequential migration `switch`, and make sure `Restore` copes with the old data
(clamping, dedupe, resting things that no longer fit). Test saves from before a
migration are the cheapest way to find what `Restore` missed.

`SaveFile` writes to disk with an atomic replace and sets aside `.corrupt` / `.newer`
files. Tests that need real disk use `SaveFile.DirectoryOverride` to point at a scratch
directory — without it the fixture overwrites the developer's own editor save.

## Tests

EditMode NUnit only, under `Assets/Tests/EditMode/{Sim,Game,BuildTools}` with one
`.asmdef` per area. Run them from the Unity Test Runner window, or in CI
(`.github/workflows/android-build.yml` and `android-release.yml`, via game-ci —
`android-release.yml` gates the signed Play build on a green suite).

Prefer a test that pins the *specific* thing over one that only proves nothing threw:
a mis-keyed sprite still loads a sprite, so `ArtLibraryTests` pins each motif to its own
plate rather than asserting "something drew".

## Art, plates and attribution

`ArtCredits` lists the CC BY source works, each with the modification made — the licence
obliges credit by name and a statement of change, and every one of them must appear in
the shipped credits. Removing a plate from `Resources/` therefore also drops an
attribution: that's a deliberate pass with the credits screen open, never a drive-by
deletion. Note that everything under `Resources/` ships whether or not code references it.

## Third-party Android packages

The Google/Firebase tarballs in `Packages/manifest.json` are **not committed** — run
`tools/fetch-google-packages.sh` after cloning, and CI runs it before every Unity
invocation.

`Assets/GeneratedLocalRepo/Firebase` **is** committed (22 MB) and is what every Android
build resolves against — no CI job runs the EDM4U resolver, so deleting those `.aar`s
breaks the build rather than regenerating them. The fetch script checks them against the
tarballs they came from and fails on drift. **After bumping a package version in
`fetch-google-packages.sh`, re-resolve** or the build silently links the old Firebase:

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
