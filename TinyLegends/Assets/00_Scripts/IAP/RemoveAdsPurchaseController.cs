using System.Collections.Generic;
using System.Linq;
using IdleBattle.Ads;
using IdleBattle.Audio;
using IdleBattle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.IAP
{
    /// <summary>Google Play의 remove_ads 비소모성 상품과 상점 배너 버튼을 연결합니다.</summary>
    public sealed class RemoveAdsPurchaseController : MonoBehaviour
    {
        public const string ProductId = "remove_ads";
        private const string EntitlementKey = "iap.remove_ads.owned";
        private const string ButtonPath =
            "Shop/BackGround/SafeArea/Main/Scroll View/Viewport/Content/Banner/~PriceBtn";

        private static RemoveAdsPurchaseController instance;
        private StoreController storeController;
        private Button purchaseButton;
        private TMP_Text priceLabel;
        private bool productReady;
        private bool purchasing;

        public static bool AdsRemoved => PlayerPrefs.GetInt(EntitlementKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RemoveAdsPurchaseController>();
                if (instance == null)
                    instance = new GameObject(nameof(RemoveAdsPurchaseController))
                        .AddComponent<RemoveAdsPurchaseController>();
            }

            instance.BindShopButton();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            InitializeIap();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindShopButton();

        private async void InitializeIap()
        {
#if !UNITY_ANDROID && !UNITY_EDITOR
            UpdateButton("Android 전용", false);
            return;
#else
            storeController = UnityIAPServices.StoreController();
            storeController.OnStoreConnected += OnStoreConnected;
            storeController.OnStoreDisconnected += OnStoreDisconnected;
            storeController.OnProductsFetched += OnProductsFetched;
            storeController.OnProductsFetchFailed += OnProductsFetchFailed;
            storeController.OnPurchasesFetched += OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseFailed += OnPurchaseFailed;

            try
            {
                await storeController.Connect();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[IAP] Google Play 연결 실패: " + exception.Message, this);
                UpdateButton("스토어 연결 실패", false);
            }
#endif
        }

        private void OnStoreConnected()
        {
            storeController.FetchProducts(new List<ProductDefinition>
            {
                new ProductDefinition(ProductId, ProductType.NonConsumable)
            });
        }

        private void OnProductsFetched(List<Product> products)
        {
            var product = products.FirstOrDefault(value => value.definition.id == ProductId);
            productReady = product != null && product.availableToPurchase;
            UpdateButton(productReady ? product.metadata.localizedPriceString : "구매 불가", productReady);
            storeController.FetchPurchases();
        }

        private void OnPurchasesFetched(Orders orders)
        {
            if (orders.ConfirmedOrders.Any(IsRemoveAdsOrder) || orders.PendingOrders.Any(IsRemoveAdsOrder))
                GrantEntitlement(false);
        }

        private void Purchase()
        {
            if (AdsRemoved) { PopupService.Toast("이미 광고가 제거되었습니다."); return; }
            if (!productReady || purchasing || storeController == null)
            {
                AudioManager.Play(SfxId.UiDenied);
                PopupService.Toast("상품 정보를 불러오는 중입니다.");
                return;
            }

            purchasing = true;
            UpdateButton("처리 중...", false);
            storeController.PurchaseProduct(ProductId);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            if (!IsRemoveAdsOrder(order)) return;

            // 권한을 먼저 지급한 뒤 Google Play 구매를 승인합니다. 앱이 종료되어도 재호출되어 안전합니다.
            GrantEntitlement(true);
            storeController.ConfirmPurchase(order);
        }

        private void GrantEntitlement(bool showMessage)
        {
            PlayerPrefs.SetInt(EntitlementKey, 1);
            PlayerPrefs.Save();
            purchasing = false;
            AdMobService.Instance.HideBanner();
            UpdateButton("구매 완료", false);
            if (showMessage)
            {
                AudioManager.Play(SfxId.RewardClaim);
                PopupService.Toast("광고가 제거되었습니다.");
            }
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            if (!IsRemoveAdsOrder(order)) return;
            purchasing = false;
            UpdatePriceFromStore();
            AudioManager.Play(SfxId.UiDenied);
            PopupService.Toast(order.FailureReason == PurchaseFailureReason.UserCancelled
                ? "구매를 취소했습니다."
                : "결제에 실패했습니다. 잠시 후 다시 시도해 주세요.");
            Debug.LogWarning($"[IAP] {ProductId} 구매 실패: {order.FailureReason}, {order.Details}", this);
        }

        private void BindShopButton()
        {
            if (SceneManager.GetActiveScene().name != "Main") return;
            var canvas = SceneRefs.RootCanvas;
            var target = canvas != null ? canvas.transform.Find(ButtonPath) : null;
            if (target == null) { Debug.LogWarning("[IAP] 상점 광고 제거 버튼을 찾지 못했습니다: " + ButtonPath, this); return; }

            if (purchaseButton != null) purchaseButton.onClick.RemoveListener(Purchase);
            purchaseButton = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
            if (target.TryGetComponent(out Graphic graphic))
            {
                graphic.raycastTarget = true;
                if (purchaseButton.targetGraphic == null) purchaseButton.targetGraphic = graphic;
            }
            purchaseButton.onClick.AddListener(Purchase);
            priceLabel = target.GetComponentInChildren<TMP_Text>(true);
            UpdatePriceFromStore();
        }

        private void UpdatePriceFromStore()
        {
            if (AdsRemoved) { UpdateButton("구매 완료", false); return; }
            var product = storeController?.GetProducts().FirstOrDefault(value => value.definition.id == ProductId);
            UpdateButton(product != null ? product.metadata.localizedPriceString : "불러오는 중...",
                productReady && !purchasing);
        }

        private void UpdateButton(string text, bool interactable)
        {
            if (priceLabel != null) priceLabel.text = text;
            if (purchaseButton != null) purchaseButton.interactable = interactable && !AdsRemoved;
        }

        private static bool IsRemoveAdsOrder(Order order) =>
            order.CartOrdered.Items().Any(item => item.Product.definition.id == ProductId);

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            productReady = false;
            UpdateButton("스토어 연결 실패", false);
            Debug.LogWarning("[IAP] Google Play 연결 해제: " + failure.Message, this);
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            productReady = false;
            UpdateButton("구매 불가", false);
            Debug.LogWarning("[IAP] 상품 조회 실패: " + failure.FailureReason, this);
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure) =>
            Debug.LogWarning("[IAP] 기존 구매 조회 실패: " + failure.Message, this);

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (purchaseButton != null) purchaseButton.onClick.RemoveListener(Purchase);
            if (instance == this) instance = null;
        }
    }
}
