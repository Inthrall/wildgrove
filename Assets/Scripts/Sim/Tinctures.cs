using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Tinctures (design §5, Apothecary): buff consumables brewed at the fire.
    /// Drinking one spends a single unit of plain camp stock and grants its
    /// effects for durationSec of SIM time (live or offline catch-up alike —
    /// sub-stepping ticks the clock down either way); drinking again while
    /// active adds the full duration on top of what's left — the clock
    /// stacks, the effect never does. Active effects join
    /// <see cref="Upgrades.ActiveEffects"/>, so yields, site speed, and craft
    /// speed all flow through the existing modifier plumbing. No-ops when no
    /// tinctures are authored (fixtures).
    /// </summary>
    public static class Tinctures
    {
        public static bool Configured(GameDataAsset data)
        {
            return data?.tinctures != null && data.tinctures.Count > 0;
        }

        /// <summary>The active entry for this tincture, or null.</summary>
        public static ActiveTincture ActiveOf(GameState state, string tinctureId)
        {
            foreach (var active in state.activeTinctures)
            {
                if (active.tinctureId == tinctureId)
                {
                    return active;
                }
            }

            return null;
        }

        /// <summary>Seconds this tincture's buff has left, 0 when not active.</summary>
        public static double RemainingSeconds(GameState state, string tinctureId)
        {
            var active = ActiveOf(state, tinctureId);
            return active != null && active.remainingSeconds > 0.0 ? active.remainingSeconds : 0.0;
        }

        /// <summary>A bottle in stock is all drinking asks — extending an active buff is allowed (and the point).</summary>
        public static bool CanDrink(GameState state, TinctureData tincture)
        {
            return state != null && tincture != null && state.GetResource(tincture.id) >= BigDouble.One;
        }

        /// <summary>
        /// Drink one: spend a unit of stock and add the full duration to the
        /// buff's clock, then rebuild the modifiers its effects feed.
        /// False (no change) when the shelf is bare.
        /// </summary>
        public static bool TryDrink(GameState state, GameDataAsset data, TinctureData tincture)
        {
            if (data == null || !CanDrink(state, tincture) || tincture.durationSec <= 0.0)
            {
                return false;
            }

            state.resources[tincture.id] = state.GetResource(tincture.id) - BigDouble.One;

            var active = ActiveOf(state, tincture.id);
            if (active == null)
            {
                state.activeTinctures.Add(new ActiveTincture
                {
                    tinctureId = tincture.id,
                    remainingSeconds = tincture.durationSec,
                });
            }
            else
            {
                // The clock stacks, the effect doesn't — a second bottle banks time, not depth.
                active.remainingSeconds += tincture.durationSec;
            }

            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }

        /// <summary>The live buffs' effects, joined into the run's active-effect union.</summary>
        public static IEnumerable<EffectData> ActiveEffects(GameState state, GameDataAsset data)
        {
            if (state.activeTinctures.Count == 0 || data?.TincturesById == null)
            {
                yield break;
            }

            foreach (var active in state.activeTinctures)
            {
                // An id this data version doesn't know (saved on other data)
                // sits inert rather than crashing the run.
                if (active.remainingSeconds <= 0.0
                    || !data.TincturesById.TryGetValue(active.tinctureId ?? string.Empty, out var tincture))
                {
                    continue;
                }

                foreach (var effect in tincture.effects)
                {
                    yield return effect;
                }
            }
        }

        /// <summary>
        /// Tick the buffs down by sim time and drop the spent ones — rebuilding
        /// the modifiers once when anything expired, so a lapsed tonic's yield
        /// leaves the nodes the same step it leaves the warden.
        /// </summary>
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            if (deltaSeconds <= 0.0 || state.activeTinctures.Count == 0)
            {
                return;
            }

            var expired = false;
            for (var i = state.activeTinctures.Count - 1; i >= 0; i--)
            {
                var active = state.activeTinctures[i];
                active.remainingSeconds -= deltaSeconds;
                if (active.remainingSeconds <= 0.0)
                {
                    state.activeTinctures.RemoveAt(i);
                    expired = true;
                }
            }

            if (expired)
            {
                Upgrades.RecomputeYieldMultipliers(state, data);
            }
        }
    }
}
