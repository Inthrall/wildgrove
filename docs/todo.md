# Placeholder & TODO manifest

A living list of the deliberate placeholders and deferred work in the codebase, so
nothing quietly ships as "done". Grouped by the phase that retires it (see
`design-doc.md` §13 MVP development plan). Keep entries pointing at the code so
they're easy to find and delete when resolved.

## v0.11 design realignment

Design doc v0.11 (July 2026) makes several **DECIDED 2026-07-18** reversals. The
per-item detail below points at the code/data; the phase lists further down are
annotated **v0.11:** where a decision touches them.

**DECIDED 2026-07-23 — the collection ladder (slots · traits · store).** Mo's calls:
one slot to start, three earned early/mid/late ("rites, something like 2-5-10"),
two purchasable (the first inside the initial purchase bundle for Play Level Up,
bundle = slot + Amber); more familiars obtainable than slots; each familiar has a
single fixed trait per species ("you only have one vole ever"), replacing powerups.
Implementation + interpretations:
- **Slots = the right to hold a post.** `Kith.Slots` = `slotsBase` (1) + verse
  milestones passed (`economy.kith.verseMilestones` [2,5,10] — first guesses, tune)
  + `GameState.purchasedKithSlots`, capped at `slotsMax` (6). The roster is the
  collection — one familiar per species ever (`Roster.Recruit` refuses duplicates),
  never slot-capped; unstationed familiars **rest at camp**: no output and **no run
  XP** (interpretation — resting is fully idle; wandering ×0.5 retired because free
  half-labour would gut the ladder's value).
- **Ladder currency = lifetime verses sung** (`foldedVersesSung` banked at Migration
  + current rite's completed verses, derived — self-healing, no event counter).
- **Traits replace powerups** (`Traits.cs`; `species.json` one `trait` per species;
  13 species authored — 9 node specialists, 2 trail, 1 watch, 1 pristine).
  `offlineBonus` powerup kind dropped (was never consumed by the sim). Old Friend
  and Warden's Gallery lose their `kithSlot` effects (EffectType removed); Old
  Friend keeps Burr's bond. Species art: new species need plates (ArtLibrary
  falls back gracefully until then).
- **Gift piles rescoped: one pile per verse sung** (was one-shot). The pile at a
  node calls *that resource's un-owned specialist* (derived from traits —
  `gifts.species` config retired; validator enforces one specialist per resource).
  Interpretation: non-node species (dray-stag, tawny-owl, ermine, cavern-bat)
  currently have **no acquisition path** beyond bonds — future arrival content.
- **Bonds honour the existing companion** of their species (keeps the player's
  name) or bring it resting if never met — Sootwing/Burr no longer mint twins.
- **Store:** `starter_bundle` (slot + `economy.store.starterBundleAmber` 30, one-time
  grant flagged by `starterBundleAmberGranted`) and `kith_slot` products; both
  NonConsumable, catalogue in `StoreProductIds.All`; `KithPurchases.Apply` folds
  entitlements in (never downgrades — a billing hiccup can't shrink the ladder);
  synced on purchase only (billing stays lazy per the startup-crash fix — a
  reinstall shows purchased slots after the first store touch; no restore-purchases
  affordance yet, matching remove_ads). **Play Console: create both products.**
- **Save v26:** `powerupIds` dropped; `foldedVersesSung`/`purchasedKithSlots`/
  `starterBundleAmberGranted` added; Restore dedupes duplicate species (bonded,
  then deepest Kinship kept) and rests stationed familiars past the ladder.
  ⚠️ Existing test saves restore to a 1-slot ladder — most of the roster wakes up
  resting; wipe or re-station.
- **Early-game pacing watch:** with 1 slot the seed raven rests, so nothing hauls —
  a stationed gatherer's basket overflows (lost) until slot 2 (2 verses) unless the
  player fields the raven instead; warden hand-gather (straight to camp) is the
  bridge. Deliberate scarcity, but tune `verseMilestones[0]` down to 1 if the first
  hour drags. Also: the first pile (verse 1) usually needs a rest-someone swap
  before it can be answered (arrival needs a slot).
- **RiteGenerator:** `KithGatherPosts` now reads `economy.kith.generatorGatherPosts`
  (2, conservative run-2 slots) instead of `slotsBase − 1`.

**DECIDED 2026-07-23 — verses are sequential.** Each verse is locked behind
completion of the one before it (`Rite.IsVerseRevealed` now also requires every
earlier verse sung; `Rite.IsVerseSealed` distinguishes "site not reached" from
"not its turn"). Interpretations: (1) the gate is on *reveal*, not just delivery —
a sealed verse shows one quiet cairn card naming the verse barring it, and only
the first sealed verse gets a card; (2) "previous one" = every earlier verse in
rite order (an out-of-order legacy save can't skip ahead past an unsung verse;
its later-verse progress is preserved and resurfaces when the verse unseals);
(3) completing a verse re-syncs deed slots immediately so deeds done while
sealed count the moment the next verse opens.

**IMPLEMENTED 2026-07-21 (kith + money→XP + Exchange + naming; 370/370 EditMode green):**
- ✅ **Coin is gone — money becomes XP.** `GameState.coin` removed; Renown = lifetime
  XP (warden skills + familiars) + offering credits; upgrades reprice to skill-gate
  (`gateSkill`/`gateLevel`) + material bundle; buildings → material bundles; zones drop
  `mapCostCoin`. New **Exchange** (`Exchange.cs` + `exchange.json`) barters goods↔goods
  off the trade-value table (`Economy.TradeValuePerUnit`), spread + player-favourable
  rounding.
- ✅ **Kith of individuals + stationing.** `NodeState.familiarCount`/`GameState.carrierCount`
  gone; `GameState.roster` of `Familiar {name, species, xp→level, kinshipXp, powerupIds,
  stationId, bonded}`. `Stationing.cs` sums stationed agents (wanderers ×0.5). Powerups
  (`Powerups.cs`, `species.json` pools) chosen every 5 levels. Familiar XP + Kinship
  (`Familiars.cs`/`Kinship.cs`; run XP → Kinship √ at Migration, +start level +XP rate).
- ✅ **Bonds → roster.** Bonded familiars materialise into the roster (`Roster.SyncBonded`),
  stationed like any other; role split retired (carrying is a post).
- ✅ **Naming.** Player names a familiar on arrival (HUD `InputField` sheet) and can rename;
  default from `species.json suggestedNames`.
- ✅ **The Folio (Museum retheme).** Museum→Folio, set→spread, donate→fix throughout
  (`Folio.cs`/`folio.json`/`FolioSpreadDef`/`FolioSpreadData`/`GameState.fixedResources`,
  `EffectType.FolioSpreadBonusMult`, bond source `folioSpread`); save migrated v20→v21
  (`donatedResources`→`fixedResources`); HUD shows a Folio spread-progress section.
  Mechanic unchanged (fix a Pristine of each entry → complete spread → permanent bonus
  surviving Migration). Deferred: spreads are still 2–4 entries (design wants 4–8, a balance
  pass).

**IMPLEMENTED 2026-07-21 (the kith slot ladder — six slots, four free; and "crew"
is now the kith):**
- ✅ **Vocabulary: crew → kith** (Mo's call: a better-fitting name; pairs with
  Kinship — the kith is who walks with you, Kinship is what they remember).
  Renamed across code, tests (`TestCrew`→`TestKith`), HUD copy, design-doc,
  journal mock, and this file.
- ✅ **The ladder (design §4, decided 2026-07-21): six slots total, four free.**
  `economy.kith {slotsBase 4, slotsMax 6}` (replaces the vestigial `familiarCaps`
  section); new `EffectType.KithSlot`; `Kith.cs` derives active slots =
  slotsBase + owned kithSlot effects, ceilinged at slotsMax. Slot 5 = **The Old
  Friend** Almanac node (now carries `kithSlot` — the node opens the slot AND
  its bond Burr fills it, one purchase one arrival). Slot 6 = **the Warden's
  Gallery** spread capstone (`kithSlot` alongside its yield bonus; the Curator's
  Cabinet multiplier deliberately never scales a slot).
- ✅ **Capacity enforced.** `Roster.Recruit` returns null with no room (future
  gift event just works); `Roster.SyncBonded` leaves an earned bond waiting in
  the grass until a slot opens (GameLoop re-syncs after Almanac buys and
  specimen fixes so the wait ends the moment it can).
- ✅ **Legacy-save freeze fixed.** Restore clamps an oversized roster to
  `slotsMax` (bonded first, then deepest Kinship) — the v19→v20 count rebuild
  could mint ~96 familiars on a long-lived save and freeze the HUD on open.
- ✅ **Rite budget derived.** `RiteGenerator.CrewGatherPosts` (const 4) →
  `KithGatherPosts(data)` = slotsBase − 1 trail post = **3**; conservative by
  design (earned slots are never assumed). MVP footprints are ≤2, so no
  behaviour change today.
- HUD: the Kith card shows "N of M slots walked · the land holds the rest".

**Interpretations / deferred with it (tune/confirm):**
- "Four are free" = slot *capacity*; arrivals still fill them (2 seeds + gift
  event + first bond at MVP). Slot 6 is headroom until more sources exist.
- A bond earned with no room open still fires its celebration sheet
  (`PendingBondCelebration`) even though the companion waits — unreachable at
  MVP (sources ≤ slots at every rung), revisit if sources ever outpace slots.
- Restore clamps to `slotsMax`, not currently-earned slots — a data retune can
  never quietly drop a companion; excess familiars are dropped, not converted
  to Renown.
- ~~Roosts `perLevel: "familiarCaps"` marker string kept~~ ✅ RESOLVED 2026-07-22:
  Roosts comfort landed — `perLevel` type renamed to `comfort` with a real value
  (see the comfort section below); the dead `RoostLevel` counter became
  `Buildings.ComfortXpMultiplier`.

**IMPLEMENTED 2026-07-22 (the gift event — verse 1 answers back):**
- ✅ **One pile, one yes (design §4, decided 2026-07-18).** New `Gifts.cs`: once any
  verse of the current rite is complete, every node plate offers a dashed "leave a
  pile" line — `economy.gifts.pileGoods` (10) units of the node's OWN resource, spent
  from camp stock; the arrival (`economy.gifts.species`, deterministic: the meadow
  vole) is stationed at that node and queues for the arrival naming sheet like any
  recruit. Availability derives from the roster (`Familiar.gifted`), so a clamped
  save self-heals; save v24→v25 (additive field, no-op migration). Telemetry:
  `gift_left`.
- ✅ **The old gift cost curves retired with it.** `gifts.gathererBaseGoods`/
  `carrierBaseGoods` and `costGrowth.gathererGift`/`carrierGift` dropped from
  `economy.json` + Def/Types/Mapper/Validator (nothing repeatedly buys a creature);
  validator now checks `gifts.pileGoods > 0` and that `gifts.species` exists.

**Interpretations shipped with the gift event (tune/confirm):**
- "Unlocked by verse 1" = ANY verse of the current rite complete (run 1's first
  verse in practice; regenerated rites keep the gate meaningful on later runs —
  though at MVP the kith persists, so the gift is usually already answered).
- The pile line shows on every node while the event is live — where you leave it
  chooses both the resource spent and the newcomer's post.
- `pileGoods 10` is a first guess; tune so the third familiar lands ~45–60 min (§2).

**IMPLEMENTED 2026-07-22 (Roosts comfort — the building line finally does something):**
- ✅ **Comfort XP (design §4).** Roosts & Burrows' `perLevel` renamed
  `familiarCaps` → `comfort` and given a real value (0.1): each bought level
  grants **+10% familiar XP rate to stationed familiars** —
  `Buildings.ComfortXpMultiplier` (replaces the dead `RoostLevel` counter),
  applied in `Familiars.AddPostXp` via `Simulation.AccrueFamiliarXp` (offline
  catch-up included). Wanderers sleep rough: ×0.5, no comfort. Validator now
  requires a positive perLevel value on every type.
- ✅ **Building rows say what a level gives.** HUD `PerLevelGivesLabel`: "each
  level: +5% craft speed at this station" / "+5% basket capacity" / "+10%
  familiar XP while posted".

**Interpretations shipped with Roosts comfort (tune/confirm):**
- `comfort 0.1`/level is a first guess (not in the doc); design §8's
  postMatch multiplier is still unbuilt — comfort is the only XP-rate lever
  besides Kinship for now.
- "Late levels add roster capacity" (§4) deliberately NOT built — headcount
  stays the slot ladder's job; revisit only if the ladder ever grows sources.

**Interpretations / placeholders shipped with it (tune/confirm):**
- Wanderers' ×0.5 help is spread evenly across unlocked gather nodes; an unheld trail is a
  flat ×0.5 lane when anyone wanders (`Stationing.cs`).
- Kinship constants (`Divisor` 1000, `XpRatePerLevel` 0.02) are consts in `Kinship.cs` — move
  to a data section. `economy.familiarXp` {base 60, growth 1.12, xpPerSecond 1} are first guesses.
- Familiar XP is a flat per-second at any post (no postMatch multiplier yet, design §8;
  Roosts comfort landed 2026-07-22).
- Ungated/material-less upgrades are free once their skill gate opens (§10 material bundles are
  placeholders); building material bundles are placeholders.
- Kith is fully persisted across Migration (no presence-lapse/benching yet); the slot
  ladder landed 2026-07-21 (six slots, four free — see below); the verse-1 gift-event
  and Roosts comfort XP both landed 2026-07-22 (see below).
- Pristine "sell" dropped — Pristine is offer/donate only for now.

**IMPLEMENTED 2026-07-21 (the deep chase → living insects; headless suite green):**
- ✅ **Insects replace fossils — observe · sketch · release (§6).** Mo's call: the
  collectible is now living insects watched at an **observation site**, recorded as
  **field sketches** (portions), then **released** — nothing is kept; a completed plate
  is a book of drawings granting the same permanent multiplier + lore. Renames:
  `Fossils`→`Insects`, `Excavation`→`Observation`, `fragment`→`sketch`,
  `strataRarity`→`rarity`, the collectible's `digSites`→`habitats`,
  `fossilCards`→`insectPlates`, Rite `Fragment` slot→`Sketch` (offering tears the page
  out → re-observe), `GameState.fossilFragments`→`insectSketches`. Save **v23→v24**
  (old fossil ids don't map — that progress drops). Observation skill is now
  **entomology** (map-oldgrowth grants it; Brush Screens gates on it). MVP plates:
  The Stag's Herald / The Silver Skimmer / Those Who Sow. Amber unchanged (still surfaced
  at the sites, now framed as ancient resin — the one takeable creature and the only
  deep-past window).
  Seams left deliberately: the **`digSite`/`DigSpeed` tokens are kept** as the shared
  site/speed plumbing (zones, planters, species powerups, gear, almanac all feed them),
  reinterpreted in comments as the observation site; the `GameDataValidator` KnownSkills
  whitelist still lists a now-unused `"excavation"`. The §6 lore lines + §7 backstory in
  `design-doc.md` are a **draft to re-voice**.

**STILL TO DO (v0.11 reversals not yet built):** none — all seven reversals
have landed (Coin/Exchange, the kith + stationing, Kinship, Folio, insects,
replanting/planters, stationing-aware reachability), plus the slot ladder
(six slots, four free — 2026-07-21). Remaining v0.11 work is MVP tails /
balance, tracked in the items below.

- **Coin is gone — "money becomes XP" (§9).** The shipped economy still runs on Coin
  everywhere: `GameState.coin`, `costCoin` (upgrades), `baseCostCoin` (buildings +
  `economy.tools.baseCostCoin`), `mapCostCoin` (zones), `resources.json sellValue`,
  `Economy.SellResource/SellPristine`. The design replaces all of it: tools = skill
  gate + ingot batch; buildings = material bundles; trail maps = provisions bundles;
  selling = **the Exchange**, a goods↔goods barter caravan whose rates derive from a
  single trade-value table (`exchangeRate(a→b)=tradeValue(a)/tradeValue(b)·(1−spread)`,
  spread ~15%, rounds in the player's favour). **The Exchange does not exist** — the
  current Provisioner sells stock for Coin. Renown becomes the single always-climbing
  number, and the Phase 1 gate now asks *"can a new player say what anything is worth
  without Coin?"* (`Economy.cs`, `GameState.cs`, `upgrades.json`, `buildings.json`,
  `zones.json`, `resources.json`, `economy.json`)
- **Flock + carriers → a stationed kith of five (§2, §4).** Shipped model: per-node
  gatherer counts (`NodeState.familiarCount`), a camp carrier pool
  (`GameState.carrierCount`), flock/carrier caps (`economy.familiarCaps`), and gift
  cost curves (`economy.gifts`). Design: up to **6 named roster familiars** (four
  slots free, two earned — revised from 5 on 2026-07-21), each with
  a level and an authored **powerup** build (a choice every 5 levels from a
  deterministic species pool), **stationed** at a post; **carrying is a post (the
  trail post), not a species** — no carrier type. An unassigned familiar **wanders** at
  ×0.5 rate/XP with no powerups; the warden never wanders (post = last tended).
  **Levels never scale output** (throughput comes from tools/hauling/powerups/richness).
  Stationing, powerups, roster, and kith slots are all absent from code.
- **Bonds → Kinship two-track (§4).** `Bonds.cs`/`bonds.json` model bonded familiars by
  `role: "carrier"|"gatherer"`. Design: every roster familiar carries a permanent
  **Kinship** track (run XP → Kinship XP at Migration via √ conversion; perks = higher
  starting level + XP rate at MVP, signature traits at 1.1). **Bonding** — crossing the
  fold — becomes a separate, rarer honour; the carrier/gatherer roles disappear because
  carrying is a post. Kinship is absent from code.
- **Museum → the Folio / one journal (§6).** `Museum.cs`/`museum.json` model donation
  "sets" (`donatedResources`, `curators-cabinet`, `wardens-gallery`). Design: there is
  no museum — Pristine specimens are **fixed into the Folio** (the journal's back
  pages) and spreads of 4–8 grant permanent bonuses. Curation is the collection craft
  producing "Folio fixings"; the Compendium (records) and Deep Pages (fossil rubbings)
  round out the one journal. No Folio/spread concept exists in code.
- ✅ **DONE 2026-07-21 — built as living insects (observe · sketch · release), not
  fossils.** See the IMPLEMENTED note at the top of this section. `Insects.cs`/
  `Observation.cs`/`insects.json`; `GameState.insectSketches`; the Rite `Sketch` slot
  (torn out → re-observe); save v24. Amber unchanged. Deep-time lore now rides on amber;
  the §6/§7 prose is a draft to re-voice.
- **Replanting & planters — a new fourth output lane (§3).** Entirely absent from
  code/data. Each node gains a **richness** level raised by replanting its own resource
  (`replantCost(L)=base·r^L`, per node per run, raising base yield); **planters** are
  Bushcraft-built structures costing *other* zones' goods that raise a node's capacity /
  regrowth / second yield lane / dig steadiness. Both reset at Migration except one
  saved by the Almanac's **The First Planting**. The craft split becomes **four-way**
  (kit / caravan / spirits / land); the Carving Bench (#14) unlocks Planter recipes.
- ✅ **DONE 2026-07-21 — reachability is now stationing-aware (§2, §8).** Model
  (Mo's call): **per-slot footprint**. A slot counts as reachable only if its good's
  production footprint — the distinct raw-resource gather nodes needed to make it
  (a raw find = 1; a crafted good = the distinct raw leaves of its input tree) —
  fits `RiteGenerator.KithGatherPosts(data)` (slot-ladder-derived since 2026-07-21:
  `economy.kith.slotsBase` − 1 trail post = 3 — conservative, earned slots never assumed).
  `RiteGenerator.StationingFootprint()` computes it; `CandidateGoods` filters picks
  by it so the generator never asks for a good the kith can't keep produced; the
  runs-2–10 proof (`RiteGeneratorTests`) now counts reachable with the footprint gate.
  Deed/specimen/sketch each need one post, always within budget. MVP content is well
  under budget (footprints ≤ 2), so this is a guardrail for future content, not a
  behaviour change today. INTERPRETATIONS/DEFERRED: the
  three chosen slots are **not** budgeted together (per-slot only, by design choice);
  the import-time validator's authored-run-1 rite is NOT yet given a stationing-aware
  ≥chooseCount check (the guarantee lives in the generator + proof, which is where
  generated rites are checkable) — a cheap follow-up if wanted.

## Mid/late-game content (started 2026-07-28)

**IMPLEMENTED 2026-07-28 — region modifiers (design §8).** New `regions.json`
(17th data file; lush / misted / ashen / windswept) + `Sim/Regions.cs`. Every
run after the first wakes in a region drawn **deterministically from the
migration count** (the generator's idiom — nothing persists, a reload can't
reroll a season; run 1 is always home ground because the authored tutorial
Rite assumes it). Region effects join `Upgrades.ActiveEffects` so they flow
through the existing modifier plumbing — including a new **resource-targeted
`yieldMult` grain** ("+fish", "−flowers"; `TargetsNode` + the validator now
accept `resource` on yield effects). The Rite generator scales each generated
goods demand by `Regions.DemandWeight` (§9's modifierWeight, previously ≡ 1).
HUD: the fold sheet says "Ahead: a misted region", the Migration vignette
speaks the arriving region's `sign`, and the Trail page heads with "the
season: …". Interpretations shipped (tune/confirm):
- The draw is a **uniform seeded pick per migration** — back-to-back repeats
  are possible (~1-in-4 with four regions) and read as "another misted
  season"; add a no-repeat rule only if playtests mind.
- The demand weight applies to whatever goods id the region targets — in
  practice raw finds; **crafted goods are unweighted** (their raw leaves are
  what the season actually speeds).
- A region below 1.0 (misted −flowers) makes the generator ask for *less* of
  the lean find — deliberate: demand scales exactly as the gather rate does.
- Effect values, and four regions, are first guesses. Deed/specimen/sketch
  slots are never region-scaled (they price in taps and luck).

**IMPLEMENTED 2026-07-28 — Kinship signature deepening + plate inscriptions
(design §4/§7, the former "1.1 depth lever").** `familiarXp.signatureMilestones`
[2, 4, 7] + `signatureDeepening` 0.25: each milestone a familiar's Kinship
passes scales its species trait by +25% of its base value
(`Traits.DeepeningFactor`, folded into all four trait kinds), and its plate
earns the next authored **inscription line** (`species.json inscriptions`,
`Kinship.InscriptionsEarned`) — §7's channel about individuals, shown in the
warden's hand under the roster row. The fold sheet names familiars whose fold
would cross a milestone ("Bramble's Meadow-forager deepens at this fold").
Interpretations shipped (tune/confirm):
- Milestones [2,4,7] against kinshipDivisor 12000 (~2 Kinship per well-worked
  fold) put the first sharpening around fold 1–2 — first guesses, tune with
  fold pacing.
- Deepening is **multiplicative on the trait's value, additive per milestone**
  (value × (1 + 0.25·passed)) — at all three milestones a +40% specialist
  becomes +70%.
- All 24 inscription lines (8 species × 3) are **draft wording to re-voice**
  with the narrative pass; they spend ~190 words of the §7 1,200-word budget.
- The validator refuses inscriptions beyond the milestone count (unreachable
  words) and a deepening value with no milestones (a lever wired to nothing).

**IMPLEMENTED 2026-07-28 — Zone 5, Mistfen Marsh, and fireflies stop being a
crop (design §3/§5/§6).** Mo's call: add the next zone and switch fireflies
out. A firefly in a basket contradicted the observe·sketch·release rework
outright, so the marsh's third *find* is now **glow-moss** (foraging) and the
lanterns became **The Lantern Bearers**, a 4-portion insect plate at the
marsh's watch site. Landed with it:
- `zones.json` mistfen: resources peat/rare-herbs/glow-moss, `verseSite` "the
  lantern pool", unlocks apothecary (entomology already arrives with the
  Old-Growth map, so listing it here was a lie the validator couldn't see).
  `resources.json` prices glow-moss and drops fireflies; `folio.json`'s Marsh
  Lights spread and `ArtLibrary` follow (glow-moss borrows the lichen plate
  until the art pass).
- `map-mistfen` (upgrade #32) now grants zone + `unlockSkill apothecary` +
  `unlockDigSite` for a provisions bundle — it previously granted a zone with
  no skills and no site, the long-standing data-layer review item.
- **Apothecary + tinctures** = the new sim system: `tinctures.json` (18th data
  file) + `Sim/Tinctures.cs`. Three fire recipes brew Warden's Tonic (+25%
  gathering), Glow Salve (×1.5 observation) and Peat-Smoke Draught (×1.5
  craft speed); drinking spends one bottle and runs the buff for 600 s of
  **sim time**, so an offline catch-up ages it on the same clock. Effects join
  `Upgrades.ActiveEffects`; expiry rebuilds the modifiers the same step.
  **Save v31→v32** (`activeTinctures`; the v31 case starts it empty).
- Verse 5 (`verse-mistfen`, spotlight apothecary), Mistfen waystone + verse
  lines, and the Lantern Bearers plate lore — all in the §7 register, draft.
- HUD: a TINCTURES card on the Warden page, hidden until Apothecary is
  learned; rows show the bottle's line, stock, live time left, and a Drink
  button. Telemetry `tincture_drunk`.
- New species **osier otter** (peat + glow-moss pair specialist) with its
  three inscriptions, so the marsh's gift piles have someone to call.

Interpretations shipped (tune/confirm):
- Every tincture is 600 s and each is independently live — **refresh, never
  stack**; nothing stops all three running at once (deliberate: the cost is
  three separate brews competing for the same fire).
- Tinctures are `kind: "material"`, so they're never sold or traded — brewing
  is the only way in, drinking the only way out. That also means the Rite's
  wardens-tonic slot needs its explicit `renownGrant` (2000), like ingots.
- Buff time is sim time, not wall time: a tonic drunk before closing the app
  burns down during the offline catch-up rather than waiting.
- Mistfen quantities, tincture durations/effects, and the map's provisions
  bundle are all first guesses — the zone has had no balance pass.
- Zone 5's nodes need the same three-plate art the other zones have;
  glow-moss and the otter fall back gracefully until then.

**IMPLEMENTED 2026-07-29 — The Hollows (zone 6) + the deep amber (design
§3/§5/§6/§7).** The endgame zone, to the pattern Mistfen proved. Landed:
- **Bone beds stopped being a crop** — the same correction as fireflies: the
  buried past is borrowed with the eyes only (§6), so a bone bed in a basket
  contradicted the reframe outright. The third find is **ashglass** (the
  fused glass the burning left — a Long Winter residue, mineral, takeable),
  swept through zones/resources/folio (`hollow-relics`) and ArtLibrary
  (ashglass borrows the res-amber plate until the art pass).
- `map-hollows` (upgrade #33, glow-moss/smoked-trout/iron-ingot provisions —
  you pack light to go under) grants zone + `unlockSkill delving` +
  `unlockDigSite` + `unlockRecipe deep-ingot`. **Deepsteel** = §5's tier past
  steel: `deep-ingot` (forge 3, forgecraft 8) → `deepsteel-toolset`
  (upgrade #34, forgecraft 40, all-gathering ×2); economy.tools.tiers gained
  "deepsteel".
- **NEW SIM SYSTEM: the deep amber** — `ambers.json` (19th data file) +
  `Sim/DeepAmber.cs` + `Data/DeepAmberDef.cs`. Four authored pieces (The
  Wing / The Seed / The Ash / The Maker's Mark, each with its field note —
  the §7 deep-past channel) surface **strictly in authored order** at the
  Hollows' watch site, rolled beside the ordinary amber channel in
  `Observation.Advance`; a pity clock (`pityHoursWatched` 12) guarantees the
  lore can't starve. The completed set is **The Deep Amber** plate: effects
  (+25% all yields) join the active-effect union, and the count crosses the
  fold with the journal (pity clock doesn't). **Save v32→v33**
  (`deepAmberFound` + `deepAmberPityHours`). Telemetry `deep_amber_found`.
  Record page: the Deep Pages card ends with the amber's entries — found
  pieces with their notes, "the resin holds more" until it doesn't.
- **The Quiet Court** — the rarest plate (rarity 0.2, 5 sketches, +25%
  delving) at the Hollows site, plus its §7 line. Two new species:
  **horseshoe bat** (deep-ores + crystals pair, echo-themed inscriptions)
  and **ermine** (ashglass + glacier-ice — the burning's two residues; the
  crags pairing lands with v1.2).
- Verse 6 (`verse-hollows`, spotlight delving + forgecraft; deep-ingot slot
  renownGrant 10000), waystone + verse lines in the §7 register (draft).

Interpretations shipped (tune/confirm):
- Deep amber timing: findsPerHour 0.05 per watcher before digSpeedMult
  stacking (~3–7 h a piece well-modified, 12 h pity worst case) — first
  guesses; the four pieces are meant to be a run-spanning chase, not a
  session.
- The deep pieces grant NO premium Amber — the channels stay separate
  (ordinary finds keep paying; pieces pay in words and, at the end, the
  plate).
- The verse-6 numbers and the Hollows quantities have had **no balance
  pass**, same as Mistfen — the zones 4–6 sweep is the next-but-one slice.
- Ermine's pair reaches into v1.2 (glacier-ice), matching the bramble-hare
  precedent (herbs + rare-herbs before the marsh existed).
- The old `Validate_VerseZoneNoTrailMapOpens` pin used the-hollows as its
  never-unlockable example — premise rot once map-hollows landed; it asks
  after highland-crags now, with the corruption-must-land guard.

**Fold-pacing pass (2026-07-29)** — Mo reached the end of the content in a
morning on run 3. Diagnosis: the content ladder is gated by ABSOLUTE numbers
(34 rungs of fixed materials, skill gates topping out at forgecraft 40, six
zones) while permanent power compounds every fold, so each fold re-runs the
same content faster; meanwhile the only thing that *did* scale, `demandGrowth`
at 2.5, walled by about fold 5. Both halves are now addressed:

- `demandGrowth` **2.5 → 1.45** (see the generator interpretations below).
- **The breadth ramp** — `chooseCountPerMigrations` / `chooseCountMax` in
  rites.json, `RiteGenerator.ScaledChooseCount`, `Rite.RequiredSlots`. Every
  second fold a verse asks for one more filled slot and the generator widens it
  by one goods slot in step. Bounded by the verse's slot count, so it cannot
  outrun the power curve the way a quantity multiplier does.
- **The endless Almanac line** — *The Long Song* in almanac.json
  (`repeatable`, `costGrowth.almanac` 1.25, `state.almanacLevels`, **save v35**).
  The one-off tree totals 99 Verdure and finishes around fold 3; the endless
  line means the currency never dead-ends. It grants the SAME additive value to
  gathering and carrying so the haul ratio can't drift — the validator refuses a
  multiplicative effect on a repeatable line, because levels scale the value
  linearly and a multiplicative type would want value^levels.

**Still open from this pass:** the early folds (2–4) will still shorten,
because the fold-to-fold power ratio is ~3× there — that's the Verdure curve
(`verdure.exponent` 0.5 / `renownDivisor` 2800), not the Rite. Deliberately NOT
retuned: flattening the exponent would make the Almanac last but would also
flatten the power curve to ~1.03× per fold, and the +2%/pt passive would stop
feeling like anything. The endless line is the better answer to the same
symptom. Revisit only if playtests say folds 2–4 feel hollow rather than fast.
Numbers throughout are model-derived (scratch model against the shipping
constants), NOT playtested — the whole pass wants a real run-3-to-run-6 sitting.

**NEXT SLICES (the mid/late plan, in order):**
1. **Almanac depth** — the §8 exotic nodes (starting tool tiers, auto-craft,
   zone skips) that currently wait for their systems. These are the lever that
   scales the ladder re-climb DOWNWARD with the fold count, which is the half of
   the pacing fix no knob can deliver. (The endless line above is the sink half,
   already landed.)
2. **Content gated on migration count** — nothing in the ladder, the zones or
   the species reads `migrationCount`; the generator re-walks the same six zones
   forever. A `minMigration` on zones/upgrades/species is what actually answers
   "I've seen it all in a morning"; the pacing knobs only change how fast.
3. **A balance pass over zones 4–6** — the marsh and the Hollows both landed
   unbalanced by design; they want the spreadsheet treatment alongside the
   tincture numbers and the deep-amber timing.

## Phase 1 — Core loop slice (current)

- **Tending burst values are a first guess.** `burstYieldMult` / `burstDurationSec`
  in `design/data/economy.json` aren't in the design doc — tune once the loop is
  playable. (`$note` in the file.)
- ~~**Feeder base amount is a first guess.**~~ **v0.11:** superseded by the kith
  reversal (§4) — no carrier type; gifts became one-shot *recruitment events*.
  ✅ RESOLVED 2026-07-22: the gift event landed and
  `gifts.carrierBaseGoods`/`carrierGift` were retired with it (see the v0.11
  section above).
- **Warden gather rate is a first guess.** `warden.gatherPerSecond = 0.5` — the
  warden's passive trickle at their post (always on, burst-boosted), and the
  bare-node gift bootstrap. Replaced the old burst-only hand-gather so the
  early game is assignment, not a tap surge. Tune so the first gift lands in
  ~20 s of just standing there.
  **v0.11:** still the mechanism, and §3 confirms it — the warden's post trickle
  now also **self-funds a virgin node's first replant** (presence, not currency).
- **The FPS overlay is dev-only now.** `FpsCounter` (top-right: avg fps + worst
  frame ms) is gated behind `Debug.isDebugBuild` in `Bootstrap` — visible in the
  editor and development builds, stripped from release/store builds. Flip a
  development build on when tuning performance on-device.
- **Play Games is done and confirmed on device (2026-07-28): sign-in,
  achievements, score submission, cloud Snapshots and the Renown board all work.**
  The diagnostics scaffolding that got us there (`Diag`, the startup status
  popup, `OpenInfoSheet`, the Standing card's status row) has been removed now
  that all of it is confirmed — which is the safe order, and the opposite of the
  mistake described below. Two things deliberately stayed, because they are what
  made a run of silent failures findable at all, and they cost nothing: a
  try/catch on every JNI entry point, and a one-line `[play-games]` log of every
  status that used to be discarded (`SignInStatus`, `ReportScore` success,
  `SavedGameRequestStatus`, overlay `UIStatus`, row count + player rank). GPGS's
  own verbose trace sits behind `Debug.isDebugBuild`; our own lines cover release
  logcat without Google's spam.
  **Do not use Play Games' own overlay.** `ShowLeaderboardUI` /
  `ShowAchievementsUI` route through `com.google.games.bridge.HelperFragment`,
  which extends the framework `android.app.Fragment` (deprecated since API 28) —
  read straight out of the shipped AAR with `javap`. On `targetSdk 36` that
  throws **synchronously** on the JNI class lookup, which is why it presented as
  a hung callback and cost two builds to pin down. Everything routed to the GMS
  clients instead is fine, and that working/broken split is the whole diagnosis;
  it matches upstream issue #3318. **GPGS 2.1.0 is the latest release — there is
  no upstream fix to upgrade to.** The Standing is therefore read with
  `IGameServices.LoadLeaderboard` (`LoadScores` + `LoadUsers` for names) and
  drawn by `JournalSheets.OpenStandingSheet`, which suits the journal better
  anyway. `ShowLeaderboard` is kept, unused and try/caught, for the day the
  bridge is fixed.
  **A leaderboard's top public page can come back empty while Play still ranks
  the player** — `read 0 rows` with submissions being accepted. So never rely on
  `LoadScores` alone: fall back to `data.PlayerScore` and show the player's own
  line. If a board is ever empty *and* the log says `player unranked`, Play is
  not ranking the game at all — check the PGS **configuration** publish state (a
  separate thing from an individual leaderboard being "live", and from the app's
  testing track) and the account's "appear on public leaderboards" setting.
  **The lesson worth keeping:** the first diagnostics sink was retired in the
  very same commit as the R8 `gms.tasks` keeps that were meant to fix the sign-in
  hang. Fix and instrument removed together — so when the symptom returned there
  was nothing left to read it with, and it cost a full build cycle to get back to
  where we had been. Confirm on device first, then remove the instrument.
- **Android target SDK is pinned to API 36 (2026-07-27).** Was
  `AndroidApiLevelAuto`, which follows whatever platform the installed Android
  module ships — an editor or module update could move a release's target
  silently. 36 is the newest platform this editor has (34/35/36) and what Auto
  already resolved to in shipped AABs, so the pin is a no-op for the built
  artifact. Raise it deliberately when Play's required level moves.
  (`ProjectSettings.asset AndroidTargetSdkVersion`, `Assets/Editor/ProjectSetup.cs`)
- **Play's "androidx.fragment 1.1.0 is outdated" warning is stale — no fix needed
  (checked 2026-07-27).** 1.1.0 came in transitively via
  `play-services-basement` back when nothing declared fragment explicitly. The
  AdMob import (`GoogleMobileAdsDependencies.xml`, first released in **v43**)
  declares `androidx.fragment:fragment:1.7.1`, and Gradle takes the highest — so
  every release from v43 on satisfies Play's 1.2.1+ ask. Verified from the
  uploaded artifacts, not inferred: `v42` bundles 1.1.0, `v62` bundles 1.7.1.
  The warning persists only while a pre-v43 artifact is still active in a track.
  To read a bundled transitive version: download the AAB and
  `unzip -p <aab> base/root/META-INF/<group>_<artifact>.version`.
- **The haul bottleneck, and what was done about it (2026-07-28).** As slots
  unlocked, nodes jammed on full baskets and the overflow was silently destroyed.
  The measured cause: **gathering and hauling grew on different curves.** A
  gatherer slot multiplies against mastery (+5%/level), richness (+10%/replant),
  planters and the Verdure global (+2%/point); a carrier slot added a flat
  `1.5/s × haulMult`. So the carrier share needed to break even *rose* with
  gather power — 40% of the kith at `g=1`, 67% at `g=3`, i.e. 4 of 6 bodies on
  the trail. Worse, the five `haulMult` rungs gate on **crafting** skill
  (bushcraft 12/20/35 ≈ 86/229/1084 batches) while the pressure on them arrives
  on the **verse** clock, so a slot unlocked mid-gap was pure loss.
  Three changes:
  1. **A full basket is no longer a cliff.** The node's own gatherers shoulder the
     excess: they walk a carrier's trip and gather nothing while walking, so
     `overflow / (1 + rate · trip / load)` survives (`Simulation.SelfHaul`). At
     `selfHaulTripMultiplier` 1 that keeps 60% of a 1/s gatherer and 13% of a
     10/s one. It is a **floor, not a lane** — a posted carrier loses no
     gathering and serves every node, so delegating always wins, and
     `Advance_ACarrierBeatsSelfHauling_SoTheTrailPostIsWorthASlot` pins that.
  2. **Hauling rides the same smooth curve as gathering.** A carrier's load now
     takes the Verdure global too (`Simulation.HaulLoad`), so the ratio stops
     drifting between rungs instead of only stepping five times a run.
  3. **The mid rungs come earlier** — stag-harness bushcraft 12 → 8, wagon 20 → 14
     (≈46 and ≈112 batches). A first guess, to confirm in playtest.
  Still true and worth remembering: **basket capacity buys time, not throughput.**
  Buildings (+5%/level) and the Timber Frame planter (+50%) do nothing for a
  bottleneck — they only delay it. And overflow still credits XP, Mastery and the
  Compendium, so skills climb while camp stock doesn't; the Trail page now says
  "the trail is behind — gathering X/s, carrying Y/s" so the shortfall is visible
  before it costs anything.
- **Autosave interval (30 s) and welcome-back threshold (60 s credited) are first
  guesses.** Tune with the loop playtest. (`GameLoop.AutosaveIntervalSeconds`,
  `GameHud.WelcomeBackMinSeconds`)
- **The upgrade shop shows the next 3 unpurchased rungs.** A window over the §9
  ladder in order; material-costed rungs appear (with their costs shown) before
  crafting exists to pay them — an honest preview, but they sit unaffordable
  until the crafting system lands. (`GameHud.UpgradeShopWindow`)
  **v0.11:** with Coin gone (§9), rungs are priced by **skill gate + material
  bundle**, not `costCoin` — the shop's cost display changes with the economy rework.

## Phase 2 — Adaptive UI & input

- **Windfall bubbles replaced tap-to-tend (2026-07-24).** A worked node drifts a
  bubble up the strip (`economy.bubbles`: spawn interval / lifetime / max live /
  rewardSeconds — all first guesses); catching it pays rewardSeconds of that
  node's current output straight to camp AND tends the node (burst + Pristine
  window + Rite tend deed — so the tend deed slots and the Cordage Wraps gear
  stay reachable, `Sim/Bubbles.cs`). Tapping a node plate now opens the posting
  sheet (the node IS the assign gesture); the vacant "+" badge is gone (occupied
  badges still show who holds the post). A "N / M POSTED" slots-in-use counter
  is pinned to the page's top-right corner. Interpretations shipped (tune/confirm):
  - Bubbles **spawn only in live play** (world layer, `Time.time`) — nothing
    persists, no offline accrual, and time paused under a sheet still ages them.
  - Spawn is **round-robin over eligible (worked) nodes**; a camp with no one
    posted anywhere drifts nothing.
  - Space / pad-(A) **catches the longest-adrift bubble** (it no longer tends
    the selected node); node selection now only drives the ring highlight.
  - The bubble reward is computed **at catch time** — a node gone fallow while
    its bubble drifted pops empty (a quiet margin note, no grant).
  - design-doc §5/§8 still describe tap-to-tend; re-voice those lines when the
    mechanic settles.
  - **The windfall's face (2026-07-27, from Mo's device pass — the tinted disc
    read as tiny and oddly coloured).** It now drifts as **the resource's own
    naturalist plate** (`ArtLibrary.ForResource`) on a soft parchment mount,
    turning slowly as it rises, at **68% of a node plate** (was 45% with a
    hash-derived resource tint, which is where the strange colour came from).
    A resource with no plate still falls back to the tinted disc + highlight.
    `BubbleWorldView`'s class name and `economy.bubbles` still say "bubble" —
    rename to windfall if the object sticks.

- **The journal HUD (2026-07-21) follows `docs/wildgrove-journal.html`, still built in
  code.** `GameHud` now lays out the mock's structure — paper palette, title
  head, currency ledger, margin note, pinned Rite/Fold tracker, and four bottom tabs
  (Trail · Camp · Warden · Record) — as runtime uGUI, with the reskin pass on top:
  the four journal typefaces, generated ruled ink borders, paper-grain + stitched-spine
  overlays, and the tend-flash / trail-carrier motion touches. Still no hand-drawn
  line art (node plates and the compendium have no naturalist illustrations).
  Interpretations shipped with it (tune/confirm):
  - The **world strip stays above the page on every tab**; the mock has no strip
    (its plates ARE the world). It goes when the real region scene lands.
  - The **margin note** is a flavour line set by actions (tend/replant/trade/offer/
    build), hardcoded strings in `GameHud` — not a data-driven dialogue channel.
  - The **ledger** carried every held resource and grew a wrap per zone; after the
    device-scale pass that left the open page ~21% of the screen. Resolved
    (2026-07-28): the ledger is the three meta currencies only (and taps through to
    the Record page, which now shows "N held" beside each compendium entry), the
    header's eyebrow is gone (it restated the lit tab; its camp count moved to the
    Standing card), the trail-home line moved to the Trail page and the camp actions
    to the Camp page. **Chrome budget rule going forward: a bar is only pinned if it
    is read on every tab** — the page is the row that pays for it. `UpdateWorldGap`
    now makes the strip the shock absorber (it takes what's left after the measured
    chrome and a 32% page floor, clamped to 14–26% of the screen), so the next thing
    that grows shrinks the strip's whitespace instead of the page.
  - The Rite verse card now renders **all four slot types** (the old HUD skipped
    specimen/sketch/deed); spotlight (✳) markers are not shown yet.
  - Migration runs tracker **Fold button → confirm sheet → full-dark vignette**
    (lines from `dialogue.migrationVignette`); the vignette shows the Verdure gain
    only — per-familiar Kinship gains aren't itemised.
  - The **waystone arrival modal is restored** (it had been dropped in the v0.11
    HUD rewrite); it queues behind arrival/bond/welcome sheets.
  - Kith **post buttons are a 4-column grid sized to the page** of
    node/trail/watch/wander; fine at MVP station counts, revisit when zones
    multiply. The kith card (roster + posts) lives on the **Trail page**
    (Mo's call 2026-07-21: assignment belongs with the land) — the design's
    "roster & slots on Warden" reading is folded into it; if Warden ever
    needs a roster summary, split the card.
  - **Craft / Raise / planter / gear / upgrade material lines show camp
    stock** ("4 berries (have 35.8K)", shortfall inked ochre) and their
    buttons disable without a full bundle in stock (crafts also gate on a
    single batch of inputs; Stop is always allowed).
  - Store capture pages renamed to the four tabs (`StoreCaptureRunner`); legacy
    page names still map inside `GameHud.OpenTab`.
  (`Assets/Scripts/Game/GameHud.cs`)
- **Real typefaces via legacy `Text` (2026-07-21), not TMP.** The mock's four roles
  ship as OFL TTFs in `Assets/Resources/Fonts/` (licenses in `docs/font-licenses/`):
  IM Fell English (titles/verses/lore), IM Fell English SC (chrome/buttons — real
  small caps), Caveat (margin notes/posted lines), Lora (body). Rendering is Unity's
  dynamic-font path, so bold/italic are synthesized and exotic glyphs fall back to
  OS fonts (the HUD avoids ✎/★/✓/→ for that reason). A TMP swap (SDF crispness,
  proper style faces) is still open if the raster look isn't good enough on device.
  Caveat + Lora are variable fonts — Unity renders their default instance. (`GameHud`)
- **Node sprites are runtime-generated placeholder discs in a screen strip.**
  `PlaceholderArt` makes one tinted disc per resource and `WorldView` lays them out
  in the gap the HUD leaves open; the hand-drawn naturalist plates and a real region
  scene replace them (the camera/world seam and screen-point hit test stay).
  (`Assets/Scripts/Game/World/`)
- ~~**Portrait-only: the mock's wide (≥880px) two-column layout isn't built.**~~
  ✅ RESOLVED 2026-07-28 — the book opens to a **spread** on a wide canvas: the
  open page left, the **Trail pinned right**, and the Trail's tab hidden
  (an open tab you cannot close reads as broken). Asking for the Trail while
  wide — including the tracker's deep links — lands on the Camp, as the mock
  does. `JournalLayout.IsWide` is the breakpoint, and it asks an *aspect*
  question rather than the mock's CSS pixel one: under ScaleWithScreenSize a
  4:3 tablet in portrait is physically broad but still a column, while a
  landscape phone is barely wider in canvas units and clearly wants the
  spread. Rule = width ≥ 1200 canvas units **and** w/h ≥ 1.2, pinned by
  `JournalLayoutTests` against the shapes real devices produce.
  Implementation note: every page builder writes through `GameHud.Body` (and
  `JournalWidgets.Content`), so the spread just repoints both at one column
  at a time — the pages have no idea they are a column. Interpretations
  (tune/confirm): the two columns **share one vertical scroll** like the mock,
  rather than scrolling independently; the right column carries a "THE TRAIL"
  running head since it has no lit tab to name it; the world strip keeps its
  existing 14–26% clamp in both layouts, so a short landscape screen squeezes
  the strip rather than the page.
  ~~Safe-area insets are also not applied.~~ Stale when written — the
  device-scale pass had already landed them: `FitLayoutToScreen` offsets the
  root by `Screen.safeArea` and re-applies on every safe-area or canvas
  change (now including width, which the spread needs).
  Still open here: `HeightClampedElement`/`TrackedScrollRect` are no longer
  used by the HUD (kept compiling — delete or reuse).
  ~~**full keyboard / controller navigation is the other half of the Phase 2
  gate** — the input abstraction exists but menu focus traversal does not.~~
  ✅ RESOLVED 2026-07-29 — see the keyboard/controller item below.
  (`GameHud`, `Assets/Scripts/Game/Journal/JournalLayout.cs`)

- **Keyboard / controller navigation (2026-07-29) — the other half of the
  Phase 2 gate.** uGUI's EventSystem already moves focus geometrically once
  something is selected, so the build supplies what it doesn't:
  `Assets/Scripts/Game/Journal/JournalNav.cs` (public, tested — tab stepping,
  rebuild-safe focus restore, shortest-distance scroll reveal) plus a focus
  section on `GameHud`. Bindings: arrows / WASD / d-pad / left stick move,
  Submit (Enter, Space, pad South) presses, **Esc or pad East** backs out
  (Back gained the pad button — a controller could open a sheet and not close
  it), the **shoulders or Q/E** turn the page, and **pad West or C** catches a
  windfall. The two touch-only interactions both have page-reachable paths
  already: posting is the Trail page's own "Post here" buttons, and the catch
  now has a focus-independent binding.
  Interpretations shipped (tune/confirm):
  - **Touch-first**: nothing is focused until the player asks to move, and a
    pointer press puts the mark away again — a focus ring left lit after a tap
    reads as a cursor a phone doesn't have. The pointer handler deliberately
    does NOT clear uGUI's own selection (that would cancel the rename field
    the same frame a tap opened it), so waking focus resumes a live selection
    rather than jumping to the top of the page.
  - The mark is a **doubled ochre rule just outside the control**, not a fifth
    parchment tint — every plate is already one of four paper shades, so a
    tint would read as another kind of button. It lives inside the control it
    marks, so it rides the layout, scrolls with the page, and is clipped by
    the viewport for free.
  - **The modal trap is one flag**: a sheet switches the page's `CanvasGroup`
    off, which makes its controls report non-interactable, and uGUI's
    `FindSelectable` skips exactly those. Nothing dims because every button
    plate disables to white (`NeverDim` extends that to the tabs, ledger and
    tracker, which were plain Buttons on the default grey).
  - Focus-in-context is **self-healing** rather than hooked: whenever the
    selection is missing or out of context, focus re-seeks. That is why
    opening a sheet needed no change in `JournalSheets` at all.
  - **Space / pad South is Submit while a control is marked**, and the
    windfall catch only when nothing is — which settles the double-fire this
    file has flagged since Phase 1 (pad South being both Submit and the
    catch). The pad-West/C binding is what keeps the catch reachable mid-page.
  - **Rebuild survival is by index**, not identity: the page's controls are
    counted, the position remembered, and focus clamped back into the rebuilt
    page (`JournalNav.RestoreIndex`). Flipping between portrait and a spread
    can land focus somewhere unrelated — a shape change is a deliberate act,
    so that's accepted.
  - Scroll stitches and the sheets' half-second tap guard are taken out of
    navigation (`NoNavigation`) — draggable furniture and a click-eater are
    not places to stand.
  - **Known edge:** the Input System's default UI actions bind Navigate to
    WASD as well as the arrows, so typing a familiar's name in the rename
    field can also move focus. Fixing it properly means shipping a custom
    actions asset; revisit if it bites in the pad/K&M gate pass.
  - The margin note's teaching tail still says "space / (A) catches one",
    which is true in the teaching moment (nothing is focused yet) — the full
    binding set is documented here and in §12 rather than in a margin note,
    which is flavour and not a manual.
  Still open for the Phase 2 gate: the **gamepad manifest**, and playing it
  through on real 4:3 / 16:10 / 21:9 / foldable hardware.
  (`GameHud`, `Assets/Scripts/Game/Journal/JournalNav.cs`,
  `Assets/Scripts/Game/Input/`)
- **Runtime bootstrap instead of a bootstrap scene.** `Bootstrap` spawns GameLoop +
  GameHud via `[RuntimeInitializeOnLoadMethod]` so Play works with zero scene setup.
  Replace with a real bootstrap scene when there's content to lay out.
  (`Assets/Scripts/Game/Bootstrap.cs`)

## Phase 3+ — Systems build-out

- **Excavation drops fragments and amber — no excavation XP yet.** Dig sites,
  diggers, fragment drops (rate + pity), fossil assembly, permanent fossil
  effects, and the amber channel (design §10 — a separate roll, so fully-dug
  ground keeps surfacing it; a CURRENCY on GameState, not a resources.json
  entry) are all live. The amber sink is the time-skip (full live-rate hours,
  no cap — that's what's paid for); amber numbers (digFindsPerHour 0.06,
  perFind 2, skip 4h/15) are first guesses against the ~40-free-per-week
  lean. The earn paths have since landed — amber packs (`amber_pack`), the
  rewarded-ad drip (`amber_drip`), the weekly cache (`weekly_amber_cache`) and
  amber-find telemetry (`amber_found`) are all live, though the weekly cache is
  not yet gated on a Play redemption (see the Play Games Rewards item below).
  Still waiting: cosmetics/extra craft queues as further sinks — cosmetics have
  no substrate at all, the same blocker as the Wayfarer's Cloak — excavation
  skill XP ("XP from every action" — fragments are too rare for per-unit XP;
  decide a grant when tool-tier / level gates need the level), and the fossil
  card lore (Compendium).
  Interpretations to confirm: digger gifts cost gathererBaseGoods of EACH of
  the zone's resources (a dig site has no resource of its own to leave a pile
  of); diggers share the zone flock cap; `excavation.baseFragmentsPerHour`
  (0.25) is a first guess not in the doc.
  (`Wildgrove.Sim/Excavation.cs`, `Fossils.cs`)
  **v0.11 (§6):** the deep chase is now **uncover · record · rebury** — nothing dug
  up is kept. `fragments` become **field sketches** of **portions** (3–5/fossil, pity
  per 4 h), the fossil is **reburied** with a wordless sign, and the completed plate is
  a book of rubbings keeping the same permanent multiplier + lore. Rename
  `fragment`→`sketch`/`portion` here and in `fossils.json`; "diggers share the zone
  flock cap" is superseded by stationing. Amber stays takeable (unchanged).
- **Play Games Rewards items — one of three is half-built, and the delivery path
  doesn't exist at all (reviewed 2026-07-29).** Three items is the complete
  designed set (§11 line 514, §12 row: two single-use + one repeatable), and two
  single-use by **Sep 30 2026** is the nearest hard date in the whole plan — it
  and Mar 1 2027 (the repeatable) are the only forward-looking dates in the
  design doc; every other date there is a past decision stamp. What's actually
  outstanding is more than the Play Console setup:
  - **Weekly Amber Cache (20 amber, max 1/wk) — the in-game half is done and
    tested, but it is currently a free weekly tap.** Live: `weeklyCacheAmber` 20,
    `Amber.CanClaimWeeklyCache`/`ClaimWeeklyCache`, the 7-day cooldown,
    `weeklyCacheClaimedUnixMs` persisted and carried through a fold
    (`Migration`), `weekly_amber_cache` telemetry, a Claim row in THE AMBER card,
    and coverage in AmberTests/SaveCodecTests/MigrationTests. What's missing is
    the gate: `GameLoop.CanClaimWeeklyCache` asks only `GameServices.IsSignedIn`,
    so nothing ties the claim to a Play redemption. As it stands the requirement
    isn't met *and* it hands out 20 free amber/week against the ~40-free-per-week
    lean. Keep the signed-out behaviour when the gate moves — the button
    deliberately *is* the sign-in, because hiding the row hid the free claim from
    exactly the players it should convert.
  - **The Drover's Halter (a fell pony walking a second haul lane) — the sim,
    save and UI are built; only the redemption is missing.** Renamed from "Spare
    Wing" and redesigned 2026-07-29 (design §11 DECIDED): the reward grants an
    animal rather than an abstract post, because a body that can stand nowhere
    else cannot leak its slot exemption into gathering. Live: the `fell-pony`
    species, `Familiar.PonyStation`/`IsPony`, `Kith.Walking` skipping her,
    `Roster.Station` refusing to move her (and refusing anyone else into her
    lane), `Roster.SyncDroversHalter` deriving her arrival and station from
    `GameState.droversHalterOwned`, save v34 + fold carry-over, the second dot on
    the Trail page, the untameable note on her info page, and exclusion from
    every posting sheet. **What's left is the grant:** nothing sets
    `droversHalterOwned` yet — that's the shared plumbing above.
    Balance: her trait is the new `trailCarryFactor` kind (0.5) — the lane's load
    as a *fraction* of a carrier's rather than a bonus on one, and the only trait
    that never Kinship-deepens. So the free always-manned lane is a standing +50%
    on a manned trail rather than a doubling. That value is the §14 dial — verify
    the bottleneck triangle with two lanes (see the haul-bottleneck item in
    Phase 1).
  - **Wayfarer's Cloak (cosmetic) — not built, and it has nowhere to appear.**
    There is no cosmetic system of any kind (nothing matching skin / wardrobe /
    appearance), and no warden or familiar sprite — presentation is journal text
    plus naturalist plates. So the first job is a design call on what a cosmetic
    even *is* here (a journal cover, a seal on the Standing card, a camp plate),
    not an art request. This is the largest of the three, and it's the same
    substrate the amber cosmetics sink wants — build it once.
  - **No redemption or grant path exists for any of the three.** No `RewardIds`,
    no reward SKUs in `StoreProductIds.All`, and nothing in the repo matching
    redeem / promo / Play Points. Reward offers are awarded on Quest completion,
    so something has to receive the grant and apply it. The foundation is already
    there: `UnityIapStore` handles arrivals from outside the app via
    `FetchPurchases`, `OnPurchasesFetched` and `OnPurchasePending`/`HandlePending`.
  - **Play Console: create all three products**, alongside the store SKUs noted in
    the v0.11 section. Any reward UI must be drawn **in-journal** — Play Games' own
    overlay is permanently dead on `targetSdk 36` (see the Play Games item in
    Phase 1).
  - **Re-check the repeatable requirement before planning to it.** Level Up's
    guidelines were revamped and there's a Level Up+ tier now. "≥2 single-use by
    Sep 30 2026" holds; the Mar 1 2027 repeatable date comes from the design doc
    and wasn't corroborated against current guidance.
  (`Wildgrove.Sim/Amber.cs`, `Stationing.cs`, `GameLoop.cs`,
  `Assets/Scripts/Game/Journal/CampPage.cs`,
  `Assets/Scripts/Game/Services/ServiceIds.cs`, `UnityIapStore.cs`)
- **Tool tiers are the named ladder rungs, not a separate purchase flow.** The
  run's tool tier derives from owned upgrades tagged `toolTier`
  (flint-sickle → flint … steel-toolset → steel), and zone trail maps gate on
  `zones.requiredTool` (§3: Zone 2 flint … deeper steel+). Interpretations:
  §8's standalone toolCost formula (100·12^(t−1) + ingot batch) is expressed
  through the rungs' own Coin+ingot costs rather than computed; the ×2 yield
  per tier is the rungs' yieldMult effects; a HIGHER tier satisfies a lower
  requirement. Tool-tier gating of *recipes* (§4 "levels gate recipes and
  tool tiers") still waits on skill-level design.
  (`Upgrades.ToolTierIndex/MeetsToolRequirement`)
  **v0.11 (§9):** with Coin gone, §8's toolCost formula is settled the way this item
  already leans — a tool tier is purely **skill gate + ingot batch**, no wallet term.
  Drop the "Coin+ingot" phrasing when the economy rework lands.
- **Building perLevel values are first guesses, and two are interpretations.**
  The 5% speed/capacity tapers aren't in the design doc. Interpretive calls to
  confirm in balance: the §9 Store's "storage capacity" is implemented as
  basket capacity (camp storage caps don't exist), and the Clay Furnace is
  simply the forge line's first bought level (its ~8,000 debut price is the
  line's baseCostCoin). The Spare Wing is not a building rung and never gets one —
  it's a Play Games Rewards item granting **+1 trail post** (§11), and what
  building it takes is spelled out in the Play Games Rewards item above.
  (`design/data/buildings.json`)
  **v0.11:** buildings are now a **goods sink**, not a Coin sink (§10) — `baseCostCoin`
  becomes a material bundle; and Roosts & Burrows re-scopes to **familiar comfort**
  (+XP rate per level, roster capacity at late levels), not headcount caps.
- **`crafting.baseCraftSeconds` (5 s, uniform) is a first guess.** Not in the
  design doc, and one duration for every recipe is a placeholder — tune against
  the §2 pacing targets (first recipe cooked ~20 min), and consider per-recipe
  times with the balance pass. (`design/data/economy.json`)
- **Mastery curve and value-bonus interpretation are first guesses.** base 50 /
  growth 1.15 / xpPerUnit 0.25 aren't in the design doc, and §4's "+5%
  yield/value" is implemented as one yieldBonusPerLevel applying to the node's
  yield and to the raw resource's direct sale — never to goods crafted from it
  (recipe derivation uses base values, same convention as sellValueBonus).
  (`Wildgrove.Sim/Mastery.cs`, `design/data/economy.json`)
- **Skill XP gains and recipe skillLevels are first guesses.** xp.gatherPerUnit
  (1, credited on the gross gather — basket overflow loses the goods but still
  pays XP) and xp.craftPerBatch (25) aren't in the design doc, nor are the
  per-recipe skillLevel picks (skewer/trout 2, reed baskets and bronze 3,
  iron 5). Tool-tier level gating (§4) waits for the tools system, and the
  Migration skill reset (§8) for the prestige build.
  (`Wildgrove.Sim/Skills.cs`, `design/data/recipes.json`)
  **v0.11 (§5):** "levels are the gate, materials are the cost, everywhere" is now
  decided — the skill-XP spine carries the pacing that Coin used to. Familiar XP joins
  it as the second track (earned at the post), which the kith rework introduces.
- **The Pristine three-way choice is complete; the Compendium's plates and
  lifetime counters are not.** Quality pools now feed all three forks — the
  Provisioner windfall, Rite specimen slots, and Museum donations (one
  Pristine per set entry, permanent set bonuses × the Curator's Cabinet).
  Still waiting: the Compendium's hand-drawn plates and entry text (the art
  + narrative pass — the system layer with lifetime counters, discovery, and
  the field-notes HUD section is live in `Wildgrove.Sim/Compendium.cs`;
  counters record GROSS gathering like skill XP, crafted batches, and
  Pristine UNITS — units not windfall events, an interpretation), familiar
  species plates, and a compendium_entry_discovered
  telemetry event (skipped for now: offline catch-up would burst-fire it).
  Museum sets now cover all eight zones plus the Warden's Gallery capstone
  (one donation from every zone) — set/effect sizes still first guesses. Interpretations to
  confirm in balance: the whole batch takes the rolled tier;
  pristineValueMult (10×) isn't in the doc; hand-gather and the no-hauling
  fallback never roll; staggered-fleet cadence and fullest-basket-first
  routing; museum set/effect sizes are first guesses.
  (`Wildgrove.Sim/Quality.cs`, `Museum.cs`)
  **v0.11 (§6):** the Museum is **retired** — the three-way choice survives, but the
  keep-fork becomes **fixing a Pristine into the Folio** (the journal's back pages),
  where **spreads** of 4–8 grant the permanent bonus. `Museum.cs`/`museum.json`/
  `donatedResources` reframe as the Folio; sets → spreads (one MVP spread grants kith
  slot 5's moment); `wardens-gallery`/`curators-cabinet` become Folio furniture. The
  buried past is never kept (see the excavation item) — only the *living* land's gifts
  can be fixed.
- **Crafting and gifts spend only common stock.** Fine finds can't feed a
  recipe or a gift — probably right (they're for selling/offering), but it
  means a run holding only Fine berries can't gift a gatherer. Revisit with
  balance. (`Crafting`, `Economy`)
- **Hauling numbers are first guesses.** `baseCarryCapacity` / `tripSeconds` /
  `basketCapacity` in `design/data/economy.json` aren't in the design doc — tune with
  the loop playtest.
- **The Rite, Migration, and the run-2+ generator are all live.**
  Verses reveal with their zones, offerings consume goods/specimens/fragments
  and credit Renown, verses complete at chooseCount, the Rite at
  all-verses-sung, and Migration folds the camp (confirm sheet with the
  vignette + Verdure forecast; reset per §7, keeping Verdure/Renown/fossils/
  rng/migration count/Almanac). Runs 2+ generate from the authored template
  (same zones, slot shape, and value anchor; goods re-picked from the content
  available by each zone's order, spotlight rotated by migration, demand
  × demandGrowth^m, spotlight slots discounted / off-spotlight at a premium);
  the ≥3-slots-reachable proof runs 2–10 lives in RiteGeneratorTests, not
  the import-time validator (the validator can't see generated rites — it
  validates the generator's tuning instead). Still open: showing
  `dialogue.verses` lines at the verse site (generated verses reuse the
  zone's site but have no authored lines — the narrative pass decides what
  a run-3 verse *says*). ✅ Region modifiers landed 2026-07-28 (see the
  Mid/late-game content section) — modifierWeight is live via
  `Regions.DemandWeight`. Generator interpretations flagged:
  demandGrowth was **retuned 2.5 → 1.45 on 2026-07-29** after Mo reached the
  end of the content in a morning on run 3. The exponent has to track the
  fold's power ratio, and that ratio is not constant: production scales with
  (1 + 0.02·Verdure), Verdure is √(lifetime Renown), so the ratio runs ~3×
  across folds 2–3 and settles at 1.10–1.15× from fold 7. At 2.5 fold 12 asked
  ~1000× the wall-clock of fold 1 (exponential demand always beats an
  asymptotically-linear power curve); at 1.45 it asks ~2.5×, with folds 2–8
  still faster than the first. **A single exponent cannot fix both ends** —
  the early folds shorten because Verdure explodes, which is the
  `verdure.exponent`/`renownDivisor` curve's problem, not the Rite's (see the
  Verdure-curve item). spotlightDiscount 0.6 / offSpotlightPremium 1.5 are
  still first guesses against §8's "similar share of
  each run's lifetime output" — tune with real run-2 playtests; deed/
  specimen/fragment COUNTS stay authored (they price in taps and luck —
  only their renownGrant scales); Coin-bought skills with no home zone
  (forgecraft via the Fire Ring) debut at zone order 2 for candidate gating;
  a verse's raw candidates are its OWN zone's resources only (authored
  pattern). Older Rite interpretations still flagged:
  plain-resource offerings credit Renown at the CURRENT sell value (incl.
  owned bonuses), specimen offerings auto-pick the largest matching pool,
  fragment offerings take from the richest incomplete fossil, deeds before a
  verse's reveal still count (lifetime, no per-verse baseline), and partial
  fossil FRAGMENTS survive Migration alongside completed fossils ("every
  fossil"). (`Wildgrove.Sim/Rite.cs`, `RiteGenerator.cs`, `Migration.cs`)
  **v0.11 (§6, §8):** the `Fragment` offering slot becomes a **field-sketch** slot —
  the page is torn out and that portion must be **re-uncovered** (the pity timer keeps
  it fair); it stays the steepest offering, priced highest in Renown. The generator's
  ≥3-reachable-slots guarantee must become **stationing-aware** (satisfiable under
  plausible stationing with the current kith size, §2), replacing zone-unlock
  reachability. Migration now also keeps the **roster + every Kinship level** (§4)
  alongside the journal/Verdure/fossils.
- **Bonded familiars are live; species abilities are not.** Two MVP bonds
  (design §7): Sootwing, a pack raven (carrier) bonds when the Meadow Blooms
  Museum set completes; Burr, a meadow vole (gatherer) crosses with the
  Old Friend Almanac node (12 Verdure, deliberately effect-less — the
  companion IS the effect). Earned state is DERIVED from the source — never
  stored — so bonds survive Migration and stale saves for free. Role rules:
  the carrier hauls with the fleet outside carrierCount, its slots, and the
  gift curve; the gatherer works the warden's last-tended node (the first
  node until the first tend of a run — an interpretation of "work any zone"),
  outside the flock count and cap. Interpretations to confirm: the follow-
  the-warden post rule, bond sources chosen (first Museum set + a dedicated
  Almanac node), 12-Verdure pricing against the "1 bond per 2–3 Migrations"
  lean. Waiting: species abilities (v1.1), more bonds, a bonding
  moment/celebration in the HUD (currently just rows changing text), world
  sprites for companions. (`Wildgrove.Sim/Bonds.cs`, `design/data/bonds.json`)
  **v0.11 (§4):** bonds are reframed under the **two-track familiar model**. Every
  roster familiar now carries a permanent **Kinship** track (run XP → Kinship XP at
  Migration, √ conversion; perks = higher starting level + XP rate at MVP, signature
  traits at 1.1); **bonding** (crossing the fold, present from minute one) becomes the
  separate rarer honour this item already describes. The `role: carrier|gatherer`
  split goes away — carrying is a post, so a bonded familiar is stationed like any
  other. Final bond counts/rarity still Mo's to settle (§14).
- **Two kit effects are inert: `offlineNightFullRate` (Pitch Torch) and
  `noSpoilage` (Clay-Lined Creel).** There is no night-rate reduction and no
  spoilage system for them to modify — both are recorded on the worn kit and
  shown in the HUD, waiting for their mechanics (night-rate with the offline
  balance pass, spoilage if it ever ships). While they stay inert the optimal
  kit is fully determined (Cordage Wraps · Birch Frame Pack · Oilskin Tarp) —
  the Pack and Camp slots have no live tradeoff yet, so the swapping the kit
  bag now allows has nothing to reward it until these two land.
  (`Wildgrove.Sim/Gear.cs`)
- **Crafted gear is worn immediately — there is no separate equip step.** A
  piece goes straight into its slot when made; the displaced piece keeps in the
  kit bag (`GameState.gearCrafted`, save v31) and is re-worn free, so nothing is
  ever destroyed and each piece is paid for once per run. Migration folds the
  bag with the kit. Old v30 saves seed the bag from what was worn — anything
  those saves overwrote is unrecoverable and is made again at cost.
  (`Wildgrove.Sim/Gear.cs`)
- **Tending's Pristine window is live but invisible.** `Simulation.Tend` opens
  the 30 s pristineBonusRemaining window (chance × (1 + pristineChanceBonus))
  alongside the yield burst, but the HUD gives no cue that it's running —
  surface it with the real art pass. (`GameHud`)
- **Verdure / almanac / museum / fossil / boost multipliers.** `Simulation.YieldPerSecond`
  folds in the Verdure global bonus only; the other multipliers arrive with their
  systems and multiply in there.
  **v0.11 (§9):** the yield formula gains **`richnessMult(node)` and `planterMult`**
  (per-node, from replanting/planters) and "museum" becomes **Folio spreads**
  (`museumSets` → spread bonuses) — fold both in with their systems.

- **The Almanac is 12 nodes of existing effect types; costs and the
  allocation model are interpretations.** Verdure is never destroyed — a node
  allocates from the banked total (available = verdurePoints − owned costs)
  so the +2%/pt passive keeps counting the full total and Migration's
  recompute-from-lifetime-Renown can't refund spent points. The §7 exotic
  nodes (starting tool tiers, auto-craft, starting-zone skips) wait for
  their systems; all costs are first guesses tuned to ~10 Verdure from the
  first Migration. (`design/data/almanac.json`, `Wildgrove.Sim/Almanac.cs`)
  **v0.11 (§3, §8):** add **The First Planting** — a node that lets one planter survive
  the fold — once replanting/planters land. The Almanac deliberately gets **no
  familiar-power nodes** (§4): its kith-adjacent scope is limited to slot access (kith
  slot 4) and The First Planting.

- **The narrative display layer is live; most of the words are not.**
  Waystones reveal once per zone on arrival (modal sheet, marks read on
  "Walk on", re-readable in the Compendium; the read-set survives Migration
  — lore stays read); verse lines show under revealed verse headings;
  assembled fossils show their card line in the dig row. Unauthored (empty)
  dialogue simply never shows, so authoring can land line by line.
  Pacing DECIDED (2026-07-17): the starting zone's waystone showing at
  minute 0 is accepted (its unlock IS first launch) — the §8 table's
  "~10min first waystone" now reads as zone 2's stone. Still waiting:
  provisioner trigger lines (first-visit / after-migration), waystones as
  tappable world objects (the modal stands in), and most of the ~1,200-word
  budget itself. (`Wildgrove.Sim/Narrative.cs`)
  **v0.11 (§7):** narrative grows to **six channels** — a new **plate-inscription**
  channel adds a margin line to a familiar's plate at Kinship milestones (the only
  channel about individuals), which lands with the Kinship system.

## Narrative authoring

- **MVP dialogue is drafted, not final.** All four waystones, all four verse
  lines, and all three fossil cards in `design/data/dialogue.json` now have
  text in the §7 register, and the validator enforces waystone + verse text
  for every mvp-scope zone. The words are a first draft — re-voice anything
  that misses the tone before release. Still unwritten (by design, later
  scope): v1.1+ zone waystones/verses, more Provisioner lines, the final
  waystones chain.

## Data-layer review items (open from the data-layer PR review)

- **Skills vocabulary hardcoded** in `GameDataValidator` as a C# `HashSet` rather than
  sourced from data.
- **`zone.unlocks` is documentation-only and diverges from upgrade effects.**
  The worst case (excavation never granted) was fixed with the excavation
  system — `map-oldgrowth` now carries `unlockSkill: excavation` — but the
  field itself still isn't consumed; the validator only reads the starting
  zone's.
- ~~**`map-mistfen` grants a zone but no dig site / skills**~~ ✅ RESOLVED
  2026-07-28 with the Mistfen build: the map now carries `unlockSkill
  apothecary` and `unlockDigSite mistfen-marsh` (see the Mid/late-game
  content section).

## Number formatting

- **`NumberFormat` suffix table** runs `K, M, B, T` then `aa, ab, …` before falling
  back to scientific — first-pass abbreviations; revisit if a naming convention is
  chosen. (`Assets/Scripts/Game/NumberFormat.cs`)
