using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// Reachability: whether the run can actually arrive at what the data
    /// gates. A fold gate past the last fold anyone reaches, a tool tier no
    /// zone grants, a rite whose verses ask for a region the chain never opens:
    /// each of these is authored data that looks fine and is quietly dead.
    /// <para>
    /// Run from <see cref="ValidateZones"/> rather than from
    /// <see cref="Validate"/>, since a zone's unlocks are what open the ground
    /// these checks then walk.
    /// </para>
    /// </summary>
    public static partial class GameDataValidator
    {
        /// <summary>
        /// The design §8 fold gate: content may wait for a fold, but it must
        /// never wait forever, and the run it waits out must still be finishable.
        ///
        /// Every rule here guards a soft-lock the sim cannot detect at runtime —
        /// a Rite with no verse in play, or a starting zone that does not exist
        /// on the first run, leaves a save that simply cannot progress.
        /// </summary>
        private static void ValidateFoldGating(GameData data, List<string> issues)
        {
            foreach (var zone in data.Zones.Where(z => z.MinMigration < 0))
            {
                issues.Add($"Zone '{zone.Id}' minMigration {zone.MinMigration} is negative");
            }

            foreach (var upgrade in data.Upgrades.Where(u => u.MinMigration < 0))
            {
                issues.Add($"Upgrade '{upgrade.Id}' minMigration {upgrade.MinMigration} is negative");
            }

            foreach (var species in data.Species.Where(s => s.MinMigration < 0))
            {
                issues.Add($"Species '{species.Id}' minMigration {species.MinMigration} is negative");
            }

            // The run has to start somewhere: the factory seeds this zone before
            // any fold has happened, so gating it would open run 1 with no trail.
            var starting = data.Zones.FirstOrDefault(z => z.Id == GameData.StartingZoneId);
            if (starting != null && starting.MinMigration > 0)
            {
                issues.Add($"Starting zone '{starting.Id}' has minMigration {starting.MinMigration} — the first run would open with no trail");
            }

            // A trail is gated in ONE place, the zone. A map rung carrying its
            // own fold as well is two numbers that must agree forever; Upgrades
            // .FoldGate takes the greater of them, so a mismatch doesn't lock
            // anything, but it does make the ladder and the Rite answer to
            // different authoring.
            foreach (var upgrade in data.Upgrades)
            {
                if (upgrade.MinMigration != 0
                    && upgrade.Effects.Any(e => e.Type == EffectType.UnlockZone && !string.IsNullOrEmpty(e.Zone)))
                {
                    issues.Add($"Upgrade '{upgrade.Id}' sets minMigration and also unlocks a zone — gate the zone instead, the map rung inherits it");
                }
            }

            // A rung that calls a familiar must not arrive before the species
            // will answer: Roster.Recruit doesn't consult the species' fold (the
            // rung's own gate is the gate), so the earlier rung would quietly
            // win and the species' gate would mean nothing.
            var speciesById = data.Species.ToDictionary(s => s.Id, s => s);
            foreach (var upgrade in data.Upgrades)
            {
                foreach (var effect in upgrade.Effects.Where(e => e.Type == EffectType.RecruitSpecies && !string.IsNullOrEmpty(e.Species)))
                {
                    if (speciesById.TryGetValue(effect.Species, out var species)
                        && species.MinMigration > upgrade.MinMigration)
                    {
                        issues.Add($"Upgrade '{upgrade.Id}' recruits '{species.Id}' at fold {upgrade.MinMigration} but that species is gated to fold {species.MinMigration}");
                    }
                }
            }

            ValidateRiteFoldReach(data, issues);
        }

        /// <summary>
        /// Every Rite must have at least one verse in play on the fold it serves,
        /// and the authored template must have one on the first run. A Rite with
        /// nothing in play can never complete, and the Rite is the Migration
        /// gate — so the fold that cannot finish its Rite is the last fold the
        /// save will ever see.
        /// </summary>
        private static void ValidateRiteFoldReach(GameData data, List<string> issues)
        {
            var rites = data.Rites?.Rites;
            if (rites == null)
            {
                return;
            }

            var gateByZone = data.Zones.ToDictionary(z => z.Id, z => z.MinMigration);
            foreach (var rite in rites)
            {
                var inPlay = rite.Verses.Count(v =>
                    gateByZone.TryGetValue(v.Zone ?? string.Empty, out var gate) && gate <= rite.Migration);
                if (rite.Verses.Count > 0 && inPlay == 0)
                {
                    issues.Add($"Rite '{rite.Id}' (migration {rite.Migration}) has no verse whose zone is open by then — the Rite could never be completed");
                }
            }

            // Runs past the authored set generate from the FIRST rite as their
            // template, so its verse list is what every later fold draws on. A
            // zone gated beyond a fold simply stands its verse aside then.
            var template = rites.FirstOrDefault();
            if (template != null && template.Verses.Count > 0
                && !template.Verses.Any(v => gateByZone.TryGetValue(v.Zone ?? string.Empty, out var gate) && gate <= 0))
            {
                issues.Add("The rite template has no verse open on the first run — run 1 could never complete its Rite");
            }
        }

        /// <summary>
        /// The design §3 tool gate: zone requiredTool and upgrade toolTier must
        /// both name real tiers, and every demanded tier must be grantable by
        /// some upgrade — an ungrantable requirement walls off the zone (and
        /// everything behind it) forever.
        /// </summary>
        private static void ValidateToolGating(GameData data, List<string> issues)
        {
            var tiers = data.Economy?.Tools?.Tiers ?? new List<string>();
            var bestGrantable = -1;

            foreach (var upgrade in data.Upgrades)
            {
                if (string.IsNullOrEmpty(upgrade.ToolTier))
                {
                    continue;
                }

                var index = tiers.IndexOf(upgrade.ToolTier);
                if (index < 0)
                {
                    issues.Add($"Upgrade '{upgrade.Id}' toolTier '{upgrade.ToolTier}' is not in economy.tools.tiers");
                }
                else if (index > bestGrantable)
                {
                    bestGrantable = index;
                }
            }

            foreach (var zone in data.Zones)
            {
                if (string.IsNullOrEmpty(zone.RequiredTool))
                {
                    continue;
                }

                var index = tiers.IndexOf(zone.RequiredTool);
                if (index < 0)
                {
                    issues.Add($"Zone '{zone.Id}' requiredTool '{zone.RequiredTool}' is not in economy.tools.tiers");
                }
                else if (index > bestGrantable)
                {
                    issues.Add($"Zone '{zone.Id}' requiredTool '{zone.RequiredTool}' can never be met — no upgrade grants that tier");
                }
            }
        }

        /// <summary>
        /// Every zone a run could ever stand in: the starting zone, whatever an
        /// upgrade's unlockZone effect grants, and — since singing a verse opens
        /// the next trail along (design §7) — the zone after each verse's own.
        /// Ground outside this set can never open, so anything keyed to it (a
        /// verse, an amber window, the final waystones) is authored into a
        /// place the player cannot reach.
        ///
        /// The fold and tool gates are deliberately NOT read here: they delay a
        /// trail, they don't deny it, and a validator that folded them in would
        /// refuse content that is merely late.
        /// </summary>
        private static HashSet<string> UnlockableZoneIds(GameData data)
        {
            var ids = new HashSet<string> { GameData.StartingZoneId };
            ids.UnionWith(data.Upgrades
                .SelectMany(upgrade => upgrade.Effects)
                .Where(effect => effect.Type == EffectType.UnlockZone && effect.Zone != null)
                .Select(effect => effect.Zone));

            if (data.Rites?.Rites == null)
            {
                return ids;
            }

            var byOrder = data.Zones.OrderBy(zone => zone.Order).ToList();
            foreach (var verse in data.Rites.Rites.SelectMany(rite => rite.Verses))
            {
                if (verse.Zone == null || !data.ZonesById.TryGetValue(verse.Zone, out var sung))
                {
                    continue;
                }

                var next = byOrder.FirstOrDefault(zone => zone.Order > sung.Order);
                if (next != null)
                {
                    ids.Add(next.Id);
                }
            }

            return ids;
        }
    }
}
