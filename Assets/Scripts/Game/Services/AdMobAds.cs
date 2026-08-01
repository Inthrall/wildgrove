using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
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
        private bool _adsStarted;

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

            // A consent answer given on an earlier launch is already held by the
            // SDK, so ads start now rather than waiting on the network round
            // trip below — the refresh still runs, and a changed answer takes
            // effect on the requests that follow it.
            if (ConsentInformation.CanRequestAds())
            {
                StartAds();
            }

            GatherConsent();
        }

        /// <summary>
        /// Ask Google's UMP layer what this player's region requires, and show
        /// the consent form when one is required and unanswered. Consent is
        /// gathered before ads are requested (Google's stated order); where no
        /// form is required — most of the world — every step here resolves
        /// immediately and <see cref="StartAds"/> runs on the same launch.
        /// <para>
        /// A failure at either step is logged and then ignored on purpose:
        /// an offline first launch must still reach <see cref="StartAds"/> if
        /// the SDK says ads may be requested, or a player with no signal would
        /// lose the rewarded ads that pay for their skips.
        /// </para>
        /// </summary>
        private void GatherConsent()
        {
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning("[ads] consent update failed: " + updateError.Message);
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                    {
                        Debug.LogWarning("[ads] consent form failed: " + formError.Message);
                    }

                    if (ConsentInformation.CanRequestAds())
                    {
                        StartAds();
                    }
                    else
                    {
                        Debug.Log("[ads] consent withheld — no ads requested");
                    }
                });
            });
        }

        /// <summary>
        /// Wake the ads SDK and preload each placement. Guarded because both the
        /// cached-consent path and the freshly-gathered one can reach it on the
        /// same launch.
        /// </summary>
        private void StartAds()
        {
            if (_adsStarted)
            {
                return;
            }

            _adsStarted = true;
            MobileAds.Initialize(_ =>
            {
                Load(RewardedPlacement.OfflineBoost);
                Load(RewardedPlacement.TimeSkip);
                Load(RewardedPlacement.AmberDrip);
            });
        }

        public bool PrivacyOptionsAvailable =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions(Action onClosed = null)
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null)
                {
                    Debug.LogWarning("[ads] privacy options form failed: " + error.Message);
                }

                onClosed?.Invoke();
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
