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
    /// in the editor, the Unity IAP implementation on device — without touching
    /// the call sites. The catalogue is intentionally tiny: the one-off
    /// non-consumables in <see cref="StoreProductIds"/> (remove_ads, the
    /// starter bundle, the plain kith slot).
    /// </summary>
    public interface IStore
    {
        /// <summary>
        /// Raised when a consumable purchase is confirmed with no live purchase
        /// callback waiting for it — i.e. a purchase whose session ended before it
        /// resolved, fetched back and consumed on the next launch. The store has
        /// already consumed the Play token, so the handler MUST credit the pile or
        /// it is lost with the money. Fires on the main thread. The one-off
        /// entitlements don't need this — their ownership is read from the store.
        /// </summary>
        event Action<string> ConsumablePurchased;

        /// <summary>True once the billing connection is established and entitlements are known.</summary>
        bool IsInitialised { get; }

        /// <summary>Whether the one-off remove_ads product is owned (persisted by the store).</summary>
        bool RemoveAdsOwned { get; }

        /// <summary>Whether a one-off product is owned (persisted by the store; false until the connection resolves).</summary>
        bool IsOwned(string productId);

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
