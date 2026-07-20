using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Cached snapshot of every effect-derived modifier. The raw derivation
    /// walks the whole active-effect union (purchased upgrades, fossils,
    /// Museum sets — which clones — Almanac nodes, worn gear); doing that per
    /// accessor per tick made a late run crawl. The union only changes when
    /// something is bought, donated, worn, assembled, or restored — all of
    /// which funnel through <see cref="Upgrades.RecomputeYieldMultipliers"/>
    /// (plus building purchases), which bumps
    /// <see cref="GameState.modifierVersion"/>. A count fingerprint backstops
    /// direct list mutation in tests and tools.
    /// </summary>
    public sealed class ModifierSnapshot
    {
        public int version = -1;
        public GameDataAsset data;
        public long fingerprint = -1;

        public double haulCapacityMultiplier = 1.0;
        public double tendingBurstBonus;
        public double pristineChanceBonus;
        public double digSpeedMultiplier = 1.0;
        public double offlineCapRaiseTo;
        public double offlineCapBonusHours;
        public double basketCapacityMultiplier = 1.0;
        public double craftSpeedGlobal = 1.0;
        public readonly Dictionary<string, double> craftSpeedBySkill = new Dictionary<string, double>();
        public readonly Dictionary<string, double> sellValueBonusByResource = new Dictionary<string, double>();
        public readonly HashSet<string> unlockedSkills = new HashSet<string>();
        public readonly HashSet<string> unlockedRecipeIds = new HashSet<string>();
    }

    public static class Modifiers
    {
        /// <summary>The current snapshot, rebuilt only when the effect sources changed.</summary>
        public static ModifierSnapshot Of(GameState state, GameDataAsset data)
        {
            var snapshot = state.modifierSnapshot;
            var fingerprint = Fingerprint(state);
            if (snapshot == null
                || snapshot.version != state.modifierVersion
                || snapshot.fingerprint != fingerprint
                || !ReferenceEquals(snapshot.data, data))
            {
                snapshot = Build(state, data);
                snapshot.version = state.modifierVersion;
                snapshot.fingerprint = fingerprint;
                snapshot.data = data;
                state.modifierSnapshot = snapshot;
            }

            return snapshot;
        }

        /// <summary>
        /// A cheap change detector over the effect-source collections, for
        /// mutations that bypass the bump (hand-built test states). Same-count
        /// replacement (re-crafting into a worn gear slot) is caught by the
        /// explicit bump instead.
        /// </summary>
        private static long Fingerprint(GameState state)
        {
            var buildingLevels = 0L;
            foreach (var pair in state.buildingLevels)
            {
                buildingLevels += pair.Value;
            }

            var fragments = 0L;
            foreach (var pair in state.fossilFragments)
            {
                fragments += pair.Value;
            }

            return state.purchasedUpgradeIds.Count
                   + state.almanacNodeIds.Count * 1000L
                   + state.fixedResources.Count * 1000_000L
                   + state.gearBySlot.Count * 1000_000_000L
                   + buildingLevels * 1000_000_000_000L
                   + fragments * 1000_000_000_000_000L;
        }

        private static ModifierSnapshot Build(GameState state, GameDataAsset data)
        {
            var snapshot = new ModifierSnapshot();

            var haulMult = 1.0;
            var carrierBonus = 0.0;
            foreach (var effect in Upgrades.ActiveEffects(state, data))
            {
                switch (effect.type)
                {
                    case EffectType.HaulMult:
                        haulMult *= effect.value;
                        break;
                    case EffectType.CarrierCapacityBonus:
                        carrierBonus += effect.value;
                        break;
                    case EffectType.TendingBurstBonus:
                        snapshot.tendingBurstBonus += effect.value;
                        break;
                    case EffectType.PristineChanceBonus:
                        snapshot.pristineChanceBonus += effect.value;
                        break;
                    case EffectType.DigSpeedMult:
                        snapshot.digSpeedMultiplier *= effect.value;
                        break;
                    case EffectType.OfflineCapHours:
                        snapshot.offlineCapRaiseTo = System.Math.Max(snapshot.offlineCapRaiseTo, effect.value);
                        break;
                    case EffectType.OfflineCapBonusHours:
                        snapshot.offlineCapBonusHours += effect.value;
                        break;
                    case EffectType.CraftSpeedMult:
                        if (string.IsNullOrEmpty(effect.skill))
                        {
                            snapshot.craftSpeedGlobal *= effect.value;
                        }
                        else
                        {
                            snapshot.craftSpeedBySkill.TryGetValue(effect.skill, out var current);
                            snapshot.craftSpeedBySkill[effect.skill] = (current == 0.0 ? 1.0 : current) * effect.value;
                        }

                        break;
                }
            }

            snapshot.haulCapacityMultiplier = haulMult * (1.0 + carrierBonus);

            // Sell-value bonuses are purchased-only (a design decision — set
            // and fossil bonuses never inflate the Provisioner).
            foreach (var effect in Upgrades.PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.SellValueBonus && !string.IsNullOrEmpty(effect.resource))
                {
                    snapshot.sellValueBonusByResource.TryGetValue(effect.resource, out var current);
                    snapshot.sellValueBonusByResource[effect.resource] = current + effect.value;
                }
            }

            Upgrades.BuildUnlockedSkills(state, data, snapshot.unlockedSkills);
            Upgrades.BuildUnlockedRecipeIds(state, data, snapshot.unlockedRecipeIds);
            snapshot.basketCapacityMultiplier = Buildings.ComputeBasketCapacityMultiplier(state, data);

            // Bonded familiars are ordinary roster members now (materialised by
            // Roster.SyncBonded) — they gather and haul through Stationing like
            // any other, so there's no separate bonded accumulator here.

            return snapshot;
        }
    }
}
