using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdleBattle.Audio;
using IdleBattle.Ads;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// Main 씬의 오프라인 보상(OfflineReward) 판넬을 <see cref="OfflineRewardService"/>에 연결합니다.
    ///
    /// 판넬은 기본으로 꺼져 있어 거기에 컴포넌트를 붙이면 Awake가 돌지 않기 때문에,
    /// 우편함과 같이 항상 켜져 있는 자기 오브젝트에서 판넬을 밖에서 묶습니다.
    ///
    /// 보상 칸은 씬에 있는 첫 번째 슬롯을 틀로 삼아 복제합니다. 디자인을 바꾸면 그대로 따라갑니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineRewardController : MonoBehaviour
    {
        private const string MainSceneName = "Main";
        private const string PanelName = "OfflineReward";
        private const float FadeDuration = 0.2f;

        private OfflineRewardService service;
        private RectTransform panel;
        private CanvasGroup panelGroup;
        private RectTransform content;
        private GameObject slotTemplate;
        private TMP_Text timeText;
        private TMP_Text capText;
        private Sprite goldIcon;
        private Button claimButton;
        private Button doubleButton;
        private Button closeButton;
        private Button dimButton;

        private readonly List<SlotView> slots = new();
        private OfflineRewardService.Reward pending;
        private Coroutine fadeRoutine;
        private bool claiming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToCurrentScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToCurrentScene();

        private static void AttachToCurrentScene()
        {
            if (SceneManager.GetActiveScene().name != MainSceneName) return;
            if (FindFirstObjectByType<OfflineRewardController>(FindObjectsInactive.Include) != null) return;

            new GameObject(nameof(OfflineRewardController)).AddComponent<OfflineRewardController>();
        }

        private void Awake()
        {
            service = OfflineRewardService.Instance;
            SceneRefs.Register(this);
            if (BindView()) HideImmediate();
        }

        private void Start()
        {
            _ = EvaluateAsync();
        }

        private void OnDestroy()
        {
            if (claimButton != null) claimButton.onClick.RemoveListener(OnClaimClicked);
            if (doubleButton != null) doubleButton.onClick.RemoveListener(OnDoubleClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnClaimClicked);
            if (dimButton != null) dimButton.onClick.RemoveListener(OnClaimClicked);
        }

        // ------------------------------------------------------------------
        // 연결
        // ------------------------------------------------------------------

        private bool BindView()
        {
            var canvas = SceneRefs.RootCanvas;
            panel = canvas != null ? canvas.transform.Find(PanelName) as RectTransform : null;
            if (panel == null)
            {
                Debug.LogWarning("[Offline] Canvas 아래에서 'OfflineReward' 판넬을 찾지 못했습니다.", this);
                return false;
            }

            panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var window = panel.Find("SafeArea/Window");
            content = window != null ? window.Find("Rewards/Scroll View/Viewport/Content") as RectTransform : null;
            if (window == null || content == null || content.childCount == 0)
            {
                Debug.LogWarning("[Offline] Rewards/Scroll View/Viewport/Content 또는 보상 칸 틀을 찾지 못했습니다.", this);
                panel = null;
                return false;
            }

            slotTemplate = content.GetChild(0).gameObject;
            slotTemplate.SetActive(false);
            // 씬에 미리 깔아 둔 나머지 칸은 필요한 만큼만 다시 켭니다.
            for (var i = 1; i < content.childCount; i++)
                slots.Add(new SlotView(content.GetChild(i).gameObject));

            timeText = window.Find("TimeCard/Value")?.GetComponent<TMP_Text>();
            capText = window.Find("Cap")?.GetComponent<TMP_Text>();

            // 골드 칸 아이콘은 판넬이 이미 들고 있는 보상 일러스트를 그대로 씁니다.
            var art = window.Find("Art")?.GetComponent<Image>();
            goldIcon = art != null ? art.sprite : null;

            claimButton = EnsureButton(window.Find("ClaimBtn"));
            doubleButton = EnsureButton(window.Find("DoubleBtn"));
            closeButton = EnsureButton(window.Find("CloseBtn"));
            dimButton = EnsureButton(panel.Find("Dim"));

            // 닫기도 "받기"와 같게 둡니다. 판넬을 닫았다고 쌓인 보상이 날아가면 안 됩니다.
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnClaimClicked);
            if (dimButton != null) dimButton.onClick.AddListener(OnClaimClicked);
            if (doubleButton != null) doubleButton.onClick.AddListener(OnDoubleClicked);

            if (capText != null)
                capText.text = $"보상은 최대 {(int)OfflineRewardService.MaxAccrual.TotalHours}시간까지 쌓입니다.";

            return true;
        }

        private static Button EnsureButton(Transform target)
        {
            if (target == null) return null;
            if (!target.TryGetComponent(out Button button))
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            if (target.TryGetComponent(out Image graphic))
            {
                graphic.raycastTarget = true;
                if (button.targetGraphic == null) button.targetGraphic = graphic;
            }

            return button;
        }

        // ------------------------------------------------------------------
        // 흐름
        // ------------------------------------------------------------------

        private async Task EvaluateAsync()
        {
            // 계산이 끝나야 하트비트가 돌기 시작합니다. 여기서 그냥 포기하면 접속 시각이 멈춰 있어
            // 다음 접속 때 "노는 동안"까지 오프라인으로 쳐 버리므로, 몇 번은 다시 시도합니다.
            var reward = await EvaluateWithRetryAsync(3);
            if (this == null || reward == null) return;

            if (panel == null)
            {
                // 판넬이 없어도 쌓인 보상을 잃지 않게 그냥 넣어 줍니다.
                await service.ClaimAsync(reward);
                if (this != null)
                    PopupService.Toast($"오프라인 보상 골드 {reward.Gold:N0}");
                return;
            }

            pending = reward;
            Show(reward);
        }

        /// <summary>
        /// 서버 시각 조회가 실패하면 잠깐 쉬었다 다시 시도합니다.
        /// 끝까지 실패하면 접속 시각을 건드리지 않고 넘어갑니다. 보상을 지우는 것보다 낫습니다.
        /// </summary>
        private async Task<OfflineRewardService.Reward> EvaluateWithRetryAsync(int attempts)
        {
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    return await service.EvaluateAsync();
                }
                catch (Exception exception)
                {
                    if (this == null) return null;
                    if (attempt == attempts)
                    {
                        Debug.LogError($"[Offline] 서버 시각을 {attempts}번 시도했지만 읽지 못했습니다: {exception.Message}", this);
                        return null;
                    }

                    Debug.LogWarning($"[Offline] 서버 시각 조회 실패({attempt}/{attempts}), 다시 시도합니다: {exception.Message}", this);
                    await Task.Delay(TimeSpan.FromSeconds(3d * attempt));
                }
            }

            return null;
        }

        private void Show(OfflineRewardService.Reward reward)
        {
            if (timeText != null)
                timeText.text = "오프라인 시간  " + OfflineRewardService.Format(reward.Offline);

            if (capText != null && reward.IsCapped)
                capText.text = $"{(int)OfflineRewardService.MaxAccrual.TotalHours}시간까지만 쌓여 여기서 멈췄습니다.";

            BindSlots(reward);

            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            Fade(1f);
            UiSfxBinder.Bind(panel);
            AudioManager.Play(SfxId.UiPanelOpen);
            if (content.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void BindSlots(OfflineRewardService.Reward reward)
        {
            var needed = (reward.Gold > 0L ? 1 : 0) + reward.Items.Count;
            EnsureSlotCount(needed);

            var index = 0;
            if (reward.Gold > 0L)
                slots[index++].Bind(goldIcon, null, reward.Gold.ToString("N0"));

            var catalog = service.CatalogForDisplay;
            foreach (var entry in reward.Items)
            {
                var frame = catalog != null ? catalog.GetRarityFrame(entry.Item.Rarity) : null;
                slots[index++].Bind(entry.Item.Icon, frame, "x" + entry.Amount.ToString("N0"));
            }

            for (; index < slots.Count; index++) slots[index].Hide();
        }

        private void EnsureSlotCount(int count)
        {
            while (slots.Count < count)
            {
                var clone = Instantiate(slotTemplate, content, false);
                clone.name = $"Reward_{slots.Count + 1:00}";
                slots.Add(new SlotView(clone));
            }
        }

        private void OnClaimClicked()
        {
            _ = ClaimAsync();
        }

        private async Task ClaimAsync()
        {
            if (claiming || pending == null) return;
            claiming = true;
            SetButtonsInteractable(false);

            var reward = pending;
            pending = null;
            try
            {
                await service.ClaimAsync(reward);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (this == null) return;
            claiming = false;
            SetButtonsInteractable(true);
            AudioManager.Play(SfxId.RewardClaim);
            Hide();
            PopupService.Toast($"오프라인 {OfflineRewardService.Describe(reward.Offline)} 보상을 받았습니다.");
        }

        private void OnDoubleClicked()
        {
            if (claiming || pending == null) return;
            claiming = true;
            SetButtonsInteractable(false);
            AdMobService.Instance.ShowRewarded(OnDoubleAdCompleted);
        }

        private void OnDoubleAdCompleted(bool earned)
        {
            if (this == null) return;
            if (!earned)
            {
                claiming = false;
                SetButtonsInteractable(true);
                AudioManager.Play(SfxId.UiDenied);
                PopupService.Toast("리워드 광고가 아직 준비되지 않았습니다.");
                return;
            }

            var original = pending;
            if (original == null) return;
            var doubledItems = new List<OfflineRewardService.RewardItem>(original.Items.Count);
            foreach (var item in original.Items)
                doubledItems.Add(new OfflineRewardService.RewardItem(item.Item, item.Amount * 2));
            pending = new OfflineRewardService.Reward
            {
                Offline = original.Offline,
                RawOffline = original.RawOffline,
                Monsters = original.Monsters,
                Gold = original.Gold * 2L,
                Items = doubledItems
            };
            claiming = false;
            _ = ClaimAsync();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (claimButton != null) claimButton.interactable = interactable;
            if (doubleButton != null) doubleButton.interactable = interactable;
            if (closeButton != null) closeButton.interactable = interactable;
            if (dimButton != null) dimButton.interactable = interactable;
        }

        // ------------------------------------------------------------------
        // 표시
        // ------------------------------------------------------------------

        private void HideImmediate()
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        private void Hide() => Fade(0f);

        private void Fade(float targetAlpha)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            var opening = targetAlpha > 0f;
            panelGroup.interactable = opening;
            panelGroup.blocksRaycasts = opening;

            var start = panelGroup.alpha;
            for (var elapsed = 0f; elapsed < FadeDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / FadeDuration);
                panelGroup.alpha = Mathf.Lerp(start, targetAlpha, t * t * (3f - 2f * t));
                yield return null;
            }

            panelGroup.alpha = targetAlpha;
            if (!opening) panel.gameObject.SetActive(false);
            fadeRoutine = null;
        }

        /// <summary>씬에 깔린 보상 칸 하나입니다. 틀: 슬롯 > Image(테두리) > Image(아이콘), 슬롯 > Text (TMP) (1).</summary>
        private sealed class SlotView
        {
            private readonly GameObject root;
            private readonly Image frame;
            private readonly Image icon;
            private readonly TMP_Text amount;
            private readonly Sprite defaultFrame;

            public SlotView(GameObject root)
            {
                this.root = root;
                frame = root.transform.Find("Image")?.GetComponent<Image>();
                icon = root.transform.Find("Image/Image")?.GetComponent<Image>();
                amount = root.transform.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>()
                         ?? root.GetComponentInChildren<TMP_Text>(true);
                defaultFrame = frame != null ? frame.sprite : null;
            }

            public void Bind(Sprite iconSprite, Sprite frameSprite, string label)
            {
                root.SetActive(true);
                if (frame != null) frame.sprite = frameSprite != null ? frameSprite : defaultFrame;
                if (icon != null)
                {
                    icon.sprite = iconSprite;
                    icon.preserveAspect = true;
                    icon.enabled = iconSprite != null;
                }
                if (amount != null) amount.text = label;
            }

            public void Hide() => root.SetActive(false);
        }
    }
}
