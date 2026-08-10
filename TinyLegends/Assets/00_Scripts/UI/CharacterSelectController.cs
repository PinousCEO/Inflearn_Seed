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
    /// 저장된 캐릭터가 없을 때 진입하는 Select 씬의 화면 로직입니다.
    /// 카탈로그의 캐릭터 수만큼 버튼을 만들고, 고른 캐릭터의 정보를 CharacterInfo 패널에 채웁니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectController : MonoBehaviour
    {
        /// <summary>Status 슬롯 하나에 대응하는 라벨 · 슬라이더 · 수치 묶음입니다.</summary>
        [Serializable]
        public sealed class StatRow
        {
            public TMP_Text label;
            public Slider slider;
            public TMP_Text value;
        }

        [Header("데이터")]
        [SerializeField] private CharacterCatalog catalog;

        [Header("캐릭터 버튼")]
        [Tooltip("버튼 5개가 생성될 부모입니다. 비워 두면 CharacterInfo 위쪽에 자동으로 만듭니다.")]
        [SerializeField] private RectTransform buttonContainer;
        [SerializeField] private Vector2 buttonSize = new Vector2(196f, 132f);
        [SerializeField] private float buttonSpacing = 10f;

        [Header("CharacterInfo")]
        [SerializeField] private CanvasGroup infoGroup;
        [SerializeField] private RectTransform infoRoot;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private Image[] difficultyStars = new Image[CharacterData.MaxDifficulty];
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text skillText;
        [SerializeField] private StatRow[] statRows = new StatRow[CharacterData.StatCount];
        [SerializeField] private Image[] skillSlots = new Image[4];

        [Header("이름 입력")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text nameHintText;

        [Header("모험 시작")]
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private string nextSceneName = "Main";

        [Header("연출")]
        [SerializeField, Min(0f)] private float panelFadeDuration = 0.22f;
        [SerializeField, Min(0f)] private float statFillDuration = 0.55f;
        [SerializeField, Min(0f)] private float statStagger = 0.05f;
        [SerializeField, Min(0f)] private float popDuration = 0.24f;
        [SerializeField] private float panelSlideDistance = 24f;

        [Header("색상")]
        [SerializeField] private Color starOnColor = new Color(1f, 0.83f, 0.44f, 1f);
        [SerializeField] private Color starOffColor = new Color(0.20f, 0.27f, 0.35f, 1f);
        [SerializeField] private Color hintNeutralColor = new Color(0.50f, 0.56f, 0.64f, 1f);
        [SerializeField] private Color hintOkColor = new Color(0.50f, 0.84f, 0.64f, 1f);
        [SerializeField] private Color hintErrorColor = new Color(0.88f, 0.54f, 0.48f, 1f);

        private readonly List<CharacterSelectButton> buttons = new List<CharacterSelectButton>();
        private readonly List<Coroutine> activeTweens = new List<Coroutine>();
        private CharacterData current;
        private bool isStarting;
        private Coroutine infoRoutine;
        private Vector3 infoOriginalPosition;
        private bool infoPositionCached;

        /// <summary>현재 화면에서 고른 캐릭터입니다. 아직 아무것도 고르지 않았으면 null입니다.</summary>
        public CharacterData Current => current;

        /// <summary>캐릭터 목록입니다. Coming Soon 라벨 등 외부 연출이 잠금 여부를 읽을 때 씁니다.</summary>
        public CharacterCatalog Catalog => catalog;

        /// <summary>해당 인덱스의 캐릭터를 고를 수 있는지입니다. 잠긴 캐릭터면 false입니다.</summary>
        public bool IsIndexSelectable(int index)
        {
            var data = catalog != null ? catalog.Get(index) : null;
            return data != null && data.IsPlayable;
        }

        private void Reset()
        {
            AutoBind();
        }

        private void Awake()
        {
            AutoBind();
        }

        private void Start()
        {
            if (catalog == null)
            {
                Debug.LogError(
                    "CharacterCatalog가 없습니다. Assets/00_Data/Characters/CharacterCatalog.asset을 연결하세요.",
                    this);
                return;
            }

            CacheInfoPosition();
            BuildButtons();
            SetupNameInput();
            SetupStartButton();

            var initial = catalog.FirstPlayable();
            if (initial != null)
                Select(initial, animate: false);

            RefreshNameState();
        }

        private void OnDestroy()
        {
            if (nameInput != null)
            {
                nameInput.onValueChanged.RemoveListener(OnNameChanged);
                nameInput.onSubmit.RemoveListener(OnNameSubmitted);
            }

            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartClicked);
        }

        // ------------------------------------------------------------------
        // 버튼 생성
        // ------------------------------------------------------------------

        private void BuildButtons()
        {
            buttons.Clear();

            var container = EnsureButtonContainer();
            if (container == null) return;

            // 씬에 미리 만들어 둔 버튼이 있으면 그대로 쓴다.
            buttons.AddRange(container.GetComponentsInChildren<CharacterSelectButton>(true));

            if (buttons.Count == 0)
            {
                // 없을 때만 런타임에 만든다. 평소에는 에디터 메뉴로 만들어 두는 쪽을 권장한다.
                var font = ResolveFont();
                for (var i = 0; i < catalog.Count; i++)
                {
                    var data = catalog.Get(i);
                    if (data == null) continue;
                    buttons.Add(CharacterSelectButton.Create(container, data, font, buttonSize));
                }

                Debug.LogWarning(
                    "씬에 캐릭터 버튼이 없어 런타임에 임시로 만들었습니다. " +
                    "Tools/게임 데이터 관리/Select 씬 캐릭터 버튼 생성 을 실행해 두세요.",
                    this);
            }

            foreach (var button in buttons)
            {
                if (button == null || button.Button == null) continue;
                var captured = button;
                button.Button.onClick.RemoveAllListeners();
                button.Button.onClick.AddListener(() => OnCharacterClicked(captured));
            }
        }

        private RectTransform EnsureButtonContainer()
        {
            if (buttonContainer != null)
            {
                EnsureHorizontalLayout(buttonContainer);
                return buttonContainer;
            }

            // 지정된 부모가 없으면 CharacterInfo 바로 위에 가로줄을 하나 만든다.
            var parent = infoRoot != null ? infoRoot.parent as RectTransform : null;
            if (parent == null)
            {
                Debug.LogError("캐릭터 버튼을 만들 부모를 찾지 못했습니다. buttonContainer를 지정하세요.", this);
                return null;
            }

            var host = new GameObject("CharacterButtons", typeof(RectTransform));
            buttonContainer = (RectTransform)host.transform;
            buttonContainer.SetParent(parent, false);
            buttonContainer.SetSiblingIndex(infoRoot.GetSiblingIndex());
            buttonContainer.anchorMin = new Vector2(0.5f, 1f);
            buttonContainer.anchorMax = new Vector2(0.5f, 1f);
            buttonContainer.pivot = new Vector2(0.5f, 1f);
            buttonContainer.anchoredPosition = new Vector2(0f, -infoRoot.rect.height - 40f);
            buttonContainer.sizeDelta = new Vector2(
                catalog.Count * buttonSize.x + (catalog.Count - 1) * buttonSpacing,
                buttonSize.y);

            EnsureHorizontalLayout(buttonContainer);
            return buttonContainer;
        }

        private void EnsureHorizontalLayout(RectTransform target)
        {
            if (!target.TryGetComponent(out HorizontalLayoutGroup layout))
                layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = buttonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
        }

        private TMP_FontAsset ResolveFont()
        {
            if (characterNameText != null && characterNameText.font != null) return characterNameText.font;
            if (descriptionText != null && descriptionText.font != null) return descriptionText.font;
            return TMP_Settings.defaultFontAsset;
        }

        // ------------------------------------------------------------------
        // 선택
        // ------------------------------------------------------------------

        /// <summary>
        /// 카탈로그 인덱스로 캐릭터를 고릅니다. 버튼이 아닌 곳(3D 캐릭터 클릭 등)에서 부르는 통로입니다.
        /// </summary>
        public void SelectByIndex(int index)
        {
            if (catalog == null)
            {
                Debug.LogError("catalog가 없어 캐릭터를 고를 수 없습니다.", this);
                return;
            }

            var data = catalog.Get(index);
            if (data == null) return;

            // 잠긴(Coming Soon) 캐릭터는 선택할 수 없습니다.
            if (!data.IsPlayable)
            {
                SetHint(data.LockedMessage, hintErrorColor);
                AudioManager.Play(SfxId.UiDenied);
                return;
            }

            Select(data, animate: true);
        }

        /// <summary>캐릭터 ID로 고릅니다. 인덱스 순서가 바뀌어도 안전한 쪽입니다.</summary>
        public void SelectById(string characterId)
        {
            if (catalog == null || !catalog.TryGet(characterId, out var data)) return;
            Select(data, animate: true);
        }

        private void OnCharacterClicked(CharacterSelectButton source)
        {
            var data = source != null ? source.Data : null;
            if (data == null) return;

            // 잠긴(Coming Soon) 캐릭터는 흔들림 피드백만 주고 선택하지 않습니다.
            if (!data.IsPlayable)
            {
                StartCoroutine(source.ShakeRoutine());
                SetHint(data.LockedMessage, hintErrorColor);
                AudioManager.Play(SfxId.UiDenied);
                PopupService.Toast(data.LockedMessage);
                return;
            }

            Select(data, animate: true);
        }

        private void Select(CharacterData data, bool animate)
        {
            if (data == null) return;

            current = data;

            // 처음 화면을 채울 때(animate=false)는 조용히 두고, 실제로 고른 순간에만 울립니다.
            if (animate) AudioManager.Play(SfxId.CharacterFocus);

            foreach (var button in buttons)
                if (button != null)
                    button.SetSelected(button.Data == data, animate);

            StopAllTweens();
            infoRoutine = StartCoroutine(ApplyCharacterRoutine(data, animate));
            activeTweens.Add(infoRoutine);

            RefreshNameState();
        }

        /// <summary>정보 패널을 채우면서 페이드 · 슬라이드 · 슬라이더 채우기를 함께 재생합니다.</summary>
        private IEnumerator ApplyCharacterRoutine(CharacterData data, bool animate)
        {
            SetText(characterNameText, data.DisplayName);
            SetText(roleText, data.RoleName);
            SetText(descriptionText, data.Description);
            SetText(skillText, BuildSkillLine(data));

            for (var i = 0; i < statRows.Length; i++)
            {
                var row = statRows[i];
                if (row == null) continue;
                SetText(row.label, CharacterData.GetStatLabel((CharacterStatType)i));
                PrepareSlider(row.slider);
                if (row.slider != null) row.slider.value = 0f; // 항상 0에서 다시 시작한다.
                if (row.value != null) row.value.text = "0";
            }

            // 별 · 스킬 · 패널은 어색한 팝/페이드/슬라이드 없이 즉시 반영한다.
            ApplyStars(data.Difficulty, false);
            ApplySkillSlots(data, false);
            if (infoGroup != null) infoGroup.alpha = 1f;
            RestoreInfoPosition();

            if (!animate)
            {
                for (var i = 0; i < statRows.Length; i++)
                    ApplyStatImmediate(statRows[i], data.GetStat(i));
                yield break;
            }

            // 캐릭터를 새로 고르면 스탯 슬라이더만 0에서 목표치까지 차오른다.
            for (var i = 0; i < statRows.Length; i++)
            {
                var row = statRows[i];
                var target = data.GetStat(i);
                if (row != null)
                    activeTweens.Add(StartCoroutine(FillStatRoutine(row, target, i * statStagger)));
            }
        }

        private IEnumerator FadeAndSlideRoutine()
        {
            CacheInfoPosition();

            var duration = Mathf.Max(0.01f, panelFadeDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));

                if (infoGroup != null) infoGroup.alpha = Mathf.Lerp(0.25f, 1f, t);
                if (infoRoot != null && infoPositionCached)
                    infoRoot.localPosition = infoOriginalPosition +
                        new Vector3(0f, Mathf.Lerp(-panelSlideDistance, 0f, t), 0f);

                yield return null;
            }

            if (infoGroup != null) infoGroup.alpha = 1f;
            RestoreInfoPosition();
        }

        /// <summary>슬라이더를 0에서 목표치까지 채우고 숫자도 같은 속도로 올립니다.</summary>
        private IEnumerator FillStatRoutine(StatRow row, int target, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            var duration = Mathf.Max(0.01f, statFillDuration);
            var elapsed = 0f;
            var lastShownValue = -1;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                var shown = target * t;

                if (row.slider != null) row.slider.value = shown;
                var rounded = Mathf.RoundToInt(shown);
                if (row.value != null) row.value.text = rounded.ToString();
                // 숫자가 실제로 올라간 프레임에만 아주 작은 틱을 냅니다.
                if (rounded != lastShownValue)
                {
                    lastShownValue = rounded;
                    AudioManager.Play(SfxId.StatTick);
                }
                yield return null;
            }

            ApplyStatImmediate(row, target);
        }

        private void ApplyStatImmediate(StatRow row, int target)
        {
            if (row == null) return;
            PrepareSlider(row.slider);
            if (row.slider != null) row.slider.value = target;
            if (row.value != null) row.value.text = target.ToString();
        }

        private static void PrepareSlider(Slider slider)
        {
            if (slider == null) return;
            slider.wholeNumbers = false;
            slider.minValue = CharacterData.MinStat;
            slider.maxValue = CharacterData.MaxStat;
            slider.interactable = false;
        }

        private void ApplyStars(int difficulty, bool animate)
        {
            if (difficultyStars == null) return;

            for (var i = 0; i < difficultyStars.Length; i++)
            {
                var star = difficultyStars[i];
                if (star == null) continue;

                star.color = i < difficulty ? starOnColor : starOffColor;
                if (animate && i < difficulty)
                    activeTweens.Add(StartCoroutine(PopRoutine(star.rectTransform, i * 0.055f)));
                else
                    star.rectTransform.localScale = Vector3.one;
            }
        }

        private void ApplySkillSlots(CharacterData data, bool animate)
        {
            if (skillSlots == null) return;

            var skillSet = data.SkillSet;
            for (var i = 0; i < skillSlots.Length; i++)
            {
                var slot = skillSlots[i];
                if (slot == null) continue;

                var skill = skillSet != null && i < SkillSection.SkillCount
                    ? skillSet.GetSection(0)?.GetSkill(i)
                    : null;

                if (skill != null && skill.Icon != null)
                {
                    slot.sprite = skill.Icon;
                    slot.color = Color.white;
                }
                else
                {
                    // 아직 스킬을 채우지 않은 슬롯은 캐릭터 색을 옅게 깔아 자리만 표시한다.
                    var tint = data.ThemeColor;
                    tint.a = i == 0 ? 0.55f : 0.20f;
                    slot.color = tint;
                }

                if (animate)
                    activeTweens.Add(StartCoroutine(PopRoutine(slot.rectTransform, 0.10f + i * 0.045f)));
                else
                    slot.rectTransform.localScale = Vector3.one;
            }
        }

        private string BuildSkillLine(CharacterData data)
        {
            var starting = data.StartingSkill;
            if (starting != null)
                return $"시작 스킬 {starting.DisplayName} · 나머지 스킬은 Lv.5부터 순차 해금됩니다";

            return data.SkillSet != null
                ? "시작 스킬을 스킬 관리 창에서 지정하세요"
                : "스킬 세트가 아직 연결되지 않았습니다";
        }

        // ------------------------------------------------------------------
        // 이름 입력 · 모험 시작
        // ------------------------------------------------------------------

        private void SetupNameInput()
        {
            if (nameInput == null) return;

            nameInput.characterLimit = CharacterData.MaxNameLength;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.onValueChanged.RemoveListener(OnNameChanged);
            nameInput.onValueChanged.AddListener(OnNameChanged);
            nameInput.onSubmit.RemoveListener(OnNameSubmitted);
            nameInput.onSubmit.AddListener(OnNameSubmitted);
        }

        private void SetupStartButton()
        {
            if (startButton == null) return;
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnNameChanged(string _) => RefreshNameState();

        private void OnNameSubmitted(string _)
        {
            if (startButton != null && startButton.interactable) OnStartClicked();
        }

        /// <summary>이름 규칙과 캐릭터 선택 상태를 함께 보고 시작 버튼을 열고 닫습니다.</summary>
        private void RefreshNameState()
        {
            var typed = nameInput != null ? nameInput.text : string.Empty;
            var nameValid = CharacterData.IsValidPlayerName(typed, out var message);
            var characterReady = current != null && current.IsPlayable;

            if (current != null && !current.IsPlayable)
            {
                SetHint(current.LockedMessage, hintErrorColor);
            }
            else if (string.IsNullOrEmpty(typed?.Trim()))
            {
                SetHint(message, hintNeutralColor);
            }
            else
            {
                SetHint(message, nameValid ? hintOkColor : hintErrorColor);
            }

            var ready = nameValid && characterReady;
            if (startButton != null) startButton.interactable = ready;
            if (startButtonLabel != null)
            {
                var color = startButtonLabel.color;
                color.a = ready ? 1f : 0.45f;
                startButtonLabel.color = color;
            }
        }

        private void SetHint(string message, Color color)
        {
            if (nameHintText == null) return;
            nameHintText.text = message;
            nameHintText.color = color;
        }

        private void OnStartClicked()
        {
            if (isStarting) return;
            if (current == null || !current.IsPlayable) return;

            var playerName = nameInput != null ? nameInput.text.Trim() : string.Empty;
            if (!CharacterData.IsValidPlayerName(playerName, out var message))
            {
                SetHint(message, hintErrorColor);
                AudioManager.Play(SfxId.UiDenied);
                PopupService.Toast(message);
                return;
            }

            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogWarning("다음 씬 이름이 비어 있어 씬 전환을 건너뜁니다.", this);
                return;
            }

            // 직업과 이름은 계정에 그대로 저장되어 되돌릴 수 없으므로, 두 개짜리 버튼으로 한 번 더 묻는다.
            PopupService.Confirm(
                "모험 시작",
                $"{current.DisplayName} · {playerName}(으)로 시작합니다.\n직업과 이름은 바꿀 수 없습니다.",
                onConfirm: () => _ = StartAdventureAsync(playerName),
                confirmLabel: "시작",
                cancelLabel: "취소");
        }

        private async Task StartAdventureAsync(string playerName)
        {
            if (isStarting) return;
            isStarting = true;
            if (startButton != null) startButton.interactable = false;
            AudioManager.Play(SfxId.AdventureStart);

            // 로컬 캐시(빠른 참조용)와 계정 저장본(Firestore)에 모두 남긴다.
            CharacterSelection.Save(current.CharacterId, playerName);
            try
            {
                await PlayerDataManager.Instance.SetCharacterAsync(current.CharacterId, playerName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (this == null) return;

            // 페이드 아웃 -> Main 로드 -> 페이드 인.
            SceneTransition.Instance.LoadScene(nextSceneName);
        }

        // ------------------------------------------------------------------
        // 연출 도우미
        // ------------------------------------------------------------------

        private IEnumerator PopRoutine(RectTransform target, float delay)
        {
            if (target == null) yield break;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            var duration = Mathf.Max(0.01f, popDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // 0.6 -> 1.12 -> 1.0 로 살짝 튀어 오른다.
                var scale = t < 0.6f
                    ? Mathf.Lerp(0.6f, 1.12f, EaseOutCubic(t / 0.6f))
                    : Mathf.Lerp(1.12f, 1f, EaseOutCubic((t - 0.6f) / 0.4f));
                target.localScale = Vector3.one * scale;
                yield return null;
            }

            target.localScale = Vector3.one;
        }


        /// <summary>이전 선택의 연출이 남아 새 수치를 덮어쓰지 않도록 모두 멈춥니다.</summary>
        private void StopAllTweens()
        {
            foreach (var tween in activeTweens)
                if (tween != null)
                    StopCoroutine(tween);

            activeTweens.Clear();
            infoRoutine = null;
        }

        private void CacheInfoPosition()
        {
            if (infoPositionCached || infoRoot == null) return;
            infoOriginalPosition = infoRoot.localPosition;
            infoPositionCached = true;
        }

        private void RestoreInfoPosition()
        {
            if (infoRoot != null && infoPositionCached)
                infoRoot.localPosition = infoOriginalPosition;
        }

        private static float EaseOutCubic(float t)
        {
            var inverted = 1f - Mathf.Clamp01(t);
            return 1f - inverted * inverted * inverted;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        // ------------------------------------------------------------------
        // 자동 연결
        // ------------------------------------------------------------------

        /// <summary>
        /// 씬의 오브젝트 이름을 기준으로 비어 있는 참조만 채웁니다.
        /// 인스펙터에서 직접 연결한 값은 건드리지 않습니다.
        /// </summary>
        public void AutoBind()
        {
            var info = FindInScene("CharacterInfo");
            if (info != null)
            {
                infoRoot ??= info as RectTransform;
                if (infoGroup == null && infoRoot != null)
                    infoGroup = infoRoot.GetComponent<CanvasGroup>() ??
                                infoRoot.gameObject.AddComponent<CanvasGroup>();

                characterNameText ??= FindText(info, "Horizontal (1)/Title");
                roleText ??= FindText(info, "Level");
                descriptionText ??= FindText(info, "Des");
                skillText ??= FindText(info, "Skill");

                if (IsEmpty(difficultyStars))
                    difficultyStars = CollectChildren<Image>(info.Find("Star"), CharacterData.MaxDifficulty);

                if (IsEmpty(skillSlots))
                    skillSlots = CollectChildren<Image>(info.Find("Horizontal"), 4);

                if (IsEmpty(statRows))
                    statRows = CollectStatRows(info.Find("Grid"));
            }

            var nameInputRoot = FindInScene("NameInput");
            if (nameInputRoot != null)
            {
                nameInput ??= nameInputRoot.GetComponentInChildren<TMP_InputField>(true);
                nameHintText ??= FindText(nameInputRoot, "Title");
            }

            if (startButton == null)
            {
                var candidate = FindInScene("StartBtn") ?? FindStartButtonFallback();
                if (candidate != null)
                {
                    startButton = candidate.GetComponent<Button>();
                    if (startButton == null) startButton = candidate.gameObject.AddComponent<Button>();
                    startButtonLabel ??= candidate.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (buttonContainer == null)
            {
                var container = FindInScene("CharacterButtons");
                if (container != null) buttonContainer = container as RectTransform;
            }
        }

        /// <summary>SafeArea 바로 아래에 있는, TMP 한 개만 들고 있는 Image를 시작 버튼으로 본다.</summary>
        private Transform FindStartButtonFallback()
        {
            var safeArea = FindInScene("SafeArea");
            if (safeArea == null) return null;

            for (var i = safeArea.childCount - 1; i >= 0; i--)
            {
                var child = safeArea.GetChild(i);
                if (child.name != "Image") continue;
                if (child.GetComponent<Image>() == null) continue;
                if (child.childCount == 1 && child.GetComponentInChildren<TMP_Text>(true) != null)
                    return child;
            }

            return null;
        }

        private static StatRow[] CollectStatRows(Transform grid)
        {
            var rows = new StatRow[CharacterData.StatCount];
            if (grid == null) return rows;

            var filled = 0;
            for (var i = 0; i < grid.childCount && filled < rows.Length; i++)
            {
                var child = grid.GetChild(i);
                if (!child.name.StartsWith("Status", StringComparison.Ordinal)) continue;

                var texts = child.GetComponentsInChildren<TMP_Text>(true);
                rows[filled] = new StatRow
                {
                    label = texts.Length > 0 ? texts[0] : null,
                    slider = child.GetComponentInChildren<Slider>(true),
                    value = texts.Length > 1 ? texts[texts.Length - 1] : null
                };
                filled++;
            }

            return rows;
        }

        private static T[] CollectChildren<T>(Transform parent, int count) where T : Component
        {
            var result = new T[count];
            if (parent == null) return result;

            var filled = 0;
            for (var i = 0; i < parent.childCount && filled < count; i++)
            {
                var component = parent.GetChild(i).GetComponent<T>();
                if (component == null) continue;
                result[filled++] = component;
            }

            return result;
        }

        private static bool IsEmpty<T>(IReadOnlyList<T> values) where T : class
        {
            if (values == null || values.Count == 0) return true;
            foreach (var value in values)
                if (value != null)
                    return false;

            return true;
        }

        private static TMP_Text FindText(Transform root, string path)
        {
            var child = root.Find(path);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindInScene(string objectName)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var match = FindChild(root.transform, objectName);
                if (match != null) return match;
            }

            return null;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }
    }

    /// <summary>고른 캐릭터와 이름을 기기에 남겨 두어, 다음 실행에서 Select 씬을 건너뛰게 합니다.</summary>
    public static class CharacterSelection
    {
        private const string CharacterIdKey = "selected_character_id";
        private const string PlayerNameKey = "selected_character_name";

        public static bool HasSelection =>
            !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(CharacterIdKey, string.Empty));

        public static string CharacterId => PlayerPrefs.GetString(CharacterIdKey, string.Empty);

        public static string PlayerName => PlayerPrefs.GetString(PlayerNameKey, string.Empty);

        public static void Save(string characterId, string playerName)
        {
            PlayerPrefs.SetString(CharacterIdKey, characterId ?? string.Empty);
            PlayerPrefs.SetString(PlayerNameKey, playerName ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(CharacterIdKey);
            PlayerPrefs.DeleteKey(PlayerNameKey);
            PlayerPrefs.Save();
        }
    }
}
