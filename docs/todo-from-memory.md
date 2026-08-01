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

- **The Amber-drip rewarded ad unit is still a placeholder.**
  `AdUnitIds.AmberDrip` (`Assets/Scripts/Game/Services/ServiceIds.cs:21`) is the
  *same id* as `TimeSkip`. Dev builds serve Google's test unit regardless, so
  this only bites in production: the two placements share fill, frequency
  capping and reporting. Needs its own rewarded unit in AdMob before release.

- **The Deep Amber plate draws the wrong picture.** `ArtLibrary`'s `Line` map
  (`Assets/Scripts/Game/ArtLibrary.cs:189`) keys `deep-amber` to
  `Resources/Art/Plates/Resources/res-amber` — the resource photograph.
  `insect-deep-amber.png` exists and the Play Console store card already uses
  it; only the in-game key was never repointed.

- **A failed IAP store connect can never report failure.** `ConnectAsync`'s
  catch (`UnityIapStore.cs:95`) and `OnStoreDisconnected` (`:122`) both only
  `Debug.LogError`. Neither calls `FinishReady()`, so `_ready` stays false
  forever and the callback queued by `Initialise(() => Purchase(…))` is
  orphaned — the buy button silently no-ops with no way to surface the fault.
  The comment at `:214` already describes the behaviour ("the retry simply
  never fires"). This is the failure shape that hid the UGS-unlinked bug for
  days; worth a `FinishReady()` on both paths plus a `StoreResult` the UI can
  show.

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
