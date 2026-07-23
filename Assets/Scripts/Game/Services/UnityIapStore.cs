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

        private StoreController _controller;
        private Action _onReady;
        private bool _ready;

        public event Action<string> ConsumablePurchased;

        public bool IsInitialised => _ready;

        public bool RemoveAdsOwned => IsOwned(StoreProductIds.RemoveAds);

        public bool IsOwned(string productId)
        {
            return _owned.Contains(productId);
        }

        public void Initialise(Action onReady = null)
        {
            if (_ready)
            {
                onReady?.Invoke();
                return;
            }

            // Queue the callback: v5 connection is asynchronous and callers
            // (including lazy purchase/restore retries) may arrive mid-connect.
            _onReady += onReady;
            if (_controller != null)
            {
                return;
            }

            _controller = UnityIAPServices.StoreController();
            _controller.OnStoreConnected += OnStoreConnected;
            _controller.OnStoreDisconnected += OnStoreDisconnected;
            _controller.OnProductsFetched += OnProductsFetched;
            _controller.OnProductsFetchFailed += OnProductsFetchFailed;
            _controller.OnPurchasePending += OnPurchasePending;
            _controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _controller.OnPurchaseFailed += OnPurchaseFailed;
            _controller.OnPurchasesFetched += OnPurchasesFetched;
            _controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

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
                // buy button simply reports failure until a later Initialise succeeds.
                await _controller.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError("[store] IAP connect failed: " + e.Message);
            }
        }

        private void OnStoreConnected()
        {
            var definitions = new List<ProductDefinition>();
            foreach (var productId in StoreProductIds.All)
            {
                // Amber packs are consumable (re-purchasable); ConfirmPurchase
                // consumes them on Google Play by their fetched product type,
                // while the one-off entitlements are acknowledged and kept.
                var type = StoreProductIds.IsConsumable(productId)
                    ? ProductType.Consumable
                    : ProductType.NonConsumable;
                definitions.Add(new ProductDefinition(productId, type));
            }

            _controller.FetchProducts(definitions);
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            Debug.LogError("[store] IAP disconnected: " + description?.Message);
        }

        private void OnProductsFetched(List<Product> products)
        {
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
            // ownership and finish readiness — purchases just can't be started.
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
                    if (!StoreProductIds.IsConsumable(productId))
                    {
                        _owned.Add(productId);
                    }
                }
            }

            // A purchase left unacknowledged by a previous session (e.g. the app
            // closed before ProcessPurchase) resurfaces here as pending.
            foreach (var order in orders.PendingOrders)
            {
                HandlePending(order);
            }

            FinishReady();
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
        {
            Debug.LogError("[store] IAP purchases fetch failed: " + description?.Message);
            FinishReady();
        }

        private void FinishReady()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            var callback = _onReady;
            _onReady = null;
            callback?.Invoke();
        }

        public void Purchase(string productId, Action<StoreResult> onComplete)
        {
            if (!_ready)
            {
                // Lazy connect: billing stays off the startup path until the
                // player actually initiates a purchase. If the connection fails,
                // ConnectAsync logs it and the retry simply never fires.
                Initialise(() => Purchase(productId, onComplete));
                return;
            }

            // Consumables re-buy every time; only the one-off entitlements
            // short-circuit as already owned.
            if (!StoreProductIds.IsConsumable(productId) && _owned.Contains(productId))
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
            foreach (var productId in ProductIdsOf(order.CartOrdered))
            {
                if (!StoreProductIds.IsConsumable(productId))
                {
                    _owned.Add(productId);
                }
            }

            // Acknowledge (or, for a consumable, consume) the purchase; the
            // caller's callback fires once the store confirms in OnPurchaseConfirmed.
            _controller.ConfirmPurchase(order);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            foreach (var productId in ProductIdsOf(order.CartOrdered))
            {
                if (!StoreProductIds.IsConsumable(productId))
                {
                    _owned.Add(productId);
                }

                if (_pending.ContainsKey(productId))
                {
                    // The live purchase flow: its callback grants the pile.
                    Resolve(productId, StoreResult.Purchased);
                }
                else if (StoreProductIds.IsConsumable(productId))
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

        private void Resolve(string productId, StoreResult result)
        {
            if (_pending.TryGetValue(productId, out var callback))
            {
                _pending.Remove(productId);
                callback?.Invoke(result);
            }
        }

        public void RestorePurchases(Action onComplete = null)
        {
            if (!_ready)
            {
                Initialise(() => RestorePurchases(onComplete));
                return;
            }

            // Android resolves owned non-consumables from the purchase history, so a
            // restore is just a re-read; OnPurchasesFetched refreshes the owned set.
            _controller.FetchPurchases();
            onComplete?.Invoke();
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
