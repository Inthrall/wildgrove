using System;
using UnityEngine;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Placeholder <see cref="IStore"/> until the Unity IAP implementation lands.
    /// Tracks remove_ads ownership in memory only, so the purchase and entitlement
    /// paths are exercisable without a store connection. Never charges anything;
    /// ownership does not survive a restart (the real store persists it).
    /// </summary>
    public sealed class StubStore : IStore
    {
        public bool IsInitialised { get; private set; }

        public bool RemoveAdsOwned { get; private set; }

        public void Initialise(Action onReady = null)
        {
            IsInitialised = true;
            Debug.Log("[store] stub initialised");
            onReady?.Invoke();
        }

        public void Purchase(string productId, Action<StoreResult> onComplete)
        {
            Debug.Log("[store] stub purchase " + productId);
            if (productId == StoreProductIds.RemoveAds)
            {
                var result = RemoveAdsOwned ? StoreResult.AlreadyOwned : StoreResult.Purchased;
                RemoveAdsOwned = true;
                onComplete?.Invoke(result);
                return;
            }

            onComplete?.Invoke(StoreResult.Failed);
        }

        public void RestorePurchases(Action onComplete = null)
        {
            Debug.Log("[store] stub restore");
            onComplete?.Invoke();
        }
    }
}
