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

        /// <summary>
        /// Standing orders one station id may hold: one, or two once this
        /// run's second queue is bought (design §9's sink slate). Per run —
        /// the second slot lapses at the fold.
        /// </summary>
        public static int OrderCapacity(GameState state)
        {
            return state != null && state.secondQueueBought ? 2 : 1;
        }

        /// <summary>The slot working <paramref name="recipe"/>, or null — searches every slot the station holds, not just the first.</summary>
        public static StationState ActiveStationFor(GameState state, RecipeData recipe)
        {
            foreach (var station in state.stations)
            {
                if (station.stationId == recipe.station && station.recipeId == recipe.id)
                {
                    return station;
                }
            }

            return null;
        }

        /// <summary>
        /// The recipe this station currently holds, or null when it stands
        /// idle — what a row needs to say "this would set aside the jam",
        /// since assigning displaces silently.
        /// </summary>
        public static RecipeData WorkingRecipe(GameState state, GameDataAsset data, string stationId)
        {
            var station = StationFor(state, stationId);
            if (station?.recipeId == null || !data.RecipesById.TryGetValue(station.recipeId, out var recipe))
            {
                return null;
            }

            return recipe;
        }

        /// <summary>
        /// Assign <paramref name="recipe"/> to its station. It takes an idle
        /// slot, or opens one while <see cref="OrderCapacity"/> allows —
        /// otherwise it displaces the station's first order (an in-flight
        /// batch's inputs are refunded — switching is never a punishment).
        /// No-op if it's already assigned to any slot.
        /// </summary>
        public static void Assign(GameState state, GameDataAsset data, RecipeData recipe)
        {
            if (state == null || recipe == null || !IsWorkable(state, data, recipe))
            {
                return;
            }

            if (ActiveStationFor(state, recipe) != null)
            {
                return;
            }

            StationState first = null;
            var slots = 0;
            foreach (var station in state.stations)
            {
                if (station.stationId != recipe.station)
                {
                    continue;
                }

                if (station.recipeId == null)
                {
                    station.recipeId = recipe.id;
                    return;
                }

                first = first ?? station;
                slots++;
            }

            if (slots < OrderCapacity(state))
            {
                state.stations.Add(new StationState { stationId = recipe.station, recipeId = recipe.id });
                return;
            }

            RefundInFlight(state, data, first);
            first.recipeId = recipe.id;
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

        /// <summary>
        /// The order an Assign of <paramref name="recipe"/> would displace
        /// right now, or null when nothing would be (an idle slot or spare
        /// capacity takes it, or it is already assigned). Mirrors
        /// <see cref="Assign"/> exactly — the page warns off this, and a
        /// warning that disagrees with the act is worse than none.
        /// </summary>
        public static RecipeData WouldDisplace(GameState state, GameDataAsset data, RecipeData recipe)
        {
            if (state == null || recipe == null || ActiveStationFor(state, recipe) != null)
            {
                return null;
            }

            StationState first = null;
            var slots = 0;
            foreach (var station in state.stations)
            {
                if (station.stationId != recipe.station)
                {
                    continue;
                }

                if (station.recipeId == null)
                {
                    return null;
                }

                first = first ?? station;
                slots++;
            }

            if (slots < OrderCapacity(state) || first == null)
            {
                return null;
            }

            return data.RecipesById.TryGetValue(first.recipeId, out var displaced) ? displaced : null;
        }

        /// <summary>True when camp stock covers one batch of the recipe's inputs.</summary>
        public static bool HasInputs(GameState state, RecipeData recipe)
        {
            return MissingInput(state, recipe) == null;
        }

        /// <summary>
        /// The first input camp stock can't cover a batch of, or null when it
        /// can — so a halted station can name what it's waiting on instead of
        /// just sitting there at 0%.
        /// </summary>
        public static string MissingInput(GameState state, RecipeData recipe)
        {
            foreach (var input in recipe.inputs)
            {
                if (state.GetResource(input.id) < input.amount)
                {
                    return input.id;
                }
            }

            return null;
        }

        /// <summary>
        /// True when a station holds this recipe but no batch is turning — the
        /// bar is frozen. Stock short of the next batch is the usual cause; a
        /// gate the run has slipped behind (a retune, a restored save) is the
        /// other, and the row names those separately.
        /// </summary>
        public static bool IsStalled(GameState state, RecipeData recipe)
        {
            var station = ActiveStationFor(state, recipe);
            return station != null && !station.inFlight;
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

            for (var slotIndex = 0; slotIndex < state.stations.Count; slotIndex++)
            {
                var station = state.stations[slotIndex];
                if (station.recipeId == null || !data.RecipesById.TryGetValue(station.recipeId, out var recipe))
                {
                    continue;
                }

                // A slot past the station's capacity sits idle rather than
                // crafting — a restored save (or a fold) can hold more orders
                // than the run has bought, and working them would hand out the
                // second queue for free.
                if (SlotRank(state, slotIndex) >= OrderCapacity(state))
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

                    // Duration is live (speed upgrades mid-batch shrink it); never
                    // let it undercut banked progress or the negative step would
                    // refund time into `remaining` and mint free batches.
                    var step = System.Math.Min(remaining, System.Math.Max(0.0, duration - station.progressSeconds));
                    station.progressSeconds += step;
                    remaining -= step;

                    // Epsilon guards float residue: a batch a rounding error
                    // short of done must still complete, or the loop spins.
                    if (station.progressSeconds >= duration - 1e-9)
                    {
                        state.AddResource(recipe.output, BigDouble.One);
                        Skills.AddCraftXp(state, data, recipe.skill);
                        Compendium.RecordCraft(state, recipe.id);
                        Compendium.RecordStationWorked(state, station.stationId);
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

        /// <summary>
        /// One batch's craft time: the recipe's own craftSeconds when it
        /// authors one (a smelt is a slow burn, and the ingots say so),
        /// otherwise the uniform base — divided in both cases by the skill's
        /// craftSpeedMult upgrades and the station line's speed levels.
        /// </summary>
        private static double BatchSeconds(GameState state, GameDataAsset data, RecipeData recipe)
        {
            var authored = recipe.craftSeconds > 0.0
                ? recipe.craftSeconds
                : data.economy.crafting.baseCraftSeconds;
            return authored
                   / Upgrades.CraftSpeedMultiplier(state, data, recipe.skill)
                   / Buildings.StationSpeedMultiplier(state, data, recipe.station)
                   / Wheel.CraftSpeedMult(state, data, recipe.skill);
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

        /// <summary>How many earlier entries share this slot's station id — its position in the station's own order, 0 first.</summary>
        private static int SlotRank(GameState state, int slotIndex)
        {
            var rank = 0;
            for (var earlier = 0; earlier < slotIndex; earlier++)
            {
                if (state.stations[earlier].stationId == state.stations[slotIndex].stationId)
                {
                    rank++;
                }
            }

            return rank;
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
