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
        /// <summary>One-off, non-consumable: suppresses forced ads.</summary>
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
    /// Play Games Services achievement IDs, from the console-generated
    /// games-ids.xml (the encoded IDs, not the resource names).
    /// </summary>
    public static class AchievementIds
    {
        /// <summary>"First kith" — achievement_first_kith.</summary>
        public const string FirstKith = "CggIp4me7kEQAhAC";
    }
}
