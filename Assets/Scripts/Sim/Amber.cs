using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Amber (design §10): the premium currency, kept generous and
    /// player-initiated. This is the in-game economy layer — the free earn
    /// (observation sites surface it, rolled in <see cref="Observation"/>), the
    /// rewarded-ad drip, the weekly Play Games cache, and the time-skip sink.
    /// IAP amber packs credit through <see cref="GrantPack"/>. Amber survives
    /// Migration ("you keep … Amber") and can never fill a verse slot — the
    /// gate is not for sale, so no such code path exists.
    /// </summary>
    public static class Amber
    {
        /// <summary>Milliseconds in the weekly-cache cooldown (design §11: one claim per week).</summary>
        public const long WeeklyCacheCooldownMs = 7L * 24L * 60L * 60L * 1000L;

        /// <summary>Unity can't serialize a null section — a zeroed sink also reads as "no amber system".</summary>
        public static bool Configured(EconomyData economy)
        {
            return economy?.amber != null && economy.amber.timeSkipCostAmber > 0.0 && economy.amber.timeSkipHours > 0.0;
        }

        public static bool CanTimeSkip(GameState state, GameDataAsset data)
        {
            return Configured(data.economy) && state.amber >= data.economy.amber.timeSkipCostAmber;
        }

        /// <summary>
        /// Spend Amber to instantly credit timeSkipHours of production at the
        /// FULL live rate — unlike offline credit there is no cap and no rate
        /// multiplier; that's what makes it worth paying for. Returns the
        /// hours credited, or 0 when refused (unconfigured or short).
        /// </summary>
        public static double TryTimeSkip(GameState state, GameDataAsset data)
        {
            if (!CanTimeSkip(state, data))
            {
                return 0.0;
            }

            var amber = data.economy.amber;
            state.amber -= amber.timeSkipCostAmber;
            Simulation.Advance(state, data, amber.timeSkipHours * 3600.0);
            return amber.timeSkipHours;
        }

        /// <summary>
        /// Credit the rewarded-ad Amber drip (design §10). The caller shows the
        /// ad and only calls this on the reward; returns the amount granted, or
        /// 0 when the drip is unconfigured.
        /// </summary>
        public static double GrantDrip(GameState state, GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.adDripAmber <= 0.0)
            {
                return 0.0;
            }

            state.amber += amber.adDripAmber;
            return amber.adDripAmber;
        }

        /// <summary>Whether the weekly Amber cache is configured and its week has elapsed since the last claim.</summary>
        public static bool CanClaimWeeklyCache(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.weeklyCacheAmber <= 0.0)
            {
                return false;
            }

            return state.weeklyCacheClaimedUnixMs <= 0L
                || nowUnixMs - state.weeklyCacheClaimedUnixMs >= WeeklyCacheCooldownMs;
        }

        /// <summary>
        /// Claim the weekly Amber cache (design §11): credit its pile and stamp
        /// the claim time so it re-arms a week later. Returns the amount granted,
        /// or 0 when unconfigured or still cooling down.
        /// </summary>
        public static double ClaimWeeklyCache(GameState state, GameDataAsset data, long nowUnixMs)
        {
            if (!CanClaimWeeklyCache(state, data, nowUnixMs))
            {
                return 0.0;
            }

            var amount = data.economy.amber.weeklyCacheAmber;
            state.amber += amount;
            state.weeklyCacheClaimedUnixMs = nowUnixMs;
            return amount;
        }

        /// <summary>
        /// Credit a purchased Amber pack (design §10 IAP). The caller resolves the
        /// pile from the store catalogue by product id; a non-positive amount is a
        /// no-op so an unconfigured pack can never mint amber. Returns the amount.
        /// </summary>
        public static double GrantPack(GameState state, double amount)
        {
            if (amount <= 0.0)
            {
                return 0.0;
            }

            state.amber += amount;
            return amount;
        }
    }
}
