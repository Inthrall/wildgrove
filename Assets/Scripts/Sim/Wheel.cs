using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Wheel (design §15): eight real-world sabbats a year, hemisphere-
    /// mirrored. Each takes the wheel at warden-local midnight on its own night
    /// and holds it until the next sabbat's, so the year is eight seasons back
    /// to back with no gap between them; the open sabbat's ambient touch leans
    /// the world a little — the world's ONE lean since the drawn region season
    /// retired (design §8, 2026-08-08).
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
    /// minted. Unconfigured data, an unset hemisphere, an unstamped cursor and
    /// a cursor outside the authored calendar all read as "no tide": absent is
    /// inert (fixtures).
    /// </para>
    /// </summary>
    public static class Wheel
    {
        public const int HemisphereUnset = 0;
        public const int HemisphereNorth = 1;
        public const int HemisphereSouth = 2;

        /// <summary>
        /// How long the calendar's LAST authored night holds the wheel, having
        /// no successor to hand it to: one turn of the eight, near enough. It
        /// is what stops the final sabbat being the one that silently never
        /// opens, and the wheel goes quiet after it rather than extrapolating
        /// dates nobody authored — the evergreen rule is about topping the
        /// calendar up (see sabbats.json), not about guessing at it.
        /// </summary>
        private const int LastNightHoldsDays = 46;

        private const long DayMs = 86400000L;
        private const long MinuteMs = 60000L;

        public static bool Configured(GameDataAsset data)
        {
            return data?.wheel?.sabbats != null && data.wheel.sabbats.Count > 0;
        }

        /// <summary>The sabbat whose tide is open right now — null only where the calendar does not reach, and whenever the Wheel is inert.</summary>
        public static SabbatData OpenTide(GameState state, GameDataAsset data)
        {
            return Cache(state, data)?.open;
        }

        /// <summary>
        /// The sabbat that takes the wheel when the open tide gives it up, and
        /// when it does so (UTC unix ms) — the open tide's own close, said the
        /// other way about. Null once the authored calendar has run out, which
        /// is the one case where a tide ends in nothing.
        /// </summary>
        public static SabbatData NextTide(GameState state, GameDataAsset data, out long takesTheWheelUnixMs)
        {
            var cache = Cache(state, data);
            takesTheWheelUnixMs = cache?.next != null ? cache.nextStartMs : 0L;
            return cache?.next;
        }

        /// <summary>The open tide's sabbat night as a days-since-epoch date, or -1 — the keeping keys its year on this.</summary>
        public static int OpenNightDay(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.openNightDay : -1;
        }

        /// <summary>When the open tide gives the wheel up (UTC unix ms) — the next sabbat's own midnight; 0 when no tide is open.</summary>
        public static long OpenTideCloseMs(GameState state, GameDataAsset data)
        {
            var cache = Cache(state, data);
            return cache?.open != null ? cache.toMs : 0L;
        }

        /// <summary>
        /// The soonest sabbat night still ahead of the cursor. Null when the
        /// Wheel is inert or the authored calendar has run out (top up
        /// sabbats.json). While a tide holds this is the sabbat taking the
        /// wheel off it — the same answer <see cref="NextTide"/> gives from the
        /// cache, reached the long way for the surfaces that ask before any
        /// tide has opened at all.
        /// </summary>
        public static SabbatData NextSabbat(GameState state, GameDataAsset data, out long nightStartUnixMs)
        {
            nightStartUnixMs = 0L;
            if (state == null || !Configured(data))
            {
                return null;
            }

            SabbatData best = null;
            var bestStart = long.MaxValue;
            foreach (var sabbat in data.wheel.sabbats)
            {
                if (NextNightOf(state, data, sabbat, out var nightStart) && nightStart < bestStart)
                {
                    best = sabbat;
                    bestStart = nightStart;
                }
            }

            nightStartUnixMs = best != null ? bestStart : 0L;
            return best;
        }

        /// <summary>
        /// ONE sabbat's next turn: the soonest of its authored nights still
        /// ahead of the cursor, which is both the night and the moment its tide
        /// opens — they are the same midnight now. False when the Wheel is
        /// inert or this sabbat's authored nights have run out while others
        /// still have theirs.
        /// <para>
        /// Uncached on purpose: <see cref="Cache"/> answers "what is open now"
        /// in O(1) for the hook sites that read it every step, and this walks
        /// the calendar for a page that is redrawn a few times a minute at
        /// worst. A second cache keyed by sabbat would be the expensive answer
        /// to the cheap question.
        /// </para>
        /// </summary>
        public static bool NextNightOf(GameState state, GameDataAsset data, SabbatData sabbat,
            out long nightStartUnixMs)
        {
            nightStartUnixMs = 0L;
            if (state == null || sabbat == null || !Configured(data)
                || state.hemisphere == HemisphereUnset || state.simNowUnixMs <= 0L)
            {
                return false;
            }

            var bestStart = long.MaxValue;
            foreach (var nightDay in NightsFor(sabbat, state.hemisphere))
            {
                var nightStart = LocalDayStartMs(nightDay, state.utcOffsetMinutes);
                if (nightStart > state.simNowUnixMs && nightStart < bestStart)
                {
                    bestStart = nightStart;
                }
            }

            if (bestStart == long.MaxValue)
            {
                return false;
            }

            nightStartUnixMs = bestStart;
            return true;
        }

        /// <summary>The open tide's yield lean on one resource — 1.0 for everything a tide doesn't name, and wherever the calendar does not reach.</summary>
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
            cache.next = null;
            cache.nextStartMs = 0L;
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

            // The latest night already fallen holds the wheel; the soonest still
            // to come is what takes it. One pass over the calendar answers both,
            // and the pair of them IS the window — a tide runs night to night.
            var now = state.simNowUnixMs;
            SabbatData holding = null;
            var holdingNightDay = -1;
            var heldFromMs = long.MinValue;
            var takenAtMs = long.MaxValue;

            foreach (var sabbat in data.wheel.sabbats)
            {
                foreach (var nightDay in NightsFor(sabbat, state.hemisphere))
                {
                    var nightMs = LocalDayStartMs(nightDay, state.utcOffsetMinutes);
                    if (nightMs <= now)
                    {
                        if (nightMs > heldFromMs)
                        {
                            holding = sabbat;
                            holdingNightDay = nightDay;
                            heldFromMs = nightMs;
                        }
                    }
                    else if (nightMs < takenAtMs)
                    {
                        cache.next = sabbat;
                        cache.nextStartMs = nightMs;
                        takenAtMs = nightMs;
                    }
                }
            }

            if (holding == null)
            {
                // The cursor sits before the calendar's first night — nothing
                // has taken the wheel yet. Hold until something does.
                cache.toMs = takenAtMs;
                return;
            }

            var givesUpMs = cache.next != null
                ? takenAtMs
                : LocalDayStartMs(holdingNightDay + LastNightHoldsDays, state.utcOffsetMinutes);
            if (givesUpMs <= now)
            {
                // Past the last authored night and past the span it holds for:
                // the wheel is quiet until the calendar is topped up, and no
                // edge is coming, so the cache never needs rebuilding again.
                cache.fromMs = givesUpMs;
                return;
            }

            cache.open = holding;
            cache.openNightDay = holdingNightDay;
            cache.fromMs = heldFromMs;
            cache.toMs = givesUpMs;
            BuildTouch(cache, holding);
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

        /// <summary>
        /// Warden-local midnight opening the Monday of the week a moment falls
        /// in, as UTC unix ms — the amber cache's week (see <see cref="Amber"/>).
        /// <para>
        /// A plain week is no business of the Wheel's, but midnight is: the
        /// tides close at the warden's own, and a week turning over at UTC's
        /// instead would run somewhere between an hour and half a day out of
        /// step with every other clock in the journal.
        /// </para>
        /// </summary>
        public static long WeekStartMs(long nowUnixMs, int utcOffsetMinutes)
        {
            var today = LocalEpochDay(nowUnixMs, utcOffsetMinutes);
            // 1970-01-01 fell on a Thursday, so the epoch's first Monday is day 4.
            var sinceMonday = ((today - 4) % 7 + 7) % 7;
            return LocalDayStartMs(today - sinceMonday, utcOffsetMinutes);
        }

        /// <summary>
        /// Warden-local midnight opening the NEXT Monday. Always strictly ahead
        /// of <paramref name="nowUnixMs"/> — asked on a Monday it answers with
        /// the one after, never with today, so a countdown drawn from it never
        /// reads zero.
        /// </summary>
        public static long NextWeekStartMs(long nowUnixMs, int utcOffsetMinutes)
        {
            // A whole week on from this week's own start, which is exact: the
            // offset is one stamped value, so both midnights share it.
            return WeekStartMs(nowUnixMs, utcOffsetMinutes) + 7L * DayMs;
        }

        /// <summary>
        /// The warden-local day a moment falls in, as days since the epoch.
        /// Floors rather than truncating: integer division rounds toward zero,
        /// which would file every local time before the epoch under the day
        /// above its own.
        /// </summary>
        private static int LocalEpochDay(long unixMs, int utcOffsetMinutes)
        {
            var local = unixMs + utcOffsetMinutes * MinuteMs;
            var day = local / DayMs;
            if (local < 0L && local % DayMs != 0L)
            {
                day--;
            }

            return (int)day;
        }
    }

    /// <summary>
    /// Cached open-tide window and its precomputed touch — see <see cref="Wheel"/>.
    /// Valid while the cursor stays inside [fromMs, toMs) under the same data,
    /// hemisphere and offset, so reads stay O(1) between one sabbat's midnight
    /// and the next's. Never saved.
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

        /// <summary>The sabbat this window ends in, and its own midnight — null once the calendar has run out.</summary>
        public SabbatData next;
        public long nextStartMs;

        public double digSpeedMult = 1.0;
        public double craftSpeedGlobal = 1.0;
        public double bubbleRewardBonus;
        public double replantCostMult = 1.0;
        public double spreadEase;
        public readonly Dictionary<string, double> yieldMultByResource = new Dictionary<string, double>();
        public readonly Dictionary<string, double> craftSpeedBySkill = new Dictionary<string, double>();
    }
}
