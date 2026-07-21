using System;
using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Builds the Rite for runs 2+ (design §7/§8). The authored first Rite is
    /// the template: each generated verse keeps its zone, its slot shape (how
    /// many goods slots, which deed/specimen/sketch slots) and its value
    /// anchor — but re-picks WHAT the land asks for from the content
    /// available by that zone's point in the run, rotates the spotlight by
    /// migration count (spotlight(m) = rotate(skills, m)), and scales demand
    /// by demandGrowth^migration (verseDemand(m) = baseQty · d^m). Spotlight
    /// slots price at a discount, off-spotlight at a premium — the spotlight
    /// stays the cheapest path. Deterministic: the same migration count
    /// always generates the same Rite, so a reload can never reroll it.
    /// </summary>
    public static class RiteGenerator
    {
        /// <summary>
        /// Whether the data carries generator tuning. Unity serialization
        /// can't round-trip a null section, so a zeroed demandGrowth also
        /// reads as "no generator" (the Configured pattern).
        /// </summary>
        public static bool Configured(RitesBundle rites)
        {
            return rites?.generator != null && rites.generator.demandGrowth > 0.0;
        }

        /// <summary>The generated Rite for this migration, or null when the data has no generator or no template.</summary>
        public static RiteData Generate(GameDataAsset data, int migration)
        {
            var rites = data.rites?.rites;
            if (!Configured(data.rites) || rites == null || rites.Count == 0)
            {
                return null;
            }

            var template = rites[0];
            var config = data.rites.generator;
            var scale = Math.Pow(config.demandGrowth, migration);

            // A private rng thread derived from the migration count alone —
            // independent of gameplay rng, identical on every regeneration.
            var seed = Rng.Sanitise((ulong)migration * 0x9E3779B97F4A7C15UL);

            var rite = new RiteData { id = $"rite-m{migration}", migration = migration };
            foreach (var verse in template.verses)
            {
                rite.verses.Add(GenerateVerse(data, verse, config, migration, scale, ref seed));
            }

            return rite;
        }

        private static RiteVerseData GenerateVerse(GameDataAsset data, RiteVerseData template,
            RiteGeneratorConfigData config, int migration, double scale, ref ulong seed)
        {
            data.ZonesById.TryGetValue(template.zone, out var zone);
            var candidates = CandidateGoods(data, zone);
            var anchor = AverageGoodsValue(data, template);

            // spotlight(m) = rotate: the pool is every skill the candidates
            // answer to, in stable data order; the window slides one skill
            // per migration, changing what the region asks of you.
            var pool = SpotlightPool(data, candidates);
            var spotlight = new List<string>();
            var spotlightCount = Math.Min(Math.Max(template.spotlight.Count, 1), 2);
            for (var i = 0; i < spotlightCount && i < pool.Count; i++)
            {
                spotlight.Add(pool[(migration + i) % pool.Count]);
            }

            // Split the pool: spotlight goods fill the first (cheapest) slots.
            var inSpotlight = new List<string>();
            var offSpotlight = new List<string>();
            foreach (var goods in candidates)
            {
                (spotlight.Contains(GoodsSkill(data, goods)) ? inSpotlight : offSpotlight).Add(goods);
            }

            var goodsCount = 0;
            foreach (var slot in template.slots)
            {
                if (slot.type == RiteSlotType.Resource)
                {
                    goodsCount++;
                }
            }

            goodsCount = Math.Min(goodsCount, candidates.Count);
            var spotlightSlots = Math.Min(Math.Min(2, goodsCount), inSpotlight.Count);

            var picks = new List<RiteSlotData>();
            for (var i = 0; i < goodsCount; i++)
            {
                var fromSpotlight = i < spotlightSlots;
                var source = fromSpotlight ? inSpotlight : offSpotlight;
                if (source.Count == 0)
                {
                    source = fromSpotlight ? offSpotlight : inSpotlight;
                    fromSpotlight = !fromSpotlight;
                }

                picks.Add(GoodsSlot(data, TakeRandom(source, ref seed),
                    anchor * scale * (fromSpotlight ? config.spotlightDiscount : config.offSpotlightPremium)));
            }

            // Rebuild in template order: goods slots take the picks, the
            // special slots (deed/specimen/sketch) keep their authored
            // shape with the grant scaled to this run's demand.
            var verse = new RiteVerseData
            {
                id = $"verse-m{migration}-{template.zone}",
                zone = template.zone,
                spotlight = spotlight
            };
            var nextPick = 0;
            foreach (var slot in template.slots)
            {
                if (slot.type == RiteSlotType.Resource)
                {
                    if (nextPick < picks.Count)
                    {
                        verse.slots.Add(picks[nextPick++]);
                    }
                }
                else
                {
                    verse.slots.Add(new RiteSlotData
                    {
                        type = slot.type,
                        deed = slot.deed,
                        quality = slot.quality,
                        // Deed/specimen/sketch counts stay authored — they
                        // price in taps and luck, not goods; only the Renown
                        // they're worth grows with the run.
                        count = slot.count,
                        renownGrant = ToLongSaturating(slot.renownGrant * scale)
                    });
                }
            }

            return verse;
        }

        /// <summary>
        /// A goods slot asking for `target` worth of the picked goods —
        /// materials (trade value zero) carry the equivalent renownGrant so
        /// offering them never taxes prestige.
        /// </summary>
        private static RiteSlotData GoodsSlot(GameDataAsset data, string goodsId, double target)
        {
            var unit = Economy.NotionalUnitValue(data, goodsId).ToDouble();
            var amount = Math.Max(1L, ToLongSaturating(target / unit));
            return new RiteSlotData
            {
                type = RiteSlotType.Resource,
                resource = goodsId,
                amount = amount,
                renownGrant = Economy.TradeUnitValue(data, goodsId) > BigDouble.Zero
                    ? 0L
                    : ToLongSaturating(amount * unit)
            };
        }

        /// <summary>
        /// Casting a double beyond long range is unchecked (long.MinValue) —
        /// a demand-growth power at deep migration counts would turn a grant
        /// negative and corrupt Renown. Saturate instead.
        /// </summary>
        private static long ToLongSaturating(double value)
        {
            if (double.IsNaN(value) || value <= 0.0)
            {
                return 0L;
            }

            if (value >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)Math.Round(value);
        }

        /// <summary>
        /// What this verse may ask for: the zone's own raw finds, plus every
        /// obtainable recipe output whose skill — and whole input chain — is
        /// available by this zone's point in the run. Order-gating is what
        /// keeps a generated verse 1 answerable from raw finds alone.
        /// </summary>
        private static List<string> CandidateGoods(GameDataAsset data, ZoneData zone)
        {
            var candidates = new List<string>();
            if (zone == null)
            {
                return candidates;
            }

            foreach (var resourceId in zone.resources)
            {
                if (Economy.NotionalUnitValue(data, resourceId) > BigDouble.Zero)
                {
                    candidates.Add(resourceId);
                }
            }

            if (data.recipes != null)
            {
                foreach (var recipe in data.recipes)
                {
                    if (RecipeAvailableByOrder(data, recipe, zone.order, null)
                        && !candidates.Contains(recipe.output)
                        && Economy.NotionalUnitValue(data, recipe.output) > BigDouble.Zero)
                    {
                        candidates.Add(recipe.output);
                    }
                }
            }

            return candidates;
        }

        private static bool RecipeAvailableByOrder(GameDataAsset data, RecipeData recipe, int maxOrder, HashSet<string> visiting)
        {
            if (!RecipeObtainable(data, recipe) || SkillDebutOrder(data, recipe.skill) > maxOrder)
            {
                return false;
            }

            visiting = visiting ?? new HashSet<string>();
            if (!visiting.Add(recipe.output))
            {
                return false;
            }

            foreach (var input in recipe.inputs)
            {
                if (!GoodsAvailableByOrder(data, input.id, maxOrder, visiting))
                {
                    visiting.Remove(recipe.output);
                    return false;
                }
            }

            visiting.Remove(recipe.output);
            return true;
        }

        private static bool GoodsAvailableByOrder(GameDataAsset data, string goodsId, int maxOrder, HashSet<string> visiting)
        {
            foreach (var zone in data.zones)
            {
                if (zone.order <= maxOrder && zone.resources.Contains(goodsId))
                {
                    return true;
                }
            }

            var recipe = FindRecipeProducing(data, goodsId);
            return recipe != null && RecipeAvailableByOrder(data, recipe, maxOrder, visiting);
        }

        /// <summary>Known by default or granted by some purchasable upgrade — mirrors the validator's obtainability rule.</summary>
        private static bool RecipeObtainable(GameDataAsset data, RecipeData recipe)
        {
            if (recipe.defaultKnown)
            {
                return true;
            }

            foreach (var upgrade in data.upgrades)
            {
                foreach (var effect in upgrade.effects)
                {
                    if (effect.type == EffectType.UnlockRecipe && effect.recipe == recipe.id)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The zone order a skill debuts at: the earliest zone whose unlocks
        /// grant it, or — for upgrade-granted skills — the order of the zone
        /// the same upgrade opens (a trail-map skill arrives with its zone),
        /// else 2 for Coin-bought camp skills (forgecraft via the Fire Ring
        /// lands in the zone-2 era, never minute one).
        /// </summary>
        private static int SkillDebutOrder(GameDataAsset data, string skill)
        {
            var debut = int.MaxValue;
            foreach (var zone in data.zones)
            {
                if (zone.unlocks.Contains(skill) && zone.order < debut)
                {
                    debut = zone.order;
                }
            }

            if (debut < int.MaxValue)
            {
                return debut;
            }

            foreach (var upgrade in data.upgrades)
            {
                var grantsSkill = false;
                var zoneOrder = int.MaxValue;
                foreach (var effect in upgrade.effects)
                {
                    if (effect.type == EffectType.UnlockSkill && effect.skill == skill)
                    {
                        grantsSkill = true;
                    }

                    if (effect.type == EffectType.UnlockZone
                        && data.ZonesById.TryGetValue(effect.zone, out var zone)
                        && zone.order < zoneOrder)
                    {
                        zoneOrder = zone.order;
                    }
                }

                if (grantsSkill)
                {
                    debut = Math.Min(debut, zoneOrder < int.MaxValue ? zoneOrder : 2);
                }
            }

            return debut;
        }

        /// <summary>The distinct skills the candidates answer to, in stable candidate order — the spotlight rotation's wheel.</summary>
        private static List<string> SpotlightPool(GameDataAsset data, List<string> candidates)
        {
            var pool = new List<string>();
            foreach (var goods in candidates)
            {
                var skill = GoodsSkill(data, goods);
                if (!string.IsNullOrEmpty(skill) && !pool.Contains(skill))
                {
                    pool.Add(skill);
                }
            }

            return pool;
        }

        private static string GoodsSkill(GameDataAsset data, string goodsId)
        {
            if (data.ResourcesById.TryGetValue(goodsId, out var resource))
            {
                return resource.skill;
            }

            return FindRecipeProducing(data, goodsId)?.skill;
        }

        private static RecipeData FindRecipeProducing(GameDataAsset data, string goodsId)
        {
            if (data.recipes == null)
            {
                return null;
            }

            foreach (var recipe in data.recipes)
            {
                if (recipe.output == goodsId)
                {
                    return recipe;
                }
            }

            return null;
        }

        /// <summary>
        /// The template verse's value anchor: the mean worth of its goods
        /// slots (amount × notional value, or the authored grant for
        /// materials) — so retuning rites.json retunes the generator with it.
        /// </summary>
        private static double AverageGoodsValue(GameDataAsset data, RiteVerseData template)
        {
            var total = 0.0;
            var slots = 0;
            foreach (var slot in template.slots)
            {
                if (slot.type != RiteSlotType.Resource)
                {
                    continue;
                }

                total += slot.renownGrant > 0
                    ? slot.renownGrant
                    : (Economy.NotionalUnitValue(data, slot.resource) * slot.amount).ToDouble();
                slots++;
            }

            return slots > 0 ? total / slots : 0.0;
        }

        private static string TakeRandom(List<string> list, ref ulong seed)
        {
            var index = (int)(Rng.NextDouble(ref seed) * list.Count);
            if (index >= list.Count)
            {
                index = list.Count - 1;
            }

            var item = list[index];
            list.RemoveAt(index);
            return item;
        }
    }
}
