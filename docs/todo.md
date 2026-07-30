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
  Friend keeps Burr's bond. Species art: all 12 species have plates as of
  2026-07-30; a 13th added ahead of its art still falls back gracefully.
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
  Lights spread and `ArtLibrary` follow (glow-moss borrowed the lichen plate;
  own plate landed 2026-07-30).
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
- ~~Mistfen quantities, tincture durations/effects, and the map's provisions
  bundle are all first guesses — the zone has had no balance pass.~~ ✅ Balanced
  2026-07-30 (the zones 4–6 pass below): tincture durations 1200 s + inputs ×3,
  tonic grant 6000; quantities and the map bundle CONFIRMED as shipped — the
  bundle's real gate is smoked-trout's firecraft-28 recipe, not the amounts.
- Zone 5's nodes have their three plates as of 2026-07-30 (glow-moss got its
  own; peat and rare-herbs already had theirs).

**IMPLEMENTED 2026-07-29 — The Hollows (zone 6) + the deep amber (design
§3/§5/§6/§7).** The endgame zone, to the pattern Mistfen proved. Landed:
- **Bone beds stopped being a crop** — the same correction as fireflies: the
  buried past is borrowed with the eyes only (§6), so a bone bed in a basket
  contradicted the reframe outright. The third find is **ashglass** (the
  fused glass the burning left — a Long Winter residue, mineral, takeable),
  swept through zones/resources/folio (`hollow-relics`) and ArtLibrary
  (it borrowed the res-amber plate; own plate landed 2026-07-30).
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
- Deep amber timing: ~~findsPerHour 0.05 per watcher~~ **0.1 as of the zones
  4–6 pass (2026-07-30)** — at 0.05 the mean (20 h) sat above the 12 h pity, so
  pity metronomed every piece; at 0.1 the roll is live (mean 10 h, ~2–4 h a
  piece well-modified, pity the backstop). Still a run-spanning chase, not a
  session.
- The deep pieces grant NO premium Amber — the channels stay separate
  (ordinary finds keep paying; pieces pay in words and, at the end, the
  plate).
- ~~The verse-6 numbers and the Hollows quantities have had **no balance
  pass**, same as Mistfen — the zones 4–6 sweep is the next-but-one slice.~~
  ✅ Swept 2026-07-30: quantities/anchor CONFIRMED as shipped (the model puts
  the real Hollows walls at forgecraft 40 and the fold gate, where they
  belong); the zone gained its season (ashen +ashglass).
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

**IMPLEMENTED 2026-07-30 — the missing plates (every id in the data now has
one).** The mid/late content landed faster than its art, so five zones' worth of
new ids were drawing the placeholder disc or borrowing a neighbour's plate. Ten
new plates, all public domain, sourced and cut to the existing pipeline (rembg
cut for the naturalist plates, sepia-ink keying for the PSF pen drawings), placed
with hand-authored sprite metas, and credited in `CREDITS.md`:
- **Familiars** — `familiar-otter`, `familiar-bat`, `familiar-ermine`
  (Protheroe's coloured plates, the same book as the hare and the weasel). The
  three unplated species from the traits rework now have portraits on the kith
  roster, the world strip and the bond cards.
- **Insects** — `insect-lantern-bearers` (both sexes of the glow-worm on one
  page, Jacobson's beetle plate) and `insect-quiet-court` (Curtis's mole
  cricket — the insect that sings from galleries it digs itself).
- **Tinctures** — `goods-tonic`, `goods-salve`, `goods-draught`, one vessel
  each, so the three read apart in the crafts card and on the new icon column
  of the TINCTURES card (`WardenPage.BuildBrewsCard`).
- **Borrows retired** — `ashglass` had the amber plate (an insect in resin,
  standing in for volcanic glass) and now has `res-ashglass`; `glow-moss` had
  the lichen plate and now has `res-glow-moss`. `deep-ingot` joins copper,
  bronze and iron on the one ingot plate, which is the deliberate kind of
  sharing.
- **`ArtLibraryTests` (8 tests)** walks the real GameData asset and asserts
  every resource, recipe output, species, insect, zone, gear piece, building,
  skill, journal furnishing and line motif resolves to a sprite — the coverage
  this pass establishes, and the only thing that would ever notice a plate
  being renamed out from under `ArtLibrary` (a missing one is silent by
  design). 718/718 EditMode green.
- Rerunning `Wildgrove/Fix Art Import Settings` over the new files also caught
  three older plates the tool had never seen: `familiar-pony` and
  `insect-wayfarers-plate` were still importing at 2048 with crunch off, and
  **`res-timber` had `alphaUsage: 0`** — its transparency was being discarded
  on import, so the re-baked beech tree had been drawing on a solid block since
  the 2026-07-29 swap. Run the menu item after adding art.

Still deliberately unwired: `res-flint` (there is no flint resource — a spare
kept for a tool-tier surface that doesn't exist yet). Cheap future win found
while sourcing: Kurr's coal plate, fig. 6, is a **public-domain amber with
insects in it** — swapping `res-amber` to it would drop one of the five CC BY
attributions the build has to carry, but it also feeds the store's amber icons,
so it wants a deliberate pass rather than a drive-by.

**IMPLEMENTED 2026-07-30 — the fold gate: content that arrives over runs
(design §8).** Nothing in the ladder, the zones or the species read
`migrationCount`, so the generator re-walked the same six zones forever and a
run-1 warden was expected to reach the Hollows. Zones (and, for tuning,
upgrades and species) now carry **`minMigration`** — folds that must be behind
the warden before that content exists. 742/742 EditMode green on both Android
and Win64.

- **The Rite is the load-bearing part, not the ladder.** A Rite completes only
  when every verse in it is sung, a verse reveals only once its zone is open,
  and the Rite is the *only* Migration gate — so a verse for a zone the fold
  cannot reach would have sealed Migration **permanently**, including the fold
  that would have opened that zone. `Rite.IsVerseInPlay` is what prevents it,
  and `Rite.VersesInPlay` is what the journal, the tracker and the counts read.
  An early run therefore walks a **shorter** Rite, not a slower one.
- **Authored in one place.** `minMigration` on the zone gates the trail map *and*
  holds the verse; `Upgrades.FoldGate` reads the zone's fold through the map
  rung so the number is never written twice, and the validator refuses a map
  rung that carries its own (two numbers that must agree forever is the typo
  that would reach the hard-lock above).
- **Validator rules added** (each guards a save that could never progress):
  negative folds; a gated starting zone; a map rung with its own fold; a recruit
  rung earlier than the species it calls (`Roster.Recruit` doesn't consult the
  species' fold, so the rung would silently win); and any Rite — authored or
  template — with no verse open on the fold it serves.
- **HUD:** a gated rung reads "not before the next fold" / "not for another N
  folds" *instead of* its shopping list, deliberately — a trail that does not
  exist has no use for one, and "needs iron tools" beside it would send the
  warden off earning something that changes nothing this run.
- **Species gating is nearly moot and kept anyway:** a species whose resources
  live in a gated zone already has no node to leave a pile at, so the field only
  earns its keep for holding one back inside a zone the run has opened.

Interpretations shipped (tune/confirm):
- **First guesses: zones 1–3 open run 1, then one new trail per fold** —
  Silverrun on run 2 (`minMigration` 1), Mistfen on run 3, the Hollows on run 4.
  Model-derived, NOT playtested; a real run-1-to-run-5 sitting is what should
  set them. A test pins the *shape* (non-decreasing in zone order, never
  skipping a fold) rather than the exact numbers, so retuning doesn't fight it.
- **Fewer verses early means fewer gift piles**, and the kith-slot milestones
  (§4, verses 2/5/10) come later — the second slot now lands in run 2 rather
  than run 1. Whether that reads as pacing or as a tax wants the same sitting.
- Opening a zone is **never taken back**: a save whose data was retuned
  underneath it keeps a trail it already holds, and keeps that verse in play
  (the alternative is a zone you can work whose verse doesn't count).
- The generator still generates every verse and `IsVerseInPlay` filters them, so
  there is ONE truth about what a run is walking rather than two that can drift.
  Its reachability sweep is unchanged.
- No upgrade or species is gated in the shipping data — the zone gate is doing
  all the work. The fields exist for tuning; don't sprinkle numbers into them
  without a reason.

**IMPLEMENTED 2026-07-30 — Almanac depth: the exotic lines (design §8).** The
fold gate paces what a run may *reach*; these pace what it must *repeat*. Each
fold re-ran the whole ladder from bare hands, and no yield knob can shorten
that — only starting further up can. Two new effect types, both Almanac-only
(validator-enforced):

- **`grantUpgrade` — granted rungs.** An owned node puts a named ladder rung on
  every run free — no materials, no skill gate; the grant IS the head start.
  Shipping nodes: *The Remembered Edge I/II* (start at flint, then copper
  tools) and *The Known Way I/II* (start with the Bramble, then Old-Growth
  trail maps) — one requires chain, edge→way→edge→way, so the tool always
  precedes the trail that demands it. `Almanac.SyncGrantedUpgrades` is
  idempotent and re-derived at every fold, on buy (the run that pays gets it
  NOW, not next fold), and on restore (a save older than the grant picks it
  up); granted ids simply join `purchasedUpgradeIds`, so the ladder UI, zone
  sync, and multipliers all follow for free. The **fold gate and tool
  requirement still hold** — a granted trail behind `minMigration` waits for
  the run that earns it — and the sweep runs to a fixpoint so a granted tool
  can satisfy a granted map whatever order the ladder lists them.
- **`keepCraftOrders` — *The Fire Remembers*.** The stations carry their
  standing orders across the fold: the assignment only, never the batch (the
  old camp's in-flight inputs fold with it). No new bookkeeping — `Advance`
  already re-checks workability every tick, so each station stalls quietly
  until the new run re-earns its recipe's skill and heat, then takes the order
  up again unasked.
- **Validator rules added:** grantUpgrade/keepCraftOrders outside the Almanac;
  a grant naming an unknown rung; a grant of a recruit rung (§4 — familiar
  permanence is Kinship's alone, the Almanac never buys creatures, not even
  sideways); and a map grant whose requires chain doesn't carry the zone's
  covering tool — that node would be bought and then sit inert forever, which
  is worse than refused.
- **Costs are first guesses:** 6/10/14/22 up the granted chain + 8 for the Fire
  Remembers (~60 Verdure across the five, one-off tree total now 159 from 99).
  Tuned so the line opens around folds 2–4; wants the same run-3-to-run-6
  sitting as the rest of the pacing pass.

**IMPLEMENTED 2026-07-30 — the zones 4–6 balance pass.** The spreadsheet
treatment (scratch model mirroring `RiteGenerator` anchors + `YieldPerSecond`
against the shipping JSON), measured at each zone's debut fold under the fold
gate (Silverrun m=1, Mistfen m=2, Hollows m=3).

**The model's headline: goods quantities are not where zone 4–6 pacing
lives.** At debut-fold production (tool re-climb, Verdure global, mastery,
richness, specialist traits) every generated goods slot fills in seconds-to-
minutes of a posted node — the anchors (silverrun 3225 → mistfen ~8100 →
hollows 18000) ramp each zone's ask ~3.2× over the one before, coherently. The
levers that actually pace these zones are the craft-XP gates (firecraft 28 =
~540 batches feeds both late trail-map bundles via smoked-trout; forgecraft 40
= ~1770 batches is the real deepsteel wall, auto-resumed by The Fire
Remembers), pristine-specimen luck, the haul lane, offline caps, and the fold
gate itself. All left alone deliberately — they're the right walls in the
right places.

What moved (all data-only, no sim change):
- **Regions now touch every gatherable zone** — the marsh and the Hollows were
  season-blind (no region weighted any of their finds, so the generator's
  seasonal demand variation was dead there): misted +glow-moss 1.25, ashen
  +ashglass 1.5 (the burned land gives up its own glass — and its digSpeed
  already suited the Hollows watch), windswept +peat 1.25 (wind dries the turf).
- **Deep amber `findsPerHour` 0.05 → 0.1** — the old mean (20 h) sat ABOVE the
  12 h pity clock, so every piece arrived by pity at exactly 12 h: a metronome
  pretending to be a roll. Mean 10 h puts the roll back in charge with pity as
  the backstop; undecorated set ~40 watched hours, well-modified ~2–4 h a piece.
- **Tinctures: durations 600 → 1200 s, brew inputs ×3** — 20 min is one drink
  per sitting instead of a ten-minute nag; the tripled bundles (120 herbs /
  90 glow-moss / 75 peat…) make a bottle read as a real brew without gating
  anything. Effect sizes unchanged.
- **verse-mistfen tonic grant 2000 → 6000** — tracks the tripled brew's
  notional worth (6 × 960), same convention as the ingot slots; also lifts the
  mistfen anchor ~14% (the softest step in the ramp).
- The `Validate_DeepAmberWithDeadRates` pin was tightened while its literal
  moved — its old contains-guard passed even when the corruption no-oped
  (`"findsPerHour": 0` is a substring of the healthy value).

Numbers are model-derived, NOT playtested — same caveat as the fold-pacing
pass; the run-3-to-run-6 sitting is now the gate on ALL of it.

**IMPLEMENTED 2026-07-30 — the paid-skip budget (the whale throttle, design
§10).** Mo: "cap the amber time skips to ×2 the speed of a FTP." Sim-time is
the only thing money buys in Wildgrove (a skip runs the whole sim — craft XP,
watched hours, tincture clocks), so bounding how much of it PAID skips may add
per real day bounds a heavy spender's pace outright. `timeSkipDailyCapHours`
(economy.json amber, 24 = a free player's 24 natural sim-hours + at most 24
skipped = ×2) drives a **leaky-bucket budget**: it refills at cap/24 per
wall-clock hour and holds at the cap, so the rule is identical on every
timescale — no midnight counter, no timezone or date-rollover question, and
unspent days never bank extra hastening. Landed as:
- `Amber.SkipBudgetHours/SkipBudgetRemainingMs` + the spend inside
  `TryTimeSkip`; `CanTimeSkip` now takes `nowUnixMs`. **Save v37→v38**
  (`timeSkipBudgetHours` + `timeSkipBudgetStampUnixMs`; absent = full budget).
  The budget crosses the fold like the other amber stamps — migrating must not
  refill the day's hastening.
- **The REWARDED skip stays outside the budget** — free players get it too, so
  it's part of the shared baseline, and its own 4 h cooldown already bounds it.
- Validator: a positive cap below one skip is a sink that can never be spent —
  refused, not merely slow. 0/absent = uncapped (pre-cap data keeps working).
- HUD: the hasten row counts down until the budget covers one skip again
  (the drip row's WaitingTail idiom); being short of amber only greys the
  button.
- Cap 24 is the ×2 answer, not a model output — revisit with the same
  playtest sitting if ×2 still reads too fast (the next lever after this one
  is fold-gated content, not a tighter cap).

**NEXT SLICES (the mid/late plan, in order):**
1. **The run-3-to-run-6 playtest sitting** — every mid/late number (fold gate,
   demandGrowth, Almanac costs, this pass) is model-derived and waiting on it.

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
  ~~Still open here: `HeightClampedElement`/`TrackedScrollRect` are no longer
  used by the HUD (kept compiling — delete or reuse).~~ ✅ RESOLVED 2026-07-30:
  `HeightClampedElement` came back into use (the sheet scroll clamp,
  `JournalSheets.BeginSheet`); `TrackedScrollRect` was still referenced by
  nothing and is deleted.
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
  rewarded-ad drip (`amber_drip`) and amber-find telemetry (`amber_found`) are
  all live; the weekly cache is now granted only by a Play Games delivery
  (`play_reward_received`) rather than a free tap — see the Play Games Rewards
  item below.
  Still waiting: cosmetics/extra craft queues as further sinks — cosmetics have
  no substrate at all, which is what retired the cosmetic reward cloak — excavation
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
- **Play Games Rewards — the delivery path is BUILT (2026-07-29) and all three
  items land (the third since 2026-07-30).** Three items is the designed set
  (§11 line 514, §12 row: two single-use + one repeatable). Requirements
  re-checked against Google's live guidelines the same day, and they hold as the
  doc has them: **≥2 single-use by Sep 30 2026** (awarded on Quest completion),
  **≥1 repeatable by Mar 1 2027** (awarded on Social Challenge completion, max 1
  per player per week). Both belong to base **Level Up**, not the newer Level Up+
  tier — Level Up+ is the reduced-service-fee tier for games meeting *all* the
  revamped guidelines, so these are a gate on it rather than an extra of it. The
  one Level Up+-only thing nearby is **Play Points product promotions**, which
  ride the very same one-time-product plumbing built here, so that door is open
  if the tier is ever taken.
  - **How a reward actually arrives** (worth knowing before touching any of it):
    a reward is an ordinary **one-time product** in the console with a Play Games
    Reward offer attached. Google awards it, and it is delivered through the
    **out-of-app purchase flow** — no promo codes, no separate API. The game
    queries purchases on launch/resume, finds an unacknowledged order, and owes
    the player, in this order: **grant → tell them → acknowledge**. Acknowledging
    first loses the reward outright if anything goes wrong between; leaving it
    unacknowledged is the safe failure — **Play refunds the offer after three
    days** and it can be awarded again. Everything below is shaped by that.
  - **Shared plumbing — DONE.** `RewardProductIds` (three ids named, two
    catalogued) + `StoreCatalogue` (the union the store fetches, and the single
    consumable/entitlement answer for both bought and awarded products);
    `IStore.RewardRedeemed` is a **`Func<string,bool>`, not an event**, precisely
    so the store can wait for the grant's answer before confirming the order —
    `UnityIapStore.HandlePending` grants first and only calls `ConfirmPurchase`
    when every reward in the order landed. `RewardGrants.Apply` maps an id to its
    grant and the words owed for it; `GameLoop.OnRewardRedeemed` queues the
    confirmation and saves before answering true. Also fixed on the way past:
    `RestorePurchases`'s callback used to fire the moment `FetchPurchases` was
    *called*, so it could never report what arrived — it now waits for the fetch.
  - **The in-game confirmation — DONE, and it is a compliance artifact, not
    flavour.** Google's rules for anything granted outside the app: name the item
    plainly, say the source out loud, no way to decline, and it stays up until
    the player acknowledges it. `JournalSheets.OpenRewardSheet` does all four —
    plain statement first and the grove's voice second, one Continue button, inert
    scrim, and Esc/pad-East takes the same door as Continue. It pumps after
    welcome-back and **before** the arrivals, because the Halter's pony is herself
    an arrival and being asked to name her before being told where she came from
    read backwards.
  - **Weekly Amber Cache (20 amber, max 1/wk) — RE-GATED.** It was a free weekly
    tap; it is now only ever granted by a Play delivery. `Amber.ClaimWeeklyCache`
    is gone, replaced by `Amber.ReceiveWeeklyCache` — **deliberately
    unconditional**, because Play owns the cadence now and refusing an early
    delivery would drop a reward the player can never be offered again. The old
    cooldown survives as `Amber.WeeklyCacheDue` + the countdown, for the page's
    reading only. The card's row keeps the sign-out behaviour (the button still
    *is* the sign-in) and becomes **"Look"** when signed in — a manual re-read for
    someone who redeemed a moment ago and would rather not relaunch. That also
    closes the ~40-free-per-week leak the free tap was opening.
  - **The Drover's Halter — GRANTED now.** `PlayRewards.ApplyDroversHalter` sets
    `droversHalterOwned` and stands the pony in her lane, from both the redemption
    moment and — the reinstall-proof half — `GameLoop.SyncRewardEntitlements`
    reading the store's owned set, folded in beside `SyncKithPurchases` at startup
    and after a cloud-save adoption. Additive only: a store that can't see the
    entitlement (offline, mid-connect) never takes the pony back.
    Balance unchanged and still the §14 dial: her `trailCarryFactor` 0.5 makes the
    free always-manned lane a standing +50% on a manned trail rather than a
    doubling — verify the bottleneck triangle with two lanes (see the
    haul-bottleneck item in Phase 1).
  - **The Wayfarer's Plate — BUILT 2026-07-30, and it closes the Sep 30 bar.**
    The second single-use reward: an insect plate that arrives already recorded,
    drawn by a hand that walked the trail first. `rewarded: true` in
    `insects.json` marks the one plate no observation site can offer — the
    validator refuses a habitat or a draw weight on such a plate, and
    `Observation.EligibleInsectsInto` skips them outright rather than relying on
    "no habitats" as a convention. The grant writes it into the Folio
    (`Insects.Record`) instead of deriving it from the entitlement, which is why
    it needs **no Migration handling**: recorded plates already cross the fold.
    Its effect (`pristineChanceBonus` 0.005) is deliberately the mildest in the
    book — a test pins that it sits below anything earnable in the same band,
    because a free permanent that crosses every fold must not out-earn the pages
    someone walked for. Save **v36** carries `wayfarersPlateOwned`, which only
    bridges sessions starting before billing resolves.
    (`design/data/insects.json`, `Sim/Insects.cs`, `Sim/PlayRewards.cs`,
    `Services/ServiceIds.cs`, `RewardGrants.cs`)
  - **The cosmetic cloak was RETIRED unbuilt (2026-07-30).** It wanted a cosmetic
    substrate the game has never had — no skin/wardrobe/appearance system, no
    warden or familiar sprite, presentation is journal text plus plates — and
    building one to justify one reward is backwards. Its id is gone from
    `RewardProductIds`; a test pins that `reward_wayfarers_cloak` is not a
    catalogued reward, so an award of it could never be acknowledged. **Do not
    create that console product.** If a cosmetic substrate is ever built for the
    amber sink, a cosmetic reward can be reconsidered on its own merits.
    ~~**Still owed for the plate:** its own plate art, and the console product
    `reward_wayfarers_plate`.~~ ✅ Both landed 2026-07-30 — the moth from Helena
    Scott's 1864 plate, and the console product is created.
  - **Play Console — all three products are DONE (2026-07-30).** One-time products
    `reward_drovers_halter`, `reward_weekly_amber_cache` and
    `reward_wayfarers_plate` are created and activated. All that remains is to
    **attach a Play Games Reward offer to each — the association UI and reward
    testing do not open until Sep 1 2026**, so that is a September job, and the
    window against the Sep 30 bar is one month wide. Any reward UI must be drawn
    **in-journal**; Play Games' own overlay is permanently dead on
    `targetSdk 36` (see the Play Games item in Phase 1).
  - Still untested on a device, like everything billing: the real out-of-app
    delivery. `StubStore.DeliverReward` exercises the whole path in the editor
    (grant → acknowledge, and the refusal branch), and a live Quest award can't
    be tried before Sep 1. What the active products DO make checkable now: an
    internal-track build's catalogue fetch should resolve both reward ids with a
    price. An id coming back unavailable means the console entry and
    `RewardProductIds` disagree — the one failure that would silently swallow
    every future award.
  (`Wildgrove.Sim/Amber.cs`, `PlayRewards.cs`, `Stationing.cs`, `GameLoop.cs`,
  `Assets/Scripts/Game/Journal/CampPage.cs`, `JournalSheets.cs`,
  `Assets/Scripts/Game/Services/ServiceIds.cs`, `RewardGrants.cs`,
  `UnityIapStore.cs`, `StubStore.cs`)
- **Game Stats — decided and wired 2026-07-29, but it cannot submit yet, and that
  is Google's side not ours.** The guideline wants 5 repetitive stats (≥1 usable
  for competitive engagement) + 1 progression stat. Chosen, all on the free path
  because the guideline forbids stats reachable only by paying or watching an ad:
  **resources gathered** (SUM, the competitive one — the same quantity the Renown
  board ranks), **goods crafted** (SUM), **windfalls caught**, **specimens fixed**,
  **verses sung**, **migrations** (COUNT), and **trails walked** as the
  progression level. Trails walked is `seenWaystoneZoneIds.Count` deliberately:
  waystones cross a fold (`Migration.Migrate` carries them) so the level only ever
  climbs, where zones-open would drop to 1 on every Migration and read as a bug on
  the profile.
  - **The two continuous stats go as deltas.** Play aggregates SUM over events
    received; the game holds lifetime totals. So `GameStats` keeps a baseline and
    reports the growth — seeded at launch (`Rebase`) so a loaded run's lifetime
    total isn't posted as one session's work, and re-seeded on cloud-save adoption
    so the gap between two runs isn't either. A delta is only banked once the event
    is actually recorded, so a signed-out stretch accumulates instead of vanishing.
    The baseline is **in-memory, not saved**: a hard kill loses the stretch since
    the last save. Persisting it would mean a save-version bump for a stat, which
    isn't worth it — revisit only if the profile numbers read visibly low.
  - **Cadence is the save cadence** (autosave 30 s / pause / quit), not per event:
    hauls and crafts land every tick, so "as soon as it occurs" would be an event
    per frame. The discrete five fire at their own call sites.
  - **Blocked on Google, twice over.** Client integration "will be made available
    using Unity, Java and C++ SDKs" — the shipped plugin (GPGS 2.1.0, July 2025,
    still the newest release) has no `PlayerGameEvent` and no `RecordEvent`, and
    the Java coordinate is unpublished so there is nothing to reach over JNI
    either. The API is GA **July 2026** and the **Play Console CSV upload opens
    August 2026**. So: `PlayGamesServices.RecordStat` counts what it could not send
    and says so in logcat; when the plugin lands it becomes three lines
    (`new PlayerGameEvent.Builder(name)` → `.AddProperty` → `RecordEvent`) and
    nothing else moves. Server-to-server is live now but needs service-account
    credentials a client can't hold — not a path for a serverless game.
  - **Console side is authored and waiting** in `store/play-games/gamestats/`
    (`PlayerGameEvent.csv` + the two config CSVs + a README). Two knowingly-unfinished
    parts: the **stat icons aren't drawn** (Google has published no size spec — draw
    them with `tools/make-store-art.py` once the console says what shape), and the
    column values (`HIGHER`, the free-text units) are the guide's documented
    spellings, not ones a console has accepted. Expect one correction round.
    A test (`EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema`) fails if code
    and CSV drift, because Play drops undeclared events silently.
  (`Assets/Scripts/Game/Services/GameStats.cs`, `IGameServices.cs`,
  `PlayGamesServices.cs`, `StubGameServices.cs`, `GameLoop.cs`,
  `store/play-games/gamestats/`)
- **Sidekick — ON in Play Console 2026-07-29, CONFIRMED ON DEVICE 2026-07-30. Nothing to build; one setting that bites if missed.** The
  overlay is added at *upload* time for App Bundle games: Play Console → create an
  internal/closed release with **"Sidekick is on by default"**, then Testing →
  Advanced settings → **Play Games Sidekick** → *"Automatically make Sidekick is on
  by default for new app bundles you upload"*. The SDK dependency route
  (`com.google.android.play:sidekick`, minSdk 23) is only for APK publishing, which
  we don't do — our minSdk is 26 either way. **The trap:** every release here is
  uploaded by `android-release.yml` via `r0adkll/upload-google-play`, so without
  that "automatically" setting each CI upload lands Sidekick-less and the guideline
  quietly fails. Testing needs a device on Android 13+ with 4 GB+ RAM, the build
  installed from Play (not sideloaded), and **Play Store → Settings → About → tap
  Play Store version ×7 → General → Developer options → Play Games Sidekick** on.
  **That developer-options toggle is the whole verification** — it was on in the
  console and still invisible until the toggle was flipped, so an absent overlay
  means the device, not the build. Verified against v0.1.104 (built from the tip,
  so the full 45-achievement ladder was there to surface — before 2026-07-30 the
  panel would have read nearly empty, which is a misleading way to test it).
- **Level Up milestone dates the compliance table was missing.** From the March
  2026 Level Up post: **July 2026** — Sidekick integrated *and* achievements
  implemented with PGS: **both DONE and both confirmed on device 2026-07-30** (45
  published achievements, unlocking; Sidekick surfacing them);
  **November 2026** — cloud save (done: Snapshots, single-device confirmed);
  Rewards **Sep 30 2026** / **Mar 1 2027** as already tracked above. The only
  Level Up work left is the Rewards offer association, which cannot start before
  **Sep 1 2026**, and Game Stats, which waits on Google's own client SDK.
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
  line's baseCostCoin). The old Spare Wing is not a building rung and never gets
  one — it became **The Drover's Halter** (§11 DECIDED 2026-07-29), a Play Games
  Reward granting a fell pony rather than an abstract post, and it is built and
  granted; see the Play Games Rewards item above.
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
- ~~**Two kit effects are inert: `offlineNightFullRate` (Pitch Torch) and
  `noSpoilage` (Clay-Lined Creel).**~~ ✅ RESOLVED 2026-07-30 — Mo's call: don't
  wait for the mechanics; the dead items **move into the Almanac as
  Verdure nodes** with effects the sim already consumes. The three dead
  effect types (`offlineNightFullRate`, `noSpoilage`, and
  `unlockVerdureForecast` on the Almanac Desk — the forecast was never gated,
  so that rung was a no-op too) are **removed from the vocabulary** (enum,
  validator, journal copy); Pitch Torch and Clay-Lined Creel leave the kit
  (their plates stay on disk as spares, like `res-flint`) and the Almanac
  Desk leaves the ladder (33 rungs now). The kept things, in the tree:
  - **The Pitch Torch** (6 Verdure, off Patient Hands) →
    `craftSpeedMult firecraft ×1.25` — stacks with Patient Hands' global.
  - **The Clay-Lined Creel** (5 Verdure, off Old Songs I) →
    `yieldMult fish ×1.25` (the catch comes home whole — the old
    no-spoilage flavour, via the region modifiers' resource grain).
  - **The Almanac Desk** (8 Verdure, off The Long Watch II) →
    `offlineCapBonusHours +2` — additive, deliberately NOT another
    raise-to so it stacks on the Long Watch line instead of shadowing it.
  One-off tree total 159 → 178. Costs/values are first guesses — tune with
  the run-3-to-run-6 sitting. Consequence accepted with the call: the kit is
  back to one piece per slot, so the kit bag's swap has nothing to reward
  until new gear ships. If a real night-rate or spoilage mechanic ever
  lands, re-add its effect type then.
  (`design/data/almanac.json`, `gear.json`, `upgrades.json`, `EffectDef.cs`,
  `GameDataValidator.cs`, `JournalText.cs`, `ArtLibrary.cs`)
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

- **The Almanac's costs and the allocation model are interpretations.**
  Verdure is never destroyed — a node
  allocates from the banked total (available = verdurePoints − owned costs)
  so the +2%/pt passive keeps counting the full total and Migration's
  recompute-from-lifetime-Renown can't refund spent points. The §8 exotic
  nodes (starting tool tiers, zone skips, auto-craft) landed 2026-07-30 as
  `grantUpgrade`/`keepCraftOrders`; all costs are first guesses tuned to ~10
  Verdure from the
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

## UI surfacing gaps (audit 2026-07-30)

A sim-vs-journal diff: every `Sim/*` system and `GameState` field checked
against every reader in `Assets/Scripts/Game/**`. These are systems that
work — and pay out — without the player ever being shown them. (The three
dead effects the same audit found are already resolved; see the Phase 3
kit-effects item.)

**Fully invisible systems (real mechanics, zero UI):**

- **Mastery is entirely invisible — the biggest hole.** `Mastery.Level` /
  `ProgressToNext` are called from nowhere in the Game assembly; no `GameLoop`
  wrapper exists. Node cards show richness but not mastery, which silently
  compounds to +495% yield *and* sell value at cap — the stated long-tail
  chase. Wants a per-node line (level + % to next) on the Trail node cards.
  (`Sim/Mastery.cs`, `TrailPage.BuildNodePlate`)
- **The Fine pool is a black hole.** `state.fineResources` has no reader in
  any view — 3.5% of every haul batch lands where the player can't see,
  trade, or spend it (`Exchange.TryTrade` reads only `resources`; its only
  exit is a fine-quality Rite specimen slot). Surface the stock (Compendium
  row beside Pristine?) and decide whether the Exchange should take Fine.
  (`Sim/Quality.cs`, `RecordPage`)
- **Rite deed slots render no progress and no action** — `deedCounts`
  accumulates and `SlotName` can *name* a deed slot, but `BuildSlotRow` only
  builds rows for Resource/Specimen/Sketch, so deed progress is invisible
  until the slot happens to complete. Looks like an actual gap in the verse
  card, not a deferral. (`TrailPage.BuildSlotRow`, `Sim/Rite.cs`)
- **No "current bonuses" readout anywhere.** The live `ModifierSnapshot`
  (region + gear + tinctures + upgrades + plates + spreads + Almanac) is
  never shown as an aggregate — effects only ever appear as per-source label
  strings, so "why is this node at 4.2/s" is unanswerable. (`Sim/Modifiers.cs`)
- **Observation rates and pity clocks are never surfaced.** Sketch chance per
  site, `DigSiteState.pityHours` (4 h guarantee) and `deepAmberPityHours`
  (12 h) — both load-bearing anti-starvation timers — appear in no UI. The
  watch plate says only "someone wanders / no one wanders".
  (`Sim/Observation.cs`, `Sim/DeepAmber.cs`, `TrailPage.BuildWatchPlate`)

**Partially surfaced — the system shows but its key numbers don't:**

- Familiar rows show a Roman level with no progress readout —
  `GameLoop.FamiliarLevelProgress` exists and is called by nothing (skills
  show "% to next"; familiars should match). (`WardenPage`)
- Kinship's actual perks (+2% XP rate/level, trait deepening ×1.25 per
  milestone, starting-level carry) are never stated as numbers — only the
  level and inscriptions show. (`Sim/Kinship.cs`, `Sim/Traits.cs`)
- The warden's own hands (0.5/s straight to camp) are excluded from the node
  plates' "X/s" (`Simulation.YieldPerSecond`), so posting the warden looks
  like it does nothing. (`Sim/Warden.cs`)
- Roosts comfort (`Buildings.ComfortXpMultiplier`) is computed every tick but
  never shown as a live rate on the roster.
- `Regions.DemandWeight` is invisible — a modified season's verse just costs
  more with no explanation; and nothing explains that runs 2+ re-pick verse
  contents on a rotating spotlight (discount/premium). (`Sim/Regions.cs`,
  `Sim/RiteGenerator.cs`)
- The Compendium shows lifetime *gathered* but never lifetime *crafted*
  (`lifetimeCrafted` feeds only achievements/Game Stats); `lifetimePristine`
  likewise. Cross-run collection (`speciesEverBefriended`,
  `stationsEverWorked`) has no page — the roster is this-run only and there
  is no in-game achievements screen. (`RecordPage`, `Sim/Compendium.cs`)
- The Ladder card windows to the next 3 rungs; the shape of the 34-rung tree
  is never visible (the Almanac, by contrast, lists revealed tiers).
  (`CampPage`)
- A live tincture is only visible on the Warden tab — no global buff
  indicator. (`Sim/Tinctures.cs`)

**Structural absences:**

- **No settings surface at all** — no audio, no save management, no
  cloud-save status (the most-played-wins reconcile in `RunPersistence` is
  completely silent), no privacy/consent access beyond the SDK's own UMP
  dialog.
- **No aggregate camp production view** — per-resource rates exist only on
  individual node cards; the only rollup is the trail's gather-vs-carry
  shortfall line.

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
