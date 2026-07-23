using System;
using UnityEngine;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Placeholder <see cref="IAds"/> used until the AdMob (Google Mobile Ads)
    /// implementation lands: rewarded ads are always "ready" and grant their
    /// reward immediately, so the reward call sites are exercisable in the editor
    /// and in builds without the SDK. Logs each step (like UnityLogTelemetry) so
    /// the flow is visible in the console and logcat.
    /// </summary>
    public sealed class StubAds : IAds
    {
        public bool IsRewardedReady(RewardedPlacement placement) => true;

        public void Initialise()
        {
            Debug.Log("[ads] stub initialised");
        }

        public void ShowRewarded(RewardedPlacement placement, Action onReward, Action onClosed = null)
        {
            Debug.Log("[ads] stub rewarded shown placement=" + placement + " (granting immediately)");
            onReward?.Invoke();
            onClosed?.Invoke();
        }
    }
}
