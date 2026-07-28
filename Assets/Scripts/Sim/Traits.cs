using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Applies a species' single fixed trait (design §4) where its familiar is
    /// stationed. Kinds: nodeYieldBonus (a node of the trait's resource),
    /// trailThroughputBonus (holding the trail), pristineBonus (points at its
    /// node), digSpeedBonus (watching the sites while wandering). Resting
    /// familiars contribute nothing, and everything no-ops when species data
    /// is absent (fixtures).
    /// </summary>
    public static class Traits
    {
        /// <summary>A familiar's trait, resolved from its species — null when the species (or its trait) is unknown.</summary>
        public static TraitData Of(GameDataAsset data, Familiar familiar)
        {
            if (familiar == null || data?.SpeciesById == null
                || !data.SpeciesById.TryGetValue(familiar.speciesId ?? string.Empty, out var species))
            {
                return null;
            }

            return species.trait;
        }

        /// <summary>
        /// Signature deepening (design §4, the Kinship depth lever): the factor
        /// a familiar's trait value scales by — 1 + signatureDeepening per
        /// milestone its Kinship has passed. 1 when signatures aren't
        /// configured, so every trait maths below is unchanged without them.
        /// </summary>
        public static double DeepeningFactor(GameDataAsset data, Familiar familiar)
        {
            var xp = data?.economy?.familiarXp;
            if (xp == null || xp.signatureDeepening <= 0.0)
            {
                return 1.0;
            }

            return 1.0 + xp.signatureDeepening * Kinship.SignatureMilestonesPassed(familiar, data);
        }

        /// <summary>Yield factor a familiar assigned to <paramref name="node"/> contributes: 1, plus its (Kinship-deepened) trait when the resource matches.</summary>
        public static double NodeYieldFactor(Familiar familiar, NodeState node, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            if (trait != null && trait.kind == "nodeYieldBonus"
                && (trait.resources == null || trait.resources.Count == 0 || trait.CoversResource(node.resourceId)))
            {
                return 1.0 + trait.value * DeepeningFactor(data, familiar);
            }

            return 1.0;
        }

        /// <summary>
        /// Trail-lane factor a familiar holding a haul lane contributes: 1, plus
        /// a (Kinship-deepened) trailThroughputBonus trait.
        ///
        /// A trailCarryFactor trait REPLACES the lane instead of adding to it —
        /// it is the fraction of a carrier's load the animal walks, and it never
        /// deepens. Kinship cannot tame what was never broken to harness (§11:
        /// the fell pony's lane is free and always manned, so its half load is
        /// what keeps two lanes from doubling the trail).
        /// </summary>
        public static double TrailThroughputFactor(Familiar familiar, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            if (trait == null)
            {
                return 1.0;
            }

            if (trait.kind == "trailCarryFactor")
            {
                return trait.value;
            }

            return trait.kind == "trailThroughputBonus"
                ? 1.0 + trait.value * DeepeningFactor(data, familiar)
                : 1.0;
        }

        /// <summary>Watch-speed factor a familiar at an observation site contributes: 1, plus a (Kinship-deepened) digSpeedBonus trait.</summary>
        public static double DigSpeedFactor(Familiar familiar, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            return trait != null && trait.kind == "digSpeedBonus"
                ? 1.0 + trait.value * DeepeningFactor(data, familiar)
                : 1.0;
        }

        /// <summary>Summed Pristine-chance points from the soft-pawed familiars assigned to <paramref name="node"/>.</summary>
        public static double PristineBonusAt(GameState state, GameDataAsset data, NodeState node)
        {
            var bonus = 0.0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting || familiar.stationId != node.id)
                {
                    continue;
                }

                var trait = Of(data, familiar);
                if (trait != null && trait.kind == "pristineBonus")
                {
                    bonus += trait.value * DeepeningFactor(data, familiar);
                }
            }

            return bonus;
        }
    }
}
