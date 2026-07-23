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
        private RewardedAd _amberDrip;
        private bool _initialised;

        public bool IsRewardedReady(RewardedPlacement placement)
        {
            var ad = AdFor(placement);
            return ad != null && ad.CanShowAd();
        }

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
                Load(RewardedPlacement.AmberDrip);
            });
        }

        public void ShowRewarded(RewardedPlacement placement, Action onReward, Action onClosed = null)
        {
            var ad = AdFor(placement);
            if (ad == null || !ad.CanShowAd())
            {
                // Nothing loaded yet — don't reward; kick a fresh load for next time.
                Load(placement);
                onClosed?.Invoke();
                return;
            }

            // Close and present-failure are mutually exclusive in the SDK, but
            // guard anyway so onClosed (which re-enables the button and preloads)
            // fires exactly once however the ad ends.
            var finished = false;
            void Finish()
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                onClosed?.Invoke();
                Load(placement); // preload the next one
            }

            ad.OnAdFullScreenContentClosed += Finish;
            // Without this, a presentation failure after Show() would strand the
            // reward flow — no reward, and the button never re-enables.
            ad.OnAdFullScreenContentFailed += _ => Finish();
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

                Store(placement, ad);
            });
        }

        private RewardedAd AdFor(RewardedPlacement placement)
        {
            switch (placement)
            {
                case RewardedPlacement.TimeSkip:
                    return _timeSkip;
                case RewardedPlacement.AmberDrip:
                    return _amberDrip;
                default:
                    return _offlineBoost;
            }
        }

        private void Store(RewardedPlacement placement, RewardedAd ad)
        {
            switch (placement)
            {
                case RewardedPlacement.TimeSkip:
                    _timeSkip = ad;
                    break;
                case RewardedPlacement.AmberDrip:
                    _amberDrip = ad;
                    break;
                default:
                    _offlineBoost = ad;
                    break;
            }
        }

        private static string UnitFor(RewardedPlacement placement)
        {
            switch (placement)
            {
                case RewardedPlacement.TimeSkip:
                    return AdUnitIds.TimeSkip;
                case RewardedPlacement.AmberDrip:
                    return AdUnitIds.AmberDrip;
                default:
                    return AdUnitIds.OfflineBoost;
            }
        }
    }
}
