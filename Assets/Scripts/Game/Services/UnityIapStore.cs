using System;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The real <see cref="IStore"/>, backed by Unity IAP over Google Play
    /// Billing. Owns the tiny catalogue (the one-off non-consumable remove_ads),
    /// tracks entitlement from the product receipt, and surfaces purchase
    /// outcomes as <see cref="StoreResult"/>. Selected on device by GameLoop;
    /// the editor keeps <see cref="StubStore"/> so Play mode needs no billing
    /// connection.
    /// </summary>
    public sealed class UnityIapStore : IStore, IStoreListener
    {
        private IStoreController _controller;
        private Action _onReady;
        private Action<StoreResult> _pending;

        public bool IsInitialised => _controller != null;

        public bool RemoveAdsOwned { get; private set; }

        public void Initialise(Action onReady = null)
        {
            _onReady = onReady;
            if (_controller != null)
            {
                onReady?.Invoke();
                return;
            }

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(StoreProductIds.RemoveAds, ProductType.NonConsumable);
            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            RefreshOwnership();
            _onReady?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, null);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            // A missing billing connection shouldn't take the game down — the
            // buy button simply reports failure until a later Initialise succeeds.
            Debug.LogError("[store] IAP init failed: " + error + " " + message);
        }

        public void Purchase(string productId, Action<StoreResult> onComplete)
        {
            if (_controller == null)
            {
                onComplete?.Invoke(StoreResult.Failed);
                return;
            }

            var product = _controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                onComplete?.Invoke(StoreResult.Failed);
                return;
            }

            if (product.hasReceipt)
            {
                if (productId == StoreProductIds.RemoveAds)
                {
                    RemoveAdsOwned = true;
                }

                onComplete?.Invoke(StoreResult.AlreadyOwned);
                return;
            }

            _pending = onComplete;
            _controller.InitiatePurchase(product);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == StoreProductIds.RemoveAds)
            {
                RemoveAdsOwned = true;
            }

            var pending = _pending;
            _pending = null;
            pending?.Invoke(StoreResult.Purchased);
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            var pending = _pending;
            _pending = null;
            pending?.Invoke(failureReason == PurchaseFailureReason.UserCancelled
                ? StoreResult.Cancelled
                : StoreResult.Failed);
        }

        public void RestorePurchases(Action onComplete = null)
        {
            // Android resolves owned non-consumables on Initialise, so a restore
            // is just a re-read of the receipts.
            RefreshOwnership();
            onComplete?.Invoke();
        }

        private void RefreshOwnership()
        {
            var product = _controller?.products?.WithID(StoreProductIds.RemoveAds);
            if (product != null && product.hasReceipt)
            {
                RemoveAdsOwned = true;
            }
        }
    }
}
