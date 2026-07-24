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

        /// <summary>
        /// Cooldown between rewarded Amber-drip claims. The ad-watch used to be
        /// the only throttle; once Remove Ads grants the reward with no ad, this
        /// is what keeps the drip from being tapped without limit. Tuning value —
        /// safe to adjust.
        /// </summary>
        public const long AdDripCooldownMs = 4L * 60L * 60L * 1000L;

        /// <summary>Cooldown between rewarded time-skips — same role as <see cref="AdDripCooldownMs"/> for the "Hasten a while" reward. Tuning value.</summary>
        public const long TimeSkipCooldownMs = 4L * 60L * 60L * 1000L;

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
        /// The Amber a familiar's rename asks (design §4: rename any time), or 0
        /// when the amber system is inert — a rename is free then, never blocked.
        /// </summary>
        public static double RenameCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.renameCostAmber > 0.0 ? amber.renameCostAmber : 0.0;
        }

        /// <summary>Whether a rename is affordable right now — free (cost 0), or the warden holds enough Amber for the price.</summary>
        public static bool CanRename(GameState state, GameDataAsset data)
        {
            return state != null && state.amber >= RenameCost(data);
        }

        /// <summary>
        /// Rename a familiar for its Amber price (design §4) — the arrival
        /// naming pays the same price. The cost is spent only when the name
        /// actually changes: a blank or unchanged name (e.g. keeping the
        /// suggested arrival name) is a free no-op, and it's never spent when
        /// the warden is short. Returns whether the name changed.
        /// </summary>
        public static bool TryRename(GameState state, GameDataAsset data, Familiar familiar, string name)
        {
            if (state == null || familiar == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // Re-typing the same name spends nothing — the price is for a change.
            if (name.Trim() == familiar.name)
            {
                return false;
            }

            if (!CanRename(state, data))
            {
                return false;
            }

            Roster.Rename(familiar, name);
            state.amber -= RenameCost(data);
            return true;
        }

        /// <summary>Whether the rewarded Amber drip is configured and off cooldown — gates the "Watch" button on both the ad and the ad-free (Remove Ads) paths.</summary>
        public static bool CanGrantDrip(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.adDripAmber <= 0.0)
            {
                return false;
            }

            return state.adDripClaimedUnixMs <= 0L
                || nowUnixMs - state.adDripClaimedUnixMs >= AdDripCooldownMs;
        }

        /// <summary>
        /// Credit the rewarded-ad Amber drip (design §10). The caller shows the
        /// ad and only calls this on the reward; returns the amount granted, or
        /// 0 when unconfigured or still cooling down. Stamps the claim time so
        /// the cooldown holds even when Remove Ads grants without an ad.
        /// </summary>
        public static double GrantDrip(GameState state, GameDataAsset data, long nowUnixMs)
        {
            if (!CanGrantDrip(state, data, nowUnixMs))
            {
                return 0.0;
            }

            var amber = data.economy.amber;
            state.amber += amber.adDripAmber;
            state.adDripClaimedUnixMs = nowUnixMs;
            return amber.adDripAmber;
        }

        /// <summary>Whether the rewarded time-skip is off cooldown — gates "Hasten a while" on both the ad and the ad-free paths. (The amber-paid <see cref="TryTimeSkip"/> is throttled by its own cost, not this.)</summary>
        public static bool CanRewardedTimeSkip(GameState state, long nowUnixMs)
        {
            return state.timeSkipClaimedUnixMs <= 0L
                || nowUnixMs - state.timeSkipClaimedUnixMs >= TimeSkipCooldownMs;
        }

        /// <summary>Stamp a rewarded time-skip's claim time, re-arming the cooldown. Called by the sim caller after it credits the skip.</summary>
        public static void StampRewardedTimeSkip(GameState state, long nowUnixMs)
        {
            state.timeSkipClaimedUnixMs = nowUnixMs;
        }

        /// <summary>Milliseconds until the rewarded time-skip re-arms, or 0 when it's ready now — drives the camp strip's countdown.</summary>
        public static long RewardedTimeSkipCooldownRemainingMs(GameState state, long nowUnixMs)
        {
            if (state.timeSkipClaimedUnixMs <= 0L)
            {
                return 0L;
            }

            var remaining = TimeSkipCooldownMs - (nowUnixMs - state.timeSkipClaimedUnixMs);
            return remaining > 0L ? remaining : 0L;
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
