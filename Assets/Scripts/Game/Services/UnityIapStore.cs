using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The real <see cref="IStore"/>, backed by Unity IAP (v5) over Google Play
    /// Billing. Owns the tiny catalogue (the one-off non-consumables in
    /// <see cref="StoreProductIds"/>), tracks entitlement from fetched purchases,
    /// and surfaces purchase outcomes as <see cref="StoreResult"/>.
    /// Selected on device by GameLoop; the editor keeps <see cref="StubStore"/>
    /// so Play mode needs no billing connection.
    /// </summary>
    public sealed class UnityIapStore : IStore
    {
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly Dictionary<string, Product> _products = new Dictionary<string, Product>();
        private readonly Dictionary<string, Action<StoreResult>> _pending = new Dictionary<string, Action<StoreResult>>();
        private readonly StoreConnection _connection = new StoreConnection();

        private StoreController _controller;
        private Action<bool> _onPurchasesFetched;
        private bool _catalogueFetched;

        public event Action<string> ConsumablePurchased;

        public event Action EntitlementsResolved;

        public Func<string, bool> RewardRedeemed { get; set; }

        public bool IsInitialised => _connection.IsConnected;

        public bool RemoveAdsOwned => IsOwned(StoreProductIds.RemoveAds);

        public bool IsOwned(string productId)
        {
            return _owned.Contains(productId);
        }

        public string PriceLabel(string productId)
        {
            return _products.TryGetValue(productId, out var product)
                ? product.metadata?.localizedPriceString
                : null;
        }

        public void Initialise(Action onReady = null)
        {
            // Only a connection that came up runs the caller's work; a failed one
            // is reported to the callers who can show it (purchase and restore)
            // and simply not acted on here.
            WhenConnected(connected =>
            {
                if (connected)
                {
                    onReady?.Invoke();
                }
            });
        }

        /// <summary>
        /// Queue work behind the billing connection and start one if none is in
        /// flight. <paramref name="resume"/> is told which way it went — the
        /// false answer is the whole point: v5 connects asynchronously, so every
        /// caller arrives mid-connect, and a caller never called back is a
        /// button that does nothing with nothing to explain it.
        /// </summary>
        private void WhenConnected(Action<bool> resume)
        {
            if (_connection.Wait(resume))
            {
                BeginConnect();
            }
        }

        private void BeginConnect()
        {
            if (_controller == null)
            {
                _controller = UnityIAPServices.StoreController();

                // Subscribed once, for the life of the store. The controller
                // outlives a failed attempt so a retry reconnects this one
                // rather than stacking a second set of handlers — which would
                // deliver every purchase callback twice.
                _controller.OnStoreConnected += OnStoreConnected;
                _controller.OnStoreDisconnected += OnStoreDisconnected;
                _controller.OnProductsFetched += OnProductsFetched;
                _controller.OnProductsFetchFailed += OnProductsFetchFailed;
                _controller.OnPurchasePending += OnPurchasePending;
                _controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _controller.OnPurchaseFailed += OnPurchaseFailed;
                _controller.OnPurchaseDeferred += OnPurchaseDeferred;
                _controller.OnPurchasesFetched += OnPurchasesFetched;
                _controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            }

            _ = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            try
            {
                // IAP v5 requires Unity Gaming Services to be initialised before the
                // store connects. Kept here (not at startup) so billing still stays
                // off the launch path — see GameLoop's lazy-init note.
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                // A missing billing connection shouldn't take the game down — the
                // buy button reports Unavailable and the next press tries again.
                await _controller.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError("[store] IAP connect failed: " + e.Message);
                ConnectionFailed();
            }
        }

        /// <summary>
        /// The connection did not come up. Release everyone queued behind it so
        /// the fault reaches the page, and leave no state behind — the next buy
        /// or restore starts a fresh attempt.
        /// </summary>
        private void ConnectionFailed()
        {
            _connection.Failed();
        }

        private void OnStoreConnected()
        {
            var definitions = new List<ProductDefinition>();
            // The union of what can be bought and what Play can award — a reward
            // product missing from here can't be resolved when its order arrives,
            // which is exactly how an ungrantable one stays unacknowledged.
            foreach (var productId in StoreCatalogue.All)
            {
                // Amber packs and repeatable rewards are consumable
                // (re-deliverable); ConfirmPurchase consumes them on Google Play
                // by their fetched product type, while the one-off entitlements
                // are acknowledged and kept.
                var type = StoreCatalogue.IsConsumable(productId)
                    ? ProductType.Consumable
                    : ProductType.NonConsumable;
                definitions.Add(new ProductDefinition(productId, type));
            }

            _controller.FetchProducts(definitions);
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            Debug.LogError("[store] IAP disconnected: " + description?.Message);

            // This fires for a connection that never came up AND for one dropped
            // mid-session. Only the first has callers waiting, and only the first
            // is released here — a mid-session drop leaves the entitlements
            // already read standing (they are still true) and any purchase begun
            // after it fails through the ordinary OnPurchaseFailed path.
            ConnectionFailed();
        }

        private void OnProductsFetched(List<Product> products)
        {
            _catalogueFetched = true;
            foreach (var product in products)
            {
                _products[product.uSku] = product;
            }

            // Owned non-consumables resolve from the purchase history.
            _controller.FetchPurchases();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogError("[store] IAP product fetch failed: " + failure?.FailureReason);

            // Entitlements are independent of product metadata, so still resolve
            // ownership and finish readiness — purchases just can't be started,
            // and say so as Unavailable rather than as a refusal.
            _controller.FetchPurchases();
        }

        private void OnPurchasesFetched(Orders orders)
        {
            foreach (var order in orders.ConfirmedOrders)
            {
                foreach (var productId in ProductIdsOf(order.CartOrdered))
                {
                    // Consumables are never owned — they were consumed on
                    // confirmation, so a lingering confirmed order isn't standing
                    // entitlement.
                    if (!StoreCatalogue.IsConsumable(productId))
                    {
                        _owned.Add(productId);
                    }
                }
            }

            // A purchase left unacknowledged by a previous session (e.g. the app
            // closed before ProcessPurchase) resurfaces here as pending — and so
            // does a Play Games Reward awarded while the game wasn't running,
            // which is the only way one ever arrives.
            foreach (var order in orders.PendingOrders)
            {
                HandlePending(order);
            }

            FinishFetch(true);
            _connection.Succeeded();

            // Ownership is now known — say so, whichever attempt got here. The
            // first successful connect also runs the callback queued behind it,
            // so the fold happens twice on that one launch; the folds are
            // additive and idempotent, and the alternative is a late connection
            // that fills this set and tells nobody.
            EntitlementsResolved?.Invoke();
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
        {
            Debug.LogError("[store] IAP purchases fetch failed: " + description?.Message);

            // The connection itself is up — it is the re-read that failed. So the
            // store is ready (a purchase can still be started) but the restore
            // caller is told plainly that nothing was answered.
            FinishFetch(false);
            _connection.Succeeded();
        }

        /// <summary>
        /// Release whoever asked for the last purchase re-read, and tell them
        /// whether it resolved. Callers wait on the resolved fetch, not the
        /// request — a restore that returned the moment FetchPurchases was
        /// *called* could never report what arrived.
        /// </summary>
        private void FinishFetch(bool answered)
        {
            var callback = _onPurchasesFetched;
            _onPurchasesFetched = null;
            callback?.Invoke(answered);
        }

        public void Purchase(string productId, Action<StoreResult> onComplete)
        {
            if (!_connection.IsConnected)
            {
                // Lazy connect: billing stays off the startup path until the
                // player actually initiates a purchase. A connection that never
                // comes up answers Unavailable — it used to answer nothing at
                // all, which left the button dead with no way to say why.
                WhenConnected(connected =>
                {
                    if (connected)
                    {
                        Purchase(productId, onComplete);
                        return;
                    }

                    onComplete?.Invoke(StoreResult.Unavailable);
                });
                return;
            }

            // Consumables re-buy every time; only the one-off entitlements
            // short-circuit as already owned.
            if (!StoreCatalogue.IsConsumable(productId) && _owned.Contains(productId))
            {
                onComplete?.Invoke(StoreResult.AlreadyOwned);
                return;
            }

            if (_pending.ContainsKey(productId))
            {
                // A purchase for this product is already in flight — a double tap,
                // or several lazy-init retries queued before the connection came
                // up. Launching a second Play flow makes Google reject it as
                // "you already own this item" (non-consumable) or risk a double
                // charge (consumable). The in-flight callback delivers the result.
                return;
            }

            if (!_catalogueFetched)
            {
                // Connected, but Play never handed over the catalogue, so there
                // is no product to start a flow with. Not a refusal — a store
                // that was never really reached.
                onComplete?.Invoke(StoreResult.Unavailable);
                return;
            }

            if (!_products.TryGetValue(productId, out var product) || !product.availableToPurchase)
            {
                onComplete?.Invoke(StoreResult.Failed);
                return;
            }

            _pending[productId] = onComplete;
            _controller.PurchaseProduct(product);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            HandlePending(order);
        }

        private void HandlePending(PendingOrder order)
        {
            var rewards = 0;
            var granted = 0;
            foreach (var productId in ProductIdsOf(order.CartOrdered))
            {
                if (!StoreCatalogue.IsConsumable(productId))
                {
                    _owned.Add(productId);
                }

                if (!RewardProductIds.IsReward(productId))
                {
                    continue;
                }

                // A Play Games Reward: grant it BEFORE acknowledging. An
                // acknowledged reward the game never landed is gone for good; an
                // unacknowledged one is refunded by Play in three days and can be
                // offered again. So an order we can't fully honour is left alone.
                rewards++;
                if (RewardRedeemed != null && RewardRedeemed(productId))
                {
                    granted++;
                }
                else
                {
                    Debug.LogError("[store] Play reward not granted, leaving it unacknowledged: " + productId);
                }
            }

            if (rewards > 0 && granted < rewards)
            {
                return;
            }

            // Acknowledge (or, for a consumable, consume) the purchase; the
            // caller's callback fires once the store confirms in OnPurchaseConfirmed.
            _controller.ConfirmPurchase(order);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            foreach (var productId in ProductIdsOf(order.CartOrdered))
            {
                if (!StoreCatalogue.IsConsumable(productId))
                {
                    _owned.Add(productId);
                }

                if (_pending.ContainsKey(productId))
                {
                    // The live purchase flow: its callback grants the pile.
                    Resolve(productId, StoreResult.Purchased);
                }
                else if (RewardProductIds.IsReward(productId))
                {
                    // A reward was granted before this confirmation was asked
                    // for — that ordering is the contract. Nothing to recover.
                }
                else if (StoreCatalogue.IsConsumable(productId))
                {
                    // A consumable confirmed with no waiting callback = a purchase
                    // whose session ended before it resolved (fetched back as
                    // pending on launch and consumed just now). The token is gone;
                    // credit the pile through the recovery hook so it isn't lost.
                    ConsumablePurchased?.Invoke(productId);
                }
            }
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var result = order.FailureReason == PurchaseFailureReason.UserCancelled
                ? StoreResult.Cancelled
                : StoreResult.Failed;

            foreach (var productId in ProductIdsOf(order.CartOrdered))
            {
                Resolve(productId, result);
            }
        }

        /// <summary>
        /// Play took the order but the payment hasn't cleared. Release whoever is
        /// waiting on it: no Confirmed and no Failed ever follows a deferred
        /// order, so without this the purchase resolves to nothing at all — the
        /// buy button stays down on "Opening the store…" and the in-flight guard
        /// in <see cref="Purchase"/> swallows every retry for the rest of the
        /// session.
        /// <para>
        /// Nothing is granted and nothing is marked owned here. When the payment
        /// clears, Play delivers the order the ordinary way — through
        /// <see cref="OnPurchasePending"/> if the game is still up, or the launch
        /// purchase fetch if it isn't — and the pile lands there.
        /// </para>
        /// </summary>
        private void OnPurchaseDeferred(DeferredOrder order)
        {
            foreach (var productId in ProductIdsOf(order.CartOrdered))
            {
                Resolve(productId, StoreResult.Deferred);
            }
        }

        private void Resolve(string productId, StoreResult result)
        {
            if (_pending.TryGetValue(productId, out var callback))
            {
                _pending.Remove(productId);
                callback?.Invoke(result);
            }
        }

        public void RestorePurchases(Action<bool> onComplete = null)
        {
            if (!_connection.IsConnected)
            {
                WhenConnected(connected =>
                {
                    if (connected)
                    {
                        RestorePurchases(onComplete);
                        return;
                    }

                    // Nothing was asked of Play. Saying so is the difference
                    // between "your purchase isn't there" and "we couldn't look".
                    onComplete?.Invoke(false);
                });
                return;
            }

            // Android resolves owned non-consumables from the purchase history, so a
            // restore is just a re-read; OnPurchasesFetched refreshes the owned set
            // and delivers any reward Play has set out since the last look.
            _onPurchasesFetched += onComplete;
            _controller.FetchPurchases();
        }

        private static IEnumerable<string> ProductIdsOf(ICart cart)
        {
            var items = cart?.Items();
            if (items == null)
            {
                yield break;
            }

            foreach (var item in items)
            {
                var productId = item?.Product?.uSku;
                if (productId != null)
                {
                    yield return productId;
                }
            }
        }
    }
}
