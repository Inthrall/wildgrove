using System;

namespace Wildgrove.Game.Services
{
    /// <summary>Outcome of a purchase attempt, delivered to the caller's callback.</summary>
    public enum StoreResult
    {
        Purchased,
        AlreadyOwned,
        Cancelled,
        Failed,
    }

    /// <summary>
    /// The in-app purchase seam. Game code queries entitlements and starts
    /// purchases through this; the billing backend is swappable — <see cref="StubStore"/>
    /// until the Unity IAP implementation exists, then the real store implements
    /// it without touching the call sites. The catalogue is intentionally tiny:
    /// the one-off non-consumable remove_ads (see <see cref="StoreProductIds"/>).
    /// </summary>
    public interface IStore
    {
        /// <summary>True once the billing connection is established and entitlements are known.</summary>
        bool IsInitialised { get; }

        /// <summary>Whether the one-off remove_ads product is owned (persisted by the store).</summary>
        bool RemoveAdsOwned { get; }

        /// <summary>Connect to the store and resolve owned products. Safe to call once at startup.</summary>
        void Initialise(Action onReady = null);

        /// <summary>Begin a purchase; the result is delivered to <paramref name="onComplete"/>.</summary>
        void Purchase(string productId, Action<StoreResult> onComplete);

        /// <summary>
        /// Restore non-consumable entitlements (remove_ads). Store-mandated on iOS;
        /// harmless on Android where owned products resolve on Initialise.
        /// </summary>
        void RestorePurchases(Action onComplete = null);
    }
}
