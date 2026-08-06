using System;
using System.Collections.Generic;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    // The camp (design §3, §9) — the rungs of the ladder, the building lines,
    // the stations crafting at them and the skills that answer for it, and the
    // waystones read along the way. See GameLoop.cs for the run underneath.
    public sealed partial class GameLoop
    {
        /// <summary>True once the run owns the one-off upgrade.</summary>
        public bool IsUpgradePurchased(UpgradeData upgrade)
        {
            return State.HasUpgrade(upgrade.id);
        }

        /// <summary>The tool tier blocking this upgrade (design §3 zone gate), or null when none — for the buy button's "needs … tools" line.</summary>
        public string MissingToolTier(UpgradeData upgrade)
        {
            return Upgrades.MissingToolTier(State, Data, upgrade);
        }

        /// <summary>Folds still to walk before this rung joins the ladder (design §8), or 0 when it already has — for the rung's "after two folds" line.</summary>
        public int FoldsUntilUpgrade(UpgradeData upgrade)
        {
            return Upgrades.FoldsUntilAvailable(State, Data, upgrade);
        }

        /// <summary>True when the run holds the upgrade's materials (money→XP: no Coin) — for the buy button's enabled state.</summary>
        public bool CanAffordUpgrade(UpgradeData upgrade)
        {
            return Upgrades.CanAfford(State, upgrade);
        }

        /// <summary>True when the run's skill level clears the upgrade's gate (design §9).</summary>
        public bool MeetsUpgradeSkillGate(UpgradeData upgrade)
        {
            return Upgrades.MeetsSkillGate(State, Data, upgrade);
        }

        /// <summary>Buy a one-off upgrade. Returns false (no change) when owned, gated, or unaffordable.</summary>
        public bool PurchaseUpgrade(UpgradeData upgrade)
        {
            if (!Upgrades.TryPurchase(State, Data, upgrade))
            {
                return false;
            }

            Telemetry.LogEvent("upgrade_purchased", ("upgrade_id", upgrade.id));
            return true;
        }

        /// <summary>A building line's current level (bought + owned milestone upgrades) — for the buildings row.</summary>
        public int BuildingLevel(BuildingData building)
        {
            return Buildings.TotalLevel(State, building);
        }

        /// <summary>The material bundle for the line's next level (money→XP: buildings are a goods sink) — for the build button's label.</summary>
        public List<Buildings.MaterialCost> NextBuildingBundle(BuildingData building)
        {
            return Buildings.NextLevelBundle(State, Data, building);
        }

        /// <summary>True when camp stock covers the line's next-level bundle.</summary>
        public bool CanAffordBuilding(BuildingData building)
        {
            return Buildings.CanAfford(State, Data, building);
        }

        /// <summary>Buy the line's next level. Returns false (no change) when the bundle can't be covered.</summary>
        public bool BuyBuildingLevel(BuildingData building)
        {
            if (!Buildings.TryBuyLevel(State, Data, building))
            {
                return false;
            }

            Telemetry.LogEvent("building_level_bought",
                ("building", building.id),
                ("level", Buildings.TotalLevel(State, building)));
            return true;
        }

        /// <summary>The recipes the run can see — for the HUD's crafting section (level-locked ones included, as visible goals).</summary>
        public List<RecipeData> AvailableRecipes()
        {
            return Crafting.AvailableRecipes(State, Data);
        }

        /// <summary>True when the run's skill level covers the recipe's skillLevel — for the HUD's requirement hint.</summary>
        public bool IsRecipeLevelMet(RecipeData recipe)
        {
            return Crafting.SkillLevelMet(State, Data, recipe);
        }

        /// <summary>True when a station may actively work the recipe (every gate) — the assign/advance gate.</summary>
        public bool IsRecipeWorkable(RecipeData recipe)
        {
            return Crafting.IsWorkable(State, Data, recipe);
        }

        /// <summary>The skill's current level (1 when the XP system is unconfigured).</summary>
        public int SkillLevel(string skill)
        {
            return Skills.Level(State, Data, skill);
        }

        /// <summary>Fraction of the way to the skill's next level (0 once capped).</summary>
        public double SkillProgress(string skill)
        {
            return Skills.ProgressToNext(State, Data, skill);
        }

        /// <summary>The skills this run has opened, for the HUD's readout — stable alphabetical order.</summary>
        public List<string> UnlockedSkills()
        {
            var skills = new List<string>(Upgrades.UnlockedSkills(State, Data));
            skills.Sort(StringComparer.Ordinal);
            return skills;
        }

        /// <summary>True while this recipe's station is assigned to it.</summary>
        public bool IsCrafting(RecipeData recipe)
        {
            return Crafting.ActiveStationFor(State, recipe) != null;
        }

        /// <summary>The recipe a station currently holds, or null while it stands idle.</summary>
        public RecipeData StationRecipe(string stationId)
        {
            return Crafting.WorkingRecipe(State, Data, stationId);
        }

        /// <summary>The order assigning this recipe would set aside, or null when a slot is free — the "Craft instead" warning reads this, so it never disagrees with the act.</summary>
        public RecipeData CraftWouldDisplace(RecipeData recipe)
        {
            return Crafting.WouldDisplace(State, Data, recipe);
        }

        /// <summary>Standing orders one station may hold this run — 2 once the second queue is bought.</summary>
        public int StationOrderCapacity()
        {
            return Crafting.OrderCapacity(State);
        }

        // ─────────────── The camp's name (design §9's sink slate) ────────────

        /// <summary>What to call the camp on the page — its bought name, or "the camp".</summary>
        public string CampName()
        {
            return Camp.DisplayName(State);
        }

        /// <summary>Whether this run's camp has been named — the naming sheet asks, to tell a first naming from a change.</summary>
        public bool IsCampNamed()
        {
            return Camp.IsNamed(State);
        }

        /// <summary>The Amber naming this run's camp asks — 0 when the amber system is inert (naming is free then).</summary>
        public double CampNameCost()
        {
            return Amber.CampNameCost(Data);
        }

        /// <summary>Whether naming the camp is affordable right now.</summary>
        public bool CanNameCamp()
        {
            return Amber.CanNameCamp(State, Data);
        }

        /// <summary>Name this run's camp (design §9) — charged on a change only. Returns whether the name changed.</summary>
        public bool NameCamp(string name)
        {
            var named = Amber.TryNameCamp(State, Data, name);
            if (named)
            {
                Telemetry.LogEvent("camp_named", ("amber_cost", Amber.CampNameCost(Data)));
            }

            return named;
        }

        /// <summary>True when camp stock covers one batch of the recipe's inputs.</summary>
        public bool CanCraft(RecipeData recipe)
        {
            return Crafting.HasInputs(State, recipe);
        }

        /// <summary>The in-flight batch's fraction complete (0 when idle or stalled).</summary>
        public double CraftProgress(RecipeData recipe)
        {
            return Crafting.Progress(State, Data, recipe);
        }

        /// <summary>True when this recipe's station is assigned but nothing is turning — the "Crafting halted" line.</summary>
        public bool IsCraftHalted(RecipeData recipe)
        {
            return Crafting.IsStalled(State, recipe);
        }

        /// <summary>The input camp stock is short of, or null — names what a halted station waits on.</summary>
        public string MissingCraftInput(RecipeData recipe)
        {
            return Crafting.MissingInput(State, recipe);
        }

        /// <summary>Start the recipe on its station (displacing whatever it was working, in-flight inputs refunded), or stop it if it's already running.</summary>
        public void ToggleCraft(RecipeData recipe)
        {
            if (IsCrafting(recipe))
            {
                Crafting.Stop(State, Data, recipe);
                return;
            }

            if (!Crafting.IsWorkable(State, Data, recipe))
            {
                return;
            }

            Crafting.Assign(State, Data, recipe);
            Telemetry.LogEvent("craft_started",
                ("recipe", recipe.id),
                ("station", recipe.station));
        }

        /// <summary>Mark a zone's waystone inscription as read (design §6 — shown once on arrival, re-readable in the Compendium).</summary>
        public void MarkWaystoneRead(string zoneId)
        {
            Narrative.MarkWaystoneRead(State, zoneId);
            Telemetry.LogEvent("waystone_read", ("zone", zoneId));
            // A new stone is the progression stat moving — Google asks for the
            // progress event on change, not only at launch.
            Stats.ReportProgress(State);
        }

        /// <summary>Take the next final waystone (design §7 — one a fold at the last zone, re-readable in the Deep Pages).</summary>
        public void MarkFinalWaystoneRead()
        {
            var stone = Narrative.NextFinalWaystone(State, Data);
            if (stone == null)
            {
                return;
            }

            Narrative.MarkFinalWaystoneRead(State, Data);
            Telemetry.LogEvent("final_waystone_read",
                ("stone", stone.key),
                ("read", State.finalWaystonesRead),
                ("fold", State.migrationCount));
        }
    }
}
