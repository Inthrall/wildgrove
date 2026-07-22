using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Applies a species' single fixed trait (design §4) where its familiar is
    /// stationed. Kinds: nodeYieldBonus (a node of the trait's resource),
    /// trailThroughputBonus (holding the trail), pristineBonus (points at its
    /// node), digSpeedBonus (at a watch site). Resting familiars contribute
    /// nothing, and everything no-ops when species data is absent (fixtures).
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

        /// <summary>Yield factor a familiar assigned to <paramref name="node"/> contributes: 1, plus its trait when the resource matches.</summary>
        public static double NodeYieldFactor(Familiar familiar, NodeState node, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            if (trait != null && trait.kind == "nodeYieldBonus"
                && (string.IsNullOrEmpty(trait.resource) || trait.resource == node.resourceId))
            {
                return 1.0 + trait.value;
            }

            return 1.0;
        }

        /// <summary>Trail-lane factor a familiar holding the trail post contributes: 1, plus a trailThroughputBonus trait.</summary>
        public static double TrailThroughputFactor(Familiar familiar, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            return trait != null && trait.kind == "trailThroughputBonus" ? 1.0 + trait.value : 1.0;
        }

        /// <summary>Watch-speed factor a familiar at an observation site contributes: 1, plus a digSpeedBonus trait.</summary>
        public static double DigSpeedFactor(Familiar familiar, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            return trait != null && trait.kind == "digSpeedBonus" ? 1.0 + trait.value : 1.0;
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
                    bonus += trait.value;
                }
            }

            return bonus;
        }
    }
}
