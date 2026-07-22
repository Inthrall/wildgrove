using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>A find's quality (design §5): Common sells at base, Fine at a bonus, Pristine is the windfall.</summary>
    public enum QualityTier
    {
        Common,
        Fine,
        Pristine,
    }

    /// <summary>
    /// Design §5 quality rolls: each haul batch (one carrier delivery) rolls
    /// once — never per unit, so idle rates don't shower Pristines and cheapen
    /// the windfall. The whole batch takes the rolled tier: Fine units sell at
    /// fineValueMult, Pristine units are held apart as specimens (sold for a
    /// windfall by an explicit act — and later donated or offered; the choice
    /// is the design's point, so nothing sells them automatically).
    /// </summary>
    public static class Quality
    {
        /// <summary>
        /// The system's on-switch: rolls happen only when the data gives
        /// either tier a chance. Hand-built fixtures without a quality section
        /// stay all-Common.
        /// </summary>
        public static bool Configured(EconomyData economy)
        {
            return economy?.quality != null
                   && (economy.quality.fineChance > 0.0 || economy.quality.pristineBaseChance > 0.0);
        }

        /// <summary>
        /// A batch's Pristine chance from <paramref name="node"/>, per design
        /// §8: (base + owned pristineChanceBonus points) · (1 + tending bonus)
        /// — flat bonuses add points, Tending multiplies while the node's
        /// post-tend window is live.
        /// </summary>
        public static double PristineChance(GameState state, GameDataAsset data, NodeState node)
        {
            var economy = data.economy;
            var chance = economy.quality.pristineBaseChance
                         + Upgrades.PristineChanceBonus(state, data)
                         + Traits.PristineBonusAt(state, data, node);
            if (node.pristineBonusRemaining > 0.0 && economy.tending != null)
            {
                chance *= 1.0 + economy.tending.pristineChanceBonus;
            }

            return chance < 1.0 ? chance : 1.0;
        }

        /// <summary>
        /// Roll one haul batch's quality, advancing the run's rng. Pristine
        /// wins the low end of the draw so a tending-boosted chance can't be
        /// eaten by the Fine band.
        /// </summary>
        public static QualityTier Roll(GameState state, GameDataAsset data, NodeState node)
        {
            if (!Configured(data.economy))
            {
                return QualityTier.Common;
            }

            var pristineChance = PristineChance(state, data, node);
            var roll = Rng.NextDouble(ref state.rngState);
            if (roll < pristineChance)
            {
                return QualityTier.Pristine;
            }

            if (roll < pristineChance + data.economy.quality.fineChance)
            {
                return QualityTier.Fine;
            }

            return QualityTier.Common;
        }
    }
}
