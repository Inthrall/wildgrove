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
        public event Action<bool> ConsentResolved;

        public bool IsRewardedReady(RewardedPlacement placement) => true;

        public void Initialise(bool adsWanted)
        {
            Debug.Log("[ads] stub initialised, ads " + (adsWanted ? "wanted" : "bought away"));

            // No consent layer in the editor, and nowhere for the answer to be
            // withheld — granted, so the fold path downstream is exercised
            // rather than skipped. Published whether ads are wanted or not, which
            // is the real layer's contract too: the answer binds the sink.
            ConsentResolved?.Invoke(true);
        }

        public void SetAdsWanted(bool adsWanted)
        {
            Debug.Log("[ads] stub ads " + (adsWanted ? "wanted" : "bought away"));
        }

        /// <summary>No SDK, so no form to re-open — the inside cover draws no row.</summary>
        public bool PrivacyOptionsAvailable => false;

        public void ShowPrivacyOptions(Action onClosed = null)
        {
            Debug.Log("[ads] stub privacy options (nothing to show)");
            onClosed?.Invoke();
        }

        public void ShowRewarded(RewardedPlacement placement, Action onReward, Action onClosed = null)
        {
            Debug.Log("[ads] stub rewarded shown placement=" + placement + " (granting immediately)");
            onReward?.Invoke();
            onClosed?.Invoke();
        }
    }
}
