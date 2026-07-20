using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Applies a familiar's chosen powerups (design §4) where it is stationed.
    /// Kinds: nodeYieldBonus (its node; an optional resource narrows it),
    /// trailThroughputBonus (holding the trail), pristineBonus (points at its
    /// node), digSpeedBonus (at a dig site), offlineBonus (its post's offline
    /// earnings). Wandering familiars get none (§2), and everything no-ops when
    /// species data is absent (fixtures).
    /// </summary>
    public static class Powerups
    {
        /// <summary>The chosen powerups of a familiar, resolved against its species pool.</summary>
        public static IEnumerable<PowerupData> Chosen(GameDataAsset data, Familiar familiar)
        {
            if (familiar?.powerupIds == null || data?.SpeciesById == null
                || !data.SpeciesById.TryGetValue(familiar.speciesId ?? string.Empty, out var species)
                || species.powerups == null)
            {
                yield break;
            }

            foreach (var chosenId in familiar.powerupIds)
            {
                foreach (var powerup in species.powerups)
                {
                    if (powerup.id == chosenId)
                    {
                        yield return powerup;
                        break;
                    }
                }
            }
        }

        /// <summary>Yield factor a familiar assigned to <paramref name="node"/> contributes: 1 plus its matching nodeYieldBonus powerups.</summary>
        public static double NodeYieldFactor(Familiar familiar, NodeState node, GameDataAsset data)
        {
            var factor = 1.0;
            foreach (var powerup in Chosen(data, familiar))
            {
                if (powerup.kind == "nodeYieldBonus"
                    && (string.IsNullOrEmpty(powerup.resource) || powerup.resource == node.resourceId))
                {
                    factor += powerup.value;
                }
            }

            return factor;
        }

        /// <summary>Trail-lane factor a familiar holding the trail post contributes: 1 plus its trailThroughputBonus powerups.</summary>
        public static double TrailThroughputFactor(Familiar familiar, GameDataAsset data)
        {
            var factor = 1.0;
            foreach (var powerup in Chosen(data, familiar))
            {
                if (powerup.kind == "trailThroughputBonus")
                {
                    factor += powerup.value;
                }
            }

            return factor;
        }

        /// <summary>Dig-speed factor a familiar digging at a site contributes: 1 plus its digSpeedBonus powerups.</summary>
        public static double DigSpeedFactor(Familiar familiar, GameDataAsset data)
        {
            var factor = 1.0;
            foreach (var powerup in Chosen(data, familiar))
            {
                if (powerup.kind == "digSpeedBonus")
                {
                    factor += powerup.value;
                }
            }

            return factor;
        }

        /// <summary>Summed Pristine-chance points from the soft-paws powerups of the familiars assigned to <paramref name="node"/>.</summary>
        public static double PristineBonusAt(GameState state, GameDataAsset data, NodeState node)
        {
            var bonus = 0.0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsWandering || familiar.stationId != node.id)
                {
                    continue;
                }

                foreach (var powerup in Chosen(data, familiar))
                {
                    if (powerup.kind == "pristineBonus")
                    {
                        bonus += powerup.value;
                    }
                }
            }

            return bonus;
        }
    }
}
