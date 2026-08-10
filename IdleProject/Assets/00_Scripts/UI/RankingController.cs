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
    /// Main 씬의 랭킹(Ranking) 판넬을 <see cref="RankingService"/>에 연결합니다.
    ///
    /// 판넬은 기본으로 꺼져 있어 여기에 컴포넌트를 붙이면 Awake가 돌지 않기 때문에,
    /// 항상 켜져 있는 자기 오브젝트를 만들어 판넬과 여는 버튼(Main/SafeArea/Top/Horizontal (1)/Rank)을
    /// 밖에서 묶어 줍니다. 우편함(<see cref="MailboxController"/>)과 같은 방식입니다.
    ///
    /// 목록은 씬에 있는 Rank_01~Rank_10을 그대로 쓰고, 인원이 더 많으면 Rank_01을 복제합니다.
    /// 디자인을 바꾸면 그대로 따라갑니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RankingController : MonoBehaviour
    {
        private const string MainSceneName = "Main";
        private const string PanelName = "Ranking";
        private const string OpenButtonPath = "SafeArea/Top/Horizontal (1)/Rank";
        private const float FadeDuration = 0.18f;

        /// <summary>아직 만들지 않은 랭킹 탭을 눌렀을 때 띄우는 문구입니다.</summary>
        private const string ComingSoonMessage = "아직 준비 중인 랭킹입니다";

        private RankingService service;
        private RectTransform panel;
        private CanvasGroup panelGroup;
        private RectTransform content;
        private GameObject rowTemplate;
        private Button openButton;
        private Button closeButton;
        private Button dimButton;

        private readonly List<RowView> rows = new();
        private readonly List<TabView> tabs = new();
        private RowView myRow;
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
            if (FindFirstObjectByType<RankingController>(FindObjectsInactive.Include) != null) return;

            new GameObject(nameof(RankingController)).AddComponent<RankingController>();
        }

        private void Awake()
        {
            if (!BindView())
            {
                enabled = false;
                return;
            }

            SceneRefs.Register(this);
            service = RankingService.Instance;
            service.Changed += OnRankingChanged;
            service.PowerChanged += OnPowerChanged;

            CloseImmediate();
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.Changed -= OnRankingChanged;
                service.PowerChanged -= OnPowerChanged;
            }

            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (dimButton != null) dimButton.onClick.RemoveListener(Close);
            foreach (var tab in tabs) tab.Dispose();
            tabs.Clear();
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
                Debug.LogWarning("[Ranking] Canvas 아래에서 'Ranking' 판넬을 찾지 못했습니다.", this);
                return false;
            }

            panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var window = panel.Find("SafeArea/Window");
            content = window != null ? window.Find("Scroll View/Viewport/Content") as RectTransform : null;
            if (window == null || content == null || content.childCount == 0)
            {
                Debug.LogWarning("[Ranking] Window/Scroll View/Viewport/Content 또는 순위 줄(Rank_01)을 찾지 못했습니다.", this);
                return false;
            }

            for (var i = 0; i < content.childCount; i++)
                rows.Add(new RowView(content.GetChild(i).gameObject));
            rowTemplate = rows[0].Root;

            var myRankRoot = window.Find("MyRank");
            if (myRankRoot != null) myRow = new RowView(myRankRoot.gameObject);

            BindTabs(window.Find("Tabs"));

            closeButton = EnsureButton(window.Find("CloseBtn"));
            dimButton = EnsureButton(panel.Find("Dim"));
            openButton = EnsureButton(SceneRefs.Screen("Main")?.Find(OpenButtonPath));

            if (openButton == null)
                Debug.LogWarning($"[Ranking] 여는 버튼(Main/{OpenButtonPath})을 찾지 못했습니다.", this);
            else
                openButton.onClick.AddListener(Open);

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (dimButton != null) dimButton.onClick.AddListener(Close);

            return true;
        }

        /// <summary>
        /// 탭 세 개를 묶습니다. 지금 내용이 있는 것은 전투력(Tab_0)뿐이라 첫 탭을 고른 상태로 두고,
        /// 나머지는 눌러도 안내 문구만 띄웁니다.
        ///
        /// 선택/비선택 이미지는 씬에 이미 그렇게 잡혀 있는 Tab_0·Tab_1의 스프라이트를 그대로 빌려 씁니다.
        /// 코드가 에셋을 따로 들고 있지 않아 디자인을 바꿔도 따라갑니다.
        /// </summary>
        private void BindTabs(Transform tabsRoot)
        {
            if (tabsRoot == null || tabsRoot.childCount == 0) return;

            Sprite selectedSprite = null;
            Sprite normalSprite = null;
            for (var i = 0; i < tabsRoot.childCount; i++)
            {
                var image = tabsRoot.GetChild(i).GetComponent<Image>();
                if (image == null) continue;
                if (i == 0) selectedSprite = image.sprite;
                else if (normalSprite == null) normalSprite = image.sprite;
            }

            for (var i = 0; i < tabsRoot.childCount; i++)
            {
                var isPowerTab = i == 0;
                var tab = new TabView(tabsRoot.GetChild(i), selectedSprite, normalSprite);
                tab.SetSelected(isPowerTab);
                tab.Clicked += isPowerTab
                    ? (Action)(() => _ = LoadAsync())
                    : () =>
                    {
                        AudioManager.Play(SfxId.UiDenied);
                        PopupService.Toast(ComingSoonMessage, 1f);
                    };
                tabs.Add(tab);
            }
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
                // 씬의 버튼 이미지가 Raycast Target 꺼진 채로 놓여 있으면 눌러도 반응하지 않습니다.
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

            _ = LoadAsync();
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

        /// <summary>
        /// 내 전투력을 먼저 올리고 나서 목록을 받아 옵니다.
        /// 순서를 지켜야 방금 오른 전투력이 목록과 내 순위에 반영됩니다.
        /// </summary>
        private async Task LoadAsync()
        {
            try
            {
                await service.SubmitAsync();
                if (this == null) return;
                await service.RefreshAsync();
            }
            catch (NetworkUnavailableException)
            {
                // 끊김 팝업은 NetworkMonitor가 이미 띄웁니다.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (isOpen) PopupService.Toast("랭킹을 불러오지 못했습니다.");
            }
        }

        private void OnRankingChanged()
        {
            if (this != null) Refresh();
        }

        private void OnPowerChanged(long power)
        {
            if (this != null) RefreshMyRow();
        }

        private void Refresh()
        {
            if (content == null) return;

            var top = service.Top;
            EnsureRowCount(top.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                if (i < top.Count) rows[i].Bind(top[i]);
                else rows[i].Hide();
            }

            RefreshMyRow();

            // 판넬이 꺼져 있을 때는 레이아웃을 다시 잡을 필요도, 잡을 수도 없습니다.
            if (content.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        /// <summary>
        /// 판넬 아래 고정된 내 순위 줄입니다.
        /// 순위를 아직 못 받아 왔거나 랭킹에 오르지 못했으면 숫자 대신 '-'를 둡니다.
        /// </summary>
        private void RefreshMyRow()
        {
            if (myRow == null || service == null) return;

            var data = PlayerDataManager.Instance;
            myRow.Bind(new RankingEntry
            {
                Name = string.IsNullOrWhiteSpace(data.PlayerName) ? "플레이어" : data.PlayerName,
                CharacterId = data.CharacterId,
                Level = data.Level,
                Power = service.MyPower,
                Rank = service.MyRank
            });
        }

        private void EnsureRowCount(int count)
        {
            while (rows.Count < count)
            {
                var clone = Instantiate(rowTemplate, content, false);
                clone.name = $"Rank_{rows.Count + 1:00}";
                UiSfxBinder.Bind(clone);
                rows.Add(new RowView(clone));
            }
        }

        /// <summary>씬의 Rank_01(과 MyRank)과 같은 모양을 가진 순위 한 줄입니다.</summary>
        private sealed class RowView
        {
            public readonly GameObject Root;
            private readonly TMP_Text badge;
            private readonly TMP_Text nameText;
            private readonly TMP_Text info;
            private readonly TMP_Text value;

            public RowView(GameObject root)
            {
                Root = root;
                badge = root.transform.Find("Badge/Text")?.GetComponent<TMP_Text>();
                nameText = root.transform.Find("Name")?.GetComponent<TMP_Text>();
                info = root.transform.Find("Info")?.GetComponent<TMP_Text>();
                value = root.transform.Find("Value")?.GetComponent<TMP_Text>();
            }

            public void Bind(RankingEntry entry)
            {
                Root.SetActive(true);
                if (badge != null) badge.text = entry.Rank > 0 ? entry.Rank.ToString() : "-";
                if (nameText != null) nameText.text = entry.Name;
                if (info != null) info.text = entry.DescribeLevel();
                if (value != null) value.text = entry.Power.ToString("N0");
            }

            public void Hide() => Root.SetActive(false);
        }

        /// <summary>랭킹 종류를 고르는 탭 한 칸입니다.</summary>
        private sealed class TabView
        {
            private readonly Image image;
            private readonly Button button;
            private readonly Sprite selectedSprite;
            private readonly Sprite normalSprite;

            public event Action Clicked;

            public TabView(Transform root, Sprite selectedSprite, Sprite normalSprite)
            {
                this.selectedSprite = selectedSprite;
                this.normalSprite = normalSprite;
                image = root.GetComponent<Image>();
                button = EnsureButton(root);
                if (button != null) button.onClick.AddListener(OnClicked);
            }

            public void SetSelected(bool selected)
            {
                if (image == null) return;
                var sprite = selected ? selectedSprite : normalSprite;
                if (sprite != null) image.sprite = sprite;
            }

            public void Dispose()
            {
                if (button != null) button.onClick.RemoveListener(OnClicked);
            }

            private void OnClicked() => Clicked?.Invoke();
        }
    }
}
