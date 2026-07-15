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
- **Gamepad South double-fire.** Pad-South is also uGUI's Submit, so tending while a
  widget is focused can fire both the button and the tend. Fix in the controller/focus
  pass. (`GameHud.HandleTendInput`)
- **Runtime bootstrap instead of a bootstrap scene.** `Bootstrap` spawns GameLoop +
  GameHud via `[RuntimeInitializeOnLoadMethod]` so Play works with zero scene setup.
  Replace with a real bootstrap scene when there's content to lay out.
  (`Assets/Scripts/Game/Bootstrap.cs`)

## Phase 3+ — Systems build-out

- **`unlockSkill` / `unlockRecipe` / `unlockDigSite` effects are still inert.**
  `unlockZone` is live (trail maps create the zone's nodes with the §2 regional
  seed); the other unlock types are recorded on purchase but wait for the
  crafting and excavation systems. (`Wildgrove.Sim/Upgrades.cs`)
- **Haul is a continuous-rate approximation, not discrete batches.** Carriers drain
  baskets proportionally at units/sec; design §5's quality rolls happen *per haul
  batch* (a carrier delivery), so Phase 3's Compendium needs the haul loop reworked
  into discrete deliveries. (`Simulation.Haul`)
- **Hauling numbers are first guesses.** `baseCarryCapacity` / `tripSeconds` /
  `basketCapacity` in `design/data/economy.json` aren't in the design doc — tune with
  the loop playtest. Carrier slots (`carrierSlots = 2 + roostLevel`) are not enforced
  yet — same cap gap as the gatherer `flockCap` below.
- **The Rite is data-only.** `rites.json`, `zones.verseSite`, and `dialogue.verses` are
  parsed, validated, and mapped into GameData.asset, but no runtime system consumes
  them until the Phase 3 Rite build. Validator covers slot integrity only — the full
  ≥3-slots-reachable analysis (design §7) is a Phase 3 job.
- **buildings.json doesn't exist yet.** The §9 camp building lines (Clay Furnace /
  forge-station gating, Roosts & Burrows familiar caps) are doc-only; author the data
  and its pipeline when Phase 3 crafting lands.
- **Familiar caps not enforced in the sim.** `flockCap` / `carrierSlots` (design §8)
  arrive with the building lines; `TryGiftFamiliar` currently has no cap check.
- **The HUD column will outgrow portrait height around the third zone.** Node
  rows are a straight VerticalLayoutGroup with no scrolling — three zones is
  ~10 rows plus the shop and actions. The Phase 2 adaptive-layout pass needs a
  scroll view (or per-zone collapsing) before Old-Growth Wood is reachable in
  balance. (`GameHud`)

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
