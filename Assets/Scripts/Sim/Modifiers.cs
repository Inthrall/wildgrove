using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Cached snapshot of every effect-derived modifier. The raw derivation
    /// walks the whole active-effect union (purchased upgrades, insects,
    /// Museum sets — which clones — Almanac nodes, worn gear); doing that per
    /// accessor per tick made a late run crawl. The union only changes when
    /// something is bought, donated, worn, assembled, or restored — all of
    /// which funnel through <see cref="Upgrades.RecomputeYieldMultipliers"/>
    /// (plus building purchases), which bumps
    /// <see cref="GameState.modifierVersion"/>. A count fingerprint backstops
    /// direct list mutation in tests and tools — see
    /// <see cref="Modifiers.Fingerprint"/> for why counts alone are enough, and
    /// why it must stay O(1): the tick asks for a modifier a couple of dozen
    /// times per one-second sub-step, so a guard that walked the state's
    /// dictionaries would cost more than the rebuild it was avoiding.
    /// </summary>
    public sealed class ModifierSnapshot
    {
        public int version = -1;
        public GameDataAsset data;
        public long fingerprint = -1;

        public double tendingBurstBonus;
        public double choiceChanceBonus;
        public double digSpeedMultiplier = 1.0;
        public double offlineCapRaiseTo;
        public double offlineCapBonusHours;
        public double wardenYieldBonus;
        public double bubbleRewardBonus;
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
        /// A change detector over the effect-source collections, for mutations
        /// that bypass the bump (hand-built test states). Counts only, and
        /// deliberately: it is checked on EVERY read, so it has to be O(1).
        /// <para>
        /// Counts are enough because a mutation that changes a value without
        /// changing a count either bumps or changes no modifier. Building and
        /// repeatable-Almanac levels rise through purchase paths that end in
        /// <see cref="Upgrades.RecomputeYieldMultipliers"/>; a tincture's
        /// second bottle banks time, not depth; a sketch changes nothing until
        /// its plate is recorded, and recording bumps; a deep amber piece
        /// changes nothing until the set is complete, and completing bumps.
        /// The one thing counts miss — same-count replacement, re-crafting into
        /// a worn gear slot — was always the explicit bump's job.
        /// </para>
        /// <para>
        /// Mixed rather than packed. The old form multiplied each count by its
        /// own power of ten and summed, which aliased the moment a band reached
        /// its stride: 1,000 building levels (the Store's line is endless) read
        /// as one field sketch, and the top bands could carry the sum past
        /// long.MaxValue. A hash has no bands to overflow.
        /// </para>
        /// </summary>
        private static long Fingerprint(GameState state)
        {
            // FNV-1a, 64-bit. Order is fixed, so this is a plain sequence hash:
            // no two counts can trade places to produce the same answer.
            var hash = 14695981039346656037UL;
            Mix(ref hash, state.purchasedUpgradeIds.Count);
            Mix(ref hash, state.almanacNodeIds.Count);
            Mix(ref hash, state.almanacLevels.Count);
            Mix(ref hash, state.fixedResources.Count);
            Mix(ref hash, state.gearBySlot.Count);
            Mix(ref hash, state.buildingLevels.Count);
            Mix(ref hash, state.insectSketches.Count);
            Mix(ref hash, state.activeTinctures.Count);
            return unchecked((long)hash);
        }

        private static void Mix(ref ulong hash, int value)
        {
            unchecked
            {
                hash = (hash ^ (uint)value) * 1099511628211UL;
            }
        }

        private static ModifierSnapshot Build(GameState state, GameDataAsset data)
        {
            var snapshot = new ModifierSnapshot();

            foreach (var effect in Upgrades.ActiveEffects(state, data))
            {
                switch (effect.type)
                {
                    case EffectType.WardenYieldBonus:
                        snapshot.wardenYieldBonus += effect.value;
                        break;
                    case EffectType.BubbleRewardBonus:
                        snapshot.bubbleRewardBonus += effect.value;
                        break;
                    case EffectType.TendingBurstBonus:
                        snapshot.tendingBurstBonus += effect.value;
                        break;
                    case EffectType.ChoiceChanceBonus:
                        snapshot.choiceChanceBonus += effect.value;
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

            // Sell-value bonuses are purchased-only (a design decision — set
            // and insect bonuses never inflate the Provisioner).
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
            // The Store's bought levels keep goods longer — an additive band
            // alongside gear's offlineCapBonusHours effects.
            snapshot.offlineCapBonusHours += Buildings.ComputeOfflineCapBonusHours(state, data);

            // Bonded familiars are ordinary roster members now (materialised by
            // Roster.SyncBonded) — they gather through Stationing like any
            // other, so there's no separate bonded accumulator here.

            return snapshot;
        }
    }
}
