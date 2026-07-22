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
    }

    /// <summary>In-app purchase product IDs (Google Play Console SKUs).</summary>
    public static class StoreProductIds
    {
        /// <summary>One-off, non-consumable: suppresses forced ads.</summary>
        public const string RemoveAds = "remove_ads";
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
