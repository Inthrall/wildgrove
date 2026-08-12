using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// The things the journal collects and the pages that hold them: insects,
    /// the Almanac and the tool tiers its nodes grant, the Folio's spreads, and
    /// the deep amber chain.
    /// </summary>
    public static partial class GameDataValidator
    {
        private static void ValidateInsects(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var insect in data.Insects)
            {
                if (insect.Sketches <= 0)
                {
                    issues.Add($"Insect '{insect.Id}' has non-positive sketch count");
                }

                // An awarded plate is never entered in the roll, so a draw
                // weight or a habitat on one is a claim the sketch loop will
                // silently ignore — refuse both rather than read as drawable.
                if (insect.Rewarded)
                {
                    if (insect.Rarity != 0)
                    {
                        issues.Add($"Insect '{insect.Id}' is awarded, so it is never drawn for and must have rarity 0");
                    }

                    if (insect.Habitats.Count > 0)
                    {
                        issues.Add($"Insect '{insect.Id}' is awarded, so it must hold no habitats");
                    }
                }
                else
                {
                    if (insect.Rarity <= 0 || insect.Rarity > 1)
                    {
                        issues.Add($"Insect '{insect.Id}' rarity {insect.Rarity} is outside (0, 1]");
                    }

                    if (insect.Habitats.Count == 0)
                    {
                        issues.Add($"Insect '{insect.Id}' has no habitats");
                    }
                }

                foreach (var site in insect.Habitats)
                {
                    if (!data.ZonesById.TryGetValue(site, out var zone))
                    {
                        issues.Add($"Insect '{insect.Id}' references unknown zone '{site}'");
                    }
                    else if (!zone.DigSite)
                    {
                        issues.Add($"Insect '{insect.Id}' references zone '{site}' which has no observation site");
                    }
                }

                foreach (var effect in insect.Effects)
                {
                    ValidateEffect($"Insect '{insect.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateAlmanac(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            foreach (var node in data.Almanac)
            {
                if (node.CostVerdure <= 0)
                {
                    issues.Add($"Almanac node '{node.Id}' must cost Verdure");
                }

                if (!string.IsNullOrEmpty(node.Requires) && !data.AlmanacById.ContainsKey(node.Requires))
                {
                    issues.Add($"Almanac node '{node.Id}' requires unknown node '{node.Requires}'");
                }

                foreach (var effect in node.Effects)
                {
                    ValidateEffect($"Almanac node '{node.Id}'", effect, data, resourceIds, issues, almanacNode: true);
                }

                ValidateGrantToolCoverage(data, node, issues);

                if (!node.Repeatable)
                {
                    continue;
                }

                if (node.Effects.Count == 0)
                {
                    issues.Add($"Almanac node '{node.Id}' is repeatable but grants nothing — an endless sink must pay per level");
                }

                // Upgrades.ActiveEffects pays a repeatable line by scaling its
                // effect VALUE by the levels held. That is only the same as
                // holding the effect N times for the additive bands; a
                // multiplicative type would want value^levels, so it would
                // silently compute the wrong curve rather than fail.
                foreach (var effect in node.Effects)
                {
                    if (effect.Type != EffectType.YieldBonus
                        && effect.Type != EffectType.TendingBurstBonus
                        && effect.Type != EffectType.WardenYieldBonus
                        && effect.Type != EffectType.BubbleRewardBonus
                        && effect.Type != EffectType.OfflineCapBonusHours)
                    {
                        issues.Add($"Almanac node '{node.Id}' is repeatable but grants '{effect.Type}' — a repeatable line may only grant additive effects (levels scale the value linearly)");
                    }
                }
            }

            if (data.Almanac.Any(n => n.Repeatable)
                && data.Economy?.CostGrowth != null
                && data.Economy.CostGrowth.Almanac <= 1.0)
            {
                issues.Add("costGrowth.almanac must exceed 1 — a repeatable Almanac line priced flat is an infinite bonus for a finite Verdure total");
            }

            // The requires chain must ground out — a cycle makes every node in
            // it unbuyable forever.
            foreach (var node in data.Almanac)
            {
                var visited = new HashSet<string>();
                var current = node;
                while (current != null && !string.IsNullOrEmpty(current.Requires))
                {
                    if (!visited.Add(current.Id))
                    {
                        issues.Add($"Almanac node '{node.Id}' sits in a requires cycle");
                        break;
                    }

                    data.AlmanacById.TryGetValue(current.Requires, out current);
                }
            }
        }

        /// <summary>
        /// A zone-skip grant must never outrun the tool its trail demands.
        /// The sync applies grants under the same tool gate as a purchase, so
        /// a map rung granted without its covering tool would sit inert
        /// forever — a bought node that does nothing, which is worse than a
        /// refused one. The requires chain is the guarantee: the node itself
        /// or an ancestor must grant a tool rung of at least the needed tier,
        /// and prerequisite-owned then implies tool-granted.
        /// </summary>
        private static void ValidateGrantToolCoverage(GameData data, AlmanacDef node, List<string> issues)
        {
            var tiers = data.Economy?.Tools?.Tiers;
            if (tiers == null || tiers.Count == 0)
            {
                return;
            }

            var covered = BestGrantedToolTier(data, node, tiers);
            foreach (var effect in node.Effects)
            {
                if (effect.Type != EffectType.GrantUpgrade || effect.Upgrade == null
                    || !data.UpgradesById.TryGetValue(effect.Upgrade, out var rung))
                {
                    continue;
                }

                foreach (var rungEffect in rung.Effects)
                {
                    if (rungEffect.Type != EffectType.UnlockZone || rungEffect.Zone == null
                        || !data.ZonesById.TryGetValue(rungEffect.Zone, out var zone)
                        || string.IsNullOrEmpty(zone.RequiredTool))
                    {
                        continue;
                    }

                    if (tiers.IndexOf(zone.RequiredTool) > covered)
                    {
                        issues.Add($"Almanac node '{node.Id}' grants '{effect.Upgrade}' but zone '{zone.Id}' demands {zone.RequiredTool} tools — no node in its requires chain grants a covering tool rung, so the grant would sit inert forever");
                    }
                }
            }
        }

        /// <summary>The best tool tier granted by this node or its requires ancestors, as an index into economy.tools.tiers; −1 with none.</summary>
        private static int BestGrantedToolTier(GameData data, AlmanacDef node, IList<string> tiers)
        {
            var best = -1;
            var visited = new HashSet<string>();
            var current = node;
            while (current != null && visited.Add(current.Id))
            {
                foreach (var effect in current.Effects)
                {
                    if (effect.Type == EffectType.GrantUpgrade && effect.Upgrade != null
                        && data.UpgradesById.TryGetValue(effect.Upgrade, out var rung)
                        && !string.IsNullOrEmpty(rung.ToolTier))
                    {
                        var index = tiers.IndexOf(rung.ToolTier);
                        if (index > best)
                        {
                            best = index;
                        }
                    }
                }

                current = !string.IsNullOrEmpty(current.Requires)
                          && data.AlmanacById.TryGetValue(current.Requires, out var parent)
                    ? parent
                    : null;
            }

            return best;
        }

        private static void ValidateFolio(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var gathered = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            foreach (var spread in data.Spreads)
            {
                if (spread.Entries.Count == 0)
                {
                    issues.Add($"Folio spread '{spread.Id}' has no entries");
                }

                foreach (var entry in spread.Entries)
                {
                    // Choice specimens only come from haul batches, so a spread
                    // entry must be a GATHERED resource — a crafted good could
                    // never be fixed into the Folio.
                    if (!gathered.Contains(entry))
                    {
                        issues.Add($"Folio spread '{spread.Id}' entry '{entry}' is not a gathered resource");
                    }
                }

                foreach (var effect in spread.Effects)
                {
                    ValidateEffect($"Folio spread '{spread.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateDeepAmber(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var amber = data.DeepAmber;
            if (amber == null)
            {
                // The window is optional content — absent is a coherent world.
                return;
            }

            CheckIds(amber.Pieces.Select(p => p.Id), "deep amber piece", issues);

            if (amber.Pieces.Count == 0)
            {
                issues.Add("Deep amber is configured with no pieces — a window onto nothing");
            }

            foreach (var piece in amber.Pieces)
            {
                if (string.IsNullOrWhiteSpace(piece.Name))
                {
                    issues.Add($"Deep amber piece '{piece.Id}' has no name");
                }

                // The pieces ARE the deep-past channel (§7) — a silent one is a
                // hole the chase resolves into.
                if (string.IsNullOrWhiteSpace(piece.Lore))
                {
                    issues.Add($"Deep amber piece '{piece.Id}' has no lore — the piece is the story");
                }
            }

            if (amber.FindsPerHour <= 0 || amber.PityHoursWatched <= 0)
            {
                issues.Add("Deep amber findsPerHour and pityHoursWatched must both be positive — a zero rate with no pity can never surface a piece");
            }

            if (string.IsNullOrWhiteSpace(amber.PlateName) || string.IsNullOrWhiteSpace(amber.CompletedLore))
            {
                issues.Add("Deep amber needs a plateName and completedLore — the finished set is a plate in the journal");
            }

            if (amber.Effects == null || amber.Effects.Count == 0)
            {
                issues.Add("Deep amber has no completion effects — every plate is a multiplier as well as a chapter");
            }
            else
            {
                foreach (var effect in amber.Effects)
                {
                    ValidateEffect("Deep amber", effect, data, resourceIds, issues);
                }
            }

            if (amber.Zone == null || !data.ZonesById.TryGetValue(amber.Zone, out var zone))
            {
                issues.Add($"Deep amber references unknown zone '{amber.Zone}'");
                return;
            }

            if (!zone.DigSite)
            {
                issues.Add($"Deep amber zone '{amber.Zone}' has no observation site — nothing could ever surface a piece");
            }

            // Mirrors the verse-zone rule: a window keyed to ground nothing
            // opens can never open.
            if (!UnlockableZoneIds(data).Contains(amber.Zone))
            {
                issues.Add($"Deep amber zone '{amber.Zone}' is never unlockable — no upgrade grants it, so the deep past could never surface");
            }
        }
    }
}
