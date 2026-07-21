# Placeholder & TODO manifest

A living list of the deliberate placeholders and deferred work in the codebase, so
nothing quietly ships as "done". Grouped by the phase that retires it (see
`design-doc.md` §13 MVP development plan). Keep entries pointing at the code so
they're easy to find and delete when resolved.

## v0.11 design realignment

Design doc v0.11 (July 2026) makes several **DECIDED 2026-07-18** reversals. The
per-item detail below points at the code/data; the phase lists further down are
annotated **v0.11:** where a decision touches them.

**IMPLEMENTED 2026-07-21 (crew + money→XP + Exchange + naming; 370/370 EditMode green):**
- ✅ **Coin is gone — money becomes XP.** `GameState.coin` removed; Renown = lifetime
  XP (warden skills + familiars) + offering credits; upgrades reprice to skill-gate
  (`gateSkill`/`gateLevel`) + material bundle; buildings → material bundles; zones drop
  `mapCostCoin`. New **Exchange** (`Exchange.cs` + `exchange.json`) barters goods↔goods
  off the trade-value table (`Economy.TradeValuePerUnit`), spread + player-favourable
  rounding.
- ✅ **Crew of individuals + stationing.** `NodeState.familiarCount`/`GameState.carrierCount`
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
  pass) and no spread grants crew slot 5 yet (the slot ladder isn't built).

**Interpretations / placeholders shipped with it (tune/confirm):**
- Wanderers' ×0.5 help is spread evenly across unlocked gather nodes; an unheld trail is a
  flat ×0.5 lane when anyone wanders (`Stationing.cs`).
- Kinship constants (`Divisor` 1000, `XpRatePerLevel` 0.02) are consts in `Kinship.cs` — move
  to a data section. `economy.familiarXp` {base 60, growth 1.12, xpPerSecond 1} are first guesses.
- Familiar XP is a flat per-second at any post (no postMatch/comfort multipliers yet, design §8).
- Ungated/material-less upgrades are free once their skill gate opens (§10 material bundles are
  placeholders); building material bundles are placeholders.
- Crew is fully persisted across Migration (no presence-lapse/benching yet); the 5-slot cap
  ladder, the verse-1 gift-event (slot 3), and Roosts "comfort" XP are NOT wired.
- Pristine "sell" dropped — Pristine is offer/donate only for now.
- `familiarCaps` economy section + roosts `familiarCaps` perLevel are now vestigial (RoostLevel kept).

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
have landed (Coin/Exchange, crew-of-5/stationing, Kinship, Folio, insects,
replanting/planters, stationing-aware reachability). Remaining v0.11 work is
MVP tails / balance, tracked in the items below.

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
- **Flock + carriers → a stationed crew of five (§2, §4).** Shipped model: per-node
  gatherer counts (`NodeState.familiarCount`), a camp carrier pool
  (`GameState.carrierCount`), flock/carrier caps (`economy.familiarCaps`), and gift
  cost curves (`economy.gifts`). Design: up to **5 named roster familiars**, each with
  a level and an authored **powerup** build (a choice every 5 levels from a
  deterministic species pool), **stationed** at a post; **carrying is a post (the
  trail post), not a species** — no carrier type. An unassigned familiar **wanders** at
  ×0.5 rate/XP with no powerups; the warden never wanders (post = last tended).
  **Levels never scale output** (throughput comes from tools/hauling/powerups/richness).
  Stationing, powerups, roster, and crew slots are all absent from code.
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
  fits `RiteGenerator.CrewGatherPosts` (= 4: MVP crew of 5 minus the trail post).
  `RiteGenerator.StationingFootprint()` computes it; `CandidateGoods` filters picks
  by it so the generator never asks for a good the crew can't keep produced; the
  runs-2–10 proof (`RiteGeneratorTests`) now counts reachable with the footprint gate.
  Deed/specimen/sketch each need one post, always within budget. MVP content is well
  under budget (footprints ≤ 2), so this is a guardrail for future content, not a
  behaviour change today. INTERPRETATIONS/DEFERRED: `CrewGatherPosts` is a first-guess
  constant — derive it from the crew-slot ladder (unbuilt) / move to data later; the
  three chosen slots are **not** budgeted together (per-slot only, by design choice);
  the import-time validator's authored-run-1 rite is NOT yet given a stationing-aware
  ≥chooseCount check (the guarantee lives in the generator + proof, which is where
  generated rites are checkable) — a cheap follow-up if wanted.

## Phase 1 — Core loop slice (current)

- **Tending burst values are a first guess.** `burstYieldMult` / `burstDurationSec`
  in `design/data/economy.json` aren't in the design doc — tune once the loop is
  playable. (`$note` in the file.)
- **Feeder base amount is a first guess.** `gifts.carrierBaseGoods = 8` — N of each
  worked resource per carrier (design §13 Feeder). Watch the Phase 3 zone unlocks:
  the bundle broadens as new nodes come into work, so a freshly-gifted node briefly
  raises the carrier price in a resource the camp barely holds — probably desirable
  tension, but confirm it in balance.
  **v0.11:** superseded by the crew reversal (§4) — there is no carrier type and gifts
  become one-shot *recruitment events* (crew slot 3, unlocked by verse 1), not a cost
  curve. Retire `gifts.carrierBaseGoods`/`carrierGift` with the stationing rework.
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

- **The journal HUD (2026-07-21) follows `docs/wildgrove-journal.html`, still built in
  code.** `GameHud` now lays out the mock's structure — paper palette, eyebrow/title
  head, resource ledger, margin note, pinned Rite/Fold tracker, and four bottom tabs
  (Trail · Camp · Warden · Record) — as runtime uGUI, with the reskin pass on top:
  the four journal typefaces, generated ruled ink borders, paper-grain + stitched-spine
  overlays, and the tend-flash / trail-carrier motion touches. Still no hand-drawn
  line art (node plates and the compendium have no naturalist illustrations).
  Interpretations shipped with it (tune/confirm):
  - The **world strip stays above the page on every tab**; the mock has no strip
    (its plates ARE the world). It goes when the real region scene lands.
  - The **margin note** is a flavour line set by actions (tend/replant/trade/offer/
    build), hardcoded strings in `GameHud` — not a data-driven dialogue channel.
  - The **ledger** lists every discovered resource; it will need a cap or grouping
    once later zones make it long.
  - The Rite verse card now renders **all four slot types** (the old HUD skipped
    specimen/sketch/deed); spotlight (✳) markers are not shown yet.
  - Migration runs tracker **Fold button → confirm sheet → full-dark vignette**
    (lines from `dialogue.migrationVignette`); the vignette shows the Verdure gain
    only — per-familiar Kinship gains aren't itemised.
  - The **waystone arrival modal is restored** (it had been dropped in the v0.11
    HUD rewrite); it queues behind arrival/bond/welcome sheets.
  - Crew **post buttons are a fixed 5-column grid** of node/trail/watch/wander;
    fine at MVP station counts, revisit when zones multiply.
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
- **Portrait-only: the mock's wide (≥880px) two-column layout isn't built.** The
  journal tabs solve the outgrowing-portrait problem (each page is its own scroll),
  but landscape/desktop still gets the phone column; the mock pins the Trail page
  beside the open page on wide screens. Safe-area insets (`env(safe-area-inset-*)`
  in the mock) are also not applied. `HeightClampedElement`/`TrackedScrollRect`
  are no longer used by the HUD (kept compiling — delete or reuse).
  (`GameHud`)
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
  lean. Still waiting: IAP/rewarded-ads/weekly-cache earn paths (the §10
  plugin pass), amber-find telemetry (sim-side roll can't log), cosmetics/
  extra craft queues as further sinks, excavation skill XP ("XP from every
  action" — fragments are too rare for per-unit XP; decide a grant when
  tool-tier / level gates need the level), and the fossil card lore
  (Compendium).
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
  line's baseCostCoin). The Spare Wing's +1 carrier slot (§10) arrives with
  the PGS rewards layer. (`design/data/buildings.json`)
  **v0.11:** buildings are now a **goods sink**, not a Coin sink (§10) — `baseCostCoin`
  becomes a material bundle; Roosts & Burrows re-scopes to **familiar comfort** (+XP
  rate per level, roster capacity at late levels), not headcount caps; and the Spare
  Wing grants **+1 trail post**, not a carrier slot (§11) — carrying is a post now.
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
  it as the second track (earned at the post), which the crew rework introduces.
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
  `donatedResources` reframe as the Folio; sets → spreads (one MVP spread grants crew
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
  a run-3 verse *says*), and the design doc's region modifiers (single
  region at MVP, modifierWeight ≡ 1). Generator interpretations flagged:
  generator numbers (demandGrowth 2.5, spotlightDiscount 0.6,
  offSpotlightPremium 1.5) are first guesses against §8's "similar share of
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
  plausible stationing with the current crew size, §2), replacing zone-unlock
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
  balance pass, spoilage if it ever ships). Also interpretations: crafting
  into an occupied slot destroys the old piece (no kit bag), and crafted
  gear is worn immediately — there is no separate equip step.
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
  familiar-power nodes** (§4): its crew-adjacent scope is limited to slot access (crew
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
- **`map-mistfen` grants a zone but no dig site / skills** — flagged for v1.1.

## Number formatting

- **`NumberFormat` suffix table** runs `K, M, B, T` then `aa, ab, …` before falling
  back to scientific — first-pass abbreviations; revisit if a naming convention is
  chosen. (`Assets/Scripts/Game/NumberFormat.cs`)
