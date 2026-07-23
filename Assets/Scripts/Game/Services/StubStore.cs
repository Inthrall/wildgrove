using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Placeholder <see cref="IStore"/> for the editor and non-Android targets.
    /// Tracks ownership in memory only, so the purchase and entitlement paths
    /// are exercisable without a store connection. Never charges anything;
    /// ownership does not survive a restart (the real store persists it).
    /// </summary>
    public sealed class StubStore : IStore
    {
        private readonly HashSet<string> _owned = new HashSet<string>();

        // The stub has no persistence, so a purchase never survives to be
        // recovered on a later launch — the event exists only to satisfy IStore.
#pragma warning disable 67
        public event Action<string> ConsumablePurchased;
#pragma warning restore 67

        public bool IsInitialised { get; private set; }

        public bool RemoveAdsOwned => IsOwned(StoreProductIds.RemoveAds);

        public bool IsOwned(string productId)
        {
            return _owned.Contains(productId);
        }

        public void Initialise(Action onReady = null)
        {
            IsInitialised = true;
            Debug.Log("[store] stub initialised");
            onReady?.Invoke();
        }

        public void Purchase(string productId, Action<StoreResult> onComplete)
        {
            Debug.Log("[store] stub purchase " + productId);
            foreach (var known in StoreProductIds.All)
            {
                if (productId == known)
                {
                    // Consumables (amber packs) are bought for their effect and
                    // never owned — always a fresh Purchased so they re-buy.
                    if (StoreProductIds.IsConsumable(productId))
                    {
                        onComplete?.Invoke(StoreResult.Purchased);
                        return;
                    }

                    var result = _owned.Contains(productId) ? StoreResult.AlreadyOwned : StoreResult.Purchased;
                    _owned.Add(productId);
                    onComplete?.Invoke(result);
                    return;
                }
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
