using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Folio (design §6): the journal's back pages. Fixing a Pristine
    /// specimen of each entry in a spread completes it; a completed spread grants
    /// a permanent effect that survives Migration — the third fork of the
    /// Pristine choice (trade the windfall at the Exchange, offer it to the Rite,
    /// or fix it here for permanence; the specimen is consumed by the page). The
    /// Curator's Cabinet upgrade scales the granted values while owned.
    /// </summary>
    public static class Folio
    {
        /// <summary>True when a Pristine specimen of this resource has ever been fixed into the Folio (fixing is forever).</summary>
        public static bool IsFixed(GameState state, string resourceId)
        {
            return state.fixedResources.Contains(resourceId);
        }

        /// <summary>
        /// True when fixing this resource would be accepted: a spread wants it,
        /// it hasn't been fixed, and a Pristine specimen is held.
        /// </summary>
        public static bool CanFix(GameState state, GameDataAsset data, string resourceId)
        {
            if (state == null || data == null || resourceId == null
                || IsFixed(state, resourceId) || state.GetPristine(resourceId) < BigDouble.One)
            {
                return false;
            }

            foreach (var spread in data.folioSpreads)
            {
                if (spread.entries.Contains(resourceId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fix one Pristine specimen into the Folio: consumed forever, the entry
        /// filled for every spread that lists it, and any spread completed by it
        /// grants its permanent effects at once. Returns false (and changes
        /// nothing) when <see cref="CanFix"/> says no.
        /// </summary>
        public static bool TryFix(GameState state, GameDataAsset data, string resourceId)
        {
            if (!CanFix(state, data, resourceId))
            {
                return false;
            }

            state.pristineResources[resourceId] = state.GetPristine(resourceId) - BigDouble.One;
            state.fixedResources.Add(resourceId);
            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }

        public static bool IsSpreadComplete(GameState state, FolioSpreadData spread)
        {
            foreach (var entry in spread.entries)
            {
                if (!state.fixedResources.Contains(entry))
                {
                    return false;
                }
            }

            return spread.entries.Count > 0;
        }

        public static int FixedEntryCount(GameState state, FolioSpreadData spread)
        {
            var fixedCount = 0;
            foreach (var entry in spread.entries)
            {
                if (state.fixedResources.Contains(entry))
                {
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        /// <summary>
        /// The effects of every completed spread, scaled by any owned
        /// folioSpreadBonusMult (the Curator's Cabinet ×1.5) — folded into
        /// Upgrades.ActiveEffects.
        /// </summary>
        public static IEnumerable<EffectData> CompletedSpreadEffects(GameState state, GameDataAsset data)
        {
            var mult = SpreadBonusMultiplier(state, data);
            foreach (var spread in data.folioSpreads)
            {
                if (!IsSpreadComplete(state, spread))
                {
                    continue;
                }

                foreach (var effect in spread.effects)
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

        /// <summary>Owned folioSpreadBonusMult effects multiplied together (1 with none owned).</summary>
        public static double SpreadBonusMultiplier(GameState state, GameDataAsset data)
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
                    if (effect.type == EffectType.FolioSpreadBonusMult)
                    {
                        mult *= effect.value;
                    }
                }
            }

            return mult;
        }
    }
}
