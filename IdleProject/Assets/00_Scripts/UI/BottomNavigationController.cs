using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>Switches the persistent bottom navigation between the four game screens.</summary>
    [DisallowMultipleComponent]
    public sealed class BottomNavigationController : MonoBehaviour
    {
        private const int EquipmentIndex = 0;
        private const int DungeonIndex = 1;
        private const int MainIndex = 2;
        private const int SkillIndex = 3;

        [SerializeField] private GameObject mainScreen;
        [SerializeField] private GameObject equipmentScreen;
        [SerializeField] private GameObject skillScreen;
        [SerializeField] private GameObject dungeonScreen;
        [SerializeField] private Sprite normalFrame;
        [SerializeField] private Sprite selectedFrame;
        [SerializeField] private Image selectionIndicator;
        [SerializeField, HideInInspector] private int setupVersion;

        private readonly NavigationVisual[] visuals = new NavigationVisual[5];
        private Coroutine transitionRoutine;
        private int selectedIndex = MainIndex;
        private GameObject userInfoPanel;
        private Button infoButton;
        private Button userInfoCloseButton;

        private static readonly Color NormalText = new(0.88f, 0.84f, 0.76f, 1f);
        private static readonly Color SelectedText = new(1f, 0.86f, 0.72f, 1f);

        public int SetupVersion => setupVersion;

        private void Awake()
        {
            // Main and Equipment are full-screen opaque siblings. Keep the
            // persistent navigation above both regardless of scene save order.
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ResolveScreens();
            CacheNavigation();
            WireButtons();
            WireUserInfo();
            ShowImmediate(MainIndex);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < visuals.Length; i++)
            {
                if (visuals[i]?.Button != null)
                {
                    visuals[i].Button.onClick.RemoveAllListeners();
                }
            }

            if (infoButton != null) infoButton.onClick.RemoveListener(ShowUserInfo);
            if (userInfoCloseButton != null) userInfoCloseButton.onClick.RemoveListener(HideUserInfo);
        }

        private void ResolveScreens()
        {
            var canvas = transform.parent;
            if (canvas == null) return;

            if (mainScreen == null) mainScreen = canvas.Find("Main")?.gameObject;
            if (equipmentScreen == null) equipmentScreen = canvas.Find("Equipment")?.gameObject;
            if (skillScreen == null) skillScreen = canvas.Find("Skill")?.gameObject;
            if (dungeonScreen == null) dungeonScreen = canvas.Find("Dungeon")?.gameObject;
        }

        private void CacheNavigation()
        {
            Canvas.ForceUpdateCanvases();
            if (transform is RectTransform bottomRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(bottomRect);

            for (var i = 0; i < visuals.Length && i < transform.childCount; i++)
            {
                var item = transform.GetChild(i) as RectTransform;
                if (item == null) continue;

                var iconFrame = item.Find("IconFrame");
                var label = item.Find("Name");
                var frameRect = iconFrame as RectTransform;
                visuals[i] = new NavigationVisual
                {
                    Root = item,
                    Button = item.GetComponent<Button>(),
                    Frame = iconFrame != null ? iconFrame.GetComponent<Image>() : null,
                    Label = label != null ? label.GetComponent<TMP_Text>() : null,
                    FrameRect = frameRect,
                    RestingFramePosition = frameRect != null ? frameRect.anchoredPosition : Vector2.zero
                };
            }
        }

        private void WireButtons()
        {
            var screenIndices = new[] { EquipmentIndex, DungeonIndex, MainIndex, SkillIndex };
            foreach (var index in screenIndices)
            {
                var capturedIndex = index;
                if (visuals[capturedIndex]?.Button != null)
                    visuals[capturedIndex].Button.onClick.AddListener(() => Select(capturedIndex));
            }
        }

        private void WireUserInfo()
        {
            if (equipmentScreen == null) return;

            var equipment = equipmentScreen.transform;
            userInfoPanel = equipment.Find("UserInfo")?.gameObject;
            var info = equipment.Find("SafeArea/Character_Equipments/Character_Info/Chracter_Info/InfoBtn");
            var close = equipment.Find("UserInfo/InfoPanel/CloseBtn");

            infoButton = EnsureButton(info);
            userInfoCloseButton = EnsureButton(close);
            if (infoButton != null) infoButton.onClick.AddListener(ShowUserInfo);
            if (userInfoCloseButton != null)
            {
                userInfoCloseButton.onClick.AddListener(HideUserInfo);
            }
            HideUserInfo();
        }

        private static Button EnsureButton(Transform target)
        {
            if (target == null) return null;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            var graphic = target.GetComponent<Image>();
            if (graphic != null) graphic.raycastTarget = true;
            if (button.targetGraphic == null) button.targetGraphic = graphic;
            button.enabled = true;
            button.interactable = true;
            return button;
        }

        private void ShowUserInfo()
        {
            if (userInfoPanel == null) return;
            userInfoPanel.SetActive(true);
            userInfoPanel.transform.SetAsLastSibling();
        }

        private void HideUserInfo()
        {
            if (userInfoPanel != null) userInfoPanel.SetActive(false);
        }

        public void Select(int index)
        {
            if (!IsScreenIndex(index)) return;
            // Ignore repeated taps even while the first transition is running.
            // Restarting a transition to the same screen makes previous and next
            // reference the same object, which would deactivate it at completion.
            if (index == selectedIndex) return;

            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionTo(index));
        }

        private void ShowImmediate(int index)
        {
            selectedIndex = index;
            SetOnlyScreenActive(index);
            ApplyNavigationState(index, false, index);
        }

        private IEnumerator TransitionTo(int index)
        {
            var previousIndex = selectedIndex;
            selectedIndex = index;
            if (index != EquipmentIndex) HideUserInfo();
            ApplyNavigationState(index, true, previousIndex);

            var previous = ScreenFor(previousIndex);
            var next = ScreenFor(index);
            if (next == null)
            {
                transitionRoutine = null;
                yield break;
            }

            next.SetActive(true);
            var nextGroup = GetCanvasGroup(next);
            nextGroup.alpha = 0f;
            nextGroup.blocksRaycasts = false;

            CanvasGroup previousGroup = null;
            if (previous != null)
            {
                previousGroup = GetCanvasGroup(previous);
                previousGroup.blocksRaycasts = false;
            }

            const float duration = 0.28f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                nextGroup.alpha = eased;
                if (previousGroup != null) previousGroup.alpha = 1f - eased;
                yield return null;
            }

            nextGroup.alpha = 1f;
            nextGroup.blocksRaycasts = true;
            if (previous != null)
            {
                if (previousGroup != null) previousGroup.alpha = 1f;
                previous.SetActive(false);
            }

            transitionRoutine = null;
        }

        private void SetOnlyScreenActive(int activeIndex)
        {
            SetScreenActive(mainScreen, activeIndex == MainIndex);
            SetScreenActive(equipmentScreen, activeIndex == EquipmentIndex);
            SetScreenActive(skillScreen, activeIndex == SkillIndex);
            SetScreenActive(dungeonScreen, activeIndex == DungeonIndex);
            if (activeIndex != EquipmentIndex) HideUserInfo();
        }

        private static void SetScreenActive(GameObject screen, bool active)
        {
            if (screen != null) screen.SetActive(active);
        }

        private GameObject ScreenFor(int index)
        {
            return index switch
            {
                EquipmentIndex => equipmentScreen,
                DungeonIndex => dungeonScreen,
                MainIndex => mainScreen,
                SkillIndex => skillScreen,
                _ => null
            };
        }

        private static bool IsScreenIndex(int index)
        {
            return index == EquipmentIndex || index == DungeonIndex ||
                   index == MainIndex || index == SkillIndex;
        }

        private void ApplyNavigationState(int activeIndex, bool animate, int previousIndex)
        {
            for (var i = 0; i < visuals.Length; i++)
            {
                var visual = visuals[i];
                if (visual == null) continue;
                var selected = i == activeIndex;
                if (visual.Frame != null) visual.Frame.sprite = selected ? selectedFrame : normalFrame;
                if (visual.Label != null) visual.Label.color = selected ? SelectedText : NormalText;
                if (!animate && visual.FrameRect != null)
                    visual.FrameRect.anchoredPosition = visual.RestingFramePosition + (selected ? Vector2.up * 12f : Vector2.zero);
            }

            if (selectionIndicator != null)
            {
                selectionIndicator.gameObject.SetActive(true);
                if (!animate)
                {
                    var position = selectionIndicator.rectTransform.anchoredPosition;
                    position.x = IndicatorX(activeIndex);
                    selectionIndicator.rectTransform.anchoredPosition = position;
                }
            }

            if (animate) StartCoroutine(AnimateNavigation(previousIndex, activeIndex));
        }

        private IEnumerator AnimateNavigation(int previousIndex, int activeIndex)
        {
            var previous = previousIndex >= 0 && previousIndex < visuals.Length ? visuals[previousIndex] : null;
            var active = activeIndex >= 0 && activeIndex < visuals.Length ? visuals[activeIndex] : null;
            if (active == null) yield break;

            var indicatorStart = selectionIndicator != null ? selectionIndicator.rectTransform.anchoredPosition.x : 0f;
            var indicatorEnd = IndicatorX(activeIndex);
            const float duration = 0.26f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = t * t * (3f - 2f * t);
                if (previous?.FrameRect != null)
                    previous.FrameRect.anchoredPosition = Vector2.Lerp(previous.RestingFramePosition + Vector2.up * 12f, previous.RestingFramePosition, eased);
                if (active.FrameRect != null)
                    active.FrameRect.anchoredPosition = Vector2.Lerp(active.RestingFramePosition, active.RestingFramePosition + Vector2.up * 12f, eased);
                if (selectionIndicator != null)
                {
                    var position = selectionIndicator.rectTransform.anchoredPosition;
                    position.x = Mathf.Lerp(indicatorStart, indicatorEnd, eased);
                    selectionIndicator.rectTransform.anchoredPosition = position;
                }
                yield return null;
            }
            if (previous?.FrameRect != null) previous.FrameRect.anchoredPosition = previous.RestingFramePosition;
            if (active.FrameRect != null) active.FrameRect.anchoredPosition = active.RestingFramePosition + Vector2.up * 12f;
        }

        private IEnumerator BounceSelected(int index)
        {
            var active = index >= 0 && index < visuals.Length ? visuals[index] : null;
            if (active?.FrameRect == null) yield break;
            var raised = active.RestingFramePosition + Vector2.up * 12f;
            const float duration = 0.18f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                var lift = Mathf.Sin(t * Mathf.PI) * 4f;
                active.FrameRect.anchoredPosition = raised + Vector2.up * lift;
                yield return null;
            }
            active.FrameRect.anchoredPosition = raised;
        }

        private float IndicatorX(int index)
        {
            var visual = index >= 0 && index < visuals.Length ? visuals[index] : null;
            if (visual?.Root == null) return 0f;
            return transform.InverseTransformPoint(visual.Root.TransformPoint(visual.Root.rect.center)).x;
        }

        private static CanvasGroup GetCanvasGroup(GameObject target)
        {
            return target.TryGetComponent<CanvasGroup>(out var group) ? group : target.AddComponent<CanvasGroup>();
        }

        private sealed class NavigationVisual
        {
            public RectTransform Root;
            public Button Button;
            public Image Frame;
            public TMP_Text Label;
            public RectTransform FrameRect;
            public Vector2 RestingFramePosition;
        }
    }
}
