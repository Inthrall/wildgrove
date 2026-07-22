using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The real <see cref="IAds"/>, backed by AdMob (Google Mobile Ads). Keeps
    /// one preloaded rewarded ad per placement; Google's sample test unit stands
    /// in for non-release builds so testing never touches live inventory.
    /// Selected on device by GameLoop; the editor keeps <see cref="StubAds"/>.
    /// </summary>
    public sealed class AdMobAds : IAds
    {
        // Google's official Android rewarded test unit — used in dev builds.
        private const string TestRewardedUnit = "ca-app-pub-3940256099942544/5224354917";

        private RewardedAd _offlineBoost;
        private RewardedAd _timeSkip;
        private bool _initialised;

        public bool IsRewardedReady =>
            (_offlineBoost != null && _offlineBoost.CanShowAd())
            || (_timeSkip != null && _timeSkip.CanShowAd());

        public void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;
            // Marshal SDK callbacks to the main thread so reward handlers can
            // touch the simulation and UI safely.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ =>
            {
                Load(RewardedPlacement.OfflineBoost);
                Load(RewardedPlacement.TimeSkip);
            });
        }

        public void ShowRewarded(RewardedPlacement placement, Action onReward, Action onClosed = null)
        {
            var ad = placement == RewardedPlacement.TimeSkip ? _timeSkip : _offlineBoost;
            if (ad == null || !ad.CanShowAd())
            {
                // Nothing loaded yet — don't reward; kick a fresh load for next time.
                Load(placement);
                onClosed?.Invoke();
                return;
            }

            ad.OnAdFullScreenContentClosed += () =>
            {
                onClosed?.Invoke();
                Load(placement); // preload the next one
            };
            ad.Show(_ => onReward?.Invoke());
        }

        private void Load(RewardedPlacement placement)
        {
            var unit = Debug.isDebugBuild ? TestRewardedUnit : UnitFor(placement);
            RewardedAd.Load(unit, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    return;
                }

                if (placement == RewardedPlacement.TimeSkip)
                {
                    _timeSkip = ad;
                }
                else
                {
                    _offlineBoost = ad;
                }
            });
        }

        private static string UnitFor(RewardedPlacement placement)
        {
            return placement == RewardedPlacement.TimeSkip ? AdUnitIds.TimeSkip : AdUnitIds.OfflineBoost;
        }
    }
}
