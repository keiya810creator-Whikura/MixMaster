using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class PurchaseSaveData
    {
        public bool adSkip, unlimitedTools;
        public long charms;
        public List<string> deliveredTransactions = new List<string>();
    }

    [DisallowMultipleComponent]
    public sealed class PurchaseManager : MonoBehaviour
    {
        internal const string AdSkipId = "ad_skip";
        internal const string ToolsId = "unlimited_field_tools";
        internal const string SmallCharmId = "breeding_charms_5";
        internal const string LargeCharmId = "breeding_charms_50";
        internal static readonly string[] ProductIds = { AdSkipId, ToolsId, SmallCharmId, LargeCharmId };

        public static PurchaseManager Instance { get; private set; }
        private PurchaseSaveData data = new PurchaseSaveData();
        private SaveManager save;
        private StoreController store;
        private bool ready, busy, fetching;
        private bool restoredEntitlementsNeedSave;
        public event Action Changed;
        public string Status { get; private set; } = "ストアに接続中…";
        public bool AdSkip => data.adSkip;
        public bool UnlimitedTools => data.unlimitedTools;
        public long Charms => data.charms;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            save = GetComponent<SaveManager>();
        }

        private System.Collections.IEnumerator Start()
        {
            if (save == null) yield break;
            while (!save.IsInitialized) yield return null;
            if (Application.isMobilePlatform || Application.isEditor) Connect();
            else SetStatus("課金はAndroid・iOSで利用できます。");
        }

        private async void Connect()
        {
            if (busy || store != null || save == null || !save.IsInitialized) return;
            busy = true;
            SetStatus("ストアに接続中…");
            store = UnityIAPServices.StoreController();
            store.ProcessPendingOrdersOnPurchasesFetched(false);
            store.OnProductsFetched += ProductsFetched;
            store.OnProductsFetchFailed += ProductsFailed;
            store.OnStoreDisconnected += Disconnected;
            store.OnPurchasePending += Deliver;
            store.OnPurchasesFetched += RestoreOrders;
            store.OnPurchasesFetchFailed += OrdersFailed;
            store.OnPurchaseFailed += PurchaseFailed;
            store.OnPurchaseDeferred += PurchaseDeferred;
            store.OnPurchaseConfirmed += PurchaseConfirmed;
            await ConnectAndFetchProducts();
        }

        private void ProductsFetched(List<Product> _) => FetchOwnership();
        private void ProductsFailed(ProductFetchFailed _) => FailConnection("商品情報を取得できませんでした。");
        private void Disconnected(StoreConnectionFailureDescription _) => FailConnection("ストアに接続できませんでした。");
        private void OrdersFailed(PurchasesFetchFailureDescription _) => FailConnection("購入情報を取得できませんでした。");
        private void PurchaseFailed(FailedOrder _) => SetIdle("購入を完了できませんでした。");
        private void PurchaseDeferred(DeferredOrder _) => SetIdle("購入の承認を待っています。");
        private void PurchaseConfirmed(Order order) => SetIdle(order is FailedOrder
            ? "購入の確認を再試行してください。" : "購入内容を受け取りました。");

        private void FetchOwnership()
        {
            fetching = true;
            store.FetchPurchases();
        }

        public bool Owns(string id) => id == AdSkipId ? AdSkip : id == ToolsId && UnlimitedTools;
        public string LocalPrice(string id) => store?.GetProductById(id)?.metadata?.localizedPriceString;
        public bool CanBuy(string id) => ready && !busy && !Owns(id) &&
            store?.GetProductById(id)?.availableToPurchase == true;

        internal void Buy(string id)
        {
            if (!CanBuy(id)) return;
            busy = true;
            SetStatus("購入手続き中…");
            store.PurchaseProduct(id);
        }

        internal void RestorePurchases()
        {
            if (busy || (!Application.isMobilePlatform && !Application.isEditor)) return;
            if (store == null) { Connect(); return; }
            busy = true;
            SetStatus("購入情報を確認中…");
            if (!ready) { _ = ConnectAndFetchProducts(); return; }
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                // Unity IAP automatically fetches orders after Apple restoration.
                fetching = true;
                store.RestoreTransactions((ok, _) =>
                {
                    if (this != null && !ok)
                    {
                        fetching = false;
                        SetIdle("購入の復元に失敗しました。");
                    }
                });
            }
            else FetchOwnership();
        }

        private async System.Threading.Tasks.Task ConnectAndFetchProducts()
        {
            try
            {
                await store.Connect();
                if (this == null || !busy) return;
                store.FetchProductsWithNoRetries(ProductIds.Select(id => new ProductDefinition(id,
                    id == AdSkipId || id == ToolsId ? ProductType.NonConsumable : ProductType.Consumable)).ToList());
            }
            catch (Exception exception)
            {
                if (this == null) return;
                Debug.LogWarning("[Purchase] " + exception.Message, this);
                FailConnection("ストアに接続できませんでした。");
            }
        }

        private void RestoreOrders(Orders orders)
        {
            if (!fetching) return;
            fetching = false;
            bool previouslyHadAdsRemoved = AdSkip;
            bool previouslyHadTools = UnlimitedTools;
            foreach (var order in orders.ConfirmedOrders)
                foreach (var item in order.CartOrdered.Items())
                {
                    string id = item.Product.definition.id;
                    if (id == AdSkipId) data.adSkip = true;
                    if (id == ToolsId) data.unlimitedTools = true;
                }

            // An incomplete store response does not revoke a non-consumable
            // already granted and saved on this device.
            restoredEntitlementsNeedSave |= previouslyHadAdsRemoved != AdSkip ||
                previouslyHadTools != UnlimitedTools;
            if (restoredEntitlementsNeedSave)
                restoredEntitlementsNeedSave = !save.SaveGame();

            ready = true;
            SetIdle(restoredEntitlementsNeedSave
                ? "購入は復元されましたが保存できませんでした。「購入を復元」から再試行してください。"
                : "購入情報を確認しました。");
            foreach (var pending in orders.PendingOrders) Deliver(pending);
        }

        private void Deliver(PendingOrder order)
        {
            string transaction = order.Info.TransactionID;
            if (string.IsNullOrEmpty(transaction))
            {
                SetIdle("取引番号を確認できませんでした。");
                return;
            }

            if (!data.deliveredTransactions.Contains(transaction))
            {
                var items = order.CartOrdered.Items();
                if (items.Count == 0 || items.Any(item => item.Quantity <= 0))
                {
                    SetIdle("購入数量を確認できませんでした。");
                    return;
                }
                var before = Capture();
                foreach (var item in items)
                {
                    switch (item.Product.definition.id)
                    {
                        case AdSkipId: data.adSkip = true; break;
                        case ToolsId: data.unlimitedTools = true; break;
                        case SmallCharmId: data.charms = LongMath.SaturatingAdd(data.charms, 5L * item.Quantity); break;
                        case LargeCharmId: data.charms = LongMath.SaturatingAdd(data.charms, 50L * item.Quantity); break;
                        default:
                            data = before;
                            SetIdle("商品を確認できませんでした。");
                            return;
                    }
                }
                data.deliveredTransactions.Add(transaction);
                if (!save.SaveGame())
                {
                    data = before;
                    SetIdle("保存できませんでした。「購入を復元」から再試行してください。");
                    return;
                }
                restoredEntitlementsNeedSave = false;
            }
            busy = true;
            SetStatus("購入内容を確認中…");
            store.ConfirmPurchase(order);
        }

        internal bool SpendCharm()
        {
            if (data.charms <= 0L) return false;
            data.charms--;
            return true;
        }

        internal void RefundCharm() => data.charms++;

        internal PurchaseSaveData Capture() => new PurchaseSaveData
        {
            adSkip = AdSkip, unlimitedTools = UnlimitedTools, charms = Charms,
            deliveredTransactions = new List<string>(data.deliveredTransactions)
        };

        internal void Restore(PurchaseSaveData restored)
        {
            data = restored ?? new PurchaseSaveData();
            restoredEntitlementsNeedSave = false;
            data.charms = Math.Max(0L, data.charms);
            if (data.deliveredTransactions == null) data.deliveredTransactions = new List<string>();
            Changed?.Invoke();
        }

        private void FailConnection(string message)
        {
            ready = fetching = false;
            SetIdle(message);
        }

        private void SetIdle(string message)
        {
            busy = false;
            SetStatus(message);
        }

        private void SetStatus(string message)
        {
            Status = message;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (store == null) return;
            store.OnProductsFetched -= ProductsFetched;
            store.OnProductsFetchFailed -= ProductsFailed;
            store.OnStoreDisconnected -= Disconnected;
            store.OnPurchasePending -= Deliver;
            store.OnPurchasesFetched -= RestoreOrders;
            store.OnPurchasesFetchFailed -= OrdersFailed;
            store.OnPurchaseFailed -= PurchaseFailed;
            store.OnPurchaseDeferred -= PurchaseDeferred;
            store.OnPurchaseConfirmed -= PurchaseConfirmed;
        }
    }
}
