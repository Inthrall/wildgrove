using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// A find's grade (design §5): Poor sells at base, Decent at a bonus,
    /// Choice is the windfall. The ladder is deliberately open at the top —
    /// grades above Choice are unwritten, not absent.
    /// </summary>
    public enum QualityTier
    {
        Poor,
        Decent,
        Choice,
    }

    /// <summary>
    /// Design §5 quality rolls: each haul batch (one carrier delivery) rolls
    /// once — never per unit, so idle rates don't shower Choice finds and
    /// cheapen the windfall. The whole batch takes the rolled grade: Decent
    /// units sell at decentValueMult, Choice units are held apart as specimens
    /// (sold for a windfall by an explicit act — and later donated or offered;
    /// the decision is the design's point, so nothing sells them automatically).
    /// </summary>
    public static class Quality
    {
        /// <summary>
        /// The system's on-switch: rolls happen only when the data gives
        /// either grade a chance. Hand-built fixtures without a quality section
        /// stay all-Poor.
        /// </summary>
        public static bool Configured(EconomyData economy)
        {
            return economy?.quality != null
                   && (economy.quality.decentChance > 0.0 || economy.quality.choiceBaseChance > 0.0);
        }

        /// <summary>
        /// A batch's Choice chance from <paramref name="node"/>, per design
        /// §8: (base + owned choiceChanceBonus points) · (1 + tending bonus)
        /// — flat bonuses add points, Tending multiplies while the node's
        /// post-tend window is live.
        /// </summary>
        public static double ChoiceChance(GameState state, GameDataAsset data, NodeState node)
        {
            var economy = data.economy;
            var chance = economy.quality.choiceBaseChance
                         + Upgrades.ChoiceChanceBonus(state, data)
                         + Traits.ChoiceBonusAt(state, data, node);
            if (node.choiceBonusRemaining > 0.0 && economy.tending != null)
            {
                chance *= 1.0 + economy.tending.choiceChanceBonus;
            }

            return chance < 1.0 ? chance : 1.0;
        }

        /// <summary>
        /// Roll one haul batch's quality, advancing the run's rng. Choice
        /// wins the low end of the draw so a tending-boosted chance can't be
        /// eaten by the Decent band.
        /// </summary>
        public static QualityTier Roll(GameState state, GameDataAsset data, NodeState node)
        {
            if (!Configured(data.economy))
            {
                return QualityTier.Poor;
            }

            var choiceChance = ChoiceChance(state, data, node);
            var roll = Rng.NextDouble(ref state.rngState);
            if (roll < choiceChance)
            {
                return QualityTier.Choice;
            }

            if (roll < choiceChance + data.economy.quality.decentChance)
            {
                return QualityTier.Decent;
            }

            return QualityTier.Poor;
        }
    }
}
