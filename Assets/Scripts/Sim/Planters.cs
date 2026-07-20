using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Planters (design §3): built structures attached to a single gather node or
    /// dig site, paid in another zone's goods (the backward flow — a later zone's
    /// timber has a job improving an early node). Bushcraft-gated: the Carving
    /// Bench opens the skill and with it the planter recipes. Each planter type
    /// gives one effect at its target — basketCapacityMult (a bigger basket),
    /// nodeYieldMult (a second yield lane), or digSpeedMult (steady a dig site's
    /// sketching). One planter of each type per target; planters reset at
    /// Migration. Costs are flat (a planter is a one-off, not a levelled line).
    /// No-ops when nothing is built (fixtures).
    /// </summary>
    public static class Planters
    {
        /// <summary>The skill whose unlock opens planter recipes — the Carving Bench grants it.</summary>
        public const string GateSkill = "bushcraft";

        /// <summary>True once the run can build planters (the Carving Bench has opened Bushcraft).</summary>
        public static bool Unlocked(GameState state, GameDataAsset data)
        {
            return state != null && data != null && Upgrades.UnlockedSkills(state, data).Contains(GateSkill);
        }

        /// <summary>Yield multiplier from a node's nodeYieldMult planters (the second lane): 1 + Σ value.</summary>
        public static double NodeYieldMultiplier(GameState state, GameDataAsset data, NodeState node)
        {
            return node == null ? 1.0 : MultiplierAt(state, data, node.id, "nodeYieldMult");
        }

        /// <summary>Basket-capacity multiplier from a node's basketCapacityMult planters: 1 + Σ value.</summary>
        public static double BasketCapacityMultiplier(GameState state, GameDataAsset data, NodeState node)
        {
            return node == null ? 1.0 : MultiplierAt(state, data, node.id, "basketCapacityMult");
        }

        /// <summary>Dig-speed multiplier from a dig site's digSpeedMult planters: 1 + Σ value.</summary>
        public static double DigSpeedMultiplier(GameState state, GameDataAsset data, string zoneId)
        {
            return MultiplierAt(state, data, zoneId, "digSpeedMult");
        }

        /// <summary>True when the planter can be built here: unlocked, not already present, and camp stock covers the bundle.</summary>
        public static bool CanBuild(GameState state, GameDataAsset data, PlanterData planter, string targetId)
        {
            if (!Unlocked(state, data) || planter == null || planter.materials.Count == 0 || string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            if (state.HasPlanter(targetId, planter.id))
            {
                return false;
            }

            foreach (var material in planter.materials)
            {
                if (state.GetResource(material.id) < new BigDouble(material.amount))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Build the planter at the target, spending its material bundle from camp
        /// stock. Returns false (and changes nothing) when it can't be built.
        /// </summary>
        public static bool TryBuild(GameState state, GameDataAsset data, PlanterData planter, string targetId)
        {
            if (!CanBuild(state, data, planter, targetId))
            {
                return false;
            }

            foreach (var material in planter.materials)
            {
                state.resources[material.id] = state.GetResource(material.id) - new BigDouble(material.amount);
            }

            state.builtPlanters.Add(new BuiltPlanter { planterId = planter.id, targetId = targetId });
            return true;
        }

        private static double MultiplierAt(GameState state, GameDataAsset data, string targetId, string kind)
        {
            if (state == null || data == null || string.IsNullOrEmpty(targetId))
            {
                return 1.0;
            }

            var mult = 1.0;
            foreach (var built in state.builtPlanters)
            {
                if (built.targetId != targetId)
                {
                    continue;
                }

                if (data.PlantersById.TryGetValue(built.planterId, out var planter) && planter.kind == kind)
                {
                    mult += planter.value;
                }
            }

            return mult;
        }
    }
}
