# Open work

What is left, and nothing that is done. Trimmed 2026-08-02 from a 2,200-line
manifest that had become mostly a build log — the history of *what* shipped and
*why* lives in git and in `design-doc.md`; this file is only the outstanding
list. Entries point at code so they're easy to find and delete when resolved.

Three lists: **MVP** (in-repo work before release), **Bugs & fixes** (defects and
drift), **Outside-code** (console, device, manual). Two appendices: deferred
post-MVP scope, and the standing constraints that are not work items but would
cost a build cycle each to re-learn.

---

## 1. MVP

### 1.1 The playtest gate — the one blocking item

**A run-3-to-run-6 sitting.** With Cloudreach Peaks built (2026-08-01) the map is
walked out and there is no content slice in front of it. Every mid/late number is
**model-derived, never played**: the fold gate, `demandGrowth` 1.45, the breadth
ramp, Almanac costs, the zones 4–6 pass, the crags and peaks passes, and the
zone-demand geometric ramp. Model estimate to judge against: a debut verse holds
at ~20–60 min of its own node's production; FTP walks the fold-4 Rite in ~9–12
days, a committed payer ~4–6.

Knobs the sitting is allowed to move, by file:

| Area | Knobs |
|---|---|
| `economy.json` — kith | `verseMilestones` [2,5,10]; `generatorGatherPosts` 2; `gifts.pileGoods` 10 |
| `economy.json` — familiars | `familiarXp` {base 60, growth 1.12, xpPerSecond 1}; `signatureMilestones` [2,4,7]; `signatureDeepening` 0.25; Roosts `comfort` 0.1/level |
| `economy.json` — loop | `burstYieldMult`/`burstDurationSec`; `warden.gatherPerSecond` 0.5; `bubbles.*`; `baseCarryCapacity`/`tripSeconds`/`basketCapacity`; `selfHaulTripMultiplier` |
| `economy.json` — away cap | the whole ladder, rescaled 2026-08-03 to run `offline.baseCapHours` 2 → `offline.maxCapHours` 12. The rungs that walk it: Root Cellar 3 / Smokehouse 5 (`upgrades.json`), The Long Watch I·II·III 4/6/9 and the Almanac Desk +1 (`almanac.json`), the Oilskin Tarp +1 (`gear.json`), the Old-Growth Bounty spread +1 (`folio.json`), the Store line's +0.25/level (`buildings.json`). **A test pins the authored kit to exactly 12** (`AwayLadder_RunsFromTheBaseCapToTheCeilingAndNoFurther`), so moving any one rung means moving another or the ceiling. Two questions for the sitting: whether a 2 h first night reads as *earn your sleep* or as punishment before the Root Cellar is affordable, and whether the Store's per-level hours saturate so early that the line stops being worth levelling |
| `economy.json` — craft/skill | `baseCraftSeconds` 5 + the eight per-recipe `craftSeconds`; `xp.gatherPerUnit` 1; `xp.craftPerBatch` 25; mastery base 50 / growth 1.15 / xpPerUnit 0.25 |
| `economy.json` — amber | `digFindsPerHour` 0.06, perFind 2, skip 4h/15; `timeSkipDailyCapHours` 24 (the deliberate ×2 whale throttle). **Read 0.06 as the whole rate now**: the roll went flat and once-per-round on 2026-08-02 (was per site × `digSpeedMult`, which compounded to ~7/hour on a six-site map behind the full watch stack). The passive earn no longer grows with the map at all, so the sitting's question is whether 0.06 is now too *mean* — the drip and the weekly cache are carrying the free player |
| `zones.json` | every `minMigration` (1/2/3/4/5 — first guesses); zone 7–8 prices 90/140/70 and 300/220 |
| `rites.json` | the ×2.5-per-zone ramp (soften to ×2 if slow; ×3 walls the crags); the specimen slots' own ×1.6-per-zone ramp (1 · 2 · 3 · 5 · 8 across zones 4–8, added 2026-08-03 — the question is whether the peaks' 8 reads as an ask or as a raid on the Folio's fodder); `spotlightDiscount` 0.6 / `offSpotlightPremium` 1.5; `chooseCountPerMigrations`/`chooseCountMax` |
| `almanac.json` | one-off tree total 178; granted-chain costs 6/10/14/22 + 8; `costGrowth.almanac` 1.25; The Long Song / The Long Reach rates |
| `upgrades.json` | haul rungs stag-harness 8 / wagon 14 (moved down 2026-07-28, unconfirmed); building `perLevel` 5% tapers |
| `tinctures.json` | four brews at 1200 s; the cordial's +1 choice point |
| `ambers.json` | `findsPerHour` 0.1, `pityHoursWatched` 12 |
| `regions.json` | four regions, all effect values |
| `insects.json` | rarities (Apollo 0.4, Windborne 0.25, Quiet Court 0.2) |
| `exchange.json` | `offerMinutes` 5; flat spread across quality tiers |
| `folio.json` | spread/effect sizes |
| `GameLoop`/`GameHud` | autosave 30 s; welcome-back 60 s credited |

Three structural questions the sitting also answers:

- **Deepsteel-as-door.** Zones 7 and 8 both wait on forgecraft 40 (~1,770 batches,
  offline-resumed by The Fire Remembers) *and* the fold gate. If the two gates
  stack too harshly the lever is the **crags' `requiredTool` back to steel** — not
  the forgecraft curve, and not a ninth tool tier.
- **Folds 2–4 still shorten**, because the fold-to-fold power ratio is ~3× there.
  That is the Verdure curve (`verdure.exponent` 0.5 / `renownDivisor` 2800), not
  the Rite. Deliberately not retuned — flattening it would kill the +2%/pt
  passive, and the endless Almanac lines are the better answer to the same
  symptom. Revisit only if folds 2–4 feel *hollow* rather than fast.
- **The peaks' four final waystones are one stone per fold.** Whether four folds
  is the right span for the ending is a playtest question; the knob is the stone
  count.

### 1.2 UI surfaces still missing

The sim-vs-journal audit is otherwise closed. What still pays out unseen:

- **No page for cross-run collection.** `speciesEverBefriended` and
  `stationsEverWorked` have no reader — the roster is this-run only and there is
  no in-game achievements screen. **Bonds** are the third kind of Compendium
  entry still unlisted. (`Sim/Compendium.cs`)
- **The Ladder card windows to the next 3 rungs**; the shape of the tree is never
  visible, where the Almanac lists revealed tiers. (`CampPage`)
- **A live tincture is only visible on the Warden tab** — no global buff
  indicator. (`Sim/Tinctures.cs`)
- **No aggregate camp production view** — per-resource rates exist only on node
  cards; the only rollup is the trail's gather-vs-carry shortfall line.
- **Tending's Choice window is invisible.** `Simulation.Tend` opens the 30 s
  `choiceBonusRemaining` window and the HUD gives no cue. (`GameHud`)
- **Verse cards don't render the spotlight (✳) marker.**
- **The Migration vignette shows the Verdure gain only** — per-familiar Kinship
  gains aren't itemised, and Kinship is legible everywhere else now.
- **Kith post buttons are a 4-column grid** sized for MVP station counts. Eight
  zones exist now; this was flagged to revisit "when zones multiply" and they have.
- **Inside-cover discoverability.** Settings sit at the bottom of the Record page
  with no gear in the chrome. Right for the book, unusual for a phone game — if a
  playtester can't find it, the answer is a corner mark on the Record tab, not a
  pinned bar.
- **Zone folding, one beat to watch:** the moment the second zone unlocks, the
  meadow's plates disappear behind a heading for the first time. It names its
  resources and looks pressable, but that is the one place a player could think
  their nodes are gone. (`JournalZones.cs`)

### 1.3 Audio — the whole pass

There is **no audio anywhere in the project**: no `AudioSource`, no clip, not one
file. That is why the inside cover has no volume control — it would be a slider
wired to nothing. The settings row lands with the audio pass.

### 1.4 Narrative & art

- **Every §7-register line in the game is a first draft.** Waystones, verse lines,
  plate lore, the 24 Kinship inscriptions (~190 of the 1,200-word budget), the
  crags/peaks/final-waystone chains, and the inside cover's wording. Re-voice
  before release.
- **Most of the ~1,200-word budget is unwritten.** Still absent: Provisioner
  trigger lines (first-visit / after-migration), and lines for **generated**
  verses — runs 2+ reuse the zone's site with no authored words, so the narrative
  pass has to decide what a run-3 verse *says*.
- **Waystones are a modal, not a world object.** Tappable waystones in the world
  are still the intent; the sheet stands in.
- **The Compendium has no plates or entry text** — the system layer with lifetime
  counters and discovery is live, the art and words aren't.
- **`design-doc.md` is behind the build in two places:** §5/§8 still describe
  tap-to-tend (windfall bubbles replaced it 2026-07-24), and the §6 lore / §7
  backstory written for fossils were only lightly reframed for the
  observe·sketch·release rework.

### 1.5 Systems tails

- **`GameLoop` has no test fixture, and the gap has narrowed to ordering.**
  `RunPersistence`, `Announcements`, `SessionLog`, `Achievements`, `Leaderboards`,
  `GameStats` and `SaveFile` are all extracted and tested. What is left in the
  MonoBehaviour is the sequencing between them — `AdoptCloudRun` is eight steps
  that must happen in one breath, `StartAgain` is four with the same property, and
  getting either out of order is silent. Lift the sequences into plain classes (the
  pattern the rest already follows) rather than writing a PlayMode fixture.
- **Folio spreads are 2–4 entries; design wants 4–8.** A balance pass, deferred
  since the Folio landed.
- **No `postMatch` multiplier (design §8).** Familiar XP is a flat per-second at
  any post; Roosts comfort and Kinship are the only XP-rate levers.
- **No observation skill XP.** Sketches are too rare for per-unit XP; decide a
  grant when tool-tier or level gates need the level.
- **The amber sink is the time-skip alone.** Cosmetics and extra craft queues were
  the other two, and cosmetics have **no substrate at all** — no skin/wardrobe
  system, no warden or familiar sprite. That absence is what retired the cosmetic
  reward cloak.
- **The kit bag has nothing to reward.** With Pitch Torch and Clay-Lined Creel
  moved into the Almanac, the kit is back to one piece per slot, so the swap is
  inert until new gear ships.
- **`Bootstrap` spawns GameLoop + GameHud via `[RuntimeInitializeOnLoadMethod]`.**
  Replace with a real bootstrap scene when there is content to lay out.
- **Some species have no acquisition path but bonds** — the non-node species
  (dray-stag, tawny-owl, cavern-bat) have no gift pile to be called by. Future
  arrival content.
- **Crafting and gifts spend only plain stock.** A run holding only Decent berries
  can't gift. Probably right, but revisit with balance.

### 1.6 The Warden's Sigil — ship it or cut it

Design-doc-only (§11 IAP, ~US$7); **not in the built store catalogue**. Effect
size **decided 2026-08-01: +20% yields and craft speed**, down from ×2 (at ×2 it
halved the paid floor to ~2–3 days for the whole map and became a second pace
stacked on the skip budget's deliberate ×2). Still open: **whether it ships at
all**, and whether ~US$7 is right for a perk this quiet — a cheaper Sigil, or one
folded into a bundle with Amber and cosmetics, may be the honest answer. If it
ships it needs a permanent `yieldMult` + craft-speed entitlement path: there is
**none** today, and nothing in `KithPurchases.Apply` / `StoreProductIds` grants a
sim modifier.

---

## 2. Bugs & fixes

- **Three published incremental achievements have drifted from the data they
  count.** Each needs the same three-place fix, landing together: Play Console
  step count → `store/play-games/achievements.json` → `Achievements.cs`. Batch
  them into one console visit (the Sep 1 Rewards visit is the natural one).
  - **"The Almanac Complete"** — 14 steps; the tree is now **22** one-off nodes.
    It unlocks at 14 while promising "buy every node the Almanac holds".
  - **"The Whole Wood"** — 12 steps; the kea and pika took the roster to **14**.
  - **"All Five Plates"** — 5 steps; **7** plates are drawable. The name itself
    rots, so either rename in the console or leave the count and reword the
    description to "the first five".
  - Add with them a test pinning each constant to its data-derived count (the
    constants must stay hardcoded because the console holds the same figure) —
    `Achievements.StepTarget` and
    `AchievementsTests.EveryStoneRead_TurnsOverOnTheLastZoneTheDataActuallyHas`
    are the pattern. Worth doing for **"Reader of Stones" (4)** too, if that
    number ever means anything other than "some of them".
- **The "Pristine" achievement is renamed in the manifest but not in the
  console.** The grades are Poor / Decent / Choice from 2026-08-02, so
  `store/play-games/achievements.json` now names the achievement **"Choice"**
  ("Find your first Choice specimen", slug `choice`) and `AchievementIds.g.cs`
  carries the same encoded id under the new constant. `pgs-achievements.py`
  matches the console **by display name**, so until the console entry is renamed
  by hand a `--apply` run would read "Choice" as missing and insert a duplicate.
  Rename it in the console first, then re-run the tool; the id itself never
  changes, so unlocks in the field are unaffected either way. Re-upload
  `store/play-games/achievement-choice-512.png` in the same visit (icons are a
  manual upload — see the tool's header). Batch with the drifted-step visit above.
- **`AdUnitIds.AmberDrip` is an alias of `TimeSkip`.** The drip and the skip share
  one ad unit: one fill pool, one frequency cap, one reporting row. Nothing breaks
  — the drip's earn rate just becomes unmeasurable and each placement quietly caps
  the other. Dev builds serve Google's test unit regardless, so it only ever shows
  in production numbers. One-literal code change, gated on the console visit in
  §3.1. (`Services/ServiceIds.cs`)
- **EEA players are served ads having been asked nothing.** The code side is done
  — `AdMobAds.GatherConsent` runs `ConsentInformation.Update` →
  `LoadAndShowConsentFormIfRequired` before any ad request, and the inside cover
  re-opens the form. It is **inert until a GDPR message is published** in the
  AdMob console: the SDK has no form to load, `PrivacyOptionsAvailable` stays
  false, the row stays hidden, and there is no error anywhere. See §3.1.
- **Sim purity is a convention with nothing enforcing it.** `CLAUDE.md` states
  `Wildgrove.Sim` = `noEngineReferences: true`; the asmdef flag is **`false`**, and
  flipping it does not compile — Sim takes `GameDataAsset` in nearly every
  signature, that derives from `ScriptableObject`, and the compiler needs
  `UnityEngine.CoreModule` to resolve the base type (`CS0012` in `Exchange`,
  `Folio`, `Almanac` and more; tried 2026-08-02, reverted). The files honour the
  rule by discipline, but a `UnityEngine.Random` or `Time.deltaTime` would compile
  and ship. Closing it for real means a plain-C# `GameData` runtime type with the
  ScriptableObject reduced to a wrapper the Game layer unwraps at load — a
  signature change across most of the sim, so it wants its own pass, not a flag
  flip. Until then `CLAUDE.md` describes intent, not the build.
- **`GameDataValidator` hardcodes the skills vocabulary** as a C# `HashSet` rather
  than sourcing it from data.
- **`zone.unlocks` means two different things** depending on the zone: the
  starting zone's list seeds `UnlockedSkills`, every other zone's only informs
  `RiteGenerator.SkillDebutOrder` pacing (so a typo in a late zone's list silently
  moves that skill's debut and re-paces every run-2+ Rite). The `final-waystones`
  sentinel would be cleaner in a field of its own — a data-schema change, so it
  needs a `GameData.asset` re-import in the same commit.
- **The import-time validator doesn't check the authored run-1 Rite for a
  stationing-aware ≥`chooseCount`.** The guarantee lives in the generator plus the
  runs-2–10 proof in `RiteGeneratorTests`, which is where *generated* rites are
  checkable — the authored one is a cheap follow-up.
- **Three naming seams left open on purpose, worth closing on touch:**
  `digSite`/`DigSpeed` still carry the old excavation vocabulary as the shared
  site/speed plumbing (reinterpreted in comments); `BubbleWorldView` and
  `economy.bubbles` still say "bubble" where the UI says windfall; and
  `GameDataValidator`'s `KnownSkills` whitelist still lists the retired
  `"excavation"` — that one is now load-bearing as the last stable
  never-granted-skill test example, so leave it.
- **Kinship constants are hardcoded.** `Divisor` 1000 and `XpRatePerLevel` 0.02
  are `const`s in `Kinship.cs`; they belong in an `economy.json` section with the
  rest of the tuning.
---

## 3. Outside-code

Nothing here can be done from the repo, and most of it is inert-until-done in a
way that gives no error: the game runs, the logs are clean, and the thing simply
doesn't work.

### 3.1 AdMob console — release blockers

**The Amber-drip rewarded unit** (fixes the alias bug in §2):

1. AdMob → **Apps** → Wildgrove (`com.inthrall.wildgrove`) → **Ad units** →
   **Add ad unit**.
2. Format **Rewarded**. Name to match the other two (`Wildgrove Time Skip` /
   `Wildgrove Offline Boost`) → **`Wildgrove Amber Drip`**.
3. **Reward amount 1, item "amber"**. Never read — the game grants the drip from
   `economy.json`, not the ad payload — but AdMob requires the fields, and a
   nonsense value in the console is a thing to misread later.
4. Leave frequency capping **off**. The drip's own cooldown is the throttle; a
   second one in the console would be invisible from the code.
5. Copy the unit id (`ca-app-pub-6903871125040514/…`) and replace the
   `AmberDrip = TimeSkip` alias in `ServiceIds.cs`, restoring its XML doc line.
   That is the whole code change.
6. New units take **a few hours** to start serving. A fresh unit returning no-fill
   on the first device test is expected, not a fault.

**The GDPR/consent message:**

1. AdMob → **Privacy & messaging** → **GDPR** → **Create message**.
2. Select Wildgrove; leave the default regions (EEA + UK).
3. Consent options: **Consent / Manage options / Do not consent**. The third
   button matters — without it, it's the "consent or leave" pattern Google has
   been rejecting.
4. Privacy policy URL → `https://decryptic.app/wildgrove/privacy` (the same URL
   the Play listing and the inside cover use).
5. Style it and **Publish**. Unpublished messages do not load.
6. Repeat under **Privacy & messaging → US states** if the app is listed there —
   same shape, separate message.
7. Verify on device with a debug geography override (`ConsentDebugSettings`, EEA)
   or a VPN. Confirm: the form shows on first launch, no ad request precedes it,
   and **Ad privacy choices** then appears on the inside cover.

### 3.2 Play Console

- **Create the two store products** — `starter_bundle` (slot + 30 Amber, one-time)
  and `kith_slot`, both NonConsumable. `StoreProductIds.All` already names them;
  without the console entries the ladder's two purchasable slots cannot be bought.
- **Attach a Play Games Reward offer to each of the three reward products.** The
  products (`reward_drovers_halter`, `reward_weekly_amber_cache`,
  `reward_wayfarers_plate`) are created and activated. **The association UI and
  reward testing do not open until Sep 1 2026**, and the Level Up bar for ≥2
  single-use rewards is **Sep 30 2026** — a one-month window. (≥1 repeatable by
  **Mar 1 2027**; the weekly cache is it.)
- **Do not create `reward_wayfarers_cloak`.** The cosmetic reward was retired
  unbuilt — it wanted a cosmetic substrate the game has never had. A test pins
  that the id is uncatalogued, so an award of it could never be acknowledged.
- **Re-step the three drifted achievements** (§2) in the same visit.
- **Add Mo as a license tester** (Settings → License testing) so test purchases
  aren't charged.
- **Keep Sidekick on for CI uploads.** Sidekick is added at *upload* time for App
  Bundles. Every release here is uploaded by `android-release.yml` via
  `r0adkll/upload-google-play`, so Testing → Advanced settings → **Play Games
  Sidekick** → *"Automatically make Sidekick on by default for new app bundles"*
  must be set, or each CI upload lands Sidekick-less and the guideline quietly
  fails.

### 3.3 On device — the build is not proven until these are done

- **Cloud Snapshots cross-device.** Single-device confirmed 2026-07-28. The
  most-played-wins reconcile has never met a *second* device, which is the only
  place it differs from newest-wins — i.e. the whole of what `AdoptCloudRun`
  exists for is untested.
- **The IAP purchase flow since the v5 API rewrite.** The rewrite and the R8
  billing keeps have not been re-tested *together*; either alone passing says
  nothing about the pair. Install from the **internal track**, never sideloaded —
  billing is unreliable sideloaded, and a sideloaded failure is indistinguishable
  from a real one.
- **Ad serving for the newer placements** (amber drip, offline boost). Dev builds
  serve Google's test unit regardless, so a placement that never fills in
  production looks fine everywhere else.
- **The real out-of-app reward delivery.** `StubStore.DeliverReward` exercises
  grant → acknowledge and the refusal branch in the editor; a live Quest award
  can't be tried before Sep 1. What *is* checkable now: an internal-track build's
  catalogue fetch should resolve every reward id with a price. An id coming back
  unavailable means the console entry and `RewardProductIds` disagree — the one
  failure that would silently swallow every future award.
- **The pad / keyboard / large-screen gate.** Play it through on real 4:3, 16:10,
  21:9 and foldable hardware with a controller in hand. Keyboard and controller
  navigation is built and tested; it has never been *held*.
- **Verify the input declarations in the built AAB**, not just in the unit tests —
  they pin the transform, not the Gradle merge. `aapt2 dump badging` on the next
  release AAB should list every feature and **none as required**.
- **The inside cover has had no device pass.** It is the newest sheet and the
  longest; the scroll clamp is what keeps it on a phone screen, and the privacy
  row lands in the middle of it.
- **Play Games on PC, in Google's developer emulator** — mouse-only,
  keyboard-only, pad, and a window resize/maximise. **Mo's call and Mo's machine**:
  the emulator wants virtualisation on a work machine, so it is handed over rather
  than done here.

### 3.4 Blocked on Google

- **Game Stats cannot submit.** Six stats plus one progression level are chosen
  and wired; `PlayGamesServices.RecordStat` counts what it could not send and says
  so in logcat. GPGS 2.1.0 (July 2025, still the newest release) has no
  `PlayerGameEvent` and no `RecordEvent`, and the Java coordinate is unpublished
  so there is nothing to reach over JNI either. **API GA July 2026; Play Console
  CSV upload opens August 2026.** When the plugin lands it becomes three lines
  (`new PlayerGameEvent.Builder(name)` → `.AddProperty` → `RecordEvent`) and
  nothing else moves.
  - Console side is authored and waiting in `store/play-games/gamestats/`. Two
    knowingly-unfinished parts: the **stat icons aren't drawn** (Google has
    published no size spec — use `tools/make-store-art.py` once the console says
    what shape), and the column values (`HIGHER`, the free-text units) are the
    guide's documented spellings, not ones a console has accepted. Expect one
    correction round. `EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema`
    fails if code and CSV drift, because Play drops undeclared events silently.

---

## Appendix A — deferred, post-MVP

- **v1.1 species abilities**, more bonds, and world sprites for companions.
- **Cosmetics** as an amber sink — needs a substrate that doesn't exist (§1.5).
- **Tool-tier gating of *recipes*** (§4) — waits on skill-level design.
- **Roster capacity from late Roosts levels** (§4) — deliberately not built;
  headcount stays the slot ladder's job. Revisit only if the ladder grows sources.
- **A bond earned with no slot open** still fires its celebration while the
  companion waits. Unreachable at MVP (sources ≤ slots at every rung); revisit if
  sources ever outpace slots.
- **`compendium_entry_discovered` telemetry** — skipped because offline catch-up
  would burst-fire it.
- **A TMP font swap** (SDF crispness, real style faces). The four OFL faces render
  through Unity's dynamic-font path, so bold/italic are synthesized and exotic
  glyphs fall back to OS fonts — which is why the HUD avoids ✎/★/✓/→.
- **`NumberFormat`'s suffix table** (`K, M, B, T`, then `aa, ab, …`, then
  scientific) is first-pass; revisit if a naming convention is chosen.
- **x86-64 ABI** stays off. Play Games on PC runs ARM64 through translation, which
  is ample for a 2D URP idle game, and a third ABI costs the IL2CPP build-time
  doubling that got ARMv7 dropped. Revisit only if PC vitals show it.

## Appendix B — standing constraints

Not work items. Each of these cost real time to find.

- **Never use Play Games' own overlay.** `ShowLeaderboardUI` / `ShowAchievementsUI`
  route through `HelperFragment`, which extends the framework
  `android.app.Fragment` (deprecated since API 28) and throws **synchronously** on
  the JNI class lookup at `targetSdk 36` — it presents as a hung callback.
  Everything routed to the GMS clients is fine, and GPGS 2.1.0 is the newest
  release, so there is no upstream fix to upgrade to (issue #3318). Any reward or
  standing UI must be drawn **in-journal**.
- **A leaderboard's public page can come back empty while Play still ranks the
  player** (`read 0 rows` with submissions accepted). Never rely on `LoadScores`
  alone — fall back to `data.PlayerScore`. Empty *and* `player unranked` means
  Play isn't ranking the game at all: check the PGS **configuration** publish
  state, which is a separate thing from a leaderboard being "live".
- **Confirm on device first, then remove the instrument.** The first diagnostics
  sink was retired in the same commit as the fix it was meant to prove; when the
  symptom returned there was nothing left to read it with.
- **Run `Wildgrove/Fix Art Import Settings` after adding art.** The pass that
  caught `res-timber` importing with `alphaUsage: 0` — its transparency discarded,
  drawing on a solid block for a week — found it only by being re-run.
- **Art licensing:** prefer PD/no-attribution. `Retort (PSF)` is CC BY-SA —
  copyleft on a game asset, avoid. `Phial (PSF)` is a modern child-proof pill vial
  with a printed label, not period glassware.
- **`res-amber.jpg` and `res-flint.jpg` are not loaded by `ArtLibrary`, and must
  not be deleted as unused.** There is no amber or flint *resource* — the runtime
  never asks for either plate, which is what makes them look spare. They are
  source plates in `make-store-art.py`'s `ACHIEVEMENT_PLATES` manifest, and the
  two cards built from them (`achievement-something-older-512.png`,
  `achievement-reader-of-stones-512.png`) are **published on Play Console**.
  Deleting either fails `make-store-art.py --check` and strands a live card.
  The licence consequence: **`res-amber`'s CC BY 4.0 (Perrichot, *Dolichoderus
  longipilosus specimen tag and amber*) attribution is owed by a published
  derived work**, not merely by the file shipping under `Resources/` — so it
  cannot be dropped, and there is no five-CC-BY-works-to-four saving to be had.
  Separately and correctly recorded in `CREDITS.md`: the *IAP* amber icons
  (`amber_pack_small/large`, `reward_weekly_amber_cache`) derive from the
  `insect-deep-amber` source work (St. John's fly in amber, CC BY 2.0) — a
  different work from `res-amber`, and the two must not be conflated.
- **Re-check `resizeableActivity` after any editor upgrade.** It was OFF (a fresh
  6000.5.5f1 project has it on), which meant compatibility mode, letterboxing, a
  restart prompt on unfold, and **the wide journal spread could never appear**.
- **`Application.isMobilePlatform` is true on Play Games on PC** — it is an Android
  build. Ask `DeviceForm`
  (`PackageManager.hasSystemFeature("android.hardware.type.pc")`) instead. Getting
  this wrong hid the keyboard hint from the only player with nothing but a
  keyboard, and made Escape quit the app outright.
- **`chromeosInputEmulation` is a dead end** — `[Obsolete]` in 6000.5, serialises
  nothing. The manifest declaration is the only live lever.
- **Supported Aspect Ratio: leave it alone.** The mode is an internal property with
  no public API, and setting `maxAspectRatio` flips mode 1→2 as a side effect. Moot
  anyway — `android:maxAspectRatio` only applies to a non-resizable activity.
- **Android `targetSdk` is pinned to 36**, not Auto — an editor or module update
  could otherwise move a release's target silently. Raise it deliberately when
  Play's required level moves.
- **A permission implies a required feature.** `ACCESS_WIFI_STATE` implies
  `android.hardware.wifi`, `READ_PHONE_STATE` implies telephony — which is why
  `AndroidInputManifest` declares Google's 17 "not on a PC" features as
  not-required even though Wildgrove asks for none of them. An ad SDK bumping a
  permission would otherwise quietly cost the PC audience.
- **Play's "androidx.fragment 1.1.0 is outdated" warning is stale** and needs no
  fix. AdMob's import declares 1.7.1 from v43 on and Gradle takes the highest
  (verified from the artifacts: v42 bundles 1.1.0, v62 bundles 1.7.1). The warning
  persists only while a pre-v43 artifact is still active in a track.
- **The privacy policy lives in the Decryptic repo**
  (`src/Decryptic.App/wwwroot/wildgrove/privacy.html`) and deploys with that site.
  It has silently rotted behind the build twice; its header comment carries the
  keep-in-step warning.
- **The chrome budget rule:** a bar is only pinned if it is read on every tab — the
  page is the row that pays for it. `UpdateWorldGap` makes the world strip the
  shock absorber (14–26% of screen, after a 32% page floor), so the next thing that
  grows shrinks the strip's whitespace rather than the page.
- **Existing test saves restore to a 1-slot ladder** after the collection-ladder
  rework — most of the roster wakes resting. Wipe or re-station.
