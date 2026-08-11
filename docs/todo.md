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
| `sabbats.json` | every touch value (+20%s, ×0.8 replant, 5-pt spread ease) — the no-gate rule and the one-mastery-band ceiling are the lines to hold (design §15); top the calendar up ~2029 |
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
- **A live tincture is only visible on the Stores tab** — no global buff
  indicator. The bottle's tile wears a green rule and counts down while the brew
  runs, but nothing says so from any other page. (`Sim/Tinctures.cs`,
  `StoresPage.BuildBrewTile`)
- **No aggregate camp production view** — per-resource rates exist only on node
  cards; the only rollup is the trail's gather-vs-carry shortfall line.
- **Tending's Choice window is invisible.** `Simulation.Tend` opens the 30 s
  `choiceBonusRemaining` window and the HUD gives no cue. (`GameHud`)
  <br>It has none in the world strip either, from 2026-08-06: the golden halo
  that breathed around a node while the window ran is **deliberately removed**.
  `Bubbles.Pop` tends the node too, so the halo lit after every caught windfall
  as well as every tap — most of the time on a worked node — which made the
  strip's brightest mark its least informative. Whatever answers this item
  should say how much of the window is LEFT (a countdown, not a glow), and
  should not be a pulsing ring put back on the plate.
- **Verse cards don't render the spotlight (✳) marker.**
- **The Migration vignette shows the Verdure gain only** — per-familiar Kinship
  gains aren't itemised, and Kinship is legible everywhere else now.
- ~~**The Warden page's kith list is rows, and wants to be a Stores-style grid.**~~
  ✅ RESOLVED 2026-08-06. `BuildKithGrid` lays the roster on a `SquareCellGrid` at
  the Stores drawer's own 190 cell — a plate per companion, the name in its
  caption strip, and the crop of the ground they work as a corner mark (nothing
  at all for one resting at camp, which is how the drawer says "idle" without
  spending a word on it). Everything the row said in words moved onto the sheet
  the tile opens: level and % to next, BONDED and the Kinship numeral on one
  standing line, then the trait, the Kinship reckoning and the freshest
  inscription. The **bond** kept a channel of its own on the card — a moss rule
  instead of the ink one — because it is the one permanent honour and a plate has
  no room for the word. The tile itself is now shared
  (`JournalWidgets.PlateTile`), lifted out of `JournalSheets.Posting` since the
  roster and the posting pickers are the same drawer asked from a page rather
  than the strip; `StationPlate`/`FindNode` moved to `JournalSection` for the same
  reason. Tiles are built once per structure change and rewritten in place, so a
  rename, a move or a new bond never destroys the tile under a finger.
  (`WardenPage.BuildKithGrid`, `JournalWidgets.PlateTile`,
  `JournalSheets.OpenStationPickSheet`)
  <br>**Two things the playtest sitting should judge:** whether the moss rule
  reads as an honour beside the Stores drawer's grade rules (the one place a
  coloured border already means something else), and whether a drawer of
  portraits with only a name under each is legible before a player knows the
  species — the same open question as the Stores drawer's unlabelled plates
  below, and the two now stand or fall together.
- ~~**Renaming the warden is priced at 50 Amber and does not exist.**~~
  ✅ RESOLVED 2026-08-06. Both prices are live and played through by the same
  plumbing: a companion at **30** (`economy.amber.renameCostAmber`, up from a
  never-played 5, which was loose change beside a 15-amber skip) and the warden
  at **50** (`wardenRenameCostAmber` — dearer because it is bought once for the
  body the player wears across every fold, and it reads on every page).
  `state.wardenName` is the field, `SaveCodec` v44 the rung (`EarliestReadable`
  stays 42), and `Migration` carries the name across the fold so a purchase is
  never charged twice. `Warden.DisplayName`/`PossessiveName` are the one gate
  every surface reads, so an un-named run renders exactly the strings it always
  did — that is what made the sweep safe. Named everywhere the warden is shown
  **except the tab**, which stays "Warden" as the page's name, not the body's:
  the strip caption, both posting pickers and the plate tile, the walk/stand-down
  buttons and their notes, the empty-post notice, the trail plate's "posted"
  line, the pony's side, both "own hands" favour lines, and the gear-worn flash.
  Bought from a quill on the new **THE WARDEN** card heading the Warden page, or
  from the quill beside the walk sheet's question.
  <br>**Two knobs the playtest sitting should judge** (both are first guesses,
  like the rest of §1.1): whether 50 reads as *your own name* or as a wall in
  front of one, given a free player's ~40 Amber/week; and whether the offer
  belongs on the Warden card at all, where an un-named warden meets it every
  visit. Deliberate: a blank is refused rather than treated as clearing the name,
  so a mis-tap can never spend 50 Amber undoing one.
- **The Stores drawer has no sort or filter, and no name on a tile.** The order
  is data order — gatherables then crafted goods — which is stable but arbitrary
  once the drawer runs past a screen, and there is no "what can I craft with"
  view. Names live on tap (the tile writes the stack into the margin note), which
  is right for a drawer of plates and unproven with a player who does not yet
  know the plates. Watch this first in the playtest sitting. (`StoresPage`)
- **Inside-cover discoverability.** Settings sit at the bottom of the Record page
  with no gear in the chrome. Right for the book, unusual for a phone game — if a
  playtester can't find it, the answer is a corner mark on the Record tab, not a
  pinned bar.
- **The warden's place on the strip has had three shapes in two days — watch
  this one in the sitting.** A badge under whichever node they stood at (so a
  warden at camp was nowhere on the board at all), then a plate of their own at
  the head of the strip (2026-08-05), which broke the board's grammar: every
  plate there is a GROUND, so a body among them read as a node you could gather
  from, captioned with a name where its neighbours carried crops — and that
  caption ran into the next one. Now (2026-08-06) it is split in two:
  - Holding a node, the warden's own ground **leads the plates**
    (`WorldView.LeadWithTheWarden`) and the badge under it says whose it is,
    exactly as for a companion. The player's body reads first without being a
    plate.
  - Standing at camp, an **empty ground** leads instead (`WardenWorldView`): a
    moss (+) where the crop would be, their badge beneath it, captioned "at camp"
    (never the warden's name — that caption is what collided), and a tap that
    opens the walk sheet.
  - **Wandering shows nothing here, deliberately** (2026-08-06). A roaming warden
    holds the wander post, which is no single node and so has no plate on the
    strip — but drawing them on an empty ground would say they had no work, when
    roaming IS the work. One slot, one meaning. The cost is that the warden is
    off the board while wandering; the watch card and the pickers are where that
    posting is read.
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
  before release. The Phase 6 rule rides with the final edit: **cut 20%** — the
  1,200-word budget is a ceiling, not a target.
- **Most of the ~1,200-word budget is unwritten.** Still absent: Provisioner
  trigger lines (first-visit / after-migration), and lines for **generated**
  verses — runs 2+ reuse the zone's site with no authored words, so the narrative
  pass has to decide what a run-3 verse *says*.
- **Waystones are a modal, not a world object.** Tappable waystones in the world
  are still the intent; the sheet stands in. The §14 legibility check rides on
  this: the waystone (the past) and the verse site (the present) must read as
  distinct objects on a zone screen.
- **The Compendium has no plates or entry text** — the system layer with lifetime
  counters and discovery is live, the art and words aren't.
- **`design-doc.md` re-synced 2026-08-04** (windfall catch, the three-piece kit,
  the famXP formulas, the §12 achievement and Rewards rows). What remains there
  is voice, not accuracy: the §6 lore / §7 backstory were written for fossils
  and only lightly reframed for observe·sketch·release — that rewrite belongs
  to the narrative pass above.

### 1.5 Systems tails

- **Trail Map provisions are no longer on the critical path (2026-08-05).**
  Singing a zone's verse now opens the next zone's gathering, so the map rungs
  are a second way into ground the Rite already gives. They still carry each
  zone's dig site, specialist skill and recipes, which is real — but their
  provisions bundle was the early Exchange lesson, and nothing now forces a
  player through it. Decide at the run-1-to-run-3 sitting: leave them as an
  optional shortcut, reprice the bundles, or strip the `unlockZone` effect and
  move the dig site + skills onto the zone. Touching `Upgrades.UnlockedZoneIds`
  and `upgrades.json`'s `trail` track.
- ~~**`GameLoop` has no test fixture, and the gap has narrowed to ordering.**~~
  ✅ RESOLVED 2026-08-06. Both sequences are `RunSwap` (`Assets/Scripts/Game/Run/`),
  a plain class beside the seven already lifted: `AdoptFromCloud` is the eight steps,
  `StartAgain` the four, and the margin note the adoption owes moved with them —
  `GameLoop.TakeCloudNotice` now just drains it. What the sequences still need from
  the scene (the live state, dropping a catch-up in flight, crediting an absence,
  folding the store's entitlements, the save-and-sync) is the `IRunHost` seam, which
  `GameLoop` implements **explicitly** so the seam widens nothing into the surface
  the HUD reads. `RunSwapTests` pins the order both ways it can go wrong: the step
  list itself, and the effects a wrong position would spoil — the stats baseline
  taken from the adopted run rather than the discarded one, and the welcome-back
  summary being the adopted absence's rather than the summary that was just dropped.
  No PlayMode fixture was needed.
- **Folio spreads are 2–4 entries; design wants 4–8.** A balance pass, deferred
  since the Folio landed.
- **No `postMatch` multiplier (design §8).** Familiar XP is a flat per-second at
  any post; Roosts comfort and Kinship are the only XP-rate levers.
- **No observation skill XP.** Sketches are too rare for per-unit XP; decide a
  grant when tool-tier or level gates need the level.
- **The 2026-08-06 amber-sink slate (design §9) is BUILT end-to-end
  (2026-08-06)** — sim, economy keys, save rungs (44→48), GameLoop APIs, and
  the journal surfaces: the pile line prices the calling gift (`TrailPage`);
  the Camp page heads with the camp's name card and its naming sheet, and the
  name reads on the welcome-back and fold sheets (`JournalSheets`); the
  welcome-back sheet carries the settle-the-ledger offer; the Exchange card
  carries the drover's consideration row; the Amber card sells the second
  queue and the station rule lines say the new count (`CraftWouldDisplace`
  keeps "Craft instead" honest with the spare slot open); the Record page
  keeps THE KEEPSAKES shelf and its mount row. Still owed:
  - **a device click-through** of every new row — the suite proves the sim,
    not the layout;
  - **the narrative pass owns the wording** — every new line is a first draft
    in register, and the keepsake line especially is rendered from
    `KeepsakeState`'s facts precisely so a wording pass costs no save;
  - **the chrome fold banner deliberately does NOT carry the camp name** —
    it is a count, not a route; the name reads on the Camp page head, the
    welcome-back sheet, and the fold sheet instead. Revisit only if playtest
    misses it.

  Cosmetics stay retired — still **no substrate at all** (no skin/wardrobe
  system, no warden or familiar sprite); that absence is what retired the
  cosmetic reward cloak.
- **The kit bag has nothing to reward.** With Pitch Torch and Clay-Lined Creel
  moved into the Almanac, the kit is back to one piece per slot, so the swap is
  inert until new gear ships.
- **`Bootstrap` spawns GameLoop + GameHud via `[RuntimeInitializeOnLoadMethod]`.**
  Replace with a real bootstrap scene when there is content to lay out.
- **Some species have no acquisition path but bonds** — the non-node species
  (dray-stag, tawny-owl, cavern-bat) have no gift pile to be called by. Future
  arrival content. The counts are still Mo's open §14 call: how many are
  bondable at MVP (working assumption 1–2) and the earn rate after (1 per 2–3
  Migrations early, slower later).
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

### 1.7 Onboarding — the last pass before launch

Design §13 Phase 6's final step, carried here so it doesn't vanish with the
plan: a light tutorial layer — the teaching notes (each system's first margin
note is its instruction, §7) verified against beta FTUE analytics, plus
contextual first-time nudges (a soft mark on the first windfall catch, the
first replant, the first fielding choice) that stay inside the no-popup,
two-lines-on-screen tone. The gate it feeds: the first-hour funnel must be
green before ship (§3.5). Sequenced after the playtest sitting and the
narrative pass — the nudges point at whatever those two settle.

### 1.8 The Wheel — in build (2026-08-08, design §15)

The Wheel replaced the drawn region season the day it was decided (design §8),
so the retirement and the replacement's core land together — a run with neither
lean is a regression, not a phase. Build order:

- ~~**Retire the drawn season**~~ ✅ RESOLVED 2026-08-08 — `regions.json`,
  `Regions.cs`, `RegionDef.cs`, the draw, the effect-union feed, the
  generator's `DemandWeight`, the forecast's region line, the vignette's
  sign and `RegionsTests` all removed; keepsakes keep legacy region ids as
  text. The parked idea itself is recorded in Appendix A.
- ~~**The calendar core**~~ ✅ RESOLVED 2026-08-08 — `sabbats.json` (both
  hemispheres, 2026–2030), defs/importer/validator (incl. tide-overlap and
  date-format rules), `Wheel.cs` (O(1) window cache off the sim clock
  cursor), `HemisphereGuess` locale default. **Still owed here: the visible
  hemisphere toggle on the inside cover (locked mid-tide)** — rides with the
  observance layer; until then the locale guess is the only source.
- ~~**The ambient touch**~~ ✅ RESOLVED 2026-08-08 — live reads at all six
  hook sites (kith yield, warden's hands, sketch walk — never the amber
  roll, bubbles, replant cost, Exchange spread); the sim clock cursor is
  stamped by GameLoop, back-dated for absences, advanced per sub-step, and
  `Advance_AcrossATideEdge_LandsWhereOneCallWould` pins sliced-equals-
  unsliced across a tide edge. Suite 1038/1038 on 2026-08-08.
- ~~**Save**~~ ✅ RESOLVED 2026-08-08 — rung 48→49: hemisphere + claims,
  both default-filled; round-trip and shapeless-claim clamps tested.
- ~~**Fold forecast names the next sabbat**~~ ✅ RESOLVED 2026-08-08 —
  "{sabbat}-tide is open." / "{sabbat}, N days off." on the fold sheet; the
  Trail page's head line is now the warden's tide line + touch label.
- ~~**The observance layer**~~ ✅ RESOLVED 2026-08-08 — the keeping:
  `Keeping.cs` + `RiteGenerator.GenerateKeeping/RedrawKeeping` (persisted
  facts, whole-ask offers, Renown at trade value, one-shot tier Amber
  5/10/10, claim at the first tier, fold redraw keeps answered slots),
  save rung 49→50, THE KEEPING card on the Trail, the tracker's tide
  row/tail, THE WHEEL shelf on the Record, the hemisphere toggle on the
  inside cover (locked mid-tide). Containment is structural — the keeping
  never enters `CurrentRite` or `verseProgress`, pinned by
  `Keeping_NeverTouchesTheRitesLedgers`. Suite 1048/1048 on 2026-08-08.
  Finished same day: the tracker's tide row deep-links to the keeping card
  (the row's tap follows what it shows), tier crossings get their PumpSheets
  moment (`OpenKeepingSheet`, ahead of the stones, behind the kith beats),
  and the plate hooks are live — drop `sabbat-{id}` plates into the
  ArtLibrary and the keeping card, the tier sheet and the Record shelf
  (kept sabbats only) pick them up with zero code.
- **Word/art debts**: the eight plates ~~in the template~~ landed 2026-08-08
  as **authored almanac marks** (`Art/Plates/Wheel/sabbat-*.jpg`) — the
  warden's calendar ornament rather than a naturalist page, deliberately:
  the calendar is the warden's (§15's naming device), the set is original
  work (no licence owed, `ArtCredits` untouched), and a painted plate can
  land over the same id any time. `EverySabbat_HasItsOwnPlate` pins each to
  its own mark. Still owed: the narrative pass re-voices the eight margin
  lines drafted in `sabbats.json` and the keeping sheet's wording.

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
- **The store-screenshot harness photographs a run that is not the showcase.**
  Found 2026-08-06 while using it to look at the new roster drawer; diagnosed
  2026-08-11, and the diagnosis it was filed under was wrong.
  - ~~**`kinshipXp = 4200` is a Kinship LEVEL, not XP.**~~ ✅ RESOLVED
    2026-08-11. `Kinship.Level` returns `(int)familiar.kinshipXp` directly (the
    field stores the level; the √ conversion happens at Migration), so the
    showcase's first companion read "KINSHIP MMMMCC" in every listing shot
    taken since. Staged at 4 now, and `Stage_KinshipIsALevelACompanionCouldHold`
    pins every staged companion inside a level a plate can hold.
  - ~~**Six companions are staged and two arrive.**~~ ✅ NOT A SAVE FAULT
    2026-08-11. `SaveCodec.Capture`/`Restore` was not dropping anyone:
    `Stage_ThroughTheSaveSlot_ArrivesAsTheCampItStaged` walks the whole launch
    path (stage, capture, write the slot, load it, restore) against the real
    data and the camp arrives whole, six companions on the open ladder. Nothing
    in `Restore` can drop four familiars of four distinct species: the only
    reducer is the one-per-species dedupe, and the slot trim rests bodies
    rather than removing them.
    <br>What the captured page was reading is **another save entirely**. Two
    independent tells: "1 of 1 posts walked · 2 companions" is a ladder with no
    verses sung and no purchased slots, which the showcase sets to 10 and 2;
    and the welcome-back sheet cannot fire under 60 s of credited absence
    (`SessionLog.WelcomeBackMinSeconds`), which a save stamped `UtcNow` seconds
    before Play can never reach. **The mechanism is still unproven** (the
    staged write and the session's read both go through `SaveFile.Path`, and
    the poller's set-aside restore is gated behind the done marker), so the
    next capture is instrumented to say so instead: `WarnIfNotTheShowcase`
    counts the woken run against a freshly staged showcase and logs an error
    naming both. Read the log before trusting a shot.
  - ~~**The owl was staged at a dig post.**~~ ✅ RESOLVED 2026-08-11. A
    `dig:{zone}` station is retired, and `SaveCodec.StationValid` rests whoever
    carries one on load (deliberately, per `Familiar.DigStationPrefix`), so the
    fourth staged companion was at camp in every shot. It stands on a ground
    now, and `Stage_PostsOnlyGroundsAndTheWatch` pins that the showcase posts
    nothing the save cannot carry. This was the only remaining use of the
    prefix anywhere in the project.
  - **The staging moved out of the editor harness** to
    `Assets/Scripts/Game/ShowcaseState.cs`, beside `StoreCaptureRunner`, which
    is what let any of it be tested: `Assets/Editor` compiles into
    Assembly-CSharp-Editor and no test asmdef can reference that.
  - The welcome-back fault is fixed (`StoreCaptureRunner.ClearSheets`), but the
    reason recorded for it was wrong: the staged save is stamped `UtcNow`, not
    in the past. Draining is still right (any sheet can stand, and a scrim
    outlives a tab change) but a welcome-back sheet in front of a capture is a
    symptom of the wrong-run fault above, not of the stamp.
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

**The Amber-drip rewarded unit — DONE 2026-08-04.** `Wildgrove Amber Drip`
(`ca-app-pub-6903871125040514/5608337861`) created in the console and
`ServiceIds.cs` repointed, retiring the `AmberDrip = TimeSkip` alias. New units
take **a few hours** to start serving — no-fill on the first device test is
expected, not a fault.

**The GDPR/consent message — published 2026-08-04** (GDPR and US states both).
What remains is the on-device verification, which no editor run can stand in
for:

- `GatherConsent` passes a bare `ConsentRequestParameters` — no debug geography
  is wired — so test via a VPN to an EEA country, or temporarily add
  `ConsentDebugSettings` (`DebugGeography.EEA` plus the device hash the UMP SDK
  prints to logcat on the first unregistered run). `ConsentInformation.Reset()`
  clears a stored answer between attempts. Confirm first, then remove the
  instrument.
- A pass looks like: the form shows on first launch and no ad request precedes
  it; **Ad privacy choices** then appears on the inside cover —
  `PrivacyOptionsAvailable` flipping true is the one in-game signal the message
  is live. "Do not consent" logs `[ads] consent withheld — no ads requested`
  and the rewarded buttons stay unready, which is correct. Logcat must stay
  free of `[ads] consent update failed` / `[ads] consent form failed`: both
  paths log-and-continue on purpose, so the game will never surface a failure
  on its own.

### 3.2 Play Console

- **The product catalogue is complete — DONE 2026-08-04.** All eight ids in
  `StoreCatalogue.All` have a console entry — `starter_bundle` and `kith_slot`
  landed last, and `reward_wayfarers_cloak` is rightly absent. The amber packs
  are one-time products like the rest, which is correct: consumable-vs-durable
  is the client's distinction (`StoreCatalogue.IsConsumable`), not a console
  setting. What's left to prove is the internal-track catalogue fetch (§3.3).
- **Attach a Play Games Reward offer to each of the three reward products.** The
  products (`reward_drovers_halter`, `reward_weekly_amber_cache`,
  `reward_wayfarers_plate`) are created and activated. **The association UI and
  reward testing do not open until Sep 1 2026**, and the Level Up bar for ≥2
  single-use rewards is **Sep 30 2026** — a one-month window. (≥1 repeatable by
  **Mar 1 2027**; the weekly cache is it.)
- **Do not create `reward_wayfarers_cloak`.** The cosmetic reward was retired
  unbuilt — it wanted a cosmetic substrate the game has never had. A test pins
  that the id is uncatalogued, so an award of it could never be acknowledged.
- **The deletion URL the form asks for is an anchor, and the anchor is
  load-bearing.** `https://decryptic.app/wildgrove/privacy#deleting-your-data` —
  a *Deleting your data* section added 2026-08-05 for exactly this field. Renaming
  that `id` sends the console's link to the top of the page, where a reviewer finds
  no deletion instructions; the page carries the same warning in a comment.
  Account questions on the same form: **no** account creation (the game creates
  none), **yes** users can sign in with an account created outside the app (the
  Google account), and App access is *all functionality available without special
  access* — nothing is gated behind the sign-in.
- **Bring the Data Safety form up to what ships — before the next release.** The
  privacy page was corrected on 2026-08-05 (Game Stats disclosed, the "no accounts"
  claim withdrawn); the form is the half that isn't in a repo, and it has never
  been revisited since Play Games and Game Stats landed. It needs the Play Games
  identity (display name, gamer profile) and the gameplay stats declared, with
  *collected* / *shared* / *optional* set to match — the form and the page
  disagreeing is a policy strike in its own right, whichever of the two is right.
- **AdMob's own disclosure table, read 2026-08-05 — three rows are stricter than
  they look.** Google publishes the SDK's collection at
  `developers.google.com/admob/unity/privacy/play-data-disclosure`, and it says
  **collected *and shared*** for all four: IP address (→ *Location · Approximate
  location*, which the form must therefore declare), user product interactions (→
  *App activity · App interactions*), diagnostics (→ *App info and performance ·
  Diagnostics*), and device/account identifiers (→ *Device or other IDs*). None is
  ephemeral. Two consequences: **App interactions cannot be declared Optional** —
  Play notes governs our half, not AdMob's, and the row answers for the whole app —
  and **Diagnostics is Shared** even though Crashlytics alone wouldn't be (*Crash
  logs* stays unshared). The ad id could be blocked in the manifest to drop the
  identifier row; we don't, so it is collected.
- **The Unity question — answered 2026-08-05: Unity does not go on the page or the
  form.** Unity's own Apple privacy manifests ship inside both packages and declare
  `NSPrivacyCollectedDataTypes` empty, `NSPrivacyTrackingDomains` empty and
  `NSPrivacyTracking` false — for `com.unity.services.core` (whose only accessed-API
  reason is UserDefaults, i.e. the installation id it keeps in PlayerPrefs) and for
  `com.unity.purchasing`. There are no endpoint strings in core's runtime, and its
  Telemetry/Metrics code is plumbing for *other* UGS packages, none of which are
  installed. Unity's data-safety index points at that manifest as its whole answer
  for Services Core. **This holds only while core + purchasing are the only UGS
  packages** — Authentication, Cloud Save, Analytics and Crash Reporting all collect,
  so adding any of them reopens both the page and the form. Static evidence, not
  observed traffic; a packet check would confirm it but nothing points at needing one.
- **Done — the analytics package drop, kept here for the warning.**
  `com.unity.analytics` 3.8.2 was dropped 2026-08-05: nothing referenced
  `Unity.Services.Analytics`, and it was pulling `com.unity.services.analytics` 6.3.0
  into the AAB, which is what a Data Safety reviewer reads. The legacy
  `com.unity.modules.unityanalytics` built-in module went with it. IAP is untouched —
  `com.unity.purchasing` depends on `com.unity.services.core`, not on analytics.
  **Do not re-add either** without saying so on the privacy page and the Data Safety
  form first. UGS core still initialises whenever the store connects
  (`UnityIapStore.ConnectAsync`); that is fine, per the entry above.
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
- **Game Stats events flowing on device** — a dev build logs
  `[play-games] game-stats: recorded <event>` on the save cadence. Stats appear
  on the Gamer profile only after the console schema is live (September 2026
  window), so the log line is the whole check until then. **Check with *Play
  notes* on:** the stats now answer to that switch as well as to sign-in, so a
  silent log with the switch off is the gate working, not a fault. Check the off
  case too — no `recorded` line at all.
- **A payer's launch reaches no advertiser — check it on a device.** `Ads.Initialise`
  now takes whether ads are wanted, read from the remembered `AdsRemoved`
  entitlement, and `MobileAds.Initialize` is never called when they are not. The
  check is what is *absent* from a second launch after buying remove-ads: no
  `[ads] rewarded load` lines at all, and `[ads] ads bought away` where the SDK
  would have woken. Then buy it mid-session and confirm the same log line appears
  immediately rather than at the next launch. Also confirm the refund direction —
  the store reporting not-owned must start the ads that session, not leave the
  rewarded buttons dead until a relaunch.
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

- **Game Stats — the client submits since 2026-08-04; the console side is the
  remainder.** GPGS **2.2.0** (released 2026-07-31) added the API and is now
  vendored: `PlayGamesServices.RecordStat` builds a `PlayerGameEvent` and
  records it, `FlushStats` nudges `RequestEventsUpload` on the save cadence,
  and dev builds log `[play-games] game-stats: recorded <event>` per event as
  the on-device instrument. What remains:
  - **Editor confirm — the import half is DONE 2026-08-04.** The 2.2.0 import
    ran clean on the first open: no compile errors, only GPGS's own CS0618
    warnings about its now-deprecated `GPGSProjectSettings`, and `GPGSUpgrader`
    logged `start`/`done`. The R8 proxy sweep in `proguard-user.txt` was re-run
    at 2.2.0 and needs no new keep — the game-stats API adds no proxied name
    (`AndroidEventsClient` goes through `AndroidTaskUtils` and
    `gms.games.event`, both already kept). The **EditMode suite is green on
    2.2.0**: 914/914 in 36s via `Unity.exe -runTests -testPlatform EditMode`
    against the vendored plugin and the patched `PluginVersion.cs`, including
    `EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema` — so code and the
    authored CSVs still agree ahead of the console upload.
  - **Still owed in the editor: Android Resolver Force Resolve.** No resolve
    appears in either Unity log Unity still keeps, so
    the hand-edited templates remain unconfirmed — though they already match
    EDM's own output conventions, down to the `GooglePlayGamesPluginDependencies
    .xml:9` / `:14` end-of-element line attributions, and EDM's auto-resolve
    hook has run repeatedly without wanting to rewrite
    `AndroidResolverDependencies.xml`. (The templates being right is the claim:
    the `gpgs-plugin-support` maven line and its local m2repository are gone,
    the support lib is a plain AAR under `Runtime/Plugins/Android`, and
    `play-services-games-v2:22.0.0` + `play-services-nearby:18.5.0` are
    declared direct — 2.1.0 got both transitively, at games-v2 **21.0.0**.)
  - **Console:** upload the authored CSVs from `store/play-games/gamestats/`
    (the upload window opened August 2026; draft-config testing with test
    accounts opens **September 2026** — batch with the Sep 1 rewards visit).
    Two knowingly-unfinished parts stand: the **stat icons aren't drawn**
    (Google has published no size spec — use `tools/make-store-art.py` once
    the console says what shape), and the column values (`HIGHER`, the
    free-text units) are the guide's documented spellings, not ones a console
    has accepted. Expect one correction round.
    `EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema` fails if code and
    CSV drift, because Play drops undeclared events silently.
  - **Plugin quirks, recorded so they don't read as our bugs:** 2.2.0 ships its
    own `PluginVersion.cs` still saying "2.1.0" (`package.json` says 2.2.0) —
    **patched locally to 2.2.0 / `0x20200` / `"20200"` on 2026-08-04, and the
    patch must be re-applied on the next re-vendor.** `GPGSUpgrader` stamps that
    constant into `ProjectSettings/GooglePlayGameSettings.txt`, so upstream's
    stale value made a successful upgrade read as an upgrade that never ran —
    which is exactly how it was misread. It also ships `.orig`/`.rej` patch
    debris in the unitypackage (excluded from the vendored copy), and raises the
    plugin's minSdk floor to 24 — Wildgrove's 26 clears it.
  - **Do not re-run Window → Google Play Games → Setup to tidy the version.**
    The androidlib's `games.unityVersion` meta-data still reads 2.1.0; it is
    Google-facing telemetry only and is deliberately left alone. Regenerating it
    means re-running Android Setup, and neither `GooglePlayGameSettings.txt` nor
    `PlayGamesSettings.asset` holds the app id any more — the field would open
    blank and a confirm would write a manifest with no
    `com.google.android.gms.games.APP_ID`, which is the one meta-data sign-in
    needs. The real id lives in `GameInfo.ApplicationId` (`17679484071`) and in
    the generated `GooglePlayGamesManifest.androidlib/AndroidManifest.xml`.

### 3.5 Launch (design §13 Phase 6 — carried here so the plan's tail isn't lost)

- **The name check, before the listing.** "Wildgrove" is a working title —
  check Play Store collisions and trademark (design §14). Everything
  listing-side hangs off the answer, so it comes before screenshots or copy.
- **The store listing**, with tablet and PC screenshots (Level Up parity).
- **Closed beta: 2–3 weeks of vitals**, with the Play Games on PC opt-in and the
  Level Up self-check run during it.
- **The ship gate:** vitals green 14 consecutive days and D1 retention >30% in
  beta → ship. The onboarding pass (§1.7) feeds the same gate — the first-hour
  funnel must be green.

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
- **Per-run drawn randomness (the retired region season)** — parked 2026-08-08
  when the Wheel (design §15, todo §1.8) replaced the drawn region modifier as
  the world's one lean (design §8 records the retirement). If playtest finds
  fallow-week runs read too alike, a drawn overlay may return — as a *second*
  flavour beside the calendar, never instead of it, and re-judged from scratch
  (the old lush/misted/ashen/windswept table and its `regions.json` shape
  survive in repo history, not as a live spec).

## Appendix B — standing constraints

Not work items. Each of these cost real time to find.

- **Never use Play Games' own overlay.** `ShowLeaderboardUI` / `ShowAchievementsUI`
  route through `HelperFragment`, which extends the framework
  `android.app.Fragment` (deprecated since API 28) and throws **synchronously** on
  the JNI class lookup at `targetSdk 36` — it presents as a hung callback.
  Everything routed to the GMS clients is fine. Checked again at 2.2.0
  (2026-08-04): the bridge inside `gpgs-plugin-support.aar` still references the
  framework `android.app.Fragment`, so the rule stands (issue #3318). Any reward
  or standing UI must be drawn **in-journal**.
- **A leaderboard's public page can come back empty while Play still ranks the
  player** (`read 0 rows` with submissions accepted). Never rely on `LoadScores`
  alone — fall back to `data.PlayerScore`. Empty *and* `player unranked` means
  Play isn't ranking the game at all: check the PGS **configuration** publish
  state, which is a separate thing from a leaderboard being "live".
- **Confirm on device first, then remove the instrument.** The first diagnostics
  sink was retired in the same commit as the fix it was meant to prove; when the
  symptom returned there was nothing left to read it with.
- **The device clock is the player's, and it is settled** (2026-08-05). Every
  cooldown and the offline credit read wall-clock time, so winding the clock
  forward used to re-arm the Amber drip, refill the paid-skip budget and pay a
  fresh 12 h catch-up — repeatably, on a currency sold for money. `ClockGuard`
  ratchets: a reading is never below the highest the run has seen
  (`clockHighWaterUnixMs`, saved and carried across the fold). A wind forward is
  therefore **spent, not minted**, and a wind back is not seen. The honest cost
  is that a genuine backwards correction stalls cooldowns until real time
  catches up, which is the right way round — a pause, not a loss. This is a
  decision, not an open item: **do not add a wall-clock read that bypasses
  `GameLoop.NowUnixMs()`.**
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
  It has silently rotted behind the build three times now — the third was Game
  Stats, and the same review found it still claiming "there are no accounts" while
  describing the Google account two sections down, and a sign-out the game has
  never had. Corrected 2026-08-05; its header comment carries the keep-in-step
  warning, and the deploy is the Decryptic site's, not this repo's.
- **The type scale asks the font atlas for a lot.** Sixteen authored sizes across
  four faces, doubled by `FontScale`, plus the world strip's TextMesh labels at 64
  and a synthesised bold on the lit tab: every combination is its own glyph set in
  a shared dynamic atlas, so the atlas repacks often and each repack is a chance
  for a label to be left drawing through the old rects. Both known ways that
  happens are now defended (`GameHud.RefreshRebuiltFonts` re-generates a frame
  later, `WorldView.OnFontTextureRebuilt` guards every reach so it cannot throw
  and cut the rest of the subscriber list), but the defences treat the symptom.
  Collapsing the authored sizes toward a handful of steps would make the repacks
  rare instead — a type pass, not a bug fix, and the 12sp Android floor
  (`JournalWidgets.FontScale`) is the constraint it has to respect.
- **The chrome budget rule:** a bar is only pinned if it is read on every tab — the
  page is the row that pays for it. `UpdateWorldGap` makes the world strip the
  shock absorber (14–26% of screen, after a 32% page floor), so the next thing that
  grows shrinks the strip's whitespace rather than the page.
- **Existing test saves restore to a 1-slot ladder** after the collection-ladder
  rework — most of the roster wakes resting. Wipe or re-station.
