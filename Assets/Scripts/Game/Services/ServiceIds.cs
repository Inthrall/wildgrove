using System.Collections.Generic;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// AdMob (Google Mobile Ads) unit IDs, one rewarded unit per placement.
    /// Production IDs — the real AdMob implementation swaps in Google's sample
    /// test unit IDs for non-release builds so testing never touches live
    /// inventory. The app-level App ID lives in the Android manifest, not here.
    /// </summary>
    public static class AdUnitIds
    {
        /// <summary>Rewarded unit for doubling the welcome-back offline haul ("Login rewards").</summary>
        public const string OfflineBoost = "ca-app-pub-6903871125040514/6212222531";

        /// <summary>Rewarded unit for the time-skip reward.</summary>
        public const string TimeSkip = "ca-app-pub-6903871125040514/3141391976";

        // Reuses the time-skip unit until a dedicated Amber-drip rewarded unit is
        // created in AdMob; dev builds serve Google's test unit regardless.
        public const string AmberDrip = "ca-app-pub-6903871125040514/3141391976";
    }

    /// <summary>In-app purchase product IDs (Google Play Console SKUs). All one-off non-consumables.</summary>
    public static class StoreProductIds
    {
        /// <summary>One-off, non-consumable: the rewarded-ad rewards grant without an ad (there are no forced ads). See GameLoop.WatchRewarded.</summary>
        public const string RemoveAds = "remove_ads";

        /// <summary>The initial purchase offer (Play Level Up eligibility): opens a kith slot and grants a one-time Amber pile.</summary>
        public const string StarterBundle = "starter_bundle";

        /// <summary>Opens the last kith slot (the ladder's sixth).</summary>
        public const string KithSlot = "kith_slot";

        /// <summary>Consumable buy-amber pack — the smaller pile (design §10). Re-purchasable, never owned.</summary>
        public const string AmberPackSmall = "amber_pack_small";

        /// <summary>Consumable buy-amber pack — the larger pile (design §10). Re-purchasable, never owned.</summary>
        public const string AmberPackLarge = "amber_pack_large";

        /// <summary>The consumable amber packs — bought for their effect and immediately consumed, so ownership is never tracked (unlike the one-off entitlements).</summary>
        public static readonly string[] Consumables = { AmberPackSmall, AmberPackLarge };

        /// <summary>Every product the store initialises with — kept in one place so the catalogue and the entitlement sync can't drift.</summary>
        public static readonly string[] All = { RemoveAds, StarterBundle, KithSlot, AmberPackSmall, AmberPackLarge };

        /// <summary>Whether a product is a consumable (re-purchasable) rather than a one-off entitlement.</summary>
        public static bool IsConsumable(string productId)
        {
            foreach (var id in Consumables)
            {
                if (id == productId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Play Games Rewards product IDs (design §11) — the one-time products a
    /// Play Games Reward offer is attached to in the console. These are never
    /// bought: Google Play awards them for a Quest or a Social Challenge and
    /// delivers them through the ordinary out-of-app purchase flow, so the game
    /// receives them like any other purchase. See <see cref="RewardGrants"/> for
    /// what each one lands and the delivery contract.
    /// </summary>
    public static class RewardProductIds
    {
        /// <summary>Single-use: a fell pony walking a second haul lane (design §11). A durable entitlement — owned forever, so it re-resolves after a reinstall.</summary>
        public const string DroversHalter = "reward_drovers_halter";

        /// <summary>Repeatable: the weekly Amber cache (design §11). Consumable — Play sets one out at most weekly and each delivery is credited on arrival.</summary>
        public const string WeeklyAmberCache = "reward_weekly_amber_cache";

        /// <summary>
        /// Single-use: a plate drawn by another hand, arriving already recorded
        /// (design §11). A durable entitlement, and the second single-use reward
        /// the Sep 30 2026 guideline asks for. It replaced a cosmetic cloak that
        /// was never built: an id with no grant behind it can be catalogued but
        /// never acknowledged, so Play would refund the offer and the player
        /// would have paid a Quest for nothing. Nothing goes in <see cref="All"/>
        /// until something can land it — a test pins that.
        /// </summary>
        public const string WayfarersPlate = "reward_wayfarers_plate";

        /// <summary>Single-use rewards: durable entitlements, resolved from the store's owned set like the kith products.</summary>
        public static readonly string[] Durable = { DroversHalter, WayfarersPlate };

        /// <summary>Repeatable rewards: consumables, credited once per delivery and never owned.</summary>
        public static readonly string[] Repeatable = { WeeklyAmberCache };

        /// <summary>Every reward the game can actually grant — and only those; see the Wayfarer's Plate note.</summary>
        public static readonly string[] All = { DroversHalter, WeeklyAmberCache, WayfarersPlate };

        /// <summary>Whether a product id is a Play Games Reward this build can receive.</summary>
        public static bool IsReward(string productId)
        {
            return Contains(All, productId);
        }

        /// <summary>Whether a reward is a repeatable (consumable) offer rather than a single-use entitlement.</summary>
        public static bool IsRepeatable(string productId)
        {
            return Contains(Repeatable, productId);
        }

        private static bool Contains(string[] ids, string productId)
        {
            foreach (var id in ids)
            {
                if (id == productId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Everything the billing catalogue carries: the products a player can buy
    /// (<see cref="StoreProductIds"/>) plus the Play Games Rewards a player can
    /// be awarded (<see cref="RewardProductIds"/>). The store fetches this union
    /// — an id missing from it can't be resolved when its order arrives, which
    /// is what keeps an ungrantable reward from ever being acknowledged.
    /// </summary>
    public static class StoreCatalogue
    {
        /// <summary>Every product the store initialises with — purchasable and awarded alike.</summary>
        public static readonly string[] All = Union(StoreProductIds.All, RewardProductIds.All);

        /// <summary>
        /// Whether a catalogued product is consumable (re-deliverable) rather
        /// than a one-off entitlement. Ownership is only ever tracked for the
        /// latter, so a repeatable reward must answer true here or its second
        /// delivery would be refused as already owned.
        /// </summary>
        public static bool IsConsumable(string productId)
        {
            return StoreProductIds.IsConsumable(productId) || RewardProductIds.IsRepeatable(productId);
        }

        private static string[] Union(string[] first, string[] second)
        {
            var all = new List<string>(first);
            foreach (var id in second)
            {
                if (!all.Contains(id))
                {
                    all.Add(id);
                }
            }

            return all.ToArray();
        }
    }

    /// <summary>
    /// Play Games Services achievement IDs, from the console-generated
    /// games-ids.xml (the encoded IDs, not the resource names).
    /// </summary>
    public static class AchievementIds
    {
        /// <summary>"First kith" — achievement_first_kith.</summary>
        public const string FirstKith = "CggIp4me7kEQAhAC";
    }

    /// <summary>
    /// Play Games Services leaderboard IDs, from the console-generated
    /// games-ids.xml (the encoded IDs, not the resource names).
    /// </summary>
    public static class LeaderboardIds
    {
        /// <summary>"Renown" — leaderboard_renown. Score is log-scaled Renown (see Leaderboards.SubmitAll).</summary>
        public const string Renown = "CggIp4me7kEQAhAD";
    }
}
