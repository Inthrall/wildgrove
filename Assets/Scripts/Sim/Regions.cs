using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Region modifiers (design §8): every run after the first arrives in a
    /// flavoured region — lush, misted, ashen — whose effects stay live for
    /// the whole run and scale the Rite generator's demands (the §9
    /// modifierWeight). The draw is deterministic from the migration count
    /// alone, the generator's own idiom: nothing persists, a reload can never
    /// reroll it, and the fold forecast can truthfully name the region ahead.
    /// Run 1 is home ground — unmodified — because the authored tutorial Rite
    /// assumes it. No-ops when no regions are authored (fixtures).
    /// </summary>
    public static class Regions
    {
        // A different odd constant than the Rite generator's seed so the
        // region draw and the rite's goods picks never correlate.
        private const ulong SeedSalt = 0xC2B2AE3D27D4EB4FUL;

        public static bool Configured(GameDataAsset data)
        {
            return data?.regions != null && data.regions.Count > 0;
        }

        /// <summary>The region a given migration count wakes in — null for run 1 (migration 0) and when no regions are authored.</summary>
        public static RegionData ForMigration(GameDataAsset data, int migration)
        {
            if (migration <= 0 || !Configured(data))
            {
                return null;
            }

            var seed = Rng.Sanitise((ulong)migration * SeedSalt);
            var index = (int)(Rng.NextDouble(ref seed) * data.regions.Count);
            if (index >= data.regions.Count)
            {
                index = data.regions.Count - 1;
            }

            return data.regions[index];
        }

        /// <summary>The region this run is living in — null on home ground.</summary>
        public static RegionData Current(GameState state, GameDataAsset data)
        {
            return state == null ? null : ForMigration(data, state.migrationCount);
        }

        /// <summary>The region the NEXT fold would wake in — the §8 forecast's "ahead: a misted region".</summary>
        public static RegionData Next(GameState state, GameDataAsset data)
        {
            return state == null ? null : ForMigration(data, state.migrationCount + 1);
        }

        /// <summary>The current region's effects, joined into the run's active-effect union — empty on home ground.</summary>
        public static IEnumerable<EffectData> ActiveEffects(GameState state, GameDataAsset data)
        {
            var region = Current(state, data);
            if (region?.effects == null)
            {
                yield break;
            }

            foreach (var effect in region.effects)
            {
                yield return effect;
            }
        }

        /// <summary>
        /// The §9 modifierWeight for a generated demand: the region's yieldMult
        /// on that find, 1 when untargeted. The land asks more of what the
        /// season gives freely, and less of what it withholds — the demand
        /// scales exactly as the gather rate does, so a modified region's
        /// verse costs the same time as home ground's.
        /// </summary>
        public static double DemandWeight(RegionData region, string goodsId)
        {
            if (region?.effects == null || string.IsNullOrEmpty(goodsId))
            {
                return 1.0;
            }

            var weight = 1.0;
            foreach (var effect in region.effects)
            {
                if (effect.type == EffectType.YieldMult && effect.resource == goodsId)
                {
                    weight *= effect.value;
                }
            }

            return weight;
        }
    }
}
