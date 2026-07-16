using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Skill XP and levels (design §4): 1–99, XP from every action, levels
    /// gate recipes (tool-tier gating arrives with the tools system). The XP
    /// to advance from level L is economy.xp.baseXp · growth^(L−1); the total
    /// is capped at maxLevel so the counter can't run away once a skill tops
    /// out. Everything no-ops when economy.xp is absent (hand-built test
    /// data): XP never accrues, levels read as 1, and level gates stay open.
    /// </summary>
    public static class Skills
    {
        // FP guard on level thresholds: 210 stored XP must clear the
        // closed-form 210.00000000000003 rung, not sit one ulp under it.
        private const double LevelEpsilon = 1e-9;

        /// <summary>Total XP earned in a skill this run (0 for a skill never worked).</summary>
        public static double Xp(GameState state, string skill)
        {
            return skill != null && state.skillXp.TryGetValue(skill, out var xp) ? xp : 0.0;
        }

        /// <summary>The skill's current level, derived from its XP — never stored, so a balance retune re-levels every save.</summary>
        public static int Level(GameState state, GameDataAsset data, string skill)
        {
            var xp = data.economy?.xp;
            if (xp == null)
            {
                return 1;
            }

            // Compare against closed-form totals rather than subtracting rungs
            // one by one — sequential subtraction drifts, and the AddXp clamp
            // stores exactly TotalXpForLevel(maxLevel), which must read back as
            // exactly the max level.
            var total = Xp(state, skill);
            var level = 1;
            while (level < xp.maxLevel)
            {
                var threshold = TotalXpForLevel(xp, level + 1);
                if (total < threshold - threshold * 1e-12 - LevelEpsilon)
                {
                    break;
                }

                level++;
            }

            return level;
        }

        /// <summary>XP needed to advance from <paramref name="level"/> to the next.</summary>
        public static double XpToNext(EconomyData.XpData xp, int level)
        {
            return xp.baseXp * System.Math.Pow(xp.growth, level - 1);
        }

        /// <summary>Cumulative XP at which <paramref name="level"/> is reached (level 1 = 0).</summary>
        public static double TotalXpForLevel(EconomyData.XpData xp, int level)
        {
            // Geometric series: baseXp · (growth^(L−1) − 1) / (growth − 1).
            return xp.baseXp * (System.Math.Pow(xp.growth, level - 1) - 1.0) / (xp.growth - 1.0);
        }

        /// <summary>Fraction of the way from the current level to the next (0 at a fresh level, 1-ε just before; 0 once capped).</summary>
        public static double ProgressToNext(GameState state, GameDataAsset data, string skill)
        {
            var xp = data.economy?.xp;
            if (xp == null)
            {
                return 0.0;
            }

            var level = Level(state, data, skill);
            if (level >= xp.maxLevel)
            {
                return 0.0;
            }

            // Clamped: the epsilon in Level means "into" can sit a whisker
            // outside [0, rung) right at a boundary.
            var into = Xp(state, skill) - TotalXpForLevel(xp, level);
            var fraction = into / XpToNext(xp, level);
            return System.Math.Min(1.0, System.Math.Max(0.0, fraction));
        }

        /// <summary>Gathering XP: units gathered × xp.gatherPerUnit to the node's skill.</summary>
        public static void AddGatherXp(GameState state, GameDataAsset data, string skill, BigDouble units)
        {
            var xp = data.economy?.xp;
            if (xp != null)
            {
                AddXp(state, data, skill, units * xp.gatherPerUnit);
            }
        }

        /// <summary>Crafting XP: xp.craftPerBatch to the recipe's skill per completed batch.</summary>
        public static void AddCraftXp(GameState state, GameDataAsset data, string skill)
        {
            var xp = data.economy?.xp;
            if (xp != null)
            {
                AddXp(state, data, skill, new BigDouble(xp.craftPerBatch));
            }
        }

        /// <summary>
        /// Credit XP to a skill, clamped at the max level's total. The clamp is
        /// why the amount arrives as BigDouble but is stored as double: late-run
        /// gather rates outgrow double range, and headroom-to-cap always fits.
        /// </summary>
        public static void AddXp(GameState state, GameDataAsset data, string skill, BigDouble amount)
        {
            var xp = data.economy?.xp;
            if (xp == null || skill == null || amount <= BigDouble.Zero)
            {
                return;
            }

            var current = Xp(state, skill);
            var headroom = TotalXpForLevel(xp, xp.maxLevel) - current;
            if (headroom <= 0.0)
            {
                return;
            }

            var gained = amount >= new BigDouble(headroom) ? headroom : amount.ToDouble();
            state.skillXp[skill] = current + gained;
        }
    }
}
