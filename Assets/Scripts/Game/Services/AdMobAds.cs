using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
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

        // A failed preload waits before trying again, doubling to a ceiling —
        // AdMob's documented backoff. Some wait is required either way: the
        // retry is issued from a poll, so without one a placement with no fill
        // would be re-requested several times a second.
        private const float FirstRetryBackoffSeconds = 8f;
        private const float MaxRetryBackoffSeconds = 300f;

        private readonly Dictionary<RewardedPlacement, LoadState> _loads = new Dictionary<RewardedPlacement, LoadState>();

        private RewardedAd _offlineBoost;
        private RewardedAd _timeSkip;
        private RewardedAd _amberDrip;
        private bool _initialised;
        private bool _adsStarted;
        private bool _sdkReady;
        private bool _adsWanted;

        public event Action<bool> ConsentResolved;

        /// <summary>
        /// A placement's preload state between attempts: whether one is in
        /// flight, the earliest the next may start, and how long to wait after
        /// the next failure.
        /// </summary>
        private sealed class LoadState
        {
            internal bool InFlight;
            internal float NextAttemptAt;
            internal float Backoff = FirstRetryBackoffSeconds;
        }

        public bool IsRewardedReady(RewardedPlacement placement)
        {
            var ad = AdFor(placement);
            if (ad != null && ad.CanShowAd())
            {
                return true;
            }

            // This poll is also the retry clock. Nothing else would re-load a
            // placement whose load failed: the buttons that ask this question are
            // the same ones gated on the answer, so ShowRewarded — the only other
            // caller that loads — is never reached, and a launch that came up
            // with no signal would cost the whole session its rewarded ads.
            RequestLoad(placement);
            return false;
        }

        public void Initialise(bool adsWanted)
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;
            _adsWanted = adsWanted;
            // SDK callbacks arrive on whatever thread the SDK is on, so every one
            // below hands its body to this executor to run on the next Unity
            // Update: reward handlers touch the simulation and UI, and the load
            // throttle reads Time. Stood up here rather than left to
            // MobileAds.Initialize (which stands it up too) because the consent
            // callbacks below run before that on a first launch — ExecuteInUpdate
            // only queues, so with no executor up their work would never run and
            // ads would never start. Making the object costs a player who wants no
            // ads one hidden GameObject and no JNI.
            MobileAdsEventExecutor.Initialize();

            // A consent answer given on an earlier launch is already held by the
            // SDK, so ads start now rather than waiting on the network round
            // trip below — the refresh still runs, and a changed answer takes
            // effect on the requests that follow it.
            if (ConsentInformation.CanRequestAds())
            {
                PublishConsent();
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
            ConsentInformation.Update(
                new ConsentRequestParameters(),
                updateError => MobileAdsEventExecutor.ExecuteInUpdate(() => OnConsentUpdated(updateError)));
        }

        /// <summary>Regional requirements are known — show the form if one is owed.</summary>
        private void OnConsentUpdated(FormError updateError)
        {
            if (updateError != null)
            {
                Debug.LogWarning("[ads] consent update failed: " + updateError.Message);
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(
                formError => MobileAdsEventExecutor.ExecuteInUpdate(() => OnConsentFormClosed(formError)));
        }

        /// <summary>The form is done with (or was never owed) — act on the answer.</summary>
        private void OnConsentFormClosed(FormError formError)
        {
            if (formError != null)
            {
                Debug.LogWarning("[ads] consent form failed: " + formError.Message);
            }

            // Published either way — a refusal is an answer, and the
            // sink it also binds has no other way to hear it.
            PublishConsent();
            if (ConsentInformation.CanRequestAds())
            {
                StartAds();
            }
            else
            {
                Debug.Log("[ads] consent withheld — no ads requested");
            }
        }

        public void SetAdsWanted(bool adsWanted)
        {
            if (_adsWanted == adsWanted)
            {
                return;
            }

            _adsWanted = adsWanted;
            if (!adsWanted)
            {
                // Nothing to unsend if the SDK is already up — this launch bought
                // the ads away, so from here it is only "request no more". The
                // next launch never wakes it at all, which is the state that
                // matters and the one the privacy page describes.
                Debug.Log("[ads] ads bought away — nothing further requested");
                return;
            }

            if (ConsentInformation.CanRequestAds())
            {
                StartAds();
            }
        }

        /// <summary>Hand the current regional verdict to whoever is listening (see <see cref="IAds.ConsentResolved"/>).</summary>
        private void PublishConsent()
        {
            ConsentResolved?.Invoke(ConsentInformation.CanRequestAds());
        }

        /// <summary>
        /// Wake the ads SDK and preload each placement. Guarded because both the
        /// cached-consent path and the freshly-gathered one can reach it on the
        /// same launch.
        /// </summary>
        private void StartAds()
        {
            if (_adsStarted || !_adsWanted)
            {
                // Not wanted means the SDK is never initialised — this is the one
                // line that keeps a payer's launch from reaching Google at all.
                return;
            }

            _adsStarted = true;
            MobileAds.Initialize(_ => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                // Only now may an ad be requested — and the retry path checks
                // this too, so a poll arriving between StartAds and here can't
                // load against an SDK that isn't up.
                _sdkReady = true;
                RequestLoad(RewardedPlacement.OfflineBoost);
                RequestLoad(RewardedPlacement.TimeSkip);
                RequestLoad(RewardedPlacement.AmberDrip);
            }));
        }

        public bool PrivacyOptionsAvailable =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions(Action onClosed = null)
        {
            ConsentForm.ShowPrivacyOptionsForm(error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (error != null)
                {
                    Debug.LogWarning("[ads] privacy options form failed: " + error.Message);
                }

                // The player may have just changed their mind — the whole point
                // of re-opening the form — so the new answer goes out too.
                PublishConsent();
                onClosed?.Invoke();
            }));
        }

        public void ShowRewarded(RewardedPlacement placement, Action onReward, Action onClosed = null)
        {
            var ad = AdFor(placement);
            if (ad == null || !ad.CanShowAd())
            {
                // Nothing loaded yet — don't reward; kick a fresh load for next time.
                RequestLoad(placement);
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
                RequestLoad(placement); // preload the next one
            }

            ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(Finish);
            // Without this, a presentation failure after Show() would strand the
            // reward flow — no reward, and the button never re-enables.
            ad.OnAdFullScreenContentFailed += _ => MobileAdsEventExecutor.ExecuteInUpdate(Finish);
            ad.Show(_ => MobileAdsEventExecutor.ExecuteInUpdate(() => onReward?.Invoke()));
        }

        /// <summary>
        /// Preload the placement, unless an attempt is already in flight or the
        /// last one failed and its backoff hasn't run out. Every load goes
        /// through here, so the throttle can't be walked around.
        /// </summary>
        private void RequestLoad(RewardedPlacement placement)
        {
            if (!_sdkReady || !_adsWanted)
            {
                // _adsWanted is re-checked here, not just at StartAds: a purchase
                // mid-session leaves an SDK that is up and a poll that would go on
                // preloading against it.
                return;
            }

            var load = LoadStateFor(placement);
            if (load.InFlight || Time.realtimeSinceStartup < load.NextAttemptAt)
            {
                return;
            }

            load.InFlight = true;
            var unit = Debug.isDebugBuild ? TestRewardedUnit : UnitFor(placement);
            RewardedAd.Load(unit, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                load.InFlight = false;
                if (error != null || ad == null)
                {
                    // No fill, or no network. Back off and let the next poll try
                    // again, so one failed load isn't the end of that placement
                    // for the session.
                    load.NextAttemptAt = Time.realtimeSinceStartup + load.Backoff;
                    Debug.LogWarning("[ads] rewarded load failed (" + placement + "), retrying in "
                                     + load.Backoff + "s: " + error);
                    load.Backoff = Mathf.Min(load.Backoff * 2f, MaxRetryBackoffSeconds);
                    return;
                }

                load.Backoff = FirstRetryBackoffSeconds;
                Store(placement, ad);
            }));
        }

        private LoadState LoadStateFor(RewardedPlacement placement)
        {
            if (!_loads.TryGetValue(placement, out var load))
            {
                load = new LoadState();
                _loads[placement] = load;
            }

            return load;
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
