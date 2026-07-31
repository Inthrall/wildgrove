using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Per-familiar run mechanics (design §4): the run-track level derived from
    /// XP, and crediting familiar XP into Renown (the money→XP spine, §9).
    /// Levels never scale output — they pace XP and Kinship only; a familiar's
    /// abilities are its species' fixed trait (<see cref="Traits"/>). Kinship
    /// (the permanent track) lives in <see cref="Kinship"/>. No-ops when
    /// economy.familiarXp is absent (fixtures).
    /// </summary>
    public static class Familiars
    {
        /// <summary>A familiar's current run level, derived from its xp via the §4 curve (60·1.12^L). 1 when unconfigured.</summary>
        public static int Level(Familiar familiar, GameDataAsset data)
        {
            var xp = data.economy?.familiarXp;
            if (xp == null || familiar == null)
            {
                return 1;
            }

            return 1 + XpCurve.RungsForXp(xp.baseXp, xp.growth, xp.maxLevel - 1, familiar.xp);
        }

        /// <summary>Fraction of the way to the next level (0 once capped).</summary>
        public static double ProgressToNextLevel(Familiar familiar, GameDataAsset data)
        {
            var xp = data.economy?.familiarXp;
            if (xp == null || familiar == null)
            {
                return 0.0;
            }

            return XpCurve.ProgressToNextRung(xp.baseXp, xp.growth, xp.maxLevel - 1, familiar.xp);
        }

        /// <summary>
        /// XP a familiar earns per second at its post: the authored base rate,
        /// the roosts' comfort (§4) and its own Kinship rate perk, folded
        /// together. Zero while it rests — no post, no work, no lesson. The
        /// caller passes comfort in because the sim settles it once a tick
        /// (it cannot change mid-loop); <see cref="XpPerSecond(GameState,
        /// GameDataAsset, Familiar)"/> is the reading overload.
        /// </summary>
        public static double XpPerSecond(GameState state, GameDataAsset data, Familiar familiar, double perSecond, double comfortMultiplier)
        {
            var xp = data?.economy?.familiarXp;
            if (xp == null || familiar == null || familiar.IsResting || perSecond <= 0.0)
            {
                return 0.0;
            }

            return perSecond * comfortMultiplier * Kinship.XpRateMultiplier(familiar, xp.kinshipXpRatePerLevel);
        }

        /// <summary>
        /// The rate the journal states — the same arithmetic the sim credits,
        /// reading the base rate and the comfort multiplier off the data rather
        /// than being handed them. Kept as one formula so a page can never
        /// quote a rate the sim doesn't pay.
        /// </summary>
        public static double XpPerSecond(GameState state, GameDataAsset data, Familiar familiar)
        {
            var xp = data?.economy?.familiarXp;
            return xp == null
                ? 0.0
                : XpPerSecond(state, data, familiar, xp.xpPerSecond, Buildings.ComfortXpMultiplier(state, data));
        }

        /// <summary>
        /// Credit run XP to a familiar at its post (design §4), clamped at the
        /// max level's total, and mirror the gain into Renown (§9 — money becomes
        /// XP). A resting familiar earns nothing — no post, no work, no lesson.
        /// Roosts comfort (<paramref name="comfortMultiplier"/>, §4) scales the
        /// stationed gain. Applied to every roster familiar each tick;
        /// <paramref name="perSecond"/> is economy.familiarXp.xpPerSecond.
        /// </summary>
        public static void AddPostXp(GameState state, GameDataAsset data, Familiar familiar, double perSecond, double seconds, double comfortMultiplier = 1.0)
        {
            var xp = data.economy?.familiarXp;
            if (xp == null || familiar == null || familiar.IsResting || perSecond <= 0.0 || seconds <= 0.0)
            {
                return;
            }

            var amount = seconds * XpPerSecond(state, data, familiar, perSecond, comfortMultiplier);
            if (amount <= 0.0)
            {
                return;
            }

            var cap = XpCurve.TotalForRungs(xp.baseXp, xp.growth, xp.maxLevel - 1);
            var headroom = cap - familiar.xp;
            if (headroom <= 0.0)
            {
                return;
            }

            var gained = System.Math.Min(amount, headroom);
            familiar.xp += gained;
            state.renown += new BigDouble(gained);
        }
    }
}
