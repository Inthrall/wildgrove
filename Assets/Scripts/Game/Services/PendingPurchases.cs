using System;
using System.Collections.Generic;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The purchases Google Play has been asked for and not yet answered, and
    /// the rule that every one of them IS answered.
    /// <para>
    /// A second flow for a product already in flight must not be started —
    /// Google rejects it as "you already own this item" for a non-consumable
    /// and risks a double charge for a consumable — so the in-flight set is
    /// also a gate on the buy button. Which makes an entry that is never
    /// cleared the worst kind of leak: the button stops doing anything, for the
    /// rest of the session, with nothing on the page to say why. Play resolves
    /// a live order as Confirmed, Failed or Deferred and each clears its entry,
    /// but a billing connection that falls over mid-flow may deliver none of
    /// them — which is what <see cref="ReleaseAll"/> is for.
    /// </para>
    /// <para>
    /// Lives apart from <see cref="UnityIapStore"/> for the same reason
    /// <see cref="StoreConnection"/> does: that class reaches straight into
    /// Unity IAP and cannot be built off a device. This is the part with the
    /// states in it, so this is the part with the tests.
    /// </para>
    /// </summary>
    public sealed class PendingPurchases
    {
        private readonly Dictionary<string, Action<StoreResult>> _pending =
            new Dictionary<string, Action<StoreResult>>();

        /// <summary>How many purchases are waiting on Play.</summary>
        public int Count => _pending.Count;

        /// <summary>True when a flow for this product is already in flight — do not start a second.</summary>
        public bool IsInFlight(string productId)
        {
            return productId != null && _pending.ContainsKey(productId);
        }

        /// <summary>
        /// Take the callback for a flow about to start. Refuses (false) when one
        /// is already in flight, so the caller can return without launching a
        /// second Play flow — the in-flight callback delivers the result to
        /// whoever asked first.
        /// </summary>
        public bool Begin(string productId, Action<StoreResult> onComplete)
        {
            if (productId == null || _pending.ContainsKey(productId))
            {
                return false;
            }

            _pending[productId] = onComplete;
            return true;
        }

        /// <summary>
        /// Answer the waiter for one product and clear it. No-op when nothing is
        /// waiting — a confirmation can arrive for an order this session never
        /// started (one fetched back from a previous launch), and that is not an
        /// error.
        /// </summary>
        public void Resolve(string productId, StoreResult result)
        {
            if (productId == null || !_pending.TryGetValue(productId, out var callback))
            {
                return;
            }

            _pending.Remove(productId);
            callback?.Invoke(result);
        }

        /// <summary>
        /// Answer everyone waiting on a purchase that can no longer complete,
        /// and empty the set — for a billing connection that dropped mid-flow.
        /// <see cref="StoreResult.Unavailable"/> rather than Failed: nothing was
        /// refused, the store simply stopped being reachable, and the two read
        /// very differently on the page.
        /// </summary>
        public void ReleaseAll()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            // Copied and cleared first — a released caller is entitled to press
            // buy again from its own handler, and must land in an empty set
            // rather than one being iterated (the rule StoreConnection keeps for
            // its queue, for the same reason).
            var stranded = new List<Action<StoreResult>>(_pending.Values);
            _pending.Clear();
            foreach (var callback in stranded)
            {
                callback?.Invoke(StoreResult.Unavailable);
            }
        }
    }
}
