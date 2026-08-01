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

        public event Action EntitlementsResolved;

        public Func<string, bool> RewardRedeemed { get; set; }

        public bool IsInitialised { get; private set; }

        /// <summary>
        /// Stand in for Google Play awarding a reward, so the whole delivery path
        /// — grant, confirmation sheet, acknowledgement — is exercisable in the
        /// editor and in tests. Mirrors the real store: the handler grants first
        /// and only a true answer "acknowledges" the order (here, marks a durable
        /// reward owned). Returns whether the reward was accepted.
        /// </summary>
        public bool DeliverReward(string productId)
        {
            if (RewardRedeemed == null || !RewardRedeemed(productId))
            {
                Debug.LogWarning("[store] stub reward not granted, left unacknowledged: " + productId);
                return false;
            }

            if (!RewardProductIds.IsRepeatable(productId))
            {
                _owned.Add(productId);
            }

            return true;
        }

        public bool RemoveAdsOwned => IsOwned(StoreProductIds.RemoveAds);

        public bool IsOwned(string productId)
        {
            return _owned.Contains(productId);
        }

        public string PriceLabel(string productId)
        {
            // No catalogue in the editor — real-money lines simply omit the
            // price tail, same as a device before the fetch resolves.
            return null;
        }

        public void Initialise(Action onReady = null)
        {
            IsInitialised = true;
            Debug.Log("[store] stub initialised");
            onReady?.Invoke();

            // Nothing here can fail to connect, so this is the stub's one moment
            // of "ownership is known" — raised so the editor exercises the same
            // fold path a device takes on a late connection.
            EntitlementsResolved?.Invoke();
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

        public void RestorePurchases(Action<bool> onComplete = null)
        {
            Debug.Log("[store] stub restore");

            // The stub has no store to be out of reach of, so the re-read always
            // answers — the false path is the real store's alone.
            onComplete?.Invoke(true);
        }
    }
}
