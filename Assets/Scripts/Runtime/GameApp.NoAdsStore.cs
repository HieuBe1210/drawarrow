using UnityEngine;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Purchasing;
#endif

namespace DrawArrow
{
    public sealed partial class GameApp
    {
#if UNITY_IOS
        const string NoAdsProductId = "arrow_no_ads_bundle";
#else
        const string NoAdsProductId = "no_ads_bundle";
#endif
        bool noAdsOfferOpen;
        bool noAdsLoading, noAdsBuying, noAdsDeferred;
        string noAdsPrice = "", noAdsStoreMessage = "";
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        StoreController noAdsStore;
        Product noAdsProduct;
        bool restoringNoAds;
        string restoreNoAdsMessage = "";
#if UNITY_IOS
        const string PurchaseStoreId = AppleAppStore.Name;
#else
        const string PurchaseStoreId = GooglePlay.Name;
#endif
#endif
        static bool SupportsPurchaseRestore => Application.isMobilePlatform && !Application.isEditor;

        void DrawRestorePurchases(Rect rect)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !noAdsLoading && !noAdsBuying;
            if (Ink.Button(rect, restoringNoAds ? "Restoring…" : "Restore Purchases",
                CircuitStyle.SoftControl, CircuitStyle.ControlInk, 34))
            {
                restoringNoAds = true;
                restoreNoAdsMessage = "";
                ConnectNoAdsStore();
            }
            GUI.enabled = enabled;
            if (restoreNoAdsMessage.Length > 0)
                Ink.TextAt(new Rect(rect.x, rect.yMax + 4, rect.width, 46), restoreNoAdsMessage,
                    25, FG, TextAnchor.MiddleCenter);
#endif
        }

        bool CanBuyNoAds => !ads.NoAds && !noAdsLoading && !noAdsBuying && !noAdsDeferred && noAdsPrice.Length > 0;

        void OpenNoAdsOffer()
        {
            noAdsOfferOpen = true;
            ads.StoreOpen = true;
            if (!noAdsBuying && !noAdsDeferred) LoadNoAdsOffer();
        }

        void CloseNoAdsOffer()
        {
            noAdsOfferOpen = false;
            ads.StoreOpen = false;
        }

        void DrawNoAdsStoreIcon(Rect rect)
        {
            var matrix = Ink.BeginButtonVisual(rect);
            var center = rect.center;
            Ink.Card(rect, CircuitStyle.SoftControl, 24);
            float size = Mathf.Min(rect.width, rect.height);
            Ink.TextAt(rect, "ADS", Mathf.RoundToInt(size * .34f), CircuitStyle.ControlInk,
                TextAnchor.MiddleCenter, true);
            var slash = new Vector2(size * .34f, size * .27f);
            Ink.Line(center - slash, center + slash, size * .08f, Ink.Red);
            GUI.matrix = matrix;
            if (Ink.HitButton(rect, new GUIContent(string.Empty, "No Ads"), GUIStyle.none)) OpenNoAdsOffer();
        }

        void DrawNoAdsOffer()
        {
            Ink.Rect(new Rect(0, 0, Width, Height), new Color(0, 0, 0, .45f));
            Ink.Card(new Rect(143, 574, 794, 590), CircuitStyle.Panel, 36);
            Ink.TextAt(new Rect(200, 614, 680, 90), "No Ads", 62, FG, TextAnchor.MiddleCenter, true);
            Ink.TextAt(new Rect(195, 712, 690, 56), "Remove automatic ads", 37, FG, TextAnchor.MiddleCenter);
            Ink.TextAt(new Rect(195, 774, 690, 48), "Rewarded ads stay available", 30, FG, TextAnchor.MiddleCenter);
            Ink.TextAt(new Rect(195, 830, 690, 68), "+3 Hints", 42, CircuitStyle.Accent, TextAnchor.MiddleCenter, true);
            string status = ads.NoAds ? "Purchased" : noAdsDeferred ? "Waiting for payment approval" :
                noAdsBuying ? "Waiting for the store…" : noAdsLoading ? "Loading price…" : noAdsStoreMessage;
            Ink.TextAt(new Rect(195, 903, 690, 54), status, 27, FG, TextAnchor.MiddleCenter);
            if (Ink.Button(new Rect(207, 996, 258, 82), "Close", CircuitStyle.Background, FG, 34))
                CloseNoAdsOffer();
            else if (CanBuyNoAds && Ink.Button(new Rect(484, 996, 389, 82), noAdsPrice, CircuitStyle.Accent, Color.white, 36))
                BuyNoAds();
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            else if (!ads.NoAds && !noAdsLoading && !noAdsBuying && !CanBuyNoAds &&
                Ink.Button(new Rect(484, 996, 389, 82), noAdsDeferred ? "Check status" : "Retry",
                    CircuitStyle.SoftControl, FG, 34))
                LoadNoAdsOffer();
#endif
        }

        void LoadNoAdsOffer()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            ConnectNoAdsStore();
#else
            noAdsStoreMessage = "Available on mobile";
#endif
        }

        void BuyNoAds()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!CanBuyNoAds) return;
            noAdsBuying = true;
            noAdsStoreMessage = "";
            noAdsStore.PurchaseProduct(noAdsProduct);
#endif
        }

        void DetachNoAdsStore()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (noAdsStore == null) return;
            noAdsStore.OnStoreDisconnected -= OnNoAdsStoreDisconnected;
            noAdsStore.OnProductsFetched -= OnNoAdsProductsFetched;
            noAdsStore.OnProductsFetchFailed -= OnNoAdsProductsFailed;
            noAdsStore.OnPurchasesFetched -= OnNoAdsPurchasesFetched;
            noAdsStore.OnPurchasesFetchFailed -= OnNoAdsPurchasesFailed;
            noAdsStore.OnPurchasePending -= OnNoAdsPurchasePending;
            noAdsStore.OnPurchaseFailed -= OnNoAdsPurchaseFailed;
            noAdsStore.OnPurchaseDeferred -= OnNoAdsPurchaseDeferred;
            noAdsStore.OnPurchaseConfirmed -= OnNoAdsPurchaseConfirmed;
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        async void ConnectNoAdsStore()
        {
            if (noAdsLoading || noAdsBuying) return;
            noAdsLoading = true;
            noAdsPrice = "";
            noAdsStoreMessage = "";
            if (noAdsStore == null)
            {
                noAdsStore = UnityIAPServices.StoreController(PurchaseStoreId);
                noAdsStore.ProcessPendingOrdersOnPurchasesFetched(false);
                noAdsStore.OnStoreDisconnected += OnNoAdsStoreDisconnected;
                noAdsStore.OnProductsFetched += OnNoAdsProductsFetched;
                noAdsStore.OnProductsFetchFailed += OnNoAdsProductsFailed;
                noAdsStore.OnPurchasesFetched += OnNoAdsPurchasesFetched;
                noAdsStore.OnPurchasesFetchFailed += OnNoAdsPurchasesFailed;
                noAdsStore.OnPurchasePending += OnNoAdsPurchasePending;
                noAdsStore.OnPurchaseFailed += OnNoAdsPurchaseFailed;
                noAdsStore.OnPurchaseDeferred += OnNoAdsPurchaseDeferred;
                noAdsStore.OnPurchaseConfirmed += OnNoAdsPurchaseConfirmed;
            }
            try
            {
                await noAdsStore.Connect();
                if (this && noAdsLoading)
                    noAdsStore.FetchProductsWithNoRetries(new List<ProductDefinition>
                    {
                        new ProductDefinition(NoAdsProductId, ProductType.NonConsumable)
                    });
            }
            catch (Exception error)
            {
                if (!this) return;
                Debug.LogWarning("Store connection failed: " + error.Message);
                NoAdsStoreUnavailable();
            }
        }

        void OnNoAdsStoreDisconnected(StoreConnectionFailureDescription failure) => NoAdsStoreUnavailable();
        void OnNoAdsProductsFailed(ProductFetchFailed failure) => NoAdsStoreUnavailable();
        void OnNoAdsPurchasesFailed(PurchasesFetchFailureDescription failure) => NoAdsStoreUnavailable();

        void NoAdsStoreUnavailable()
        {
            noAdsLoading = noAdsBuying = false;
            noAdsPrice = "";
            noAdsStoreMessage = "The store is unavailable. Please retry.";
            if (restoringNoAds)
            {
                restoringNoAds = false;
                restoreNoAdsMessage = noAdsStoreMessage;
            }
        }

        void OnNoAdsProductsFetched(List<Product> products)
        {
            noAdsProduct = products.Find(product => product.definition.id == NoAdsProductId);
            if (noAdsProduct == null)
            {
                NoAdsStoreUnavailable();
                return;
            }
#if UNITY_IOS
            if (restoringNoAds)
            {
                noAdsStore.RestoreTransactions((success, error) =>
                {
                    if (!this || !restoringNoAds) return;
                    if (success) noAdsStore.FetchPurchases();
                    else
                    {
                        noAdsLoading = restoringNoAds = false;
                        restoreNoAdsMessage = "Could not restore purchases. Please retry.";
                    }
                });
                return;
            }
#endif
            noAdsStore.FetchPurchases();
        }

        void OnNoAdsPurchasesFetched(Orders orders)
        {
            noAdsLoading = false;
            noAdsDeferred = orders.DeferredOrders.Any(IsNoAdsOrder);
            bool owned = orders.ConfirmedOrders.Any(IsNoAdsOrder) || orders.PendingOrders.Any(IsNoAdsOrder);
            if (owned) GrantNoAdsBundle();
            else SetNoAdsEntitlement(false);
            if (restoringNoAds)
            {
                restoringNoAds = false;
                restoreNoAdsMessage = owned ? "Purchases restored." : "No purchases found.";
            }
            foreach (var pending in orders.PendingOrders)
                if (IsNoAdsOrder(pending)) noAdsStore.ConfirmPurchase(pending);

            if (noAdsProduct.availableToPurchase)
                noAdsPrice = noAdsProduct.metadata.localizedPriceString ?? "";
            if (noAdsPrice.Length == 0)
                noAdsStoreMessage = "This offer is unavailable in the store.";
        }

        static bool IsNoAdsOrder(Order order) =>
            order.CartOrdered.Items().Any(item => item.Product.definition.id == NoAdsProductId);

        void OnNoAdsPurchasePending(PendingOrder order)
        {
            if (!IsNoAdsOrder(order)) return;
            noAdsBuying = noAdsDeferred = false;
            GrantNoAdsBundle();
            noAdsStore.ConfirmPurchase(order);
        }

        void OnNoAdsPurchaseDeferred(DeferredOrder order)
        {
            if (!IsNoAdsOrder(order)) return;
            noAdsBuying = false;
            noAdsDeferred = true;
        }

        void OnNoAdsPurchaseFailed(FailedOrder order)
        {
            noAdsBuying = false;
            noAdsStoreMessage = order.FailureReason == PurchaseFailureReason.UserCancelled
                ? "Purchase canceled." : "Purchase failed. Please try again.";
            if (order.FailureReason == PurchaseFailureReason.DuplicateTransaction)
                LoadNoAdsOffer();
        }

        void OnNoAdsPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failure)
                Debug.LogWarning("The store did not acknowledge the No Ads purchase: " + failure.FailureReason);
        }

        void GrantNoAdsBundle()
        {
            if (!Progress.noAdsBundleHintsGranted)
            {
                Progress.freeHints += 3;
                Progress.noAdsBundleHintsGranted = true;
                SaveStore.Save(Progress);
            }
            SetNoAdsEntitlement(true);
        }
#endif
    }
}
