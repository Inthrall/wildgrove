using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The one-off upgrade ladder (design doc §9): purchasing spends Coin plus
    /// any crafted materials, records the upgrade on the run, and recomputes
    /// the derived modifiers the tick and economy read. Pure and deterministic
    /// like the rest of the sim — the MonoBehaviour driver wires it to the UI,
    /// the tests pin the maths. Effect types the sim doesn't consume yet
    /// (unlockSkill / unlockRecipe / unlockDigSite, craft and dig speed, …)
    /// are recorded on the run but stay inert until their systems land.
    /// </summary>
    public static class Upgrades
    {
        /// <summary>The Whetstone-style wildcard: a skill target matching every gathering node.</summary>
        private const string AllGatheringSkill = "all-gathering";

        /// <summary>True when the run holds every listed material (design §9: money→XP — the cost is goods + a skill gate, no Coin).</summary>
        public static bool CanAfford(GameState state, UpgradeData upgrade)
        {
            if (state == null || upgrade == null)
            {
                return false;
            }

            foreach (var material in upgrade.materials)
            {
                if (state.GetResource(material.id) < material.amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The skill gate (design §9): the run's level in the upgrade's gateSkill
        /// must reach gateLevel. Ungated upgrades (no gateSkill) always pass.
        /// </summary>
        public static bool MeetsSkillGate(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            if (upgrade == null || string.IsNullOrEmpty(upgrade.gateSkill))
            {
                return true;
            }

            return Skills.Level(state, data, upgrade.gateSkill) >= upgrade.gateLevel;
        }

        /// <summary>
        /// Buy <paramref name="upgrade"/> if it isn't owned, has something
        /// left to give, and the run clears every gate — skill level, tool
        /// tier, fold count, and material cost — then recompute the node
        /// multipliers its effects feed. Returns false (no change) otherwise,
        /// so the caller can leave the button disabled.
        /// </summary>
        public static bool TryPurchase(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            if (state == null || data == null || upgrade == null
                || state.HasUpgrade(upgrade.id) || IsSpentRecruit(state, upgrade)
                || !CanAfford(state, upgrade)
                || !MeetsToolRequirement(state, data, upgrade)
                || !MeetsFoldGate(state, data, upgrade)
                || !MeetsSkillGate(state, data, upgrade))
            {
                return false;
            }

            foreach (var material in upgrade.materials)
            {
                state.resources[material.id] = state.GetResource(material.id) - material.amount;
            }

            state.purchasedUpgradeIds.Add(upgrade.id);

            // A recruitSpecies effect answers now: the familiar joins resting
            // at camp, queueing for the naming sheet like any arrival (each
            // species joins once, ever — Recruit no-ops on a duplicate).
            foreach (var effect in upgrade.effects)
            {
                if (effect.type == EffectType.RecruitSpecies && !string.IsNullOrEmpty(effect.species))
                {
                    Roster.Recruit(state, data, effect.species, null);
                }
            }

            // A trail map's unlockZone effect takes hold immediately: the new
            // zone's nodes appear (with the design §2 regional seed) before the
            // multipliers are rebuilt so they're covered too. The newly
            // revealed verse then credits deeds already done.
            GameStateFactory.SyncUnlockedZones(state, data);
            RecomputeYieldMultipliers(state, data);
            Rite.SyncDeedSlots(state, data);
            return true;
        }

        /// <summary>
        /// True when everything this upgrade grants is a recruitSpecies whose
        /// species already walks with the warden — the rung has nothing left
        /// to give. Happens after a fold: the kith carries across but the
        /// ladder resets, and the recruit rungs must not reappear asking
        /// goods for no one. The Ladder UI hides these and TryPurchase
        /// refuses them.
        /// </summary>
        public static bool IsSpentRecruit(GameState state, UpgradeData upgrade)
        {
            if (upgrade?.effects == null || upgrade.effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in upgrade.effects)
            {
                if (effect.type != EffectType.RecruitSpecies)
                {
                    return false;
                }

                if (Roster.OfSpecies(state, effect.species) == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The run's tool tier as an index into economy.tools.tiers: the best
        /// toolTier upgrade owned, −1 with none. Data without a tools section
        /// (hand-built fixtures) reads −1 but gates nothing — see
        /// <see cref="MeetsToolRequirement"/>.
        /// </summary>
        public static int ToolTierIndex(GameState state, GameDataAsset data)
        {
            var tiers = data.economy?.tools?.tiers;
            if (tiers == null)
            {
                return -1;
            }

            var best = -1;
            foreach (var upgradeId in state.purchasedUpgradeIds)
            {
                if (data.UpgradesById.TryGetValue(upgradeId, out var upgrade)
                    && !string.IsNullOrEmpty(upgrade.toolTier))
                {
                    var index = tiers.IndexOf(upgrade.toolTier);
                    if (index > best)
                    {
                        best = index;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The design §3 tool gate: a trail map can only be bought once the
        /// run's tool tier covers every zone it unlocks (Zone 2 flint …
        /// deeper steel+). Non-map upgrades, ungated zones, and data without
        /// a tools section all pass.
        /// </summary>
        public static bool MeetsToolRequirement(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            var missing = MissingToolTier(state, data, upgrade);
            return string.IsNullOrEmpty(missing);
        }

        /// <summary>
        /// The tier name blocking this upgrade's purchase (for the buy
        /// button's "needs … tools" line), or null when nothing is missing.
        /// </summary>
        public static string MissingToolTier(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            var tiers = data.economy?.tools?.tiers;
            if (tiers == null || upgrade == null)
            {
                return null;
            }

            string missing = null;
            var missingIndex = -1;
            var owned = ToolTierIndex(state, data);
            foreach (var effect in upgrade.effects)
            {
                if (effect.type != EffectType.UnlockZone || string.IsNullOrEmpty(effect.zone)
                    || !data.ZonesById.TryGetValue(effect.zone, out var zone)
                    || string.IsNullOrEmpty(zone.requiredTool))
                {
                    continue;
                }

                var required = tiers.IndexOf(zone.requiredTool);
                if (required > owned && required > missingIndex)
                {
                    missing = zone.requiredTool;
                    missingIndex = required;
                }
            }

            return missing;
        }

        /// <summary>
        /// The fold this rung first appears on (design §8): the greatest
        /// minMigration of the upgrade itself and of every zone its unlockZone
        /// effects open. Zero — the default — means it is on the ladder from the
        /// first run.
        ///
        /// Reading the zone's gate through the map rung is what lets a trail be
        /// gated in ONE place. Authoring the fold on both the zone and its map
        /// would be two numbers that must agree, and the day they disagree the
        /// zone's verse and the rung that opens it would answer to different
        /// folds — which is the hard-lock in <see cref="Rite.IsVerseInPlay"/>'s
        /// remarks, arrived at by a typo.
        /// </summary>
        public static int FoldGate(GameDataAsset data, UpgradeData upgrade)
        {
            if (upgrade == null)
            {
                return 0;
            }

            var gate = upgrade.minMigration;
            if (data == null)
            {
                return gate;
            }

            foreach (var effect in upgrade.effects)
            {
                if (effect.type == EffectType.UnlockZone && !string.IsNullOrEmpty(effect.zone)
                    && data.ZonesById.TryGetValue(effect.zone, out var zone)
                    && zone.minMigration > gate)
                {
                    gate = zone.minMigration;
                }
            }

            return gate;
        }

        /// <summary>
        /// The design §8 fold gate: the run must have that many folds behind it.
        /// Ungated rungs (the default) always pass.
        /// </summary>
        public static bool MeetsFoldGate(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            return state != null && state.migrationCount >= FoldGate(data, upgrade);
        }

        /// <summary>
        /// Folds still to walk before this rung appears, for the ladder's
        /// "after two folds" line — zero when nothing is waiting.
        /// </summary>
        public static int FoldsUntilAvailable(GameState state, GameDataAsset data, UpgradeData upgrade)
        {
            var missing = FoldGate(data, upgrade) - (state != null ? state.migrationCount : 0);
            return missing > 0 ? missing : 0;
        }

        /// <summary>
        /// The zones this run has opened: the starting zone plus every zone an
        /// owned upgrade's unlockZone effect grants.
        /// </summary>
        public static HashSet<string> UnlockedZoneIds(GameState state, GameDataAsset data)
        {
            var ids = new HashSet<string> { GameStateFactory.StartingZoneId };
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockZone && !string.IsNullOrEmpty(effect.zone))
                {
                    ids.Add(effect.zone);
                }
            }

            return ids;
        }

        /// <summary>
        /// Rebuild every node's tool/upgrade multiplier from the purchased
        /// upgrades and completed insects: yieldMult effects multiply together,
        /// yieldBonus effects add a combined percentage on top. Call after any
        /// purchase, on restore, and when an insect plate is recorded.
        /// </summary>
        public static void RecomputeYieldMultipliers(GameState state, GameDataAsset data)
        {
            // Every effect-source mutation funnels through here — drop the
            // cached modifier snapshot alongside the node multipliers.
            state.BumpModifiers();

            foreach (var node in state.nodes)
            {
                var mult = 1.0;
                var bonus = 0.0;

                foreach (var effect in ActiveEffects(state, data))
                {
                    if (effect.type == EffectType.YieldMult && TargetsNode(effect, node))
                    {
                        mult *= effect.value;
                    }
                    else if (effect.type == EffectType.YieldBonus && TargetsNode(effect, node))
                    {
                        bonus += effect.value;
                    }
                }

                node.yieldMultiplier = mult * (1.0 + bonus);
            }
        }

        /// <summary>Summed wardenYieldBonus band from effect sources (the Birch Frame Pack's +50%) — quickens the warden's own hands alongside the pony's trait.</summary>
        public static double WardenYieldBonus(GameState state, GameDataAsset data)
        {
            return Modifiers.Of(state, data).wardenYieldBonus;
        }

        /// <summary>Summed bubbleRewardBonus band from effect sources (the Almanac's Long Reach) — fattens the flat windfall alongside the pack raven's trait.</summary>
        public static double BubbleRewardBonus(GameState state, GameDataAsset data)
        {
            return Modifiers.Of(state, data).bubbleRewardBonus;
        }

        /// <summary>Extra Tending burst strength from worn gear (the Cordage Wraps' +50%), summed — multiplies the burst's yield multiplier.</summary>
        public static double TendingBurstBonus(GameState state, GameDataAsset data)
        {
            return Modifiers.Of(state, data).tendingBurstBonus;
        }

        /// <summary>
        /// The skills this run has opened: the starting zone's unlocks plus
        /// every skill an owned upgrade's unlockSkill effect grants. Gates
        /// which recipes can be crafted.
        /// </summary>
        public static HashSet<string> UnlockedSkills(GameState state, GameDataAsset data)
        {
            // The cached set — callers read (Contains/enumerate), never mutate.
            return Modifiers.Of(state, data).unlockedSkills;
        }

        /// <summary>The raw derivation, into <paramref name="skills"/> — the snapshot builder's path.</summary>
        internal static void BuildUnlockedSkills(GameState state, GameDataAsset data, HashSet<string> skills)
        {
            if (data.ZonesById.TryGetValue(GameStateFactory.StartingZoneId, out var startingZone))
            {
                skills.UnionWith(startingZone.unlocks);
            }

            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockSkill && !string.IsNullOrEmpty(effect.skill))
                {
                    skills.Add(effect.skill);
                }
            }
        }

        /// <summary>Recipe ids granted by owned unlockRecipe effects (defaultKnown recipes don't need one).</summary>
        public static HashSet<string> UnlockedRecipeIds(GameState state, GameDataAsset data)
        {
            // The cached set — callers read, never mutate.
            return Modifiers.Of(state, data).unlockedRecipeIds;
        }

        /// <summary>The raw derivation, into <paramref name="recipes"/> — the snapshot builder's path.</summary>
        internal static void BuildUnlockedRecipeIds(GameState state, GameDataAsset data, HashSet<string> recipes)
        {
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockRecipe && !string.IsNullOrEmpty(effect.recipe))
                {
                    recipes.Add(effect.recipe);
                }
            }
        }

        /// <summary>
        /// Craft-speed multiplier for one skill's recipes: owned craftSpeedMult
        /// effects targeting that skill multiply together (Bellows Forge ×2 for
        /// forgecraft). Divides the per-batch craft time.
        /// </summary>
        public static double CraftSpeedMultiplier(GameState state, GameDataAsset data, string skill)
        {
            var snapshot = Modifiers.Of(state, data);
            snapshot.craftSpeedBySkill.TryGetValue(skill ?? string.Empty, out var perSkill);
            return snapshot.craftSpeedGlobal * (perSkill == 0.0 ? 1.0 : perSkill);
        }

        /// <summary>Sell-value multiplier for one resource: 1 + the summed sellValueBonus effects owned.</summary>
        public static double SellValueMultiplier(GameState state, GameDataAsset data, string resourceId)
        {
            Modifiers.Of(state, data).sellValueBonusByResource.TryGetValue(resourceId ?? string.Empty, out var bonus);
            return 1.0 + bonus;
        }

        /// <summary>
        /// Flat Choice-chance points from every owned choiceChanceBonus effect
        /// — upgrades, insect plates and Almanac nodes alike — summed into
        /// design §8's additive band.
        /// </summary>
        public static double ChoiceChanceBonus(GameState state, GameDataAsset data)
        {
            return Modifiers.Of(state, data).choiceChanceBonus;
        }

        /// <summary>Dig-speed multiplier from owned digSpeedMult upgrades (Brush Screens ×2) — they multiply together.</summary>
        public static double DigSpeedMultiplier(GameState state, GameDataAsset data)
        {
            return Modifiers.Of(state, data).digSpeedMultiplier;
        }

        /// <summary>Zones whose dig site an owned unlockDigSite effect has opened.</summary>
        public static HashSet<string> UnlockedDigSiteZones(GameState state, GameDataAsset data)
        {
            var zones = new HashSet<string>();
            foreach (var effect in PurchasedEffects(state, data))
            {
                if (effect.type == EffectType.UnlockDigSite && !string.IsNullOrEmpty(effect.zone))
                {
                    zones.Add(effect.zone);
                }
            }

            return zones;
        }

        /// <summary>
        /// The run's offline cap: the base cap (2 h), raised (never lowered) to
        /// the best offlineCapHours effect active (Root Cellar 3 h, Smokehouse
        /// 5 h, the Almanac's Long Watch up to 9 h), plus the additive
        /// offlineCapBonusHours band (the Oilskin Tarp, the Almanac Desk, the
        /// Old-Growth Bounty spread and the Store's per-level hours) — and
        /// finally held to economy.offline.maxCapHours, which is the ladder's
        /// stated top. The authored kit lands on the ceiling exactly; the
        /// Store's endless levels are what the clamp is there to catch.
        /// </summary>
        public static double OfflineCapHours(GameState state, GameDataAsset data)
        {
            var snapshot = Modifiers.Of(state, data);
            var cap = System.Math.Max(data.economy.offline.baseCapHours, snapshot.offlineCapRaiseTo);
            return ClampToMaxCap(cap + snapshot.offlineCapBonusHours, data);
        }

        /// <summary>
        /// What ONE offlineCapHours effect adds to the away cap, so the journal
        /// can say "+2h" about an effect that is authored as a raise-to. These
        /// raise a floor rather than stack, so the gain is measured against the
        /// floor the run would stand on without this one — a floor the run has
        /// already cleared adds nothing, and neither does one bought when the
        /// additive band has already carried the run to the ceiling.
        /// </summary>
        public static double OfflineCapGainHours(GameState state, GameDataAsset data, double raiseToHours)
        {
            var floor = data.economy.offline.baseCapHours;
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == EffectType.OfflineCapHours && effect.value != raiseToHours)
                {
                    floor = System.Math.Max(floor, effect.value);
                }
            }

            // The additive band does not cancel out of this comparison, so it
            // belongs on both sides: it decides whether the raise lands under
            // the ceiling or against it.
            var band = Modifiers.Of(state, data).offlineCapBonusHours;
            var without = ClampToMaxCap(floor + band, data);
            var with = ClampToMaxCap(System.Math.Max(floor, raiseToHours) + band, data);

            return System.Math.Max(0.0, with - without);
        }

        /// <summary>
        /// Hold an away cap to the authored ceiling. A non-positive
        /// maxCapHours means unbounded — the shape a hand-built test fixture
        /// leaves it in; authored data always states it (validator-enforced).
        /// </summary>
        private static double ClampToMaxCap(double hours, GameDataAsset data)
        {
            var ceiling = data.economy.offline.maxCapHours;

            return ceiling > 0.0 ? System.Math.Min(hours, ceiling) : hours;
        }

        /// <summary>
        /// True when anything active on the run carries an effect of this type
        /// — for the flag-shaped types with no value or target (the Almanac's
        /// keepCraftOrders), read at a moment rather than every tick.
        /// </summary>
        public static bool HasActiveEffect(GameState state, GameDataAsset data, EffectType type)
        {
            foreach (var effect in ActiveEffects(state, data))
            {
                if (effect.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Purchased upgrade effects, completed insects', owned Almanac nodes',
        /// worn gear's, and the run's region modifier (design §8) — everything
        /// currently modifying the run. The RAW walk (the Museum leg clones) —
        /// per-tick consumers read the <see cref="Modifiers"/> snapshot instead.
        /// </summary>
        internal static IEnumerable<EffectData> ActiveEffects(GameState state, GameDataAsset data)
        {
            // The region's flavour is fixed for the whole run (it derives from
            // the migration count), so it can never invalidate a snapshot
            // mid-run — a fold always builds a fresh state.
            foreach (var effect in Regions.ActiveEffects(state, data))
            {
                yield return effect;
            }

            foreach (var effect in Gear.EquippedEffects(state, data))
            {
                yield return effect;
            }

            // Live tincture buffs (design §5) — activation and expiry both
            // rebuild the modifiers, so the union is never read stale.
            foreach (var effect in Tinctures.ActiveEffects(state, data))
            {
                yield return effect;
            }

            foreach (var effect in PurchasedEffects(state, data))
            {
                yield return effect;
            }

            foreach (var effect in Insects.RecordedEffects(state, data))
            {
                yield return effect;
            }

            // The completed deep amber plate (design §6) — permanent like the
            // insect plates it sits beside in the journal.
            foreach (var effect in DeepAmber.CompletedEffects(state, data))
            {
                yield return effect;
            }

            foreach (var effect in Folio.CompletedSpreadEffects(state, data))
            {
                yield return effect;
            }

            foreach (var nodeId in state.almanacNodeIds)
            {
                if (!data.AlmanacById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                foreach (var effect in node.effects)
                {
                    yield return effect;
                }
            }

            // Repeatable lines (design §7's endless sink) pay per level held.
            // Scaling the VALUE by the level count is the same as yielding the
            // effect that many times — but ONLY for the additive bands
            // (yieldBonus sums into mult·(1+bonus)). A multiplicative type
            // would want value^level instead, so the validator refuses one on
            // a repeatable line rather than let this silently compute the
            // wrong curve.
            foreach (var pair in state.almanacLevels)
            {
                if (pair.Value <= 0 || !data.AlmanacById.TryGetValue(pair.Key, out var node) || !node.repeatable)
                {
                    continue;
                }

                foreach (var effect in node.effects)
                {
                    yield return new EffectData
                    {
                        type = effect.type,
                        skill = effect.skill,
                        zone = effect.zone,
                        resource = effect.resource,
                        recipe = effect.recipe,
                        species = effect.species,
                        upgrade = effect.upgrade,
                        value = effect.value * pair.Value
                    };
                }
            }
        }

        internal static IEnumerable<EffectData> PurchasedEffects(GameState state, GameDataAsset data)
        {
            foreach (var upgradeId in state.purchasedUpgradeIds)
            {
                // An id this data version doesn't know (saved on other data) is
                // skipped rather than crashing the run.
                if (!data.UpgradesById.TryGetValue(upgradeId, out var upgrade))
                {
                    continue;
                }

                foreach (var effect in upgrade.effects)
                {
                    yield return effect;
                }
            }
        }

        private static bool TargetsNode(EffectData effect, NodeState node)
        {
            // The region modifiers' grain (design §8): a single resource —
            // "+herbs", "−flowers" — finer than a skill or a zone.
            if (!string.IsNullOrEmpty(effect.resource))
            {
                return effect.resource == node.resourceId;
            }

            if (!string.IsNullOrEmpty(effect.zone))
            {
                return effect.zone == node.zoneId;
            }

            // "all" is the insect wildcard, "all-gathering" the upgrade one —
            // the validator accepts both spellings.
            return effect.skill == AllGatheringSkill || effect.skill == "all" || effect.skill == node.skill;
        }
    }
}
