using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>Switches the persistent bottom navigation between Main and Equipment.</summary>
    [DisallowMultipleComponent]
    public sealed class BottomNavigationController : MonoBehaviour
    {
        private const int EquipmentIndex = 0;
        private const int MainIndex = 2;

        [SerializeField] private GameObject mainScreen;
        [SerializeField] private GameObject equipmentScreen;
        [SerializeField] private Sprite normalFrame;
        [SerializeField] private Sprite selectedFrame;
        [SerializeField] private Image selectionIndicator;
        [SerializeField, HideInInspector] private int setupVersion;

        private readonly NavigationVisual[] visuals = new NavigationVisual[5];
        private Coroutine transitionRoutine;
        private int selectedIndex = MainIndex;

        private static readonly Color NormalText = new(0.88f, 0.84f, 0.76f, 1f);
        private static readonly Color SelectedText = new(1f, 0.86f, 0.72f, 1f);

        public int SetupVersion => setupVersion;

        private void Awake()
        {
            // Main and Equipment are full-screen opaque siblings. Keep the
            // persistent navigation above both regardless of scene save order.
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            CacheNavigation();
            WireButtons();
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
            if (visuals[EquipmentIndex]?.Button != null)
            {
                visuals[EquipmentIndex].Button.onClick.AddListener(() => Select(EquipmentIndex));
            }

            if (visuals[MainIndex]?.Button != null)
            {
                visuals[MainIndex].Button.onClick.AddListener(() => Select(MainIndex));
            }
        }

        public void Select(int index)
        {
            if (index != EquipmentIndex && index != MainIndex) return;
            if (index == selectedIndex && transitionRoutine == null)
            {
                StartCoroutine(BounceSelected(index));
                return;
            }

            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionTo(index));
        }

        private void ShowImmediate(int index)
        {
            selectedIndex = index;
            if (mainScreen != null) mainScreen.SetActive(index == MainIndex);
            if (equipmentScreen != null) equipmentScreen.SetActive(index == EquipmentIndex);
            ApplyNavigationState(index, false, index);
        }

        private IEnumerator TransitionTo(int index)
        {
            var previousIndex = selectedIndex;
            selectedIndex = index;
            ApplyNavigationState(index, true, previousIndex);

            var previous = previousIndex == MainIndex ? mainScreen : equipmentScreen;
            var next = index == MainIndex ? mainScreen : equipmentScreen;
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
