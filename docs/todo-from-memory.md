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

- **The Deep Amber plate draws the wrong picture.** `ArtLibrary`'s `Line` map
  (`Assets/Scripts/Game/ArtLibrary.cs:189`) keys `deep-amber` to
  `Resources/Art/Plates/Resources/res-amber` — the resource photograph.
  `insect-deep-amber.png` exists and the Play Console store card already uses
  it; only the in-game key was never repointed.

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

- **A 21.9 MB `firebase-app-unity-13.13.0.aar` is still committed** under
  `Assets/GeneratedLocalRepo/Firebase/…`. It was left in deliberately when the
  61 MB Google tarballs were stripped from history (possibly required for the
  CI resolve) and flagged as a follow-up that was never revisited. Either
  confirm CI needs it and note why, or fetch it the way
  `tools/fetch-google-packages.sh` fetches the rest.

## Test coverage gaps

Both from the 2026-07-18 whole-codebase review, skipped as out of scope then
and still true: **`GameLoop` and `SaveFile` have no fixtures at all.** They are
the two seams where load/resume/autosave/offline-credit ordering lives, and
every bug that has bitten there (pause→resume never crediting, the corrupt-save
crash loop, the cloud reconcile baseline) was found on a device rather than in
the suite.

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
