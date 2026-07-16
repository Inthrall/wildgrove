using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Museum (design §5): donate a Pristine specimen of each resource in
    /// a set to complete it; a completed set grants a permanent effect that
    /// survives Migration — the third fork of the Pristine choice (sell the
    /// windfall, offer to the Rite, or bank permanence here). The Curator's
    /// Cabinet upgrade scales the granted values while owned.
    /// </summary>
    public static class Museum
    {
        /// <summary>True when a Pristine specimen of this resource has ever been donated (donations are forever).</summary>
        public static bool IsDonated(GameState state, string resourceId)
        {
            return state.donatedResources.Contains(resourceId);
        }

        /// <summary>
        /// True when a donation of this resource would be accepted: a set
        /// wants it, it hasn't been donated, and a Pristine specimen is held.
        /// </summary>
        public static bool CanDonate(GameState state, GameDataAsset data, string resourceId)
        {
            if (state == null || data == null || resourceId == null
                || IsDonated(state, resourceId) || state.GetPristine(resourceId) < BigDouble.One)
            {
                return false;
            }

            foreach (var set in data.museumSets)
            {
                if (set.entries.Contains(resourceId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Donate one Pristine specimen: consumed forever, the entry filled
        /// for every set that lists it, and any set completed by it grants
        /// its permanent effects at once. Returns false (and changes nothing)
        /// when <see cref="CanDonate"/> says no.
        /// </summary>
        public static bool TryDonate(GameState state, GameDataAsset data, string resourceId)
        {
            if (!CanDonate(state, data, resourceId))
            {
                return false;
            }

            state.pristineResources[resourceId] = state.GetPristine(resourceId) - BigDouble.One;
            state.donatedResources.Add(resourceId);
            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }

        public static bool IsSetComplete(GameState state, MuseumSetData set)
        {
            foreach (var entry in set.entries)
            {
                if (!state.donatedResources.Contains(entry))
                {
                    return false;
                }
            }

            return set.entries.Count > 0;
        }

        public static int DonatedEntryCount(GameState state, MuseumSetData set)
        {
            var donated = 0;
            foreach (var entry in set.entries)
            {
                if (state.donatedResources.Contains(entry))
                {
                    donated++;
                }
            }

            return donated;
        }

        /// <summary>
        /// The effects of every completed set, scaled by any owned
        /// museumSetBonusMult (the Curator's Cabinet ×1.5) — folded into
        /// Upgrades.ActiveEffects.
        /// </summary>
        public static IEnumerable<EffectData> CompletedSetEffects(GameState state, GameDataAsset data)
        {
            var mult = SetBonusMultiplier(state, data);
            foreach (var set in data.museumSets)
            {
                if (!IsSetComplete(state, set))
                {
                    continue;
                }

                foreach (var effect in set.effects)
                {
                    if (mult == 1.0)
                    {
                        yield return effect;
                    }
                    else
                    {
                        // Scale a copy — the shared data asset must never be
                        // mutated by a run's upgrades.
                        yield return new EffectData
                        {
                            type = effect.type,
                            skill = effect.skill,
                            zone = effect.zone,
                            resource = effect.resource,
                            recipe = effect.recipe,
                            value = effect.value * mult,
                        };
                    }
                }
            }
        }

        /// <summary>Owned museumSetBonusMult effects multiplied together (1 with none owned).</summary>
        public static double SetBonusMultiplier(GameState state, GameDataAsset data)
        {
            var mult = 1.0;
            foreach (var upgradeId in state.purchasedUpgradeIds)
            {
                if (!data.UpgradesById.TryGetValue(upgradeId, out var upgrade))
                {
                    continue;
                }

                foreach (var effect in upgrade.effects)
                {
                    if (effect.type == EffectType.MuseumSetBonusMult)
                    {
                        mult *= effect.value;
                    }
                }
            }

            return mult;
        }
    }
}
