using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Per-resource mastery (design §4) — the long-tail chase. Each node's
    /// mastery XP accrues from every unit gathered there; levels are 0-based
    /// bonus levels (a fresh node has none) derived from the XP via the shared
    /// curve, each adding economy.mastery.yieldBonusPerLevel to the node's
    /// yield and to the raw resource's Provisioner value. Inert when the curve
    /// is unconfigured (base ≤ 0 — hand-built test data): no XP accrues and
    /// levels read 0, the pre-mastery behaviour.
    /// </summary>
    public static class Mastery
    {
        /// <summary>True when the mastery curve is authored — the whole system's on-switch.</summary>
        public static bool Configured(EconomyData economy)
        {
            return economy?.mastery != null && economy.mastery.baseXp > 0 && economy.mastery.growth > 1;
        }

        /// <summary>The node's mastery level, derived from its XP — never stored, so a curve retune re-levels every save.</summary>
        public static int Level(NodeState node, EconomyData economy)
        {
            if (node == null || !Configured(economy))
            {
                return 0;
            }

            var mastery = economy.mastery;
            return XpCurve.RungsForXp(mastery.baseXp, mastery.growth, mastery.maxLevel, node.masteryXp);
        }

        /// <summary>Fraction of the way to the node's next mastery level (0 once capped).</summary>
        public static double ProgressToNext(NodeState node, EconomyData economy)
        {
            if (node == null || !Configured(economy))
            {
                return 0.0;
            }

            var mastery = economy.mastery;
            return XpCurve.ProgressToNextRung(mastery.baseXp, mastery.growth, mastery.maxLevel, node.masteryXp);
        }

        /// <summary>
        /// Credit gathering mastery: units × xp.xpPerUnit onto the node,
        /// clamped at the max level's total (the clamp is why the amount
        /// arrives as BigDouble but is stored as double).
        /// </summary>
        public static void AddGatherXp(NodeState node, EconomyData economy, BigDouble units)
        {
            if (node == null || !Configured(economy) || units <= BigDouble.Zero)
            {
                return;
            }

            var mastery = economy.mastery;
            var headroom = XpCurve.TotalForRungs(mastery.baseXp, mastery.growth, mastery.maxLevel) - node.masteryXp;
            if (headroom <= 0.0)
            {
                return;
            }

            var amount = units * mastery.xpPerUnit;
            node.masteryXp += amount >= new BigDouble(headroom) ? headroom : amount.ToDouble();
        }
    }
}
