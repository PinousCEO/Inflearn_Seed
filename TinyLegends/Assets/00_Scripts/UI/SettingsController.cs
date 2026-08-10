using System.Collections;
using System.Collections.Generic;
using IdleBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// Main 씬의 설정(Settings) 판넬을 실제 값에 연결합니다.
    ///
    /// 우편함과 같은 방식입니다. 판넬은 기본으로 꺼져 있어 거기에 컴포넌트를 붙이면 Awake가 돌지 않으므로,
    /// 항상 켜져 있는 자기 오브젝트를 만들어 판넬과 여는 버튼(Main/SafeArea/Top/Horizontal (1)/Setting)을
    /// 밖에서 묶어 줍니다. 판넬 자체는 Tools/Idle Battle/Build Settings Panel 로 만듭니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsController : MonoBehaviour
    {
        private const string MainSceneName = "Main";
        private const string PanelName = "Settings";
        private const string OpenButtonPath = "SafeArea/Top/Horizontal (1)/Setting";
        private const float FadeDuration = 0.18f;

        private RectTransform panel;
        private CanvasGroup panelGroup;
        private Button openButton;
        private Button closeButton;
        private Button footerCloseButton;
        private Button dimButton;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private TMP_Text bgmValue;
        private TMP_Text sfxValue;
        private Button bgmMute;
        private TMP_Text bgmMuteLabel;
        private Button sfxMute;
        private TMP_Text sfxMuteLabel;
        private Button languageSelect;
        private TMP_Text languageSelectLabel;
        private LocalizedText languageSelectLocalized;
        private GameObject languagePopup;
        private Button languagePopupDim;
        private Button languagePopupClose;
        private readonly List<Button> languageOptions = new List<Button>();
        private Button shakeToggle;
        private TMP_Text shakeLabel;
        private TMP_Text versionValue;

        // 껐다 다시 켤 때 돌아갈 음량입니다.
        private const float DefaultVolume = .7f;
        private float savedBgmVolume = DefaultVolume;
        private float savedSfxVolume = DefaultVolume;

        private Coroutine fadeRoutine;
        private bool isOpen;
        // 슬라이더 값을 코드로 되돌릴 때 onValueChanged가 다시 울리지 않게 합니다.
        private bool isApplying;

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
            if (FindFirstObjectByType<SettingsController>(FindObjectsInactive.Include) != null) return;

            new GameObject(nameof(SettingsController)).AddComponent<SettingsController>();
        }

        private void Awake()
        {
            if (!BindView())
            {
                enabled = false;
                return;
            }

            SceneRefs.Register(this);
            Localization.LanguageChanged += OnLanguageChanged;
            CloseImmediate();
        }

        private void OnDestroy()
        {
            Localization.LanguageChanged -= OnLanguageChanged;
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (footerCloseButton != null) footerCloseButton.onClick.RemoveListener(Close);
            if (dimButton != null) dimButton.onClick.RemoveListener(Close);
            if (bgmMute != null) bgmMute.onClick.RemoveListener(ToggleBgmMute);
            if (sfxMute != null) sfxMute.onClick.RemoveListener(ToggleSfxMute);
            if (languageSelect != null) languageSelect.onClick.RemoveListener(OpenLanguagePopup);
            if (languagePopupDim != null) languagePopupDim.onClick.RemoveAllListeners();
            if (languagePopupClose != null) languagePopupClose.onClick.RemoveAllListeners();
            foreach (var option in languageOptions)
                if (option != null) option.onClick.RemoveAllListeners();
            if (shakeToggle != null) shakeToggle.onClick.RemoveListener(ToggleScreenShake);
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
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
                Debug.LogWarning("[Settings] Canvas 아래에서 'Settings' 판넬을 찾지 못했습니다. " +
                                 "Tools/Idle Battle/Build Settings Panel 로 먼저 만들어 주세요.", this);
                return false;
            }

            panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            var window = panel.Find("SafeArea/Window");
            if (window == null)
            {
                Debug.LogWarning("[Settings] SafeArea/Window를 찾지 못했습니다.", this);
                return false;
            }

            bgmSlider = window.Find("Row_Bgm/Slider")?.GetComponent<Slider>();
            sfxSlider = window.Find("Row_Sfx/Slider")?.GetComponent<Slider>();
            bgmValue = window.Find("Row_Bgm/Value")?.GetComponent<TMP_Text>();
            sfxValue = window.Find("Row_Sfx/Value")?.GetComponent<TMP_Text>();
            bgmMute = EnsureButton(window.Find("Row_Bgm/Mute"));
            bgmMuteLabel = window.Find("Row_Bgm/Mute/Label")?.GetComponent<TMP_Text>();
            sfxMute = EnsureButton(window.Find("Row_Sfx/Mute"));
            sfxMuteLabel = window.Find("Row_Sfx/Mute/Label")?.GetComponent<TMP_Text>();
            languageSelect = EnsureButton(window.Find("Row_Language/Select"));
            languageSelectLabel = window.Find("Row_Language/Select/Label")?.GetComponent<TMP_Text>();
            languageSelectLocalized = languageSelectLabel != null ? languageSelectLabel.GetComponent<LocalizedText>() : null;
            BindLanguagePopup();
            shakeToggle = EnsureButton(window.Find("Row_Shake/Toggle"));
            shakeLabel = window.Find("Row_Shake/Toggle/Label")?.GetComponent<TMP_Text>();
            versionValue = window.Find("Version")?.GetComponent<TMP_Text>();

            closeButton = EnsureButton(window.Find("CloseBtn"));
            footerCloseButton = EnsureButton(window.Find("CloseButton"));
            dimButton = EnsureButton(panel.Find("Dim"));
            openButton = EnsureButton(SceneRefs.Screen("Main")?.Find(OpenButtonPath));

            if (openButton == null)
                Debug.LogWarning($"[Settings] 여는 버튼(Main/{OpenButtonPath})을 찾지 못했습니다.", this);
            else
                openButton.onClick.AddListener(Open);

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (footerCloseButton != null) footerCloseButton.onClick.AddListener(Close);
            if (dimButton != null) dimButton.onClick.AddListener(Close);
            if (bgmMute != null) bgmMute.onClick.AddListener(ToggleBgmMute);
            if (sfxMute != null) sfxMute.onClick.AddListener(ToggleSfxMute);
            if (languageSelect != null) languageSelect.onClick.AddListener(OpenLanguagePopup);
            if (shakeToggle != null) shakeToggle.onClick.AddListener(ToggleScreenShake);
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);

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
            // 지난번에 언어 팝업을 띄운 채 닫았어도, 설정창은 항상 기본 화면으로 열립니다.
            if (languagePopup != null) languagePopup.SetActive(false);
            Refresh();
            Fade(1f);
            AudioManager.Play(SfxId.UiPanelOpen);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            AudioManager.Play(SfxId.UiPanelClose);
            // 음량은 만질 때마다 PlayerPrefs에 들어가지만, 닫을 때 한 번 확실히 밀어냅니다.
            PlayerPrefs.Save();
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
        // 값
        // ------------------------------------------------------------------

        private void Refresh()
        {
            // 창을 열 때의 음량을 "다시 켜면 돌아갈 값"으로 기억해 둡니다.
            if (BgmManager.Volume > 0f) savedBgmVolume = BgmManager.Volume;
            if (AudioManager.SfxVolume > 0f) savedSfxVolume = AudioManager.SfxVolume;
            RefreshVolumeRows();

            RefreshLanguageLabel();
            RefreshShakeLabel();
            if (versionValue != null) versionValue.text = "v" + Application.version;
        }

        private void OnBgmChanged(float value)
        {
            if (isApplying) return;
            SetBgmVolume(value);
            RefreshVolumeRows();
        }

        private void OnSfxChanged(float value)
        {
            if (isApplying) return;
            SetSfxVolume(value);
            RefreshVolumeRows();
            if (value > 0f) AudioManager.Play(SfxId.UiSelect);
        }

        /// <summary>
        /// 켜짐/꺼짐 버튼입니다. 끄면 0%가 되고, 다시 켜면 끄기 직전의 값으로 돌아옵니다.
        /// 끄기 전 값이 없으면(처음부터 0이었으면) 기본값으로 올려 줍니다. 켰는데 조용하면 고장으로 보입니다.
        /// </summary>
        private void ToggleBgmMute()
        {
            if (BgmManager.Volume > 0f)
            {
                savedBgmVolume = BgmManager.Volume;
                SetBgmVolume(0f);
            }
            else
            {
                SetBgmVolume(savedBgmVolume > 0f ? savedBgmVolume : DefaultVolume);
            }

            RefreshVolumeRows();
            AudioManager.Play(SfxId.UiClick);
        }

        private void ToggleSfxMute()
        {
            if (AudioManager.SfxVolume > 0f)
            {
                savedSfxVolume = AudioManager.SfxVolume;
                SetSfxVolume(0f);
            }
            else
            {
                SetSfxVolume(savedSfxVolume > 0f ? savedSfxVolume : DefaultVolume);
                AudioManager.Play(SfxId.UiClick);
            }

            RefreshVolumeRows();
        }

        private void SetBgmVolume(float value)
        {
            BgmManager.Volume = value;
            // 음량이 0이면 음소거로도 표시해 둡니다. 다른 곳에서 이 값을 보고 재생을 아낍니다.
            BgmManager.Muted = value <= 0f;
        }

        private void SetSfxVolume(float value)
        {
            AudioManager.SfxVolume = value;
            AudioManager.Muted = value <= 0f;
        }

        // ------------------------------------------------------------------
        // 언어 선택 팝업
        // ------------------------------------------------------------------

        private void BindLanguagePopup()
        {
            var popup = panel.Find("LanguagePopup");
            if (popup == null)
            {
                Debug.LogWarning("[Settings] 언어 선택 팝업(LanguagePopup)을 찾지 못했습니다.", this);
                return;
            }

            languagePopup = popup.gameObject;
            var popupWindow = popup.Find("Window");
            languagePopupDim = EnsureButton(popup.Find("Dim"));
            languagePopupClose = EnsureButton(popupWindow != null ? popupWindow.Find("CloseButton") : null);

            if (languagePopupDim != null) languagePopupDim.onClick.AddListener(CloseLanguagePopup);
            if (languagePopupClose != null) languagePopupClose.onClick.AddListener(CloseLanguagePopup);

            languageOptions.Clear();
            for (var i = 0; i < GameSettings.LanguageCodes.Length; i++)
            {
                var button = EnsureButton(popupWindow != null ? popupWindow.Find("Option_" + GameSettings.LanguageCodes[i]) : null);
                languageOptions.Add(button);
                if (button == null) continue;
                // 반복문 변수를 그대로 넘기면 마지막 값이 잡히므로 복사해 둡니다.
                var index = i;
                button.onClick.AddListener(() => SelectLanguage(index));
            }

            languagePopup.SetActive(false);
        }

        private void OpenLanguagePopup()
        {
            if (languagePopup == null) return;
            languagePopup.SetActive(true);
            languagePopup.transform.SetAsLastSibling();
            RefreshLanguageOptions();
            AudioManager.Play(SfxId.UiPopupOpen);
        }

        private void CloseLanguagePopup()
        {
            if (languagePopup == null || !languagePopup.activeSelf) return;
            languagePopup.SetActive(false);
            AudioManager.Play(SfxId.UiPanelClose);
        }

        private void SelectLanguage(int index)
        {
            // Localization.Language가 GameSettings에도 값을 넣고 LanguageChanged까지 울려 줍니다.
            // 여기서 GameSettings를 따로 건드리면 이벤트가 울지 않아 글자가 그대로 남습니다.
            Localization.Language = GameSettings.LanguageCodes[Mathf.Clamp(index, 0, GameSettings.LanguageCodes.Length - 1)];
            AudioManager.Play(SfxId.UiSelect);
            CloseLanguagePopup();
            PlayerPrefs.Save();
        }

        /// <summary>언어가 바뀌면 이 판넬에서 코드로 넣는 글자도 다시 넣어 줍니다.</summary>
        private void OnLanguageChanged()
        {
            RefreshLanguageLabel();
            RefreshVolumeRows();
            RefreshShakeLabel();
        }

        /// <summary>
        /// 언어 줄의 버튼 글자입니다. 여기에는 고른 언어의 제 이름(한국어 · 日本語 · English)이 들어갑니다.
        ///
        /// 이 글자에는 씬에서 붙여 둔 <see cref="LocalizedText"/>도 함께 있습니다. 그 키가 'language.ko'로
        /// 굳어 있으면, 언어를 바꿀 때 여기서 '日本語'로 써 놓아도 뒤이어 LocalizedText가 '한국어'로 되돌립니다
        /// (판넬을 연 뒤에 붙는 쪽이라 항상 나중에 그립니다). 그래서 글자보다 키를 먼저 갈아 끼웁니다.
        /// 그러면 누가 먼저 그리든 같은 글자가 남습니다.
        /// </summary>
        private void RefreshLanguageLabel()
        {
            if (languageSelectLabel == null) return;

            if (languageSelectLocalized != null)
                languageSelectLocalized.Key = "language." + GameSettings.LanguageCodes[GameSettings.LanguageIndex];

            languageSelectLabel.text = GameSettings.LanguageNames[GameSettings.LanguageIndex];
        }

        /// <summary>지금 쓰는 언어를 눈에 띄게 하고 나머지는 조금 흐리게 둡니다.</summary>
        private void RefreshLanguageOptions()
        {
            for (var i = 0; i < languageOptions.Count; i++)
            {
                var button = languageOptions[i];
                if (button == null) continue;
                var selected = i == GameSettings.LanguageIndex;
                var graphic = button.targetGraphic;
                if (graphic != null)
                {
                    var color = graphic.color;
                    color.a = selected ? 1f : .55f;
                    graphic.color = color;
                }
            }
        }

        private void ToggleScreenShake()
        {
            GameSettings.ScreenShakeEnabled = !GameSettings.ScreenShakeEnabled;
            RefreshShakeLabel();
            AudioManager.Play(SfxId.UiClick);
        }

        private void RefreshVolumeRows()
        {
            isApplying = true;
            RefreshVolumeRow(bgmValue, bgmMuteLabel, bgmSlider, BgmManager.Volume);
            RefreshVolumeRow(sfxValue, sfxMuteLabel, sfxSlider, AudioManager.SfxVolume);
            isApplying = false;
        }

        private static void RefreshVolumeRow(TMP_Text value, TMP_Text muteLabel, Slider slider, float volume)
        {
            var off = volume <= 0f;
            if (value != null) value.text = Percent(volume);
            if (muteLabel != null) muteLabel.text = Localization.Get(off ? "common.off" : "common.on");
            // 버튼으로 껐다 켜면 슬라이더도 함께 움직여야 합니다.
            if (slider != null) slider.SetValueWithoutNotify(volume);
        }

        private void RefreshShakeLabel()
        {
            if (shakeLabel != null)
                shakeLabel.text = Localization.Get(GameSettings.ScreenShakeEnabled ? "common.on" : "common.off");
        }

        private static string Percent(float value) => Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }
}
