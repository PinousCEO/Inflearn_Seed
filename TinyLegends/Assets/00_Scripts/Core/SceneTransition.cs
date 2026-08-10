using System.Collections;
using IdleBattle.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle
{
    /// <summary>
    /// 씬 전환용 페이드 오버레이입니다. 게임 시작 시 코드로 자동 생성되어
    /// DontDestroyOnLoad로 살아남기 때문에, 어떤 씬에도 별도 배치가 필요 없습니다.
    ///
    /// - 게임을 처음 켰을 때: 화면을 덮은 상태에서 시작해 첫 씬을 서서히 밝힙니다(페이드 인).
    /// - <see cref="LoadScene"/>: 화면을 덮고(페이드 아웃) → 씬을 비동기 로드 → 다시 밝힙니다(페이드 인).
    /// - <see cref="CoverRoutine"/>: 로딩바처럼 스스로 씬을 활성화하는 쪽에서, 활성화 직전에 화면만 덮을 때 씁니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-2000)]
    public sealed class SceneTransition : MonoBehaviour
    {
        private const float DefaultFadeDuration = 0.35f;

        private static SceneTransition instance;

        public static SceneTransition Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<SceneTransition>();
                if (instance == null)
                {
                    var host = new GameObject(nameof(SceneTransition));
                    instance = host.AddComponent<SceneTransition>();
                }

                return instance;
            }
        }

        /// <summary>페이드 색. 기본은 검정이며, 흰색으로 바꾸려면 <see cref="Color.white"/>를 넣으면 됩니다.</summary>
        public static Color FadeColor { get; set; } = Color.black;

        private CanvasGroup group;
        private Image overlay;
        private Coroutine routine;
        private bool suppressAutoReveal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // 첫 씬이 뜨기 전에 미리 만들어 두어야, 인트로 페이드 인이 자연스럽게 나온다.
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();

            // 시작은 완전히 덮은 상태 -> 첫 씬을 서서히 밝힌다(인트로 페이드 인).
            SetAlpha(1f);

            SceneManager.sceneLoaded += OnSceneLoaded;

            // sceneLoaded가 첫 씬에서 누락되는 경우를 대비한 안전장치.
            routine = StartCoroutine(RevealNextFrameRoutine(DefaultFadeDuration));
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        /// <summary>화면을 덮었다가(페이드 아웃) 대상 씬을 비동기로 로드하고 다시 밝힙니다(페이드 인).</summary>
        public void LoadScene(string sceneName, float fadeDuration = DefaultFadeDuration)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("전환할 씬 이름이 비어 있습니다.", this);
                return;
            }

            if (routine != null) return;
            routine = StartCoroutine(TransitionRoutine(sceneName, fadeDuration));
        }

        /// <summary>
        /// 화면을 덮기만 합니다(페이드 아웃). 로딩바처럼 스스로 <c>allowSceneActivation</c>을 켜는 쪽에서,
        /// 활성화 직전에 <c>yield return</c>으로 부르면 됩니다. 새 씬이 로드되면 자동으로 다시 밝힙니다.
        /// </summary>
        public IEnumerator CoverRoutine(float fadeDuration = DefaultFadeDuration)
        {
            ApplyColor();
            AudioManager.Play(SfxId.SceneFade);
            yield return FadeRoutine(1f, fadeDuration);
        }

        private IEnumerator TransitionRoutine(string sceneName, float fadeDuration)
        {
            // 이 경로는 커버/리빌을 직접 다루므로, sceneLoaded 자동 리빌은 잠시 끈다.
            suppressAutoReveal = true;

            ApplyColor();
            AudioManager.Play(SfxId.SceneFade);
            yield return FadeRoutine(1f, fadeDuration);

            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError($"'{sceneName}' 씬을 로드할 수 없습니다. Build Settings에 추가했는지 확인하세요.", this);
                suppressAutoReveal = false;
                routine = null;
                yield break;
            }

            while (!operation.isDone) yield return null;

            // 새 씬의 첫 프레임이 그려진 뒤에 밝혀야 깜빡임이 없다.
            yield return null;
            yield return FadeRoutine(0f, fadeDuration);

            suppressAutoReveal = false;
            routine = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (suppressAutoReveal) return;
            if (group == null || group.alpha <= 0f) return;

            // 인트로/로딩바 경로: 덮인 채로 새 씬이 떴으니 서서히 밝힌다.
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(RevealNextFrameRoutine(DefaultFadeDuration));
        }

        private IEnumerator RevealNextFrameRoutine(float fadeDuration)
        {
            yield return null;
            yield return FadeRoutine(0f, fadeDuration);
            routine = null;
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            if (group == null) yield break;

            var start = group.alpha;
            duration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(start, targetAlpha, t));
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        private void SetAlpha(float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.001f;
        }

        private void ApplyColor()
        {
            if (overlay == null) return;
            var color = FadeColor;
            color.a = 1f;
            overlay.color = color;
        }

        private void BuildOverlay()
        {
            var canvasObject = new GameObject("FadeCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // 어떤 UI보다도 위에 오도록.

            // 페이드 중에는 아래 UI로 클릭이 새지 않도록 레이캐스터로 막는다(CanvasGroup.blocksRaycasts와 함께).
            canvasObject.AddComponent<GraphicRaycaster>();

            group = canvasObject.AddComponent<CanvasGroup>();
            group.interactable = false;

            var imageObject = new GameObject("Overlay");
            imageObject.transform.SetParent(canvasObject.transform, false);

            overlay = imageObject.AddComponent<Image>();
            overlay.raycastTarget = true;
            ApplyColor();

            var rect = overlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
