using System;

namespace Wildgrove.Game.Services
{
    /// <summary>Outcome of a purchase attempt, delivered to the caller's callback.</summary>
    public enum StoreResult
    {
        Purchased,
        AlreadyOwned,
        Cancelled,

        /// <summary>The store was reached and the purchase did not go through.</summary>
        Failed,

        /// <summary>
        /// The store was never reached at all — no billing connection, so the
        /// purchase was never started and nothing could have been charged. Kept
        /// apart from <see cref="Failed"/> because it is the one outcome the
        /// player can act on (signal, Play services, sign-in) and because it is
        /// the outcome that used to arrive as silence.
        /// </summary>
        Unavailable,
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

        /// <summary>
        /// Set by the game to receive Play Games Rewards (design §11) — items
        /// Google Play awards for a Quest or Social Challenge and delivers
        /// through the ordinary purchase flow, with no purchase of ours behind
        /// them. Called with the reward's product id BEFORE the order is
        /// acknowledged, on the main thread.
        ///
        /// Return true only once the grant has landed and been saved: the store
        /// then acknowledges the order. Return false and the order is left
        /// unacknowledged, so Play refunds the offer after three days rather
        /// than the player spending a Quest on nothing. That ordering is the
        /// whole point of a handler that answers instead of an event that doesn't.
        /// </summary>
        Func<string, bool> RewardRedeemed { get; set; }

        /// <summary>True once the billing connection is established and entitlements are known.</summary>
        bool IsInitialised { get; }

        /// <summary>Whether the one-off remove_ads product is owned (persisted by the store).</summary>
        bool RemoveAdsOwned { get; }

        /// <summary>Whether a one-off product is owned (persisted by the store; false until the connection resolves).</summary>
        bool IsOwned(string productId);

        /// <summary>
        /// The product's localized store price ("$1.99"), or null before the
        /// catalogue is fetched / for an unknown id. Every real-money line
        /// shows this so the first price a player sees is never the Play
        /// purchase dialog itself.
        /// </summary>
        string PriceLabel(string productId);

        /// <summary>
        /// Connect to the store and resolve owned products. Safe to call once at
        /// startup. <paramref name="onReady"/> runs only if the connection comes
        /// up; a failed connection is not remembered, so a later call retries.
        /// </summary>
        void Initialise(Action onReady = null);

        /// <summary>Begin a purchase; the result is delivered to <paramref name="onComplete"/>.</summary>
        void Purchase(string productId, Action<StoreResult> onComplete);

        /// <summary>
        /// Restore non-consumable entitlements (remove_ads). Store-mandated on iOS;
        /// harmless on Android where owned products resolve on Initialise. Also
        /// the "has anything arrived from Play?" re-read: a reward redeemed a
        /// moment ago lands through <see cref="RewardRedeemed"/> during this.
        /// <para>
        /// <paramref name="onComplete"/> runs once the re-read has resolved, and
        /// is told whether the store actually answered. False means nothing was
        /// asked — which must not be reported as "nothing has arrived", because
        /// a player whose entitlement is missing would read that as the store
        /// having looked and found none.
        /// </para>
        /// </summary>
        void RestorePurchases(Action<bool> onComplete = null);
    }
}
