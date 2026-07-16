using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The crafting system (design §4): each station (fire / bench / forge)
    /// auto-crafts its assigned recipe continuously — inputs are spent from
    /// camp stock when a batch starts, the output lands at camp when it
    /// completes, and a station stalls quietly until stock can cover the next
    /// batch. Recipes become craftable when they're known (defaultKnown or an
    /// owned unlockRecipe effect), their skill is unlocked, their station line
    /// is hot enough, and the skill's level covers the recipe (design §4).
    /// Pure and deterministic like the tick.
    /// </summary>
    public static class Crafting
    {
        /// <summary>
        /// The recipes the run can see, in data order: known (defaultKnown or
        /// granted by an owned unlockRecipe effect), their skill unlocked, and
        /// their station line built to the recipe's stationLevel (design §9
        /// heat — iron needs forge 2). Recipes above the run's skill level ARE
        /// listed — they're a visible goal; <see cref="SkillLevelMet"/> gates
        /// actually assigning them.
        /// </summary>
        public static List<RecipeData> AvailableRecipes(GameState state, GameDataAsset data)
        {
            var skills = Upgrades.UnlockedSkills(state, data);
            var unlockedRecipes = Upgrades.UnlockedRecipeIds(state, data);

            var available = new List<RecipeData>();
            foreach (var recipe in data.recipes)
            {
                if ((recipe.defaultKnown || unlockedRecipes.Contains(recipe.id))
                    && skills.Contains(recipe.skill)
                    && StationLevelMet(state, data, recipe))
                {
                    available.Add(recipe);
                }
            }

            return available;
        }

        /// <summary>
        /// True when a station may actively work the recipe: known, skill
        /// unlocked, station hot enough, and skill level met. Assign checks
        /// this on the way in, and Advance re-checks it every tick — so a
        /// station restored (or data retuned) past a gate stalls instead of
        /// crafting through it.
        /// </summary>
        public static bool IsWorkable(GameState state, GameDataAsset data, RecipeData recipe)
        {
            return IsWorkable(state, data, recipe,
                Upgrades.UnlockedSkills(state, data), Upgrades.UnlockedRecipeIds(state, data));
        }

        private static bool IsWorkable(GameState state, GameDataAsset data, RecipeData recipe,
            HashSet<string> unlockedSkills, HashSet<string> unlockedRecipes)
        {
            return (recipe.defaultKnown || unlockedRecipes.Contains(recipe.id))
                   && unlockedSkills.Contains(recipe.skill)
                   && StationLevelMet(state, data, recipe)
                   && SkillLevelMet(state, data, recipe);
        }

        /// <summary>
        /// True when the run's skill level covers the recipe's skillLevel
        /// (design §4: levels gate recipes). Ungated when economy.xp is absent
        /// — hand-built test data without the XP system.
        /// </summary>
        public static bool SkillLevelMet(GameState state, GameDataAsset data, RecipeData recipe)
        {
            if (data.economy?.xp == null)
            {
                return true;
            }

            return Skills.Level(state, data, recipe.skill) >= recipe.skillLevel;
        }

        /// <summary>
        /// True when the recipe's station is hot enough: its building line
        /// (matched by id) is at ≥ the recipe's stationLevel. A station no
        /// line claims is ungated — hand-built test data without buildings.
        /// </summary>
        public static bool StationLevelMet(GameState state, GameDataAsset data, RecipeData recipe)
        {
            if (data.buildings == null || !data.BuildingsById.TryGetValue(recipe.station, out var line))
            {
                return true;
            }

            return Buildings.TotalLevel(state, line) >= recipe.stationLevel;
        }

        /// <summary>The station working <paramref name="recipe"/>, or null.</summary>
        public static StationState ActiveStationFor(GameState state, RecipeData recipe)
        {
            var station = StationFor(state, recipe.station);
            return station != null && station.recipeId == recipe.id ? station : null;
        }

        /// <summary>
        /// Assign <paramref name="recipe"/> to its station, displacing whatever
        /// the station was working (an in-flight batch's inputs are refunded —
        /// switching is never a punishment). No-op if it's already assigned.
        /// </summary>
        public static void Assign(GameState state, GameDataAsset data, RecipeData recipe)
        {
            if (state == null || recipe == null || !IsWorkable(state, data, recipe))
            {
                return;
            }

            var station = StationFor(state, recipe.station);
            if (station == null)
            {
                station = new StationState { stationId = recipe.station };
                state.stations.Add(station);
            }

            if (station.recipeId == recipe.id)
            {
                return;
            }

            RefundInFlight(state, data, station);
            station.recipeId = recipe.id;
        }

        /// <summary>Stop the station working this recipe, refunding any in-flight batch.</summary>
        public static void Stop(GameState state, GameDataAsset data, RecipeData recipe)
        {
            var station = ActiveStationFor(state, recipe);
            if (station == null)
            {
                return;
            }

            RefundInFlight(state, data, station);
            station.recipeId = null;
        }

        /// <summary>True when camp stock covers one batch of the recipe's inputs.</summary>
        public static bool HasInputs(GameState state, RecipeData recipe)
        {
            foreach (var input in recipe.inputs)
            {
                if (state.GetResource(input.id) < input.amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Run every station's bar forward by <paramref name="deltaSeconds"/>.
        /// Called from the tick (sub-stepped, so offline catch-up crafts batch
        /// by batch as gathered inputs arrive, the same as live play).
        /// </summary>
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            var crafting = data.economy?.crafting;
            if (crafting == null)
            {
                return;
            }

            // The gate sets are purchase-driven and shared by every station;
            // computed lazily so station-less ticks stay allocation-free.
            HashSet<string> unlockedSkills = null;
            HashSet<string> unlockedRecipes = null;

            foreach (var station in state.stations)
            {
                if (station.recipeId == null || !data.RecipesById.TryGetValue(station.recipeId, out var recipe))
                {
                    continue;
                }

                unlockedSkills = unlockedSkills ?? Upgrades.UnlockedSkills(state, data);
                unlockedRecipes = unlockedRecipes ?? Upgrades.UnlockedRecipeIds(state, data);

                // A station holding a recipe the run can no longer work (a
                // save restored past a gate, or a data retune) stalls with its
                // in-flight batch frozen — Stop refunds it; it never crafts
                // through a gate Assign would refuse.
                if (!IsWorkable(state, data, recipe, unlockedSkills, unlockedRecipes))
                {
                    continue;
                }

                var duration = BatchSeconds(state, data, recipe);
                var remaining = deltaSeconds;

                while (remaining > 0.0)
                {
                    if (!station.inFlight)
                    {
                        if (!HasInputs(state, recipe))
                        {
                            break; // Stalled until stock covers the next batch.
                        }

                        SpendInputs(state, recipe);
                        station.inFlight = true;
                        station.progressSeconds = 0.0;
                    }

                    var step = System.Math.Min(remaining, duration - station.progressSeconds);
                    station.progressSeconds += step;
                    remaining -= step;

                    // Epsilon guards float residue: a batch a rounding error
                    // short of done must still complete, or the loop spins.
                    if (station.progressSeconds >= duration - 1e-9)
                    {
                        state.AddResource(recipe.output, BigDouble.One);
                        Skills.AddCraftXp(state, data, recipe.skill);
                        Compendium.RecordCraft(state, recipe.id);
                        station.inFlight = false;
                        station.progressSeconds = 0.0;
                    }
                }
            }
        }

        /// <summary>The in-flight batch's fraction complete (0 when idle/stalled) — for the HUD's bar.</summary>
        public static double Progress(GameState state, GameDataAsset data, RecipeData recipe)
        {
            var station = ActiveStationFor(state, recipe);
            var crafting = data.economy?.crafting;
            if (station == null || !station.inFlight || crafting == null)
            {
                return 0.0;
            }

            var duration = BatchSeconds(state, data, recipe);
            return duration > 0.0 ? station.progressSeconds / duration : 0.0;
        }

        /// <summary>One batch's craft time: the base divided by the skill's craftSpeedMult upgrades and the station line's speed levels.</summary>
        private static double BatchSeconds(GameState state, GameDataAsset data, RecipeData recipe)
        {
            return data.economy.crafting.baseCraftSeconds
                   / Upgrades.CraftSpeedMultiplier(state, data, recipe.skill)
                   / Buildings.StationSpeedMultiplier(state, data, recipe.station);
        }

        private static StationState StationFor(GameState state, string stationId)
        {
            foreach (var station in state.stations)
            {
                if (station.stationId == stationId)
                {
                    return station;
                }
            }

            return null;
        }

        private static void SpendInputs(GameState state, RecipeData recipe)
        {
            foreach (var input in recipe.inputs)
            {
                state.resources[input.id] = state.GetResource(input.id) - input.amount;
            }
        }

        private static void RefundInFlight(GameState state, GameDataAsset data, StationState station)
        {
            if (station.inFlight
                && station.recipeId != null
                && data.RecipesById.TryGetValue(station.recipeId, out var oldRecipe))
            {
                foreach (var input in oldRecipe.inputs)
                {
                    state.AddResource(input.id, input.amount);
                }
            }

            station.inFlight = false;
            station.progressSeconds = 0.0;
        }
    }
}
