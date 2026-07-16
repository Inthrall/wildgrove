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
- **Hand-gather rate is a first guess.** `tending.handGatherPerSecond = 0.5` — the
  warden's trickle while a burst is live, and the bare-node gift bootstrap. Tune so
  a first gift feels like a few taps, not a grind.
- **Telemetry sink is the Unity log, not Firebase.** The design calls for
  Crashlytics + GA events from Phase 1; the events (session_start/end,
  upgrade_purchased, familiar_gifted, welcome_back) are instrumented behind
  `ITelemetry`, but the sink is `UnityLogTelemetry` (Debug.Log / logcat) until the
  Firebase side exists. Remaining steps: create the Firebase project + register
  `com.inthrall.wildgrove` and download `google-services.json` (console work), then
  import the Firebase Unity SDK (Analytics + Crashlytics) and swap the sink in
  `GameLoop.Initialise`. Note the SDK's External Dependency Manager patches
  `mainTemplate.gradle` — re-check the hand-authored Kotlin pins when it lands.
  (`Assets/Scripts/Game/Telemetry/`)
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
- **Gamepad South double-fire.** Pad-South is also uGUI's Submit, so tending while a
  widget is focused can fire both the button and the tend. Fix in the controller/focus
  pass. (`GameHud.HandleTendInput`)
- **Runtime bootstrap instead of a bootstrap scene.** `Bootstrap` spawns GameLoop +
  GameHud via `[RuntimeInitializeOnLoadMethod]` so Play works with zero scene setup.
  Replace with a real bootstrap scene when there's content to lay out.
  (`Assets/Scripts/Game/Bootstrap.cs`)

## Phase 3+ — Systems build-out

- **`unlockDigSite` effects are still inert.** `unlockZone` (trail maps create
  the zone's nodes), `unlockSkill` and `unlockRecipe` (both gate crafting) are
  all live; dig sites wait for the excavation system.
  (`Wildgrove.Sim/Upgrades.cs`)
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
- **Skill XP gains and recipe skillLevels are first guesses.** xp.gatherPerUnit
  (1, credited on the gross gather — basket overflow loses the goods but still
  pays XP) and xp.craftPerBatch (25) aren't in the design doc, nor are the
  per-recipe skillLevel picks (skewer/trout 2, reed baskets and bronze 3,
  iron 5). Tool-tier level gating (§4) waits for the tools system, and the
  Migration skill reset (§7) for the prestige build.
  (`Wildgrove.Sim/Skills.cs`, `design/data/recipes.json`)
- **Haul is a continuous-rate approximation, not discrete batches.** Carriers drain
  baskets proportionally at units/sec; design §5's quality rolls happen *per haul
  batch* (a carrier delivery), so Phase 3's Compendium needs the haul loop reworked
  into discrete deliveries. (`Simulation.Haul`)
- **Hauling numbers are first guesses.** `baseCarryCapacity` / `tripSeconds` /
  `basketCapacity` in `design/data/economy.json` aren't in the design doc — tune with
  the loop playtest.
- **The Rite is data-only.** `rites.json`, `zones.verseSite`, and `dialogue.verses` are
  parsed, validated, and mapped into GameData.asset, but no runtime system consumes
  them until the Phase 3 Rite build. Validator covers slot integrity only — the full
  ≥3-slots-reachable analysis (design §7) is a Phase 3 job.
- **`NodeState.yieldMultiplier` only folds in purchased upgrades.** The tick treats
  it as a black box; `Upgrades.RecomputeYieldMultipliers` rebuilds it from yieldMult /
  yieldBonus upgrade effects, but gear and tool-tier derivation still multiply in
  with their systems. (`Assets/Scripts/Sim/Upgrades.cs`)
- **Tending's Pristine-chance bump is not implemented.** `Simulation.Tend` only starts
  the yield burst; the brief Pristine bonus arrives with the quality system.
  (`Assets/Scripts/Sim/Simulation.cs`; `tending.pristineBonusDurationSec` in data.)
- **Verdure / almanac / museum / fossil / boost multipliers.** `Simulation.YieldPerSecond`
  folds in the Verdure global bonus only; the other multipliers arrive with their
  systems and multiply in there.

## Data-layer review items (open from the data-layer PR review)

- **Skills vocabulary hardcoded** in `GameDataValidator` as a C# `HashSet` rather than
  sourced from data.
- **`zone.unlocks` is documentation-only and diverges from upgrade effects** — e.g.
  excavation is never actually granted through the unlock path.
- **`map-mistfen` grants a zone but no dig site / skills** — flagged for v1.1.

## Number formatting

- **`NumberFormat` suffix table** runs `K, M, B, T` then `aa, ab, …` before falling
  back to scientific — first-pass abbreviations; revisit if a naming convention is
  chosen. (`Assets/Scripts/Game/NumberFormat.cs`)
