using System;
using System.Collections;
using GoogleMobileAds.Api;
using IdleBattle.IAP;
using UnityEngine;

namespace IdleBattle.Ads
{
    /// <summary>게임 전체에서 공유하는 AdMob 진입점입니다. 모든 광고 단위는 Google 공식 테스트 ID를 사용합니다.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class AdMobService : MonoBehaviour
    {
        private const string AndroidBanner = "ca-app-pub-3940256099942544/6300978111";
        private const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        private const string AndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        private const string IosBanner = "ca-app-pub-3940256099942544/2934735716";
        private const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        private const string IosRewarded = "ca-app-pub-3940256099942544/1712485313";

        private static AdMobService instance;
        private BannerView banner;
        private InterstitialAd interstitial;
        private RewardedAd rewarded;
        private Action<bool> rewardedResult;
        private bool initialized;
        private bool initializing;

        public static AdMobService Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<AdMobService>();
                if (instance == null) instance = new GameObject(nameof(AdMobService)).AddComponent<AdMobService>();
                return instance;
            }
        }

        public bool IsInitialized => initialized;
        public bool IsInterstitialReady => interstitial != null && interstitial.CanShowAd();
        public bool IsRewardedReady => rewarded != null && rewarded.CanShowAd();
        public bool IsBannerVisible => banner != null;
        public string LastStatus { get; private set; } = "초기화 중";
        public event Action StateChanged;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            Initialize();
        }

        public void Initialize()
        {
            if (initialized || initializing) return;
            initializing = true;
            MobileAds.Initialize(_ =>
            {
                initializing = false;
                initialized = true;
                LastStatus = "테스트 광고 로딩 중";
                LoadInterstitial();
                LoadRewarded();
                StateChanged?.Invoke();
                Debug.Log("[AdMob] SDK initialized with Google test ad units.", this);
            });
        }

        public void ShowBanner()
        {
            if (RemoveAdsPurchaseController.AdsRemoved) { HideBanner(); return; }
            if (!initialized) { Initialize(); return; }
            banner?.Destroy();
            banner = new BannerView(BannerId, AdSize.Banner, AdPosition.Bottom);
            banner.OnBannerAdLoaded += () =>
            {
                LastStatus = "배너 테스트 UI 표시";
                if (Application.isEditor) StartCoroutine(RaiseEditorBanner());
                StateChanged?.Invoke();
            };
            banner.OnBannerAdLoadFailed += error => { LastStatus = "배너 로드 실패"; StateChanged?.Invoke(); Debug.LogWarning("[AdMob] Banner load failed: " + error, this); };
            banner.LoadAd(new AdRequest());
        }

        public void HideBanner()
        {
            banner?.Destroy();
            banner = null;
            LastStatus = "배너 숨김";
            StateChanged?.Invoke();
        }

        public void ShowInterstitial()
        {
            if (RemoveAdsPurchaseController.AdsRemoved) return;
            if (!IsInterstitialReady) { LastStatus = "전면 광고 로딩 중"; LoadInterstitial(); StateChanged?.Invoke(); return; }
            LastStatus = "전면 광고 테스트 UI 표시";
            interstitial.Show();
        }

        public void ShowRewarded(Action<bool> onCompleted)
        {
            if (rewardedResult != null) { onCompleted?.Invoke(false); return; }
            if (!IsRewardedReady)
            {
                LastStatus = "리워드 광고 로딩 중";
                LoadRewarded();
                StateChanged?.Invoke();
                onCompleted?.Invoke(false);
                return;
            }

            rewardedResult = onCompleted;
            LastStatus = "리워드 광고 테스트 UI 표시";
            rewarded.Show(_ => CompleteRewarded(true));
        }

        private void LoadInterstitial()
        {
            if (!initialized) return;
            interstitial?.Destroy();
            interstitial = null;
            InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { LastStatus = "전면 광고 로드 실패"; StateChanged?.Invoke(); Debug.LogWarning("[AdMob] Interstitial load failed: " + error, this); return; }
                interstitial = ad;
                ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); interstitial = null; LoadInterstitial(); };
                ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); interstitial = null; LoadInterstitial(); };
                LastStatus = "전면 광고 준비 완료";
                StateChanged?.Invoke();
            });
        }

        private void LoadRewarded()
        {
            if (!initialized) return;
            rewarded?.Destroy();
            rewarded = null;
            RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { LastStatus = "리워드 광고 로드 실패"; StateChanged?.Invoke(); Debug.LogWarning("[AdMob] Rewarded load failed: " + error, this); return; }
                rewarded = ad;
                ad.OnAdFullScreenContentClosed += () => { CompleteRewarded(false); ad.Destroy(); rewarded = null; LoadRewarded(); };
                ad.OnAdFullScreenContentFailed += _ => { CompleteRewarded(false); ad.Destroy(); rewarded = null; LoadRewarded(); };
                LastStatus = "리워드 광고 준비 완료";
                StateChanged?.Invoke();
            });
        }

        private void CompleteRewarded(bool earned)
        {
            var callback = rewardedResult;
            rewardedResult = null;
            callback?.Invoke(earned);
        }

        private IEnumerator RaiseEditorBanner()
        {
            // 플러그인의 에디터 배너 프리팹은 sortingOrder 0이라 게임 Canvas 뒤에 가려질 수 있습니다.
            yield return null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.name.StartsWith("BANNER", StringComparison.OrdinalIgnoreCase)) continue;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 31999;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            banner?.Destroy();
            interstitial?.Destroy();
            rewarded?.Destroy();
        }

        private static string BannerId => Application.platform == RuntimePlatform.IPhonePlayer ? IosBanner : AndroidBanner;
        private static string InterstitialId => Application.platform == RuntimePlatform.IPhonePlayer ? IosInterstitial : AndroidInterstitial;
        private static string RewardedId => Application.platform == RuntimePlatform.IPhonePlayer ? IosRewarded : AndroidRewarded;
    }
}
