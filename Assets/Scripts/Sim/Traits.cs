using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Applies a species' single fixed trait (design §4) where its familiar is
    /// stationed. Kinds: nodeYieldBonus (a node of the trait's resource),
    /// choiceBonus (points at its node), digSpeedBonus (watching the site it
    /// keeps), bubbleRewardBonus (windfall bubbles pay more while it
    /// walks), wardenYieldBonus (the warden's own hands work faster while it
    /// walks). Resting familiars contribute nothing, and everything no-ops
    /// when species data is absent (fixtures).
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
        /// Summed windfall-bubble bonus from the walking kith: any non-resting
        /// familiar with a (Kinship-deepened) bubbleRewardBonus trait fattens
        /// every caught bubble — the raven fetches the windfalls home, wherever
        /// it happens to be posted.
        /// </summary>
        public static double BubbleRewardBonus(GameState state, GameDataAsset data)
        {
            return WalkingBonus(state, data, "bubbleRewardBonus");
        }

        /// <summary>
        /// Summed warden-yield bonus from the walking kith: a non-resting
        /// familiar with a (Kinship-deepened) wardenYieldBonus trait quickens
        /// the warden's own hands — the fell pony carries what the warden
        /// picks, so their hands never leave the work (§11).
        /// </summary>
        public static double WardenYieldBonus(GameState state, GameDataAsset data)
        {
            return WalkingBonus(state, data, "wardenYieldBonus");
        }

        private static double WalkingBonus(GameState state, GameDataAsset data, string kind)
        {
            if (state == null)
            {
                return 0.0;
            }

            var bonus = 0.0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting)
                {
                    continue;
                }

                var trait = Of(data, familiar);
                if (trait != null && trait.kind == kind)
                {
                    bonus += trait.value * DeepeningFactor(data, familiar);
                }
            }

            return bonus;
        }

        /// <summary>Watch-speed factor a familiar at an observation site contributes: 1, plus a (Kinship-deepened) digSpeedBonus trait.</summary>
        public static double DigSpeedFactor(Familiar familiar, GameDataAsset data)
        {
            var trait = Of(data, familiar);
            return trait != null && trait.kind == "digSpeedBonus"
                ? 1.0 + trait.value * DeepeningFactor(data, familiar)
                : 1.0;
        }

        /// <summary>Summed Choice-chance points from the soft-pawed familiars assigned to <paramref name="node"/>.</summary>
        public static double ChoiceBonusAt(GameState state, GameDataAsset data, NodeState node)
        {
            var bonus = 0.0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting || familiar.stationId != node.id)
                {
                    continue;
                }

                var trait = Of(data, familiar);
                if (trait != null && trait.kind == "choiceBonus")
                {
                    bonus += trait.value * DeepeningFactor(data, familiar);
                }
            }

            return bonus;
        }
    }
}
