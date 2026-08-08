using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Wheel (design §15): eight real-world sabbats a year, hemisphere-
    /// mirrored. Each opens a tide ~two weeks ahead of its night and closes at
    /// warden-local midnight after it; while a tide is open its sabbat's
    /// ambient touch leans the world a little — the world's ONE lean since the
    /// drawn region season retired (design §8, 2026-08-08).
    /// <para>
    /// Everything here is a pure function of the sim clock cursor
    /// (<see cref="GameState.simNowUnixMs"/>), the hemisphere choice and the
    /// authored calendar: no draw, nothing to reroll. The touch is deliberately
    /// NOT joined into the cached active-effect union — that cache invalidates
    /// on purchases and nothing wall-clock can bump it — so the hook sites read
    /// these live accessors instead. A per-state window cache keeps the reads
    /// O(1): it recomputes only when the cursor crosses a window edge, the
    /// hemisphere or UTC offset changes, or the data asset is swapped.
    /// </para>
    /// <para>
    /// The cursor is stamped from the ratchet (GameLoop) and advanced per
    /// sub-step through an offline catch-up (Simulation.Step), so an absence
    /// spanning a tide's edge earns the tide's rate for exactly the seconds
    /// inside it, and a wound-forward device celebrates alone — spent, not
    /// minted. Unconfigured data, an unset hemisphere, or an unstamped cursor
    /// all read as "no tide": absent is inert (fixtures).
    /// </para>
    /// </summary>
    public static class Wheel
    {
        public const int HemisphereUnset = 0;
        public const int HemisphereNorth = 1;
        public const int HemisphereSouth = 2;

        private const long DayMs = 86400000L;
        private const long MinuteMs = 60000L;

        public static bool Configured(GameDataAsset data)
        {
            return data?.wheel?.sabbats != null && data.wheel.sabbats.Count > 0;
        }

        /// <summary>The sabbat whose tide is open right now — null through the fallow weeks, and whenever the Wheel is inert.</summary>
        public static SabbatData OpenTide(GameState state, GameDataAsset data)
        {
            return Cache(state, data)?.open;
        }

        /// <summary>The open tide's sabbat night as a days-since-epoch date, or -1 — the keeping keys its year on this.</summary>
        public static int OpenNightDay(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.openNightDay : -1;
        }

        /// <summary>When the open tide closes (UTC unix ms) — the fire's own midnight; 0 when no tide is open.</summary>
        public static long OpenTideCloseMs(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.toMs : 0L;
        }

        /// <summary>
        /// The sabbat night at or ahead of the cursor — the open tide's own
        /// night while one is open, else the next to come. Null when the Wheel
        /// is inert or the authored calendar has run out (top up sabbats.json).
        /// </summary>
        public static SabbatData NextSabbat(GameState state, GameDataAsset data, out long nightStartUnixMs)
        {
            nightStartUnixMs = 0L;
            if (state == null || !Configured(data)
                || state.hemisphere == HemisphereUnset || state.simNowUnixMs <= 0L)
            {
                return null;
            }

            SabbatData best = null;
            var bestStart = long.MaxValue;
            foreach (var sabbat in data.wheel.sabbats)
            {
                foreach (var nightDay in NightsFor(sabbat, state.hemisphere))
                {
                    var nightStart = LocalDayStartMs(nightDay, state.utcOffsetMinutes);
                    var nightEnd = LocalDayStartMs(nightDay + 1, state.utcOffsetMinutes);
                    if (nightEnd > state.simNowUnixMs && nightStart < bestStart)
                    {
                        best = sabbat;
                        bestStart = nightStart;
                    }
                }
            }

            nightStartUnixMs = best != null ? bestStart : 0L;
            return best;
        }

        /// <summary>The open tide's yield lean on one resource — 1.0 for everything a tide doesn't name, and through the fallow weeks.</summary>
        public static double YieldMult(GameState state, GameDataAsset data, string resourceId)
        {
            var cache = Cache(state, data);
            if (cache?.open == null || string.IsNullOrEmpty(resourceId))
            {
                return 1.0;
            }

            return cache.yieldMultByResource.TryGetValue(resourceId, out var mult) ? mult : 1.0;
        }

        /// <summary>Samhain's lean: multiplies the observation sites' sketch walk — never the amber roll (design §9 flattened it; the tide must not reopen that door).</summary>
        public static double DigSpeedMult(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.digSpeedMult : 1.0;
        }

        /// <summary>Yule's lean: multiplies one skill's craft speed (or every skill's, when authored target-less).</summary>
        public static double CraftSpeedMult(GameState state, GameDataAsset data, string skill)
        {
            var cache = Cache(state, data);
            if (cache?.open == null)
            {
                return 1.0;
            }

            cache.craftSpeedBySkill.TryGetValue(skill ?? string.Empty, out var perSkill);
            return cache.craftSpeedGlobal * (perSkill == 0.0 ? 1.0 : perSkill);
        }

        /// <summary>Litha's lean: an additive band on the windfall bubble's flat haul, alongside the raven's trait and the Almanac's line.</summary>
        public static double BubbleRewardBonus(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.bubbleRewardBonus : 0.0;
        }

        /// <summary>Ostara's lean: scales the replant cost (below 1 = sowing weather).</summary>
        public static double ReplantCostMult(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.replantCostMult : 1.0;
        }

        /// <summary>Mabon's lean: points shaved off the Exchange's spread — the caravan keeps the day too, in its way.</summary>
        public static double SpreadEase(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.spreadEase : 0.0;
        }

        private static WheelCache Cache(GameState state, GameDataAsset data)
        {
            if (state == null || data == null)
            {
                return null;
            }

            var cache = state.wheelCache;
            if (cache == null)
            {
                cache = new WheelCache();
                state.wheelCache = cache;
            }

            if (!ReferenceEquals(cache.data, data)
                || cache.hemisphere != state.hemisphere
                || cache.utcOffsetMinutes != state.utcOffsetMinutes
                || state.simNowUnixMs < cache.fromMs
                || state.simNowUnixMs >= cache.toMs)
            {
                Rebuild(cache, state, data);
            }

            return cache;
        }

        private static void Rebuild(WheelCache cache, GameState state, GameDataAsset data)
        {
            cache.data = data;
            cache.hemisphere = state.hemisphere;
            cache.utcOffsetMinutes = state.utcOffsetMinutes;
            cache.open = null;
            cache.openNightDay = -1;
            cache.fromMs = long.MinValue;
            cache.toMs = long.MaxValue;
            cache.digSpeedMult = 1.0;
            cache.craftSpeedGlobal = 1.0;
            cache.bubbleRewardBonus = 0.0;
            cache.replantCostMult = 1.0;
            cache.spreadEase = 0.0;
            cache.yieldMultByResource.Clear();
            cache.craftSpeedBySkill.Clear();

            if (!Configured(data) || state.hemisphere == HemisphereUnset)
            {
                return;
            }

            if (state.simNowUnixMs <= 0L)
            {
                // Not stamped yet — stay inert, but recompute on the first real stamp.
                cache.toMs = 1L;
                return;
            }

            var now = state.simNowUnixMs;
            var openDays = data.wheel.openDaysBefore > 0 ? data.wheel.openDaysBefore : 14;
            var previousEdge = long.MinValue;
            var nextEdge = long.MaxValue;

            foreach (var sabbat in data.wheel.sabbats)
            {
                foreach (var nightDay in NightsFor(sabbat, state.hemisphere))
                {
                    var openMs = LocalDayStartMs(nightDay - openDays, state.utcOffsetMinutes);
                    var closeMs = LocalDayStartMs(nightDay + 1, state.utcOffsetMinutes);
                    if (now >= openMs && now < closeMs)
                    {
                        cache.open = sabbat;
                        cache.openNightDay = nightDay;
                        cache.fromMs = openMs;
                        cache.toMs = closeMs;
                        BuildTouch(cache, sabbat);
                        return;
                    }

                    if (closeMs <= now && closeMs > previousEdge)
                    {
                        previousEdge = closeMs;
                    }

                    if (openMs > now && openMs < nextEdge)
                    {
                        nextEdge = openMs;
                    }
                }
            }

            // Fallow weeks: the cache holds until the next tide opens.
            cache.fromMs = previousEdge;
            cache.toMs = nextEdge;
        }

        private static void BuildTouch(WheelCache cache, SabbatData sabbat)
        {
            if (sabbat.touch == null)
            {
                return;
            }

            foreach (var effect in sabbat.touch)
            {
                switch (effect.type)
                {
                    case EffectType.YieldMult:
                        if (!string.IsNullOrEmpty(effect.resource))
                        {
                            cache.yieldMultByResource.TryGetValue(effect.resource, out var current);
                            cache.yieldMultByResource[effect.resource] = (current == 0.0 ? 1.0 : current) * effect.value;
                        }

                        break;
                    case EffectType.DigSpeedMult:
                        cache.digSpeedMult *= effect.value;
                        break;
                    case EffectType.CraftSpeedMult:
                        if (string.IsNullOrEmpty(effect.skill))
                        {
                            cache.craftSpeedGlobal *= effect.value;
                        }
                        else
                        {
                            cache.craftSpeedBySkill.TryGetValue(effect.skill, out var current);
                            cache.craftSpeedBySkill[effect.skill] = (current == 0.0 ? 1.0 : current) * effect.value;
                        }

                        break;
                    case EffectType.BubbleRewardBonus:
                        cache.bubbleRewardBonus += effect.value;
                        break;
                    case EffectType.ReplantCostMult:
                        cache.replantCostMult *= effect.value;
                        break;
                    case EffectType.ExchangeSpreadEase:
                        cache.spreadEase += effect.value;
                        break;
                }
            }
        }

        private static List<int> NightsFor(SabbatData sabbat, int hemisphere)
        {
            return hemisphere == HemisphereSouth ? sabbat.southNightDays : sabbat.northNightDays;
        }

        /// <summary>
        /// Warden-local midnight opening the given epoch day, as UTC unix ms.
        /// The offset is the device's as last stamped — a DST transition inside
        /// a window can shift its edges by an hour, which at day-scale windows
        /// is accepted (design §15: clock games move hours, never rewards).
        /// </summary>
        private static long LocalDayStartMs(int epochDay, int utcOffsetMinutes)
        {
            return epochDay * DayMs - utcOffsetMinutes * MinuteMs;
        }
    }

    /// <summary>
    /// Cached open-tide window and its precomputed touch — see <see cref="Wheel"/>.
    /// Valid while the cursor stays inside [fromMs, toMs) under the same data,
    /// hemisphere and offset; through the fallow weeks it spans the whole gap,
    /// so reads stay O(1) between edges. Never saved.
    /// </summary>
    public sealed class WheelCache
    {
        public GameDataAsset data;
        public int hemisphere = -1;
        public int utcOffsetMinutes = int.MinValue;
        public long fromMs = long.MinValue;
        public long toMs = long.MinValue;
        public SabbatData open;
        public int openNightDay = -1;

        public double digSpeedMult = 1.0;
        public double craftSpeedGlobal = 1.0;
        public double bubbleRewardBonus;
        public double replantCostMult = 1.0;
        public double spreadEase;
        public readonly Dictionary<string, double> yieldMultByResource = new Dictionary<string, double>();
        public readonly Dictionary<string, double> craftSpeedBySkill = new Dictionary<string, double>();
    }
}
