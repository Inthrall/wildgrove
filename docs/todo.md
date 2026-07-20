# Placeholder & TODO manifest

A living list of the deliberate placeholders and deferred work in the codebase, so
nothing quietly ships as "done". Grouped by the phase that retires it (see
`design-doc.md` §12 MVP development plan). Keep entries pointing at the code so
they're easy to find and delete when resolved.

## Phase 1 — Core loop slice (current)

- **Tending burst values are a first guess.** `burstYieldMult` / `burstDurationSec`
  in `design/data/economy.json` aren't in the design doc — tune once the loop is
  playable. (`$note` in the file.)
- **Feeder base amount is a first guess.** `gifts.carrierBaseGoods = 8` — N of each
  worked resource per carrier (design §13 Feeder). Watch the Phase 3 zone unlocks:
  the bundle broadens as new nodes come into work, so a freshly-gifted node briefly
  raises the carrier price in a resource the camp barely holds — probably desirable
  tension, but confirm it in balance.
- **Warden gather rate is a first guess.** `warden.gatherPerSecond = 0.5` — the
  warden's passive trickle at their post (always on, burst-boosted), and the
  bare-node gift bootstrap. Replaced the old burst-only hand-gather so the
  early game is assignment, not a tap surge. Tune so the first gift lands in
  ~20 s of just standing there.
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

## Phase 2 — Adaptive UI & input

- **HUD is programmer-art, built in code.** `GameHud` constructs uGUI at runtime with
  flat colours and `LayoutGroup`s; real responsive layout (phone-portrait column ↔
  landscape dashboard) is the Phase 2 job. (`Assets/Scripts/Game/GameHud.cs`)
- **Legacy `Text` + `LegacyRuntime.ttf` builtin font.** Chosen to avoid the
  TextMeshPro essentials import for placeholder UI; swap to TMP with a real naturalist
  typeface when the art direction lands. (`GameHud`)
- **Node sprites are runtime-generated placeholder discs in a screen strip.**
  `PlaceholderArt` makes one tinted disc per resource and `WorldView` lays them out
  in the gap the HUD leaves open; the hand-drawn naturalist plates and a real region
  scene replace them (the camera/world seam and screen-point hit test stay).
  (`Assets/Scripts/Game/World/`)
- **The lower panel scrolls rather than adapting.** The node/shop/crafting/camp
  sections sit in a height-capped scroll view (45% of canvas height) with the
  actions row and hint pinned below — so the column can't outgrow portrait, but
  it's still a single list. Per-zone collapsing and the landscape dashboard are
  the remaining Phase 2 job; on short landscape screens the scroll section
  absorbs the squeeze and the world strip can collapse to nothing.
  (`GameHud.BuildScrollSection`, `HeightClampedElement`)
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
- **Building perLevel values are first guesses, and two are interpretations.**
  The 5% speed/capacity tapers aren't in the design doc. Interpretive calls to
  confirm in balance: the §9 Store's "storage capacity" is implemented as
  basket capacity (camp storage caps don't exist), and the Clay Furnace is
  simply the forge line's first bought level (its ~8,000 debut price is the
  line's baseCostCoin). The Spare Wing's +1 carrier slot (§10) arrives with
  the PGS rewards layer. (`design/data/buildings.json`)
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
  Migration skill reset (§7) for the prestige build.
  (`Wildgrove.Sim/Skills.cs`, `design/data/recipes.json`)
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

- **The Almanac is 12 nodes of existing effect types; costs and the
  allocation model are interpretations.** Verdure is never destroyed — a node
  allocates from the banked total (available = verdurePoints − owned costs)
  so the +2%/pt passive keeps counting the full total and Migration's
  recompute-from-lifetime-Renown can't refund spent points. The §7 exotic
  nodes (starting tool tiers, auto-craft, starting-zone skips) wait for
  their systems; all costs are first guesses tuned to ~10 Verdure from the
  first Migration. (`design/data/almanac.json`, `Wildgrove.Sim/Almanac.cs`)

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

## Narrative authoring

- **MVP dialogue is drafted, not final.** All four waystones, all four verse
  lines, and all three fossil cards in `design/data/dialogue.json` now have
  text in the §6 register, and the validator enforces waystone + verse text
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
