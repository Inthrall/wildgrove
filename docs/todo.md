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

### 1.1 The playtest gate — SAT 2026-08, and the table below is now a review

**The run-3-to-run-6 sitting has been played** (Mo, locally), and it is what drove
the run of changes ending 2026-08-14. So the framing this section carried for a
month is spent: the mid and late numbers are no longer "model-derived, never
played", and this is no longer the one item blocking everything else.

**What it leaves is a different job.** The knob table below was written as a list
of things nobody had ever seen in play. It now needs one pass with the sitting in
mind to say, per row, whether the number was confirmed, moved, or simply never
came up — because a row that was never reached in a run-3-to-run-6 sitting (most
of zones 7–8, the peaks' waystones, the deepsteel door) is still exactly as
unplayed as it was. **Until someone does that pass the table cannot be trusted
either way**, and its rows should be read as candidates rather than as open
questions. Anything the sitting settled belongs in `design-doc.md`.

The original framing, kept because the unreached rows are still governed by it:
with Cloudreach Peaks built (2026-08-01) the map is walked out and there is no
content slice in front of it. The model estimate to judge against was a debut
verse holding at ~20–60 min of its own node's production; FTP walking the fold-4
Rite in ~9–12 days, a committed payer ~4–6.

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
| `economy.json` kith | `slotVerseZones` — WHICH three verses open a place (design §4, named rather than tallied since 2026-08-11). Chosen off the zones' `minMigration` gates for mid-run-1 / run 3 / run 6; the pacing is a first guess and the honest test is a run-1-to-run-5 sitting. Changing one is a data edit, but moving a verse EARLIER hands the place to saves that already sang it, and moving one later takes nothing back — a verse sung is never unsung. |
| `sabbats.json` | every touch value (+20%s, ×0.8 replant, 5-pt spread ease) — the no-gate rule and the one-mastery-band ceiling are the lines to hold (design §15); since 2026-09-09 the seasons run night to night, so the world leans every day of the year and the touch is the ONLY number under watch, there being no window left to tune; top the calendar up ~2029 |
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
- **A bottle in stock still cannot be read without spending it.** Every tap on a
  brew tile now writes the bottle's authored line to the note (2026-08-13), so a
  bare shelf and a live brew both explain themselves, and a drink says what it
  did as it lands. The first tap on a bottle actually held is the gap: it drinks
  before it reads, because tapping IS drinking. Closing it means the line sitting
  under the tile rather than in the note, which costs the card four rows of prose
  on a page whose argument is that plates say it faster than words.
  (`StoresPage.BrewReading`)
- **Building Lines and the station cards are the same place drawn twice.** The
  code says so itself (`CampPage.Crafting`: "the two cards are the one place, seen
  from its two sides"), and they wear the same plates: five Raise rows on one
  card, three station cards headed by three of those five lines. Now the stations
  fold (2026-08-13) the pair reads odder, not better, because the folded heads sit
  directly under a card that names them again. Closing it means the Raise moving
  into its own station's card and Building Lines shrinking to the lines no recipe
  works (the store and the roosts), which is ~700 units down to ~300 and puts the
  raise where the work is. Left alone deliberately: it restructures two cards
  rather than folding one, and the fold was the length problem. **Building Lines
  itself folds from 2026-09-10** (Mo's call, `JournalCardFolds.Buildings`,
  arriving open like the Almanac), which answers the length half and leaves this
  entry as what it always really was: two cards naming the same five lines.
  (`CampPage.BuildBuildingsCard`)
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
  - ~~**A watching warden shows nothing here, deliberately**~~ ✅ RESOLVED
    2026-08-13, and the deliberate absence turned out to be the bug. A body
    holding an observation site's post was nowhere on the assignment board for as
    long as they held it: a site is no node, so it had no plate, and drawing the
    warden on an EMPTY ground would have said they had no work when the work is
    exactly what they were doing. One slot one meaning was the right rule and the
    wrong conclusion — the answer was to give the post a plate, not to leave the
    body off the board. Sketching posts now stand among the grounds
    (`NodeWorldView.CreateSketching`, `WorldView._sketchViews`), wearing the
    Curtis moth plate and the badge of whoever draws there, captioned
    "{zone} sketching" so two of them never read as one place. They are in the
    strip's (+) ground picker too, where they had also been held out.
    <br>**Judge in the playtest sitting:** whether a moth among the crops reads
    as *a place to send somebody* or as *a specimen you have found* — it is the
    one plate on the strip that is not a crop, and the grammar it leans on ("a
    plate says what the place gives") is being asked to stretch one step.
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
- ~~**The Compendium has no plates or entry text**~~ **— the plates landed
  2026-08-13; the entry text has not.** The Compendium and the Crafts are drawers
  of square plates now (`RecordPage.BuildFindTile`, on the shared
  `JournalWidgets.Grid`), the Folio draws each spread as the strip of specimens it
  asks for, and the Deep Pages draw the sketch ladder as marks. What design §6
  still promises and the page still hasn't got is **"a line or two" per entry** —
  the caption is a lifetime figure and the tap reads a name and a tally, so a
  recorded entry says what it is worth and nothing about what it *is*. The words
  belong to the narrative pass above; the room for them is a sheet the tile would
  open, which is also where a full-size insect plate belongs (see below).
- **The Record page's remainder, from the 2026-08-13 UX pass.** Four things were
  deliberately left, each because it is somebody's decision and not a defect:
  - **A tile opens nothing.** Tapping reads the entry into the note line, which
    is the Stores drawer's idiom and enough for a figure — but a recorded insect
    plate is still drawn full width in the card (260 units) because the
    alternative is a plate sheet that does not exist. One sheet would serve both
    it and the entry text above.
  - **The world strip keeps a quarter of the screen on the Record tab.**
    `JournalLayout.StripShareMax` is 0.26 of canvas height and the page floor is
    0.32, so the longest reading page in the book gets under half the screen for
    reading. The strip earns that band on the Trail and the Camp; here it shows
    postings nothing on the page refers to. Making the share tab-dependent is a
    change to the one piece of layout maths the tests pin, so it wants deciding
    rather than doing.
  - **An uncaught insect still keeps its haunt.** The marks say how far along a
    plate is; nothing says where to send a watcher. Design §6 says a page not yet
    earned keeps its secret — *no name, no haunt* — so revealing the habitat is a
    narrative decision, not a UI one. The name should stay hidden either way.
  - **The Almanac is on the Record page; design §677 says the Warden page.** It
    leads the Record page now (it is the one card here with a currency to spend),
    which makes the drift matter more, not less. Either the doc is stale or the
    card is on the wrong tab.
- ~~**The Trail opened on no gathering plate at all**~~ ✅ RESOLVED 2026-08-13 —
  the page's head had grown to ~1,480 canvas units against a ~1,030-unit
  viewport, so for the ~68% of the year a tide then held the Trail opened on
  preamble and the first plate was a viewport and a half down. Every cut was a
  duplicate rather than a trim: the fallow weeks' countdown (the rail's cell,
  read aloud), the tide's touch label (the tide sheet says the same sentence
  under *While it holds*), the tracker's `· {sabbat}-tide` tail (a rail cell
  wearing that plate stands two rows below it), and the trail-home bar — 100
  units animating deliveries that are automatic, lossless and untappable. The
  keeping card now folds shut behind a live head (tier · answered · closes-in,
  plus a moss clause when a slot can be met), which made `JournalRecordFolds`
  the journal-wide `JournalCardFolds`; both deep links into it open the fold on
  the way through, so a door never ends in a shut drawer. Keystone mark 120 →
  60. Design §13 Phase 2 records the rule this leaves behind: a pinned line at
  the head of the Trail has to be worth a plate. **A sixth cut, 2026-09-09**:
  the tide's sign, the last thing left above the keeping's head — a margin note
  about what day it is, standing over the plates for the whole of a six-week
  season (§1.8 above). The same pass moved the tally ONTO that head so it reads
  open as well as shut, and dropped the card's own standing line, which had
  been saying it again a finger's width below. The **carrier** went with the
  bar and is not coming back (Mo, 2026-08-13): deliveries are automatic and
  lossless, so the dot was an animation of a thing that cannot go wrong.
  **Deferred:** whether the **fell pony** gets a walk of her own again. She
  still reads on the roster (`JournalSheets.Roster.cs` — "walks her own lane"),
  so nothing about her is invisible; what she has lost is the one place she was
  a body moving rather than a line of text. If it is wanted back it belongs in
  the world strip, where ambient motion costs the page nothing — never as a
  pinned row on the Trail again.
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
  keeps "Craft instead" honest with the spare slot open).

  **The keepsake page was CUT 2026-08-11** — the one item of the slate that
  did not survive contact. The Record page's THE KEEPSAKES shelf, its mount
  row, `Sim/Keepsakes.cs`, `KeepsakeState`/`SavedKeepsake`, the GameLoop APIs,
  `keepsakePageCostAmber` and the fold's carry-forward are all removed, and
  save rung **v51** drops the persisted array (old saves simply stop carrying
  it — Newtonsoft ignores the unknown property, and no shipped build ever had
  it). Design §9 records the reasoning and the "do not re-raise as a
  page-with-a-line" note; the short version is that the shelf line's only
  distinctive content was the camp's name, so it read as "an unnamed camp"
  unless the separate 40-Amber naming came first, and the name was snapshotted
  at mounting so the wrong order bought a permanently blank page. Still owed
  on the rest of the slate:
  - **a device click-through** of every new row — the suite proves the sim,
    not the layout;
  - **the narrative pass owns the wording** — every new line is a first draft
    in register;
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
  sign and `RegionsTests` all removed. Keepsake pages were the last carrier of
  legacy region ids, and they went with the cut keepsake sink (2026-08-11), so
  no live type holds a drawn-season id any more. The parked idea itself is
  recorded in Appendix A.
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
- ~~**The Wheel had no always-on surface**~~ ✅ RESOLVED 2026-08-11 — four of
  its five surfaces drew nothing outside a tide and the fifth (the Record's
  shelf) was eight identical "unkept"s with no dates, so a player in a fallow
  week met no evidence the system existed. Four changes, together:
  `openDaysBefore` 14→30 (design §15 — the tightest authored pair is 38 days,
  so ~37 is the ceiling); the Record's shelf was re-cut (dated and
  soonest-first on the day, then **cut back to a count and the kept names on
  2026-08-12** — the dates are read on the rail and the Trail's head, which is
  where a player acts on them; design §15 records both turns); ~~the Trail's head
  counts the next tide's opening down through the fallow weeks~~ **cut
  2026-08-13** — the rail's coming-sabbat cell had made it the same countdown
  read aloud one screen away, and it was standing above the grounds to do it
  (see the Trail's head below); and the
  **events rail** (`EventRail.cs`, `GameHud.Events.cs`,
  `JournalSheets.Events.cs`) stands cells down the world strip's left edge —
  on screen at every tab, paid for out of the band's left MARGIN rather than
  the page's height. `EventRailTests` pins the ordering and, more importantly,
  the cells that must NOT stand: no plate before a tide opens, never both Wheel
  cells, nothing at all while the Wheel is inert, and no cache promised to a
  signed-out player. `HandleWorldTap` stands down over the rail so a cell tap
  cannot also pop a bubble. The strip's rect was briefly inset off the rail's
  right edge to clear it (`ReportWorldStrip`); that was **reverted 2026-08-12**
  because every plate centre is a fraction of that rect, so the inset moved all
  six over and shrank them to buy room the rail was already standing in.
- ~~**The cache's week is the calendar's now, not a rolling cooldown**~~
  ✅ RESOLVED 2026-08-12 — the weekly cache was counted off the last claim, which
  had two faults: before the first ever claim there was no anchor at all, so a
  countdown had nothing to say to a new run; and a cache taken late in the week
  pushed the next one later still, walking it around the calendar and away from
  whatever day Play sets its own out on. `Wheel.WeekStartMs` /
  `NextWeekStartMs` give the warden-local Monday (the midnight the tides already
  close on), `Amber.WeeklyCacheDue` asks whether one has been taken inside this
  week, and `Amber.WeeklyCacheNextDueInMs` is the single countdown every surface
  reads — the rail cell, the Camp row's "ready in", and the sheet. No save
  change: the claim stamp is the same field, read against a different week.
  Landed with the rail cell's own turn — it counts the week out where it used to
  say "look", and stands down entirely once the week's cache is taken. **Still
  owed: whether Play's own weekly reset is calendar-based, and on which day.**
  Ours is Monday on the guess that it is; worth confirming while §3.2's Play
  Console visits are open, because a mismatch is what puts an off-cadence
  delivery a day early or late against the row's own reading.
  **The public docs do not answer it — checked 2026-08-12 and again 2026-09-09,
  so don't re-read them.** The Level Up guideline is the only place the cadence is
  written down at all ("with a maximum of 1 reward per week per player", awarded
  after social challenges); it defines no week, no start day and no timezone, and
  neither the Rewards page nor the guideline says whether the developer can see or
  set the cap. Both pages were rechecked the day the gate opened and neither had
  gained a word on it. **Reward testing is open as of Sep 1 2026, so the one
  instrument that can answer this now exists: read the cadence off a real test
  delivery** (§3.2, §3.3). Worth
  knowing while it stands open: a mismatch cannot cost the player anything,
  because the grant is unconditional by design (§11) and an early delivery is
  taken rather than refused. The damage is confined to the countdown reading
  "ready in 2d" on a row that has just been paid, which is a wrong sentence and
  not a lost reward.
- ~~**A windfall drawing over the rail's cells could not be caught there**~~
  ✅ RESOLVED 2026-08-12 (Mo's call: the catch wins) — the rail moved into a
  camera-space canvas of its own so the band's windfalls pass in FRONT of it
  (`GameHud.Events.cs`, `StripLayers.RailCell`), which left the tap saying the
  opposite of the picture: `HandleWorldTap` stood the whole strip down over
  the rail's rect, so a windfall drawn over a cell opened the cell's sheet
  instead of being caught. The catch is tried first now, and a press that
  catches over the rail sets a one-shot the cell's click consumes
  (`_railTapCaught` → `OpenRailCell`) so the release cannot ALSO open the
  sheet. It rests on the two arriving in a fixed order — the strip reads a
  pointer PRESS (`InputSystemGameInput.TendTriggered` is
  `wasPressedThisFrame`), uGUI raises a Button click on the RELEASE — so a
  change to either end is what would break it, not a change to the rail. A
  press that catches nothing still belongs to the cell, which is the same
  rule the node plates keep.
- **The landscape chrome pass is unproven by anything but the eye** (2026-08-14).
  Chrome and band went from 44% of a landscape canvas to 26%, by three moves,
  none of which any test can see: the **tabs** leave the bottom bar for a rail
  down the page's fore-edge (`ApplyTabFold`, and `RailSeatsTabs` sends them back
  to the bar on a canvas too short to seat them — 32:9 is), the **tracker
  banner** folds up into the title's line beside the ledger (`ApplyHeadFold`),
  and the **camp's and warden's names** leave their cards for the page's own
  running head (`GameHud.PageIsNamed`). EditMode tests never build the HUD, so
  check by hand: that the lit tab in the rail still fuses to the page it opens
  (`OrientTabMerge` turns the merge strip on its side, and a strip left facing
  the wrong way looks like a rendering fault rather than a missing cue); that
  the title line does not crush the title when the Fold banner is up, which is
  the longest thing that line ever carries and has a button on it; that a pad
  walks the rail; and that turning to portrait puts the tabs back along the
  bottom, the banner back on its own row and the names back at the head of
  their pages. Two scrollbars were tried first and taken out the same day —
  don't put them back without reading `BuildPageArea`.
- **The rail's new canvas is unproven by anything but the eye.** EditMode tests
  never build the HUD, so nothing pins the rail's placement (`PlaceEventRail`
  converts the band's screen rect into the rail canvas's own units), the
  sorting rung, the modal trap it now keeps for itself (`HandleFocus`), or the
  press-then-release ordering the tap one-shot rests on. A camera-space canvas
  whose `worldCamera` is null silently draws as an overlay — which is the exact
  bug it was moved to fix, and it would look like nothing having happened.
  Check on a device that a windfall crosses IN FRONT of a cell, that tapping it
  there catches it and leaves the sheet shut, that a tap on bare plate still
  opens the sheet, and that the rail still lands in the band's left margin on a
  spread.
- **The rail wants a third inhabitant before it can be judged.** Two cells (the
  Wheel and the weekly cache) is enough to prove it is a list rather than a
  Wheel widget, and not enough to know whether the seat count, the urgency
  order or the drop-from-the-tail rule are right. The band's floor seats two;
  a third event is where `TrimRailToBand` starts making real decisions.
- ~~**The tides had gaps, and a gap is invisible**~~ ✅ RESOLVED 2026-09-09 —
  the sabbats now hold the wheel from their own night until the next takes it:
  eight pagan seasons back to back, no gap and no lead-in, and no
  `openDaysBefore` at all (design §15 records the decision and everything it
  moved). `Wheel.cs` walks one pass for the latest night fallen and the
  soonest still to come, which is the window; the calendar's last authored
  night holds 46 days and then the wheel goes quiet, so it is not the sabbat
  that silently never opens. The validator's overlap rule is now "no two
  sabbats on one night in one hemisphere". Countdowns everywhere are named for
  what ends the season (`JournalFormat.TideCloseWord`, shared by the tracker
  and the keeping's head so one wait is not told two ways). The open tide's id
  joined `StructureSignature`, or the Trail would keep the old season's head
  over the new season's slots. **The knock-on worth remembering:** the inside
  cover's reckoning was locked "while a tide is open", which is now every day
  of the year — it locks on the keeping having been BEGUN instead
  (`Keeping.Begun`, pinned by
  `Begun_IsWhatLocksTheReckoning_AndNeverDrawsTheKeepingToAnswer`), which is
  the tighter fit for the double-claim vector it was always guarding. Suite
  1170/1170.
- **Word/art debts**: the eight plates ~~in the template~~ landed 2026-08-08
  as **authored almanac marks** (`Art/Plates/Wheel/sabbat-*.jpg`) — the
  warden's calendar ornament rather than a naturalist page, deliberately:
  the calendar is the warden's (§15's naming device), the set is original
  work (no licence owed, `ArtCredits` untouched), and a painted plate can
  land over the same id any time. `EverySabbat_HasItsOwnPlate` pins each to
  its own mark. Still owed: the narrative pass re-voices the eight `sign` and
  `lore` lines drafted in `sabbats.json` and the keeping sheet's wording. The
  signs were re-cut on 2026-09-09 off the *"…, by my count"* opener, which
  dated a line that now runs six weeks; they read as a season rather than a
  night, but they are still draft.

---

## 2. Bugs & fixes

- ~~**A reward delivered while the game sits in the background is not noticed on
  the way back in**~~ ✅ RESOLVED 2026-08-12, the same day it was found. The
  resume branch of `GameLoop.OnApplicationPause` now calls `CheckPlayRewards`
  once the session is open, dropping the answer on purpose: a reward that landed
  announces itself through the confirmation sheet, and an unreachable store must
  not be reported as nothing waiting. No throttle, because a foreground check is
  what Google asks for and `FetchPurchases` is the same call a launch makes. **No
  test, deliberately:** nothing in EditMode instantiates `GameLoop`, which is the
  point of keeping its lifecycle methods this thin, and the piece worth pinning
  (`CheckPlayRewards` itself) was already only reachable through the
  MonoBehaviour. The device check belongs with §3.3's reward pass: claim in the
  Play Games app, switch back without killing the game, and the confirmation
  should be waiting. What was wrong, kept for the reasoning: Google's Rewards page
  asks the game to check for unacknowledged rewards "when the game starts or is
  foregrounded", and we did the first half only. A launch's first purchase fetch
  caught anything owed (`UnityIapStore.ProcessPurchase` on connect) and the Camp
  row's button asked on demand (`CampPage` → `CheckPlayRewards`), but a resume
  credited absence and reopened the session without asking the store anything,
  which is the *common* path for this feature: the player claims in the Play Games
  app and switches straight back to a live process. It was never a lost reward,
  since a cold start or the Camp row still found it inside the three-day claim
  window, but it made the arrival wait on the player doing something arbitrary.
- ~~**A reward's confirmation can be lost outright, where its payout cannot.**~~
  ✅ RESOLVED 2026-09-10, save rung 54→55: `GameState.rewardsOwedTelling` holds
  the ids a run still owes a telling for, `RewardGrants.Apply` writes the debt
  down as it banks the credit, `RewardGrants.Words` reads the sheet's copy back
  without granting anything again, and the Continue press on the sheet is what
  strikes it off (`GameLoop.TellingDone`). A reward is therefore told twice at
  worst and never lost. The debt crosses the fold (`Migration`), follows the
  book on a cloud adoption rather than the device (`RunSwap`, pinned in
  `RunSwapTests`), and an id this build cannot name words to null and is
  dropped, which is how the retired cloak's debt would retire with it. What
  follows, kept because it is the reasoning:
  <br>Found 2026-09-10, making the cache row honest. `Announcements` holds the
  delivered `RewardGrant` in a plain in-memory `Queue`, nothing in `SaveCodec`
  persists it, and `RewardGrants.Apply` has already credited the pile and
  stamped `weeklyCacheClaimedUnixMs` by the time it is queued. So a reward that
  lands and is acknowledged on a process that dies before
  `JournalSheets.PumpSheets` reaches the queue leaves the player paid and never
  told. It is the compliance half as well as the courtesy one: design §11 has
  the confirmation naming the item and standing until the player acknowledges
  it, which is what Google asks for after any out-of-app purchase.
  <br>The evidence went in first and stands on its own merit:
  `Amber.WeeklyCacheEverTaken` and `WeeklyCacheSinceLastMs`, read by the Camp
  row's note and the cache sheet, so a player can ask whether a cache ever came
  and when, whatever the sheet did or failed to do.

- **⚠️ THREE INCREMENTAL STEP COUNTS ARE FROZEN AND CANNOT BE CORRECTED. Found
  in the console 2026-09-09.** `Steps needed` is greyed out on a published
  achievement, over the words *"This can't be changed after the achievement is
  published."* So the plan this entry carried for a month — move the console to
  match the data — **is not available on any of the three**, and no flag, tool or
  API call gets around it. They are fixed at **12, 5 and 14** for as long as they
  exist.
  <br>**What this costs, exactly.** `Achievements.Reassert` reports absolute
  progress and Play unlocks at the *console's* target, so the console's number is
  the one that decides and the code's is only a clamp. All three therefore fire
  EARLY against their own names: The Whole Wood at 12 of 14 species, Every Plate
  Drawn at 5 of 7 plates, The Almanac Complete at 14 of 19 nodes. Nothing is
  broken and nothing fails to fire; the names and descriptions are simply lying
  about what they mean.
  <br>**Which inverts the fix.** The in-repo change made earlier the same day
  (12→14, 5→7, 14→19 in `Achievements.cs` and the manifest, with tests pinning
  each to its data-derived count) now points the wrong way: it pins the code to
  three numbers the console can never hold. Unless the entries can be deleted and
  remade, **that change has to be reversed** and the achievements reworded to be
  honest at 12, 5 and 14 instead.
  <br>**Delete is not offered either — checked in the console 2026-09-09.** A
  published achievement has no Delete in the UI, which matches the guard
  `pgs-achievements.py` already carried (`if item.get("published"): left alone`)
  and the note in its header that a published achievement carries player data and
  stays. So delete-and-recreate is out, even though it would have cost almost
  nothing here with no players and no testers to lose unlocks for.
  <br>**DECIDED 2026-09-09 (Mo): reword the three so each is honest at its frozen
  number**, rather than add a second completion tier beside them. Done in-repo the
  same day; the console renames are the remaining half.
  <br>**Do NOT run `pgs-achievements.py --apply --update` until this is settled.**
  It PUTs the whole resource including `steps`, so against a frozen field it
  either fails and aborts the run at the first of the three (`return 1`, leaving
  the rest untouched), or the API accepts the call while ignoring the field and
  the tool prints `~ updated (steps)` for a change that did not happen. The
  second is worse, because it reads as success.
  - **The three renames, all by hand in the console.** Each is a name and a
    description; the step count beside them stays exactly where it is.

    | Console now | Rename to | New description |
    |---|---|---|
    | The Whole Wood (12) | **Twelve Companions** | Befriend twelve of the species that walk the grove. |
    | Every Plate Drawn (5) | **Five Plates Drawn** | Draw and record five insect plates. |
    | The Almanac Complete (14) | **The Almanac Well Thumbed** | Buy fourteen of the nodes the Almanac holds. |

    The slugs and the C# constants keep their old spellings on purpose:
    `all-five-plates` and `AchievementIds.TheWholeWood` are internal, the encoded
    ids never change, and renaming them would be churn for nothing.
  - **`RecordedPlateCount` still skips a `rewarded` plate**, and that survives the
    reword on its own merit: the Wayfarer's Plate arrives from a Play Games Reward
    and nobody sketches it, so counting it would hand a step of a *drawing*
    achievement to whoever happened to run a Quest.
    `Observation.EligibleInsectsInto` holds the same line for the sketching pool.
  - **⚠️ Do the console renames BEFORE any tool run, and prefer no tool run at
    all.** `pgs-achievements.py` matches the console **by display name**, so a
    manifest renamed ahead of the console reads as three missing achievements and
    `--apply` inserts three duplicates. Worse, `--update` PUTs the whole resource
    including `steps`, so against a frozen field it either fails and aborts at the
    first of the three, or the API takes the call while ignoring the field and the
    tool prints `~ updated (steps)` for a change that never happened.
    <br>Once the four hand edits are done (these three plus Choice), the manifest
    and the console agree and **there is nothing left for the tool to push**. Run
    it with no flags to confirm that — that plans and writes nothing, and a clean
    run of `=` lines is the proof. The one entry that genuinely wants a tool push
    is **Fixed in Ink**, whose description alone drifted; hand-edit that too and
    the tool need not run at all this visit.
    <br>The token default was pointing at `Documents/Wildgrove-secrets/`, which
    does not exist on this machine any more; corrected 2026-09-09 to
    `../Wildgrove-secrets/` beside the checkout, where the upload keystore
    already lives. The token itself is an hour-long OAuth Playground grant with
    the `androidpublisher` scope, so it is fetched per visit, never stored.
  - **⚠️ The step counts do not go live until the configuration is PUBLISHED,
    and publish is project-wide — which couples this to Game Stats.** The tool
    writes drafts only (`published` is read-only on the API, with no publish
    method), so pushing the three counts leaves them staged behind the wrong
    published figures. Clicking publish in the console makes them live **and in
    the same act promotes the Game Stats draft to production**, ending the
    window in which a stats row can still be deleted. §3.4 wanted those two
    visits kept separate; the honest position is that they cannot be, so it is
    one decision rather than two.
    <br>**DECIDED 2026-09-09 (Mo): publish, and take Game Stats to production
    with it.** Closed testing has not begun, so there are no players and no
    testers at all: a production stats config can disturb nothing, because there
    is nothing yet for it to disturb. That makes now the cheapest moment this
    click will ever have, and it takes both figures off the board before anyone
    is looking at them. So the visit ends with a publish rather than avoiding one.
    <br>**Two things that follow.** Publish promotes *everything* staged on both
    screens, not only the rows you came for, so look over the drafts before
    clicking. And once published, a stats row is a public artifact carrying
    player data and cannot be deleted — which moves the care from after the
    click to before it, onto the CSVs in `store/play-games/gamestats/`.
  - ~~Add with them a test pinning each constant to its data-derived count~~
    ✅ DONE 2026-09-09. `TheWholeWood_AsksForEverySpeciesTheDataHolds`,
    `EveryPlateDrawn_AsksForEveryPlateThatCanActuallyBeDrawn`,
    `EveryPlateDrawn_IsNotAdvancedByAnAwardedPlate` and
    `TheAlmanacComplete_AsksForTheOneOffNodesOnly` sit beside
    `EveryStoneRead_TurnsOverOnTheLastZoneTheDataActuallyHas` and read the real
    `design/data` through a shared `ShippedDesignData()`. The constants stay
    hardcoded because the console holds the same figure and cannot be asked for
    it at runtime; the tests are the only place the two can be held together.
    Suite green at 1167/1167. Still worth doing for **"Reader of Stones" (4)**
    if that number ever means anything other than "some of them".
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
  <br>**The rename commit (`570d36f`) touched TWO achievements, and only one of
  them is manual.** Alongside `choice` it corrected **"Fixed in Ink"**, whose
  description went from "Fix a Pristine specimen into the Folio." to "Fix a
  Choice specimen into the Folio." That one's *name* never changed, so the tool
  matches it fine and `--apply --update` pushes the new description on its own.
  Nothing to do by hand for it — but it does mean the console currently shows
  the retired word "Pristine" to players in **two** places, not one, and a visit
  that fixes only the rename leaves the second sitting there.
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
    now, and `Stage_PostsOnlyGroundsAndTheSketching` pins that the showcase posts
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
  <br>**The first seam is now half closed (2026-08-13).** The *watch* was renamed
  to the *sketching* through the code and every player-facing line, and the
  persisted post id moved with it: `dig:{zone}` → `sketch:{zone}`, on **save rung
  54**, which rewrites the ids in place and is safe to because it carries the zone
  half through untouched. What is deliberately NOT renamed is the half that lives
  in data, because each of those is a schema change with a `GameData.asset`
  re-import behind it and a validator to keep in step:
  `economy.observation.watchXpPerHour`, `economy.observation.pityTimerHoursWatched`,
  `ambers.pityHoursWatched`, `economy.amber.digFindsPerHour`, `digSpeedMult` and
  its whole effect/trait family (`EffectType.DigSpeedMult`, `digSpeedBonus`,
  `Upgrades.DigSpeedMultiplier`), `EffectType.UnlockDigSite`, `zone.digSite`,
  `GameState.digSites`/`DigSiteState`, and `PlaceholderArt.DigSiteColour`. So the
  code and the pages say *sketching* while the data still says *dig* and *watch* —
  which is a smaller mismatch than the one it replaced, and one whose whole cost
  is a reader's surprise rather than a wrong noun on screen. Close it in a pass of
  its own, with the JSON and the re-imported asset in the same commit.
  <br>Also renamed in the same pass, for the record: `TrailPage.Watch.cs` →
  `TrailPage.Sketching.cs`, the card heading THE WATCH → **THE SKETCHING**, and
  `Stationing.WatchAgentsAt`/`WatchersAt` → `SketchAgentsAt`/`SketchersAt`.
  `Familiar.LegacyWanderStation` and the new `Familiar.LegacyWatchStationPrefix`
  are the two ids that keep the old words on purpose: both are read only by
  migration, and a migration must go on saying what the save it is reading said.
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
- **Attach a Play Games Reward offer to each of the three reward products —
  ✅ DONE 2026-09-09: all three offers created and ACTIVATED.** Icons came from
  `store/iap/reward_*-icon.png`, generated 2026-08-12 by `make-store-art.py`, all
  1024×1024 32-bit RGBA and inside Play's 512–1080 window. **`LU-RE-GAC` is now
  met in the console as well as in code**, five months before `LU-RE-GAD` is due.
  Note these live on the *Monetise* side, so they did not wait on the Play Games
  Services publish that §2's renames need. What remains is proving one actually
  lands, which is the device pass in §3.3.
  <br>The gate opened Sep 1 2026 and was
  re-verified 2026-09-09 against the [Rewards page](https://developer.android.com/games/rewards)
  and the [Level Up guideline](https://developer.android.com/games/guidelines):
  the wording has not moved a day. Adding a reward, removing one and proving
  end-to-end delivery are each still marked "available from September 01, 2026",
  which is now behind us, and Level Up enrollment opened the same day. Nothing
  is waiting on us and nothing was pushed back.
  <br>**`LU-RE-GAC` wants ≥2 single-use offers by Sep 30 2026**, and
  `reward_drovers_halter` and `reward_wayfarers_plate` are the two, both created
  and activated, so this is an attach and not a build. (`LU-RE-GAD` wants ≥1
  repeatable by Mar 1 2027; `reward_weekly_amber_cache` is it.) **Read both
  dates as guideline phase-ins rather than deadlines we can miss** — see the
  note below on why an unreleased title cannot fall off that cliff.
  <br>The client owes nothing here: `RewardProductIds` splits Durable from
  Repeatable, `StoreCatalogue.IsConsumable` answers true for the cache so a
  second delivery is never refused as already owned, `RewardGrants.Apply` holds
  the grant → tell → acknowledge order, and the resume path checks for
  unacknowledged rewards, which is the half Google's page asks for in as many
  words. What is owed is entirely console and device.
  <br>**Do in this order:**
  1. Attach an offer to `reward_drovers_halter` and `reward_wayfarers_plate`
     first. Those two are the Sep 30 bar; the cache can follow at leisure.
  2. Attach the offer to `reward_weekly_amber_cache`.
  3. Then the device pass in §3.3 — claim in the Play Games app and switch back
     to a live process without killing it, which is the path the 2026-08-12 fix
     was written for and the only one that has never been walked.
  <br>**Enrolling in Level Up is NOT one of these steps, and cannot be yet.**
  Enrollment is per-title and the help article's prerequisite is that the game
  is "published to production on Google Play and has been in compliance with
  all applicable Play policies" — Wildgrove is pre-launch, so the form has
  nothing to submit. Enrollment belongs in §3.5 with the launch tail, after the
  store listing and the closed beta, not on this visit.
  <br>**Which resets what Sep 30 2026 means for us.** It is the phase-in date
  for `LU-RE-GAC` as a *guideline*, i.e. the state a title must be in when it
  is assessed, not a cliff an unreleased game falls off. Attaching the two
  offers now is still the right move (it is cheap, the products are already
  live, and it takes the row off the board before launch), but it is not the
  emergency a Sep 30 date suggests. Nothing here expires in three weeks.
  <br>**Read the weekly reset off the first cache delivery while you are in
  there.** `Amber.WeeklyCacheNextDueInMs` counts a warden-local Monday on a
  guess, and the docs still refuse to define the week: re-read 2026-09-09 and
  the only sentence anywhere is the guideline's "with a maximum of 1 reward per
  week per player", with no start day and no timezone, exactly as on 2026-08-12.
  A test delivery is now the only instrument that can answer it. A mismatch
  costs the player nothing (the grant is unconditional and an early delivery is
  taken, not refused); it costs the countdown row a wrong sentence.
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
- ~~**Bring the Data Safety form up to what ships — before the next release.**~~
  ✅ **ANSWERED IN THE CONSOLE 2026-09-09 (Mo)**, to the table below. It had never
  been revisited since Play Games and Game Stats landed. Two things to confirm on
  the next visit rather than assume: that the form was **submitted** rather than
  left saved (it goes through Google's review as part of app review, and the
  review checks for policy violations, not for whether the answers match the
  SDKs), and that the store listing preview reads the way you expect. Changes here
  do NOT need an app release and propagate on their own, which is why this could
  be done pre-launch at all.
  <br>**Keep the table below as the answer of record**, because the form is the
  one artifact of this whole visit that lives nowhere in the repo. If an SDK is
  added or dropped, this is the thing to re-derive: adding any UGS package,
  re-adding `com.unity.analytics`, or dropping Firebase all move rows.
  **The answer set below was derived 2026-09-09 from
  what the build actually does**, against the shipped packages, the telemetry
  code, Google's own SDK disclosures and the live privacy page. The page itself
  was re-read the same day and is **accurate and current** — it already covers
  Firebase, Play Games, Game Stats and ads, so the form is the only stale half.
  <br>**⚠️ Firebase is a real collector and the old note here never mentioned it.**
  `com.google.firebase.analytics` 13.13.0 ships and `FirebaseTelemetry` is the
  live sink on device: gameplay and purchase events to Analytics, crashes and
  non-fatals to Crashlytics.

  | Play data type | Collected | Shared | Ephemeral | Optional | Why |
  |---|---|---|---|---|---|
  | Location › Approximate location | Yes | **Yes** | No | No | AdMob's IP plus Analytics' masked IP |
  | Personal info › Name | Yes | No | No | No | the Play Games display name, which the leaderboard shows |
  | Financial info › Purchase history | Yes | No | No | No | Analytics logs purchase events with product id, name and price |
  | App activity › App interactions | Yes | **Yes** | No | **No** | AdMob's "user product interactions" plus Analytics screen views and sessions |
  | App activity › Other actions | Yes | No | No | **Yes** | Play Games achievements, leaderboard and Game Stats counts, all gated on Play notes |
  | App info and performance › Crash logs | Yes | No | No | **No** | Crashlytics is deliberately NOT covered by the toggle |
  | App info and performance › Diagnostics | Yes | **Yes** | No | No | AdMob's diagnostics are shared even though crash logs alone would not be |
  | Device or other IDs | Yes | **Yes** | No | No | AdMob's ad id and app set id, Analytics' app-instance id, Crashlytics' install UUID |

  **Ephemeral is No on every row, and that is a finding rather than a default.**
  Play lets data processed only in memory, never persisted beyond servicing the
  request, be declared ephemeral — and declared *not collected* on that basis, so
  it is the one answer that could quietly excuse the whole table. Nothing here
  qualifies. AdMob's disclosure page **carries no ephemeral marking on any of its
  four rows**, and the form has to be answered on that absence rather than on a
  sentence saying so; Analytics and Crashlytics exist precisely to retain what
  they gather so it can be read back later; and the Play Games rows are durable by
  design, since a stat you cannot see next week is not a stat. Anyone revisiting
  this and tempted to mark a row ephemeral should treat that as a signal they have
  misread what the SDK does.

  **The three rows that catch people, each for the same reason: a row answers for
  the WHOLE app, so the least private contributor decides it.**
  - **App interactions cannot be Optional.** The "Play notes" toggle stops our
    Firebase events and the Play Games stats, but it does not stop AdMob, and
    AdMob collects product interactions regardless.
  - **Crash logs cannot be Optional either, and this one is new.**
    `FirebaseTelemetry.LogException` is not gated on `_collecting`, there is no
    `IsCrashlyticsCollectionEnabled` anywhere in the tree, and the privacy page
    says so on purpose: *"Crash reports are sent whichever way that is set: they
    carry nothing of your run, and a build that cannot report its own faults
    cannot be mended."* Code and page agree; the form must too.
  - **Diagnostics is Shared** while Crash logs is not, because the sharing comes
    from AdMob rather than from Crashlytics.

  **The rest of the form**, unchanged and re-confirmed 2026-09-09: deletion URL
  `https://decryptic.app/wildgrove/privacy#deleting-your-data` (**anchor verified
  live today** — a markdown-converted read will tell you it is missing, which is
  the converter stripping ids, so check the raw HTML before believing it); no
  account creation; users *can* sign in with an account created outside the app
  (the Google one); App access is *all functionality available without special
  access*; data is encrypted in transit; users can request deletion.
  <br>**Genuinely a judgement call, so decide rather than inherit:** whether the
  Play Games rows (achievements, cloud save, Game Stats) need declaring at all,
  or count as Google's own processing against the gamer profile. Declared above
  on the cautious reading, which is the safer way to be wrong.
- **AdMob's own disclosure table, read 2026-08-05 — three rows are stricter than
  they look.** Google publishes the SDK's collection at
  `developers.google.com/admob/unity/privacy/play-data-disclosure`, and it says
  **collected *and shared*** for all four: IP address (→ *Location · Approximate
  location*, which the form must therefore declare), user product interactions (→
  *App activity · App interactions*), diagnostics (→ *App info and performance ·
  Diagnostics*), and device/account identifiers (→ *Device or other IDs*, the ad id
  and the app set id). None is ephemeral, which is the *absence* of an ephemeral
  marking on the page rather than a sentence saying so, and the form has to be
  answered on the absence. **Re-verified 2026-08-12, and the page now names the
  version it describes: GMA 25.4.0, which is the Android SDK our GoogleMobileAds
  11.3.0 carries** — so the table is being read against what we actually ship.
  Two consequences: **App interactions cannot be declared Optional** —
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
- **Three published achievement icons are white boxes on parchment**, found
  2026-08-12 while drawing the Game Stats icons. `res-sky-blossoms`,
  `keystone-cloudfleece-ram` and `res-glacier-ice` were cut with no alpha channel,
  so the elliptical fade that dissolves a straight crop has nothing to work on and
  the plate lands as a rectangle: *twenty-five-verses*, *ten-folds* and
  *cloudreach* wear it. `make-store-art.py` now prints `WHITE BOX` for any such
  card rather than failing, because the fix is a console decision and not a code
  one: Play refuses a configuration where two achievements share an icon, so each
  needs its own replacement plate, and all three are already uploaded. Cheapest
  route if it is judged worth doing at all: cut an alpha channel into those three
  plates (they are the game's art too, so the fix would improve both), rather than
  reassigning three achievements to different subjects. Not urgent, and honestly
  invisible unless the profile is read next to the tidier cards.
- **Re-step the three drifted achievements** (§2) in the same visit.
- ~~**Add Mo as a license tester**~~ ✅ DONE 2026-09-09 (Settings → License
  testing), so test purchases aren't charged. This is the list §3.3's device pass
  needs; it is NOT the same list as the PGS project's own testers, which is what
  gates a Game Stats draft.
- ~~**Keep Sidekick on for CI uploads.**~~ ✅ DONE 2026-09-09. Sidekick is added at
  *upload* time for App Bundles. Every release here is uploaded by
  `android-release.yml` via `r0adkll/upload-google-play`, so Testing → Advanced
  settings → **Play Games Sidekick** → *"Automatically make Sidekick on by default
  for new app bundles"* had to be set, or each CI upload would land Sidekick-less
  and `LU-SK-GAA` would quietly fail. Worth re-checking after any Play Console
  redesign, since nothing in a build log would ever say it had come unset.

### 3.3 On device — the build is not proven until these are done

- **The room a big screen is given rests on `Screen.dpi`, which nothing here can
  check.** `JournalLayout.RoomFactor` (2026-08-14) asks the screen how big it
  physically is and hands a tablet up to 1.6× the canvas units of a phone, so
  the book gains rows and margins instead of being a magnified phone. **Phones
  are untouched by construction** — the curve starts at the largest handheld
  (`PhoneInches`), so nothing under a tablet moves at all — and an unknown or
  implausible density takes the phone's answer rather than the roomiest, since a
  tablet's page at a phone's size is unreadable while the reverse is merely
  generous. What still needs eyes on hardware: that a 10-inch tablet reads as
  *more book* rather than as *small text*, that a 4:3 tablet held upright keeps
  a phone's line measure with paper either side (`SideMargin`'s single-page cap,
  same date), and that a foldable opening re-scales rather than keeping the
  folded phone's units.
  **⚠️ The Editor's plain game view cannot preview a tablet, and must not be
  asked to.** `Screen.dpi` there is the DEVELOPER'S MONITOR — not the game view,
  not its resolution, not any device. Read straight it made every preset look
  like a twenty-inch screen, so the journal took its roomiest layout at every
  resolution *including phone presets*, and a phone was laid out as a tablet at
  a phone's size (seen 2026-08-14, and the reason `DeviceForm.ScreenDpi` exists).
  The game view is now answered with `JournalLayout.ReferenceDpi` instead, so a
  preset previews "this many pixels at a handheld's density" — deterministic,
  and the same on every developer's machine, but it means a large preset reads
  as a large PHONE and shows almost none of the tablet's room. **Use the Device
  Simulator with a tablet profile** (it drives `UnityEngine.Device` with that
  device's real dpi, which is why everything here reads `Device.Screen` rather
  than `Screen`), or a device.
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
- **The real out-of-app reward delivery — and it CANNOT be triggered from this
  side. Re-read 2026-09-10.** `StubStore.DeliverReward` exercises grant →
  acknowledge and the refusal branch in the editor, and that is still the only
  place the flow can be made to happen on demand. Google's Rewards page is
  explicit about who starts a real one: *"Play will grant a player a Play Games
  Reward after they have successfully completed an engagement mechanism, such as
  a Quest, for your game"*. On testing it says only that the flow can be
  *"test[ed] fully on or after September 1, 2026, once Play Game Rewards have
  been created"*, and points at the Play Billing testing guide for the rest.
  **It documents no developer-triggered test delivery, and the console offers no
  such control.** So the three walks below wait on Play setting a reward out for
  a Quest or Social Challenge; they are not a device pass anyone can sit down and
  do, and for a pre-launch title with no players they are not available at all.
  Do NOT hold anything else behind them.
  <br>Walk all three when a real award does land: claim in the Play Games app and
  come back to a **live** process (the resume branch of `OnApplicationPause`,
  fixed 2026-08-12 and never once walked in the field), claim and come back to a
  **cold start**, and let one sit past its three-day window to see it refunded
  rather than stuck.
  <br>**What IS doable now, and is the whole of the device pass here:** an
  internal-track build's catalogue fetch should resolve every reward id with a
  price. An id coming back unavailable means the console entry and
  `RewardProductIds` disagree — the one failure that would silently swallow every
  future award. It needs no Quest and no delivery.
  <br>**So the weekly cache's reset day cannot be read yet either.** §3.2 and
  §1.8 both say a real delivery is the only instrument for it, which is right;
  the instrument is simply not in our hands. Ours stays a warden-local Monday on
  a guess, and a mismatch still costs only a wrong sentence in a countdown row.
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
  - **Console: the screen exists and the CSVs have been through its validator
    once — 2026-08-13.** Rejected on content, not shape: the header row was taken
    as authored, and the twelve errors were all `Unit` (a closed physical-unit
    enum, and mandatorily empty on a `STRING`/`BOOL` property, where a value is
    read as a duration format) and the `Min limit`/`Max limit` pair, which is
    required when `Is Competitive` is `true` and forbidden when it is `false`. All
    fixed, the ZIP is rebuilt, and the whole account with the reasoning is in
    `store/play-games/gamestats/README.md`. **Re-uploaded clean the same day: all
    seven events and seven stats are in as Draft, available to testers.** Next is
    a tester read, and then **a publish — DECIDED 2026-09-09 (Mo)**. The case for
    holding was that draft is already testable and publishing ends the window in
    which a row can still be deleted; the case that won is that closed testing has
    not begun, so there is no one in the field to disturb and this is the cheapest
    this click will ever be. Publish is
    project-wide, so §2's step counts and this stats draft go to production in one
    act; those two visits do not need separating after all, they need doing
    together. **Get the CSVs right before the click:** a published stats row
    carries player data and stays.
    **The guide caught up 2026-09-09, and the console is no longer ahead of it.**
    The overview has been rewritten since it was last read on 2026-08-13: the
    "available for early feedback" caveat is gone, so is the undated "GA starting
    August 2026", and the words "beta" and "early feedback" now appear nowhere on
    it. The milestones table is down to a single row, **September 2026 — "Players
    start seeing game stats on their Gamer profile"**, alongside "The Game Stats UI
    will be available in September 2026". So GA has happened, the tester read is
    unblocked, and the player-facing half lands this month. Nothing here changed
    shape; what changed is that the last reason to wait went away.
    **The format spec was found the same day, on a page nothing here had read:**
    [Integrate Game Stats](https://developer.android.com/games/pgs/integrate-gamestats),
    which also gives the console path (**Grow users → Play Games Services → Setup
    and management → Game Stats**) and carries no beta caveat, unlike the
    overview. It bounced three things in the authored CSVs, all now corrected:
    `HIGHER` is not a value (the column takes `INCREASING`/`DECREASING`), `INT` is
    not a type (`INT64`, `DOUBLE`, `STRING`, `BOOL`, case-sensitive), and
    `Is Competitive` is lower-case `true`/`false`. **The icon spec exists too:**
    512 × 512, PNG or JPEG, ≤1 MB, in the ZIP's root — and **the seven were drawn
    2026-08-12** (`make-store-art.py`, keyed off the CSVs so a stat cannot exist
    without an icon), which means nothing on this side is owed any more: the ZIP
    is `store/play-games/gamestats/`'s own contents. The page's formal format lines
    and its worked example disagree about the config headers; **the format lines
    are the ones the console implements**, proven by the 2026-08-13 upload taking
    that header row without complaint, so the example's shape is not a fallback we
    need to keep in hand any more.
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
- **Closed beta: 2–3 weeks of vitals** — **not started; being built towards as of
  2026-09-09.** Nothing is in anyone's hands yet, so §3.3's device list is still
  work someone has to sit down and do rather than something testers will surface,
  and no vitals exist for the ship gate below to judge. Run the Play Games on PC
  opt-in and the Level Up self-check during it, as planned.
- **The ship gate:** vitals green 14 consecutive days and D1 retention >30% in
  beta → ship. The onboarding pass (§1.7) feeds the same gate — the first-hour
  funnel must be green.
- **Enrol in Level Up — AFTER the game is live in production, never before.**
  Enrollment opened 2026-09-01 and is **per-title, not per-account**, needing
  admin or account-owner permission. Path: **Monitor and improve → Policy and
  programs → Programs → Level Up**. The form asks you to answer for each
  guideline how the game implements it, to raise any exemption requests before
  confirming compliance, and to accept the terms; review takes **up to 14 days**,
  and the verdict arrives by email and as a Play Console notification. A
  rejection lists the specific failures, and the same screen then offers three
  routes back: fixed everything, appeal, or partial fix plus appeal. This is
  what design §12's table is *for* — it is the answer sheet for that form, so
  walk in with it open.
  <br>**The prerequisite is production.** The help article requires the game be
  "published to production on Google Play and has been in compliance with all
  applicable Play policies", which is why this sits here and not in §3.2.
  <br>**Two things worth knowing before it disappoints:** the rate card does not
  take effect for any approved title until **Sep 30 2026** (after that date,
  within 24 hours of enrollment), and eligibility rolls out by **user** location rather
  than developer location: AU, EEA, JP, UK and US from **Sep 30 2026**, KR from
  **Dec 31 2026**, rest of world **Sep 30 2027**. NZ players therefore fall in
  the last wave even though the AU and US ones do not, so a NZ-first audience
  sees the benefit last.
  <br>**Unresolved, and worth one look at the form rather than trusting this
  file:** the enrollment help article appears to state a **$1M USD earnings over
  the last 12 months** threshold, but it documents two programs side by side
  (Apps Experience and Level Up) with identical-looking prerequisite blocks, and
  every other Google source says the opposite in as many words — "the Level Up
  program is open to all games", "all games on Google Play qualify". The gated
  tier is **Level Up+**, whose published bar is engagement (~1.6M MAU / active
  installs), not earnings. Best reading: the $1M belongs to Apps Experience and
  base Level Up has no revenue floor. Confirm on the screen when the time comes;
  it costs nothing to be wrong about until then.

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
