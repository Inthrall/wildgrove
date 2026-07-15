# Placeholder & TODO manifest

A living list of the deliberate placeholders and deferred work in the codebase, so
nothing quietly ships as "done". Grouped by the phase that retires it (see
`design-doc.md` §Roadmap). Keep entries pointing at the code so they're easy to find
and delete when resolved.

## Phase 1 — Core loop slice (current)

- **Tending burst values are a first guess.** `burstYieldMult` / `burstDurationSec`
  in `design/data/economy.json` aren't in the design doc — tune once the loop is
  playable. (`$note` in the file.)
- **Crew hire base cost is first-pass.** `hires.crewBaseCoin = 10` in
  `design/data/economy.json` — revisit with the economy pass.
- **Offline progress isn't auto-applied on load.** `GameLoop.Awake` starts a fresh
  run every launch; `GameLoop.ApplyOfflineProgress` exists but nothing calls it until
  there's persisted state to diff against (needs the save system — Phase 5). Applying
  offline to a `NewGame` would fabricate resources, so the seam is intentionally dead
  for now. (`Assets/Scripts/Game/GameLoop.cs`)
- **Welcome-back offline summary sheet not built.** Design §Phase 1 calls for one;
  `AdvanceOffline` already returns the credited wall-seconds for it to display.
- **Upgrades 1–10 have no UI.** Data + effects model exist; no purchase surface yet.

## Phase 2 — Adaptive UI & input

- **HUD is programmer-art, built in code.** `GameHud` constructs uGUI at runtime with
  flat colours and `LayoutGroup`s; real responsive layout (phone-portrait column ↔
  landscape dashboard) is the Phase 2 job. (`Assets/Scripts/Game/GameHud.cs`)
- **Legacy `Text` + `LegacyRuntime.ttf` builtin font.** Chosen to avoid the
  TextMeshPro essentials import for placeholder UI; swap to TMP with a real naturalist
  typeface when the art direction lands. (`GameHud`)
- **Positional tend targets the selected row, not a world node.** With no world node
  sprites yet, a tap on empty space tends `_selected`. Once nodes are drawn, the
  `IGameInput` screen position should raycast to the node under the pointer.
  (`GameHud.HandleTendInput`)
- **Gamepad South double-fire.** Pad-South is also uGUI's Submit, so tending while a
  widget is focused can fire both the button and the tend. Fix in the controller/focus
  pass. (`GameHud.HandleTendInput`)
- **Runtime bootstrap instead of a bootstrap scene.** `Bootstrap` spawns GameLoop +
  GameHud via `[RuntimeInitializeOnLoadMethod]` so Play works with zero scene setup.
  Replace with a real bootstrap scene when there's content to lay out.
  (`Assets/Scripts/Game/Bootstrap.cs`)

## Phase 3+ — Systems build-out

- **`NodeState.yieldMultiplier` is an opaque, always-1 multiplier.** The tick treats
  it as a black box; the upgrade/gear/tool-tier system recomputes it later — nothing
  derives tool tiers yet. (`Assets/Scripts/Sim/GameState.cs`)
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
