using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// The economy block: every tuning number the sim reads, and the sections it
    /// cannot run without.
    /// <para>
    /// The longest check in the validator, and the one that earns its length:
    /// these are the numbers balancing changes touch, and a missing section or
    /// an out-of-range rate is the failure that reaches a player as a game that
    /// silently pays nothing.
    /// </para>
    /// </summary>
    public static partial class GameDataValidator
    {
        private static void ValidateEconomy(GameData data, List<string> issues)
        {
            var economy = data.Economy;
            if (economy == null)
            {
                issues.Add("Economy config is missing");
                return;
            }

            RequireSection(economy.CostGrowth, "costGrowth", issues);
            RequireSection(economy.Gifts, "gifts", issues);
            RequireSection(economy.Delivery, "delivery", issues);
            RequireSection(economy.Kith, "kith", issues);
            RequireSection(economy.Crafting, "crafting", issues);
            RequireSection(economy.Tools, "tools", issues);
            RequireSection(economy.Mastery, "mastery", issues);
            RequireSection(economy.Verdure, "verdure", issues);
            RequireSection(economy.Xp, "xp", issues);
            RequireSection(economy.Offline, "offline", issues);
            RequireSection(economy.Quality, "quality", issues);
            RequireSection(economy.Observation, "observation", issues);
            RequireSection(economy.Tending, "tending", issues);
            RequireSection(economy.Warden, "warden", issues);
            RequireSection(economy.FamiliarXp, "familiarXp", issues);
            RequireSection(economy.Replant, "replant", issues);

            if (economy.CostGrowth != null && economy.CostGrowth.Building <= 1)
            {
                issues.Add("Economy costGrowth factors must all be > 1");
            }

            if (economy.Gifts != null && economy.Gifts.PileGoods <= 0)
            {
                issues.Add("Economy gifts.pileGoods must be positive");
            }


            if (economy.Warden != null && economy.Warden.GatherPerSecond <= 0)
            {
                // Not merely a tuning value: the gift pile costs the node's own
                // resource, and a bare node's only source is the warden's hands.
                issues.Add("Economy warden.gatherPerSecond must be positive or bare nodes can never afford their first gift");
            }

            if (economy.Amber != null
                && economy.Amber.TimeSkipDailyCapHours > 0
                && economy.Amber.TimeSkipDailyCapHours < economy.Amber.TimeSkipHours)
            {
                // The budget refills to the cap and a skip needs timeSkipHours of
                // it, so a positive cap below one skip is a sink that can never
                // be spent — dead, not merely slow.
                issues.Add("Economy amber.timeSkipDailyCapHours must be 0 (uncapped) or at least timeSkipHours — a cap below one skip can never be spent");
            }

            if (economy.Delivery != null && economy.Delivery.BatchSeconds <= 0)
            {
                issues.Add("Economy delivery batchSeconds must be positive");
            }

            if (economy.Kith != null
                && (economy.Kith.SlotsBase <= 0 || economy.Kith.SlotsMax < economy.Kith.SlotsBase))
            {
                issues.Add("Economy kith needs a positive slotsBase and slotsMax >= slotsBase");
            }

            if (economy.Kith != null)
            {
                var slotVerses = economy.Kith.SlotVerseZones;
                if (slotVerses == null || slotVerses.Count == 0)
                {
                    issues.Add("Economy kith.slotVerseZones is empty — the earned slots need the verses that open them");
                }
                else
                {
                    // A zone id that does not exist opens NOTHING, silently and
                    // for the life of the build: the ladder simply stops one
                    // place short and there is no symptom to trace back here.
                    var zoneIds = new HashSet<string>();
                    if (data.Zones != null)
                    {
                        foreach (var zone in data.Zones)
                        {
                            zoneIds.Add(zone.Id);
                        }
                    }

                    var seenZones = new HashSet<string>();
                    foreach (var zoneId in slotVerses)
                    {
                        if (string.IsNullOrWhiteSpace(zoneId) || !zoneIds.Contains(zoneId))
                        {
                            issues.Add($"Economy kith.slotVerseZones names '{zoneId}', which is not a zone");
                        }
                        else if (!seenZones.Add(zoneId))
                        {
                            // One verse, one place. A repeat cannot be sung
                            // twice, so it costs the ladder a rung outright.
                            issues.Add($"Economy kith.slotVerseZones names '{zoneId}' twice — a verse opens one place");
                        }
                    }

                    // The ladder must land exactly on the ceiling: base + the
                    // named verses + the two store purchases (§4).
                    if (economy.Kith.SlotsBase + slotVerses.Count + 2 != economy.Kith.SlotsMax)
                    {
                        issues.Add("Economy kith ladder is off: slotsBase + slotVerseZones + 2 purchasable must equal slotsMax");
                    }
                }

                if (economy.Kith.GeneratorGatherPosts <= 0)
                {
                    issues.Add("Economy kith.generatorGatherPosts must be positive — the Rite generator's reachability proof needs it");
                }
            }

            if (economy.Crafting != null && economy.Crafting.BaseCraftSeconds <= 0)
            {
                issues.Add("Economy crafting.baseCraftSeconds must be positive");
            }

            if (economy.Tools != null && (economy.Tools.Tiers == null || economy.Tools.Tiers.Count == 0))
            {
                issues.Add("Economy tools.tiers is empty");
            }

            if (economy.Xp != null && (economy.Xp.Base <= 0 || economy.Xp.Growth <= 1 || economy.Xp.MaxLevel <= 1))
            {
                // Base ≤ 0 means every rung costs nothing — all skills read
                // max level at zero XP and every skillLevel gate falls open.
                issues.Add("Economy xp progression is degenerate");
            }

            if (economy.Xp != null && (economy.Xp.GatherPerUnit < 0 || economy.Xp.CraftPerBatch < 0))
            {
                issues.Add("Economy xp gains must not be negative");
            }

            if (economy.FamiliarXp != null
                && (economy.FamiliarXp.Base <= 0 || economy.FamiliarXp.Growth <= 1 || economy.FamiliarXp.MaxLevel <= 1))
            {
                issues.Add("Economy familiarXp progression is degenerate");
            }

            if (economy.FamiliarXp != null && economy.FamiliarXp.XpPerSecond < 0)
            {
                issues.Add("Economy familiarXp.xpPerSecond must not be negative");
            }

            if (economy.FamiliarXp != null && economy.FamiliarXp.KinshipDivisor <= 0)
            {
                // A non-positive divisor makes Kinship.Fold silently ignore the
                // authored tuning and fall back to a hardcoded constant.
                issues.Add("Economy familiarXp.kinshipDivisor must be positive");
            }

            if (economy.FamiliarXp != null)
            {
                var milestones = economy.FamiliarXp.SignatureMilestones;
                if (milestones != null && milestones.Count > 0)
                {
                    for (var i = 0; i < milestones.Count; i++)
                    {
                        if (milestones[i] <= 0 || (i > 0 && milestones[i] <= milestones[i - 1]))
                        {
                            issues.Add("Economy familiarXp.signatureMilestones must be positive and strictly ascending");
                            break;
                        }
                    }

                    if (economy.FamiliarXp.SignatureDeepening <= 0)
                    {
                        issues.Add("Economy familiarXp.signatureDeepening must be positive when signatureMilestones are authored — a milestone that sharpens nothing is a broken promise");
                    }
                }
                else if (economy.FamiliarXp.SignatureDeepening > 0)
                {
                    issues.Add("Economy familiarXp.signatureDeepening is set but signatureMilestones is empty — the deepening can never fire");
                }
            }

            if (economy.Replant != null
                && (economy.Replant.BaseCost <= 0 || economy.Replant.Growth <= 1 || economy.Replant.RichnessPerLevel <= 0))
            {
                // BaseCost ≤ 0 makes richness free; growth ≤ 1 never escalates; a
                // non-positive richnessPerLevel makes replanting do nothing.
                issues.Add("Economy replant is degenerate (baseCost > 0, growth > 1, richnessPerLevel > 0)");
            }

            if (economy.Mastery != null
                && (economy.Mastery.Base <= 0 || economy.Mastery.Growth <= 1
                    || economy.Mastery.MaxLevel < 1 || economy.Mastery.XpPerUnit <= 0))
            {
                issues.Add("Economy mastery progression is degenerate");
            }

            if (economy.Mastery != null && economy.Mastery.YieldBonusPerLevel < 0)
            {
                issues.Add("Economy mastery.yieldBonusPerLevel must not be negative");
            }

            if (economy.Verdure != null && (economy.Verdure.RenownDivisor <= 0 || economy.Verdure.Exponent <= 0))
            {
                // renownDivisor ≤ 0 makes Migration.VerdureAfterMigration bank
                // zero Verdure every fold — the permanent-progression currency
                // dies. exponent 0 makes Pow(x, 0) = 1, so every fold banks
                // exactly one point regardless of lifetime Renown.
                issues.Add("Economy verdure.renownDivisor and verdure.exponent must both be positive");
            }

            if (economy.Verdure != null && economy.Verdure.YieldBonusPerPoint < 0)
            {
                issues.Add("Economy verdure.yieldBonusPerPoint must not be negative");
            }

            if (economy.Offline != null && economy.Offline.BaseCapHours <= 0)
            {
                issues.Add("Economy offline.baseCapHours must be positive");
            }

            if (economy.Offline != null && economy.Offline.MaxCapHours < economy.Offline.BaseCapHours)
            {
                // Authored data must always state the ceiling, and a ceiling
                // under the floor would clamp a fresh run's first night below
                // the cap it is supposed to start on.
                issues.Add("Economy offline.maxCapHours must be at least offline.baseCapHours");
            }

            if (economy.Offline != null && economy.Offline.RateMultiplier <= 0)
            {
                // The sim multiplies every offline second by this — zero
                // silently voids all offline earnings.
                issues.Add("Economy offline.rateMultiplier must be positive");
            }

            if (economy.Quality != null
                && (!IsChance(economy.Quality.DecentChance) || !IsChance(economy.Quality.ChoiceBaseChance)))
            {
                issues.Add("Economy quality chances must be within [0, 1]");
            }

            if (economy.Quality != null && economy.Quality.DecentChance + economy.Quality.ChoiceBaseChance > 1)
            {
                // The two rolls share one [0,1) draw — together they must
                // leave room for Poor.
                issues.Add("Economy quality chances must not sum above 1");
            }

            if (economy.Quality != null
                && (economy.Quality.DecentValueMult <= 0 || economy.Quality.ChoiceValueMult <= 0))
            {
                issues.Add("Economy quality value multipliers must be positive");
            }

            if (economy.Tending != null && economy.Tending.ChoiceChanceBonus < 0)
            {
                issues.Add("Economy tending.choiceChanceBonus must not be negative");
            }

            if (economy.Tending != null && economy.Tending.BurstYieldMult <= 0)
            {
                // The sim multiplies a node's yield by this during the tend-burst
                // window — omitted/zero makes a freshly tended node yield nothing
                // for the burst, worse than leaving it alone.
                issues.Add("Economy tending.burstYieldMult must be positive");
            }

            if (economy.Tending != null && (economy.Tending.BurstDurationSec < 0 || economy.Tending.ChoiceBonusDurationSec < 0))
            {
                issues.Add("Economy tending burst/choice durations must not be negative");
            }

            if (economy.Bubbles != null
                && (economy.Bubbles.SpawnIntervalSec <= 0 || economy.Bubbles.LifetimeSec <= 0
                    || economy.Bubbles.MaxLive <= 0 || economy.Bubbles.RewardSeconds <= 0
                    || economy.Bubbles.RewardRatePerSecond <= 0))
            {
                // A present-but-zeroed section either never spawns a bubble or
                // spawns ones worth nothing — configure it whole or not at all.
                issues.Add("Economy bubbles values must all be positive");
            }

            // bubbles.rewardRatePerSecond used to be pinned equal to
            // warden.gatherPerSecond here, on the reading that a windfall IS a
            // minute of the warden's own picking. The two parted on 2026-08-13,
            // as that rule's own comment said they might: rescaling the warden
            // (0.5 → 0.4) when their hands began riding the node's multiplier
            // stack would have dragged the windfall from 30 units to 24, which
            // was never the intent of a warden change. The windfall's rate is
            // its own tuning value now — a notional gatherer's hands, not any
            // particular pair — so a warden retune no longer moves it and it can
            // be weighed on its own against zone 6-8 income, which is what the
            // bubbles note's standing warning has always asked for.

            if (economy.Observation != null
                && (economy.Observation.PityTimerHoursWatched <= 0 || economy.Observation.BaseSketchesPerHour <= 0
                    || economy.Observation.WatchXpPerHour <= 0))
            {
                // Zero rate AND zero pity means an observation site can never
                // surface a field sketch — every insect plate becomes unreachable.
                // A zeroed watchXpPerHour is the same shape of dead end one level
                // up: the observation craft earns XP nowhere else, so its level
                // gates would never open.
                issues.Add("Economy observation values must all be positive");
            }

            if (economy.Observation?.Skill != null && !KnownSkills.Contains(economy.Observation.Skill))
            {
                issues.Add($"Economy observation skill '{economy.Observation.Skill}' is unknown");
            }

            if (economy.Amber != null
                && (economy.Amber.DigFindsPerHour <= 0 || economy.Amber.PerFind <= 0
                    || economy.Amber.TimeSkipHours <= 0 || economy.Amber.TimeSkipCostAmber <= 0
                    || economy.Amber.AdDripAmber <= 0 || economy.Amber.WeeklyCacheAmber <= 0
                    || economy.Amber.RenameCostAmber <= 0
                    || economy.Amber.WardenRenameCostAmber <= 0
                    || economy.Amber.CallingGiftAmber <= 0
                    || economy.Amber.CampNameCostAmber <= 0
                    || economy.Amber.ConsiderationCostAmber <= 0
                    || economy.Amber.SecondQueueCostAmber <= 0))
            {
                // A present-but-zeroed section would ship an earn with no sink
                // (or a sink no one can afford) — configure it whole or not at all.
                issues.Add("Economy amber values must all be positive");
            }

            if (economy.Store != null
                && (economy.Store.StarterBundleAmber <= 0
                    || economy.Store.AmberPackSmall <= 0 || economy.Store.AmberPackLarge <= 0))
            {
                issues.Add("Economy store amber values must all be positive — the bundle and packs promise piles, not pebbles");
            }
        }

        private static void RequireSection(object section, string name, List<string> issues)
        {
            if (section == null)
            {
                issues.Add($"Economy section '{name}' is missing");
            }
        }

        private static bool IsChance(double value)
        {
            return value >= 0 && value <= 1;
        }
    }
}
