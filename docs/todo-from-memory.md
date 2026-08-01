# Carried over from Claude's memory store

Written 2026-08-01. Wildgrove notes used to live in Claude's OnePractice (work)
memory store; that store is now work-only, so anything unfinished had to come
back to the repo where it belongs.

**`docs/todo.md` is still the list.** Almost everything in those memories was
already tracked there — the playtest sitting, Warden's Sigil, the `aapt2`
verification, `type.pc`, the achievement drift, spreads 4–8, `res-flint`, the
Kurr amber plate, stat icons, Sidekick, the bubbles naming drift. What follows
is only the residue that had no home in `todo.md`, plus a short list of things
the memories called open that turn out to be done.

---

## Verified open — checked against the code on 2026-08-01

- **The Amber-drip rewarded ad unit is still a placeholder — CONSOLE STEP
  ONLY as of 2026-08-01.** `AdUnitIds.AmberDrip` is now an explicit alias of
  `TimeSkip` (`AmberDrip = TimeSkip`) with the blocker written on it, so the
  placeholder can't be misread as a unit of its own and there is exactly one
  constant to repoint. Still needs the unit created in AdMob before release —
  the two placements otherwise share fill, frequency capping and reporting, so
  the drip's earn rate is unmeasurable and each placement caps the other. Steps
  are in the release-blocker section of `todo.md`.

- ~~**The Deep Amber plate draws the wrong picture.**~~ ✅ RESOLVED
  2026-08-02. `ArtLibrary`'s `Line` map now keys `deep-amber` to
  `Plates/Insects/insect-deep-amber` — the fly in the resin, which is the whole
  of what the set is about — instead of the ordinary amber's photograph. The
  existing coverage passed throughout, because a key pointed at the wrong file
  still loads a sprite: `EveryLineMotif_DrawsItsOwnPlateRatherThanABorrowedOne`
  now pins all four motifs to their actual plate, so a borrow fails a test
  rather than drawing something plausible.
  **Left as a finding, not a change:** `res-amber` is now referenced by nothing
  in code — it joins `res-flint` as a plate that ships (everything under
  Resources/ does) and is never drawn. Its CC BY credit is therefore still
  required while the file is there. Deleting it would drop one of the five CC BY
  attributions outright, which is the cheaper version of the Kurr-coal-plate
  swap noted in `todo.md` — and the same deliberate pass, not a drive-by.
  ⚠️ Note while doing it: the store's amber icons derive from the
  *insect-deep-amber* source work (James St. John), **not** from `res-amber`
  (the Dolichoderus specimen tag) — `todo.md`'s note reads as though one work
  feeds both, and it doesn't.

- ~~**A failed IAP store connect can never report failure.**~~ ✅ RESOLVED
  2026-08-01. Both silent paths (`ConnectAsync`'s catch and
  `OnStoreDisconnected`) now release everyone queued behind the connection, via
  a new `StoreConnection` — the readiness/waiter state machine lifted out of
  `UnityIapStore` precisely because that class can't be constructed off a
  device, so the part with the states in it is now the part with the tests
  (`StoreConnectionTests`, 8). New `StoreResult.Unavailable` distinguishes "the
  store was never reached" from "the purchase was refused" at all three buy
  sites, and `IStore.RestorePurchases` gained an `Action<bool>` so the inside
  cover stops flashing "asked and answered" when Play was never asked, and the
  weekly-cache Look button stops reporting "nothing set out yet" when it never
  looked. A failed attempt is deliberately **not** remembered: the next press
  reconnects from the top.

- ~~**A 21.9 MB `firebase-app-unity-13.13.0.aar` is still committed**~~
  ✅ RESOLVED 2026-08-02 — **confirmed required, and it stays.** The "possibly"
  is now settled three ways: `settingsTemplate.gradle:28` points Gradle at
  `Assets/GeneratedLocalRepo/Firebase/m2repository`, so the committed copy is
  what every Android build resolves against; **no CI job runs the resolver**
  (`AndroidResolverRunner.ForceResolve` exists and nothing calls it, and EDM's
  auto-resolution does not run in batchmode), so deleting the files makes the
  build fail to find `firebase-app-unity` rather than regenerate them; and the
  bytes are already in history, so removing them would not shrink a clone by
  one byte. Fetching it "the way the rest are fetched" isn't available either —
  it is generated, not downloadable, though it *is* a byte-identical copy of the
  `.srcaar` inside the pinned tarball (verified: all three match).
  What the item was really pointing at was **drift** — the tarball version is
  pinned in `fetch-google-packages.sh`, the generated repo is pinned by whenever
  someone last resolved, and nothing connected the two, so a version bump
  without a re-resolve would silently link the OLD Firebase. The fetch script
  now checks the committed aars against the tarballs they came from and exits
  non-zero on a mismatch, naming the ForceResolve command to fix it. CI runs
  that script before every Unity invocation, so the check is already wired in.
  Both failure branches were exercised.

## Test coverage gaps

From the 2026-07-18 whole-codebase review: **`GameLoop` and `SaveFile` had no
fixtures at all** — the two seams where load/resume/autosave/offline-credit
ordering lives, and where every bug that has bitten (pause→resume never
crediting, the corrupt-save crash loop, the cloud reconcile baseline) was found
on a device rather than in the suite.

- ~~`SaveFile`~~ ✅ RESOLVED 2026-08-02 — `SaveFileTests`, 7 tests over the
  disk half that `RunPersistence`'s fake store deliberately doesn't reach: the
  round trip, the atomic replace (the branch only a *second* write takes, which
  is the one an autosave takes for the rest of the run), the `.corrupt` and
  `.newer` set-asides landing in their own slots with their bytes intact, and a
  failed write staying a logged error instead of taking the session down. They
  need a real disk, so `SaveFile.DirectoryOverride` was added to point them at a
  scratch directory — without it the fixture would write over the developer's
  own editor run, which is presumably part of why this never got written.
- **`GameLoop` still has no fixture, and the shape of the gap has changed.**
  Most of what the review was worried about has since been extracted into
  classes that *are* tested — `RunPersistence`, `Announcements`, `SessionLog`,
  `Achievements`, `Leaderboards`, `GameStats`, and now `SaveFile`. What is left
  in the MonoBehaviour is the **ordering between them**, and that is exactly
  where the remaining risk sits: `AdoptCloudRun` is eight steps that must happen
  in one breath (rebase stats, mark arrivals seen, drop the stale offline
  summary, credit the new absence, re-fold entitlements, save, notice), and
  `StartAgain` is four with the same property. Both are commented as sequences
  precisely because getting one out of order is silent. Testing them means
  either a PlayMode fixture or lifting the sequence into a plain class the way
  the others were lifted — the second is the pattern the file already follows.

## Called open in memory, actually already done

Recorded so they don't get re-raised:

- Amber-pack consumable icons — `store/iap/amber_pack_{small,large}-icon.png`
  both exist.
- The dangling-warden-post test — `SaveCodecTests.Restore_DanglingWardenPost_ClearsToCamp`.
- A `Modifiers` fixture — `Assets/Tests/EditMode/Sim/ModifiersTests.cs`.
- The legacy 96-familiar save freeze — `todo.md:116` records the Restore clamp
  as fixed.
- Unplated species (osier-otter, horseshoe-bat, ermine) — closed by the Round 7
  art pass (`0687b1f`).

## Only a device or the console can close these

Not repo work, but nothing else is tracking them:

- **Cloud Snapshots cross-device.** Single-device is confirmed; the
  most-played-wins reconcile has never been exercised against a second device,
  which is the only place it differs from newest-wins.
- **Ad serving for the newer placements** (amber drip, offline boost) on a real
  device.
- **The IAP purchase flow since the v5 API rewrite** — the rewrite and the R8
  billing keeps have not been re-tested together on a device.
- **Add Mo as a Play Console license tester** (Settings → License testing) so
  test purchases aren't charged, and install from the internal track rather
  than sideloading — billing is unreliable sideloaded.
