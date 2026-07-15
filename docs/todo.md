# Placeholder & TODO manifest

A living list of the deliberate placeholders and deferred work in the codebase, so
nothing quietly ships as "done". Grouped by the phase that retires it (see
`design-doc.md` §12 MVP development plan). Keep entries pointing at the code so
they're easy to find and delete when resolved.

## Phase 1 — Core loop slice (current)

- **Tending burst values are a first guess.** `burstYieldMult` / `burstDurationSec`
  in `design/data/economy.json` aren't in the design doc — tune once the loop is
  playable. (`$note` in the file.)
- **Familiar gift base cost is first-pass — and Coin-denominated.** `gifts.familiarBaseCoin = 10`
  in `design/data/economy.json`. Design v0.5 prices gifts in goods, not Coin; the Coin
  cost is a Phase-1 placeholder. Reprice (and pick the goods denomination) with the
  economy pass.
- **Autosave interval (30 s) and welcome-back threshold (60 s credited) are first
  guesses.** Tune with the loop playtest. (`GameLoop.AutosaveIntervalSeconds`,
  `GameHud.WelcomeBackMinSeconds`)
- **Upgrade rows show Coin cost only.** No Phase-1 upgrade has a materials cost, so
  the buy button doesn't display one; `Upgrades.TryPurchase` already spends materials,
  the label must learn to show them when the Phase 3 catalogue lands. (`GameHud`)

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

- **Upgrade shop is a hardcoded Phase-1 whitelist.** `GameHud.PhaseOneUpgradeIds`
  lists the Sunfield-reachable upgrades (1–3, 6, 9) by id; the full §9 ladder needs
  real content gating (zones, stations, unlock effects applied) with Phase 3. The
  unlock effect types (`unlockZone` / `unlockSkill` / `unlockRecipe` / `unlockDigSite`)
  are recorded on purchase but nothing consumes them yet. (`GameHud`, `Wildgrove.Sim/Upgrades.cs`)
- **haulMult upgrades are purchased but inert.** Waxed Satchel / Handcart record on
  the run and their Coin sink works, but nothing hauls yet — the carrier/haul sim
  arrives with Phase 3. (`Wildgrove.Sim/Upgrades.cs`)
- **The Rite is data-only.** `rites.json`, `zones.verseSite`, and `dialogue.verses` are
  parsed, validated, and mapped into GameData.asset, but no runtime system consumes
  them until the Phase 3 Rite build. Validator covers slot integrity only — the full
  ≥3-slots-reachable analysis (design §7) is a Phase 3 job.
- **buildings.json doesn't exist yet.** The §9 camp building lines (Clay Furnace /
  forge-station gating, Roosts & Burrows familiar caps) are doc-only; author the data
  and its pipeline when Phase 3 crafting lands.
- **Familiar caps not enforced in the sim.** `flockCap` / `carrierSlots` (design §8)
  arrive with the building lines; `TryGiftFamiliar` currently has no cap check.
- **Save restore only rebuilds the starting zone's nodes.** `SaveCodec.Restore`
  reconciles saved node progress against `GameStateFactory.NewGame`, which is
  Sunfield-only — when Phase 3 zone unlocks land, restore must rebuild every
  *unlocked* zone's nodes (derive the set from the run's purchased map upgrades)
  or unlocked-zone familiars vanish on load. (`Assets/Scripts/Sim/Saves/SaveCodec.cs`)

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
