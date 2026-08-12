using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// What the camp itself offers: planters, tinctures and the Exchange.
    /// </summary>
    public static partial class GameDataValidator
    {
        // The planter kinds the sim knows how to apply (Planters.cs) — a typo'd
        // kind would build a planter that does nothing.
        private static readonly HashSet<string> KnownPlanterKinds = new HashSet<string>
        {
            "nodeYieldMult", "digSpeedMult"
        };

        private static readonly HashSet<string> KnownPlanterTargets = new HashSet<string> { "node", "digSite" };

        private static void ValidatePlanters(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            CheckIds(data.Planters.Select(p => p.Id), "planter", issues);

            foreach (var planter in data.Planters)
            {
                if (!KnownPlanterKinds.Contains(planter.Kind))
                {
                    issues.Add($"Planter '{planter.Id}' has unknown kind '{planter.Kind}'");
                }

                if (!KnownPlanterTargets.Contains(planter.Target))
                {
                    issues.Add($"Planter '{planter.Id}' has unknown target '{planter.Target}'");
                }

                // A dig site is sketched, not gathered into a basket — a
                // capacity/yield planter on a dig site would do nothing.
                if (planter.Target == "digSite" && planter.Kind != "digSpeedMult")
                {
                    issues.Add($"Planter '{planter.Id}' targets a dig site but its kind '{planter.Kind}' only applies to gather nodes");
                }

                if (planter.Target == "node" && planter.Kind == "digSpeedMult")
                {
                    issues.Add($"Planter '{planter.Id}' targets a gather node but digSpeedMult only applies to dig sites");
                }

                if (planter.Value <= 0.0)
                {
                    issues.Add($"Planter '{planter.Id}' must have a positive value");
                }

                if (planter.Materials == null || planter.Materials.Count == 0)
                {
                    issues.Add($"Planter '{planter.Id}' has no material cost bundle");
                }
                else
                {
                    foreach (var material in planter.Materials.Keys.Where(m => !resourceIds.Contains(m)))
                    {
                        issues.Add($"Planter '{planter.Id}' material '{material}' is not gathered from any zone or produced by any recipe");
                    }

                    foreach (var material in planter.Materials.Where(kv => kv.Value <= 0))
                    {
                        issues.Add($"Planter '{planter.Id}' material '{material.Key}' amount must be positive");
                    }
                }
            }
        }

        private static void ValidateTinctures(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            CheckIds(data.Tinctures.Select(t => t.Id), "tincture", issues);

            foreach (var tincture in data.Tinctures)
            {
                // The brew IS the acquisition path — a tincture no recipe
                // produces can never reach camp stock, so its buff is dead.
                if (data.Recipes.All(r => r.Output != tincture.Id))
                {
                    issues.Add($"Tincture '{tincture.Id}' is not produced by any recipe — it could never be brewed");
                }

                if (string.IsNullOrWhiteSpace(tincture.Description))
                {
                    issues.Add($"Tincture '{tincture.Id}' has no description — the bottle says what it does");
                }

                if (tincture.DurationSec <= 0)
                {
                    issues.Add($"Tincture '{tincture.Id}' needs a positive durationSec");
                }

                if (tincture.Effects == null || tincture.Effects.Count == 0)
                {
                    issues.Add($"Tincture '{tincture.Id}' has no effects — an empty bottle");
                    continue;
                }

                foreach (var effect in tincture.Effects)
                {
                    ValidateEffect($"Tincture '{tincture.Id}'", effect, data, resourceIds, issues);
                }
            }
        }

        private static void ValidateExchange(GameData data, List<string> issues)
        {
            if (data.Exchange == null)
            {
                issues.Add("Exchange config is missing");
                return;
            }

            if (data.Exchange.Spread < 0.0 || data.Exchange.Spread >= 1.0)
            {
                // A spread ≥ 1 makes every trade return nothing; negative mints goods.
                issues.Add("Exchange spread must be in [0, 1)");
            }

            if (data.Exchange.OfferMinutes <= 0.0)
            {
                // The caravan names the deal now — with no rotation there is no
                // deal, and the whole card falls silent.
                issues.Add("Exchange offerMinutes must be positive");
            }
        }
    }
}
