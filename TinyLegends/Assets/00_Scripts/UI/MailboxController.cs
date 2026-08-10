using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdleBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// Main 씬의 우편함(Mailbox) 판넬을 <see cref="MailboxService"/>에 연결합니다.
    ///
    /// 판넬은 기본으로 꺼져 있어 여기에 컴포넌트를 붙이면 Awake가 돌지 않기 때문에,
    /// 항상 켜져 있는 자기 오브젝트를 만들어 판넬과 여는 버튼(Main/SafeArea/Top/Horizontal (1)/Post)을
    /// 밖에서 묶어 줍니다.
    ///
    /// 목록은 씬에 있는 Mail_01을 틀로 삼아 복제합니다. 디자인을 바꾸면 그대로 따라갑니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MailboxController : MonoBehaviour
    {
        private const string MainSceneName = "Main";
        private const string PanelName = "Mailbox";
        private const string OpenButtonPath = "SafeArea/Top/Horizontal (1)/Post";
        private const float FadeDuration = 0.18f;

        private MailboxService service;
        private RectTransform panel;
        private CanvasGroup panelGroup;
        private RectTransform content;
        private GameObject rowTemplate;
        private TMP_Text countText;
        private GameObject emptyObject;
        private Button openButton;
        private Button closeButton;
        private Button dimButton;
        private Button claimAllButton;

        private readonly List<RowView> rows = new();
        private Coroutine fadeRoutine;
        private bool isOpen;

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
            if (FindFirstObjectByType<MailboxController>(FindObjectsInactive.Include) != null) return;

            new GameObject(nameof(MailboxController)).AddComponent<MailboxController>();
        }

        private void Awake()
        {
            if (!BindView())
            {
                enabled = false;
                return;
            }

            SceneRefs.Register(this);
            service = MailboxService.Instance;
            service.Changed += OnMailboxChanged;

            CloseImmediate();
        }

        private void Start()
        {
            // 게임을 켜면 판넬을 열기 전에 미리 POST를 받아 둡니다. 열었을 때 빈 목록이 잠깐 보이지 않습니다.
            _ = LoadAsync(reload: false);
        }

        private void OnDestroy()
        {
            if (service != null) service.Changed -= OnMailboxChanged;
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (dimButton != null) dimButton.onClick.RemoveListener(Close);
            if (claimAllButton != null) claimAllButton.onClick.RemoveListener(OnClaimAllClicked);
            foreach (var row in rows) row.Dispose();
            rows.Clear();
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
                Debug.LogWarning("[Mailbox] Canvas 아래에서 'Mailbox' 판넬을 찾지 못했습니다.", this);
                return false;
            }

            panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var window = panel.Find("SafeArea/Window");
            content = window != null ? window.Find("Scroll View/Viewport/Content") as RectTransform : null;
            if (window == null || content == null || content.childCount == 0)
            {
                Debug.LogWarning("[Mailbox] Window/Scroll View/Viewport/Content 또는 우편 틀(Mail_01)을 찾지 못했습니다.", this);
                return false;
            }

            rowTemplate = content.GetChild(0).gameObject;
            rowTemplate.SetActive(false);

            countText = window.Find("Count")?.GetComponent<TMP_Text>();
            emptyObject = window.Find("Empty")?.gameObject;

            closeButton = EnsureButton(window.Find("CloseBtn"));
            claimAllButton = EnsureButton(window.Find("ClaimAllBtn"));
            dimButton = EnsureButton(panel.Find("Dim"));
            openButton = EnsureButton(SceneRefs.Screen("Main")?.Find(OpenButtonPath));

            if (openButton == null)
                Debug.LogWarning($"[Mailbox] 여는 버튼(Main/{OpenButtonPath})을 찾지 못했습니다.", this);
            else
                openButton.onClick.AddListener(Open);

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (dimButton != null) dimButton.onClick.AddListener(Close);
            if (claimAllButton != null) claimAllButton.onClick.AddListener(OnClaimAllClicked);

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
        // 열기 / 닫기
        // ------------------------------------------------------------------

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;

            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            Refresh();
            Fade(1f);
            UiSfxBinder.Bind(panel);
            AudioManager.Play(SfxId.UiPanelOpen);

            // 열 때마다 서버를 한 번 더 봅니다. 다른 기기에서 온 우편도 바로 보입니다.
            _ = LoadAsync(reload: true);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            AudioManager.Play(SfxId.UiPanelClose);
            Fade(0f);
        }

        private void CloseImmediate()
        {
            isOpen = false;
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

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

        // ------------------------------------------------------------------
        // 목록
        // ------------------------------------------------------------------

        private async Task LoadAsync(bool reload)
        {
            try
            {
                await (reload ? service.ReloadAsync() : service.EnsureLoadedAsync());
            }
            catch (NetworkUnavailableException)
            {
                // 끊김 팝업은 NetworkMonitor가 이미 띄웁니다.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (isOpen) PopupService.Toast("우편을 불러오지 못했습니다.");
            }
        }

        private void OnMailboxChanged()
        {
            if (this != null) Refresh();
        }

        private void Refresh()
        {
            if (content == null) return;

            var now = DateTime.UtcNow;
            var visible = new List<MailData>();
            foreach (var mail in service.Mails)
                if (!mail.Claimed) visible.Add(mail); // 기간이 지난 우편도 보여 주고, 받기 버튼만 잠급니다.

            EnsureRowCount(visible.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                if (i < visible.Count) rows[i].Bind(service, visible[i], now);
                else rows[i].Hide();
            }

            var claimable = service.ClaimableCount;
            if (countText != null) countText.text = $"{claimable} / {MailboxService.Capacity}";
            if (emptyObject != null) emptyObject.SetActive(visible.Count == 0);
            if (claimAllButton != null) claimAllButton.interactable = claimable > 0 && !service.IsBusy;

            // 판넬이 꺼져 있을 때는 레이아웃을 다시 잡을 필요도, 잡을 수도 없습니다.
            if (content.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void EnsureRowCount(int count)
        {
            while (rows.Count < count)
            {
                var clone = Instantiate(rowTemplate, content, false);
                clone.name = $"Mail_{rows.Count + 1:00}";
                // RowView가 ClaimBtn에 Button을 붙인 뒤에 소리를 걸어야 빠지지 않습니다.
                rows.Add(new RowView(this, clone));
                UiSfxBinder.Bind(clone);
            }
        }

        private void OnClaimAllClicked()
        {
            _ = ClaimAllAsync();
        }

        private async Task ClaimAllAsync()
        {
            if (service.IsBusy) return;

            var claimed = await ClaimGuardedAsync(() => service.ClaimAllAsync());
            if (this == null) return;
            AudioManager.Play(claimed > 0 ? SfxId.MailClaimAll : SfxId.UiDenied);
            PopupService.Toast(claimed > 0 ? $"우편 {claimed}통을 받았습니다." : "받을 우편이 없습니다.");
        }

        private async Task ClaimOneAsync(MailData mail)
        {
            if (service.IsBusy || mail == null) return;

            var reward = service.DescribeReward(mail);
            var claimed = await ClaimGuardedAsync(async () => (await service.ClaimAsync(mail)) ? 1 : 0);
            if (this == null) return;
            AudioManager.Play(claimed > 0 ? SfxId.MailClaim : SfxId.UiDenied);
            PopupService.Toast(claimed > 0 ? $"{reward} 받았습니다." : "우편을 받지 못했습니다.");
        }

        private async Task<int> ClaimGuardedAsync(Func<Task<int>> claim)
        {
            SetClaimInteractable(false);
            try
            {
                return await claim();
            }
            catch (NetworkUnavailableException)
            {
                return 0;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return 0;
            }
            finally
            {
                if (this != null)
                {
                    SetClaimInteractable(true);
                    Refresh();
                }
            }
        }

        private void SetClaimInteractable(bool interactable)
        {
            if (claimAllButton != null) claimAllButton.interactable = interactable && service.ClaimableCount > 0;
            foreach (var row in rows) row.SetInteractable(interactable);
        }

        /// <summary>씬의 Mail_01을 복제한 우편 한 줄입니다.</summary>
        private sealed class RowView
        {
            private readonly MailboxController owner;
            private readonly GameObject root;
            private readonly TMP_Text title;
            private readonly TMP_Text body;
            private readonly TMP_Text expire;
            private readonly TMP_Text rewardValue;
            private readonly Image rewardIcon;
            private readonly Image mailIcon;
            private readonly Sprite defaultRewardIcon;
            private readonly Sprite defaultMailIcon;
            private readonly Button claimButton;
            private MailData bound;

            public RowView(MailboxController owner, GameObject root)
            {
                this.owner = owner;
                this.root = root;

                title = root.transform.Find("Title")?.GetComponent<TMP_Text>();
                body = root.transform.Find("Des")?.GetComponent<TMP_Text>();
                expire = root.transform.Find("Expire")?.GetComponent<TMP_Text>();
                rewardValue = root.transform.Find("Reward/Value")?.GetComponent<TMP_Text>();
                rewardIcon = root.transform.Find("Reward/Icon")?.GetComponent<Image>();
                mailIcon = root.transform.Find("IconFrame/Icon")?.GetComponent<Image>();
                defaultRewardIcon = rewardIcon != null ? rewardIcon.sprite : null;
                defaultMailIcon = mailIcon != null ? mailIcon.sprite : null;

                claimButton = EnsureButton(root.transform.Find("ClaimBtn"));
                if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            }

            public void Bind(MailboxService service, MailData mail, DateTime utcNow)
            {
                bound = mail;
                root.SetActive(true);
                root.name = $"Mail_{mail.Id}";

                if (title != null) title.text = mail.Title;
                if (body != null) body.text = mail.Body;
                if (expire != null) expire.text = mail.DescribeRemaining(utcNow);
                if (rewardValue != null) rewardValue.text = service.DescribeReward(mail);

                // 아이템 우편만 아이콘을 갈아 끼우고, 골드·경험치는 씬에 잡아 둔 기본 아이콘을 그대로 씁니다.
                var icon = service.ResolveRewardIcon(mail);
                if (rewardIcon != null)
                {
                    rewardIcon.sprite = icon != null ? icon : defaultRewardIcon;
                    rewardIcon.preserveAspect = true;
                }
                if (mailIcon != null)
                {
                    mailIcon.sprite = icon != null ? icon : defaultMailIcon;
                    mailIcon.preserveAspect = true;
                }

                if (claimButton != null) claimButton.interactable = mail.IsClaimable(utcNow) && !service.IsBusy;
            }

            public void Hide()
            {
                bound = null;
                root.SetActive(false);
            }

            public void SetInteractable(bool interactable)
            {
                if (claimButton == null || bound == null) return;
                claimButton.interactable = interactable && bound.IsClaimable(DateTime.UtcNow);
            }

            public void Dispose()
            {
                if (claimButton != null) claimButton.onClick.RemoveListener(OnClaimClicked);
            }

            private void OnClaimClicked()
            {
                if (owner != null && bound != null) _ = owner.ClaimOneAsync(bound);
            }
        }
    }
}
