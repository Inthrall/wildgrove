using System;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Which reward the player is watching a rewarded ad to earn. The seam stays
    /// reward-agnostic — it only reports that the reward was earned; GameLoop
    /// applies the actual effect (crediting a time skip, doubling the offline
    /// haul), so the ad layer never needs to know the simulation.
    /// </summary>
    public enum RewardedPlacement
    {
        /// <summary>Watch to credit an extra stretch of offline gathering.</summary>
        TimeSkip,

        /// <summary>Watch to double the pending welcome-back offline haul.</summary>
        OfflineBoost,

        /// <summary>Watch for a small Amber drip (design §10).</summary>
        AmberDrip,
    }

    /// <summary>
    /// The rewarded-ads seam. Game code offers an opt-in rewarded ad and applies
    /// the reward through this; the SDK behind it is swappable — <see cref="StubAds"/>
    /// until the AdMob (Google Mobile Ads) implementation exists, then the real
    /// SDK implements it without touching the call sites. Only rewarded ads live
    /// here (the sole ad format the design uses); the one-off remove_ads purchase
    /// (owned by <see cref="IStore"/>) makes these rewards grant without an ad —
    /// GameLoop.WatchRewarded applies that, so the seam itself stays ad-only.
    /// </summary>
    public interface IAds
    {
        /// <summary>True once a rewarded ad for this specific placement has loaded and can be shown right now. Per-placement: a button must ask about its own placement, not whether any ad is ready.</summary>
        bool IsRewardedReady(RewardedPlacement placement);

        /// <summary>Load the SDK and preload the first rewarded ad. Safe to call once at startup.</summary>
        void Initialise();

        /// <summary>
        /// Show a rewarded ad for <paramref name="placement"/>. <paramref name="onReward"/>
        /// fires only when the player earns the reward (watches to the reward point);
        /// <paramref name="onClosed"/> always fires when the ad is dismissed — after a
        /// reward or an early close — so callers can re-enable UI and the next ad preloads.
        /// </summary>
        void ShowRewarded(RewardedPlacement placement, Action onReward, Action onClosed = null);
    }
}
