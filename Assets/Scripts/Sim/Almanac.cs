using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Almanac — the permanent tree bought with Verdure (design §7).
    /// Ownership survives Migration. Verdure is never destroyed: a node
    /// ALLOCATES from the banked total (available = verdurePoints − the sum
    /// of owned node costs), so the +2%/pt passive keeps counting the full
    /// total and Migration's recompute-from-lifetime-Renown stays honest.
    /// </summary>
    public static class Almanac
    {
        /// <summary>Levels held on a repeatable line; zero for a line never bought, and for every one-off node.</summary>
        public static int Levels(GameState state, string nodeId)
        {
            return state != null && state.almanacLevels.TryGetValue(nodeId, out var levels) ? levels : 0;
        }

        /// <summary>
        /// The geometric step on a repeatable line. Fixtures without economy
        /// tuning price every level flat (the Configured pattern) rather than
        /// collapsing the cost to zero.
        /// </summary>
        private static double CostGrowth(GameDataAsset data)
        {
            var growth = data?.economy?.costGrowth != null ? data.economy.costGrowth.almanac : 0.0;
            return growth > 0.0 ? growth : 1.0;
        }

        /// <summary>
        /// What the next level of this line costs: the flat cost for a one-off
        /// node, costVerdure · costGrowth.almanac^levels for a repeatable one.
        /// </summary>
        public static double NextCost(GameState state, GameDataAsset data, AlmanacNodeData node)
        {
            if (node == null)
            {
                return 0.0;
            }

            return node.repeatable
                ? node.costVerdure * System.Math.Pow(CostGrowth(data), Levels(state, node.id))
                : node.costVerdure;
        }

        /// <summary>Verdure allocated to owned nodes — flat for one-offs, the geometric series so far for a repeatable line.</summary>
        public static double SpentVerdure(GameState state, GameDataAsset data)
        {
            var spent = 0.0;
            foreach (var nodeId in state.almanacNodeIds)
            {
                // An id this data version doesn't know is skipped, same
                // policy as purchased upgrades.
                if (data.AlmanacById.TryGetValue(nodeId, out var node))
                {
                    spent += node.costVerdure;
                }
            }

            foreach (var pair in state.almanacLevels)
            {
                if (pair.Value <= 0 || !data.AlmanacById.TryGetValue(pair.Key, out var node) || !node.repeatable)
                {
                    continue;
                }

                // Σ cost·g^i for i < levels. The closed form drifts at g == 1
                // (zero denominator), which is exactly the untuned-fixture case.
                var growth = CostGrowth(data);
                spent += growth == 1.0
                    ? node.costVerdure * pair.Value
                    : node.costVerdure * (System.Math.Pow(growth, pair.Value) - 1.0) / (growth - 1.0);
            }

            return spent;
        }

        /// <summary>Verdure not yet allocated to a node — what the next purchase can draw on.</summary>
        public static double AvailableVerdure(GameState state, GameDataAsset data)
        {
            return state.verdurePoints - SpentVerdure(state, data);
        }

        /// <summary>
        /// Learned at all. A repeatable line counts as owned once it holds a
        /// level, so it can satisfy another node's prerequisite — but it is
        /// never "finished", so <see cref="CanBuy"/> doesn't consult this.
        /// </summary>
        public static bool IsOwned(GameState state, AlmanacNodeData node)
        {
            return node != null && (state.almanacNodeIds.Contains(node.id) || Levels(state, node.id) > 0);
        }

        /// <summary>
        /// True when the node's single prerequisite is owned (or it has none).
        /// An id this data version doesn't know is treated as met, same policy
        /// as <see cref="SpentVerdure"/> — the validator catches dangling
        /// requires at build time, so a live tree is never stranded by one.
        /// Also what decides whether a line is shown at all: the later tiers
        /// stay off the page until the tier below them is learned.
        /// </summary>
        public static bool PrerequisiteMet(GameState state, GameDataAsset data, AlmanacNodeData node)
        {
            if (state == null || data == null || node == null || string.IsNullOrEmpty(node.requires))
            {
                return true;
            }

            return !data.AlmanacById.TryGetValue(node.requires, out var prerequisite) || IsOwned(state, prerequisite);
        }

        /// <summary>True when the node can be bought: not already learned (a repeatable line always can), prerequisite owned, and unallocated Verdure covers the next cost.</summary>
        public static bool CanBuy(GameState state, GameDataAsset data, AlmanacNodeData node)
        {
            if (state == null || data == null || node == null)
            {
                return false;
            }

            if (!node.repeatable && IsOwned(state, node))
            {
                return false;
            }

            if (!PrerequisiteMet(state, data, node))
            {
                return false;
            }

            return AvailableVerdure(state, data) >= NextCost(state, data, node);
        }

        /// <summary>
        /// Buy the node: records ownership (allocating its Verdure) — or takes
        /// the next level of a repeatable line — and recomputes the yield
        /// multipliers its effects may feed. Returns false (and changes
        /// nothing) when <see cref="CanBuy"/> says no.
        /// </summary>
        public static bool TryBuy(GameState state, GameDataAsset data, AlmanacNodeData node)
        {
            if (!CanBuy(state, data, node))
            {
                return false;
            }

            if (node.repeatable)
            {
                state.almanacLevels[node.id] = Levels(state, node.id) + 1;
            }
            else
            {
                state.almanacNodeIds.Add(node.id);

                // A grantUpgrade node pays out now as well as at every fold —
                // "from the first morning" must not mean "from NEXT fold" to
                // the run that just paid the Verdure.
                SyncGrantedUpgrades(state, data);
            }

            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }

        /// <summary>
        /// Ensure every rung an owned node's grantUpgrade effect names is on
        /// the run — free, no materials or skill gate: the grant IS the head
        /// start (design §8's starting tool tiers and zone skips). The fold
        /// gate and the tool requirement still hold, so a granted trail whose
        /// fold hasn't come simply waits for the run that earns it — this is
        /// re-derived at every fold, on buy, and on restore, all idempotent.
        /// Sweeps to a fixpoint so a granted tool rung can satisfy a granted
        /// map rung whatever order the ladder lists them in.
        /// </summary>
        public static void SyncGrantedUpgrades(GameState state, GameDataAsset data)
        {
            if (state == null || data == null)
            {
                return;
            }

            var granted = new HashSet<string>();
            foreach (var nodeId in state.almanacNodeIds)
            {
                if (!data.AlmanacById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                foreach (var effect in node.effects)
                {
                    if (effect.type == EffectType.GrantUpgrade && !string.IsNullOrEmpty(effect.upgrade))
                    {
                        granted.Add(effect.upgrade);
                    }
                }
            }

            if (granted.Count == 0)
            {
                return;
            }

            var applied = false;
            bool progressed;
            do
            {
                progressed = false;
                foreach (var upgrade in data.upgrades)
                {
                    if (!granted.Contains(upgrade.id) || state.HasUpgrade(upgrade.id)
                        || !Upgrades.MeetsFoldGate(state, data, upgrade)
                        || !Upgrades.MeetsToolRequirement(state, data, upgrade))
                    {
                        continue;
                    }

                    state.purchasedUpgradeIds.Add(upgrade.id);
                    applied = true;
                    progressed = true;
                }
            }
            while (progressed);

            if (applied)
            {
                // The same wake-up a purchase does: a granted trail map opens
                // its zone (and dig site), the multipliers fold the new
                // effects in, and a newly revealed verse credits deeds done.
                Rite.Settle(state, data);
            }
        }
    }
}
