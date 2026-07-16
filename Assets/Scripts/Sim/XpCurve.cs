namespace Wildgrove.Sim
{
    /// <summary>
    /// The shared geometric XP ladder: rung r (0-based) costs baseXp·growth^r.
    /// Skills (1-based levels, design §4) and per-resource Mastery (0-based
    /// bonus levels) both climb this shape with their own constants. Rung
    /// counts compare against closed-form totals with an epsilon so stored XP
    /// that IS a threshold value reads back as having cleared it — sequential
    /// rung subtraction drifts.
    /// </summary>
    public static class XpCurve
    {
        private const double Epsilon = 1e-9;

        /// <summary>What the (0-based) rung costs to climb.</summary>
        public static double RungCost(double baseXp, double growth, int rung)
        {
            return baseXp * System.Math.Pow(growth, rung);
        }

        /// <summary>Cumulative XP at which <paramref name="rungs"/> rungs are climbed (0 rungs = 0).</summary>
        public static double TotalForRungs(double baseXp, double growth, int rungs)
        {
            // Geometric series: baseXp · (growth^rungs − 1) / (growth − 1).
            return baseXp * (System.Math.Pow(growth, rungs) - 1.0) / (growth - 1.0);
        }

        /// <summary>How many rungs (capped at <paramref name="maxRungs"/>) the XP total has climbed.</summary>
        public static int RungsForXp(double baseXp, double growth, int maxRungs, double xp)
        {
            var rungs = 0;
            while (rungs < maxRungs)
            {
                var threshold = TotalForRungs(baseXp, growth, rungs + 1);
                if (xp < threshold - threshold * 1e-12 - Epsilon)
                {
                    break;
                }

                rungs++;
            }

            return rungs;
        }

        /// <summary>Fraction of the way up the current rung, clamped to [0, 1] (0 once capped).</summary>
        public static double ProgressToNextRung(double baseXp, double growth, int maxRungs, double xp)
        {
            var rungs = RungsForXp(baseXp, growth, maxRungs, xp);
            if (rungs >= maxRungs)
            {
                return 0.0;
            }

            // Clamped: the epsilon in RungsForXp means "into" can sit a
            // whisker outside [0, cost) right at a boundary.
            var into = xp - TotalForRungs(baseXp, growth, rungs);
            var fraction = into / RungCost(baseXp, growth, rungs);
            return System.Math.Min(1.0, System.Math.Max(0.0, fraction));
        }
    }
}
