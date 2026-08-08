using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle.UI
{
    /// <summary>
    /// Select 씬에서 아직 해금되지 않은(선택 불가) 캐릭터의 이미지 위에 "COMING SOON..." 라벨을 띄웁니다.
    /// 캐릭터가 그려지는 RawImage 위에, 캐릭터의 월드 위치를 화면 좌표로 투영해 UI 텍스트를 얹기 때문에
    /// 크기·위치가 어긋나지 않고 확실히 보입니다. 위아래로 살짝 떠다니며 말줄임표가 순환합니다.
    ///
    /// Select 씬이 로드되면 자동으로 하나 생성됩니다. 위치·크기를 인스펙터에서 조절하려면
    /// 씬의 오브젝트에 이 컴포넌트를 직접 붙이면 됩니다(그 경우 자동 생성은 건너뜁니다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComingSoonPresenter : MonoBehaviour
    {
        private const string SelectSceneName = "Select";

        [Header("연결 (비워 두면 씬에서 자동으로 찾습니다)")]
        [SerializeField] private CharacterSunRaySelector raySelector;
        [SerializeField] private CharacterSelectController selectController;

        [Header("배치")]
        [Tooltip("캐릭터 발밑에서 라벨 기준점까지의 월드 높이입니다(머리 위 정도).")]
        [SerializeField] private float worldHeightOffset = 2.2f;
        [Tooltip("화면에서 위로 더 올리는 픽셀 오프셋입니다.")]
        [SerializeField] private float screenYOffset = 12f;

        [Header("모양")]
        [Tooltip("비워 두면 프로젝트의 'Outline' TMP 폰트를 자동으로 찾아 씁니다.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField, Min(1f)] private float fontSize = 30f;
        [SerializeField] private Vector2 size = new Vector2(280f, 64f);
        [SerializeField] private Color labelColor = Color.white;              // 흰색 글자
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 1f); // 검은 테두리

        [Header("애니메이션")]
        [SerializeField, Min(0f)] private float bobAmplitude = 6f;   // 픽셀
        [SerializeField, Min(0f)] private float bobSpeed = 2.2f;
        [SerializeField, Min(0.02f)] private float dotInterval = 0.35f;
        [SerializeField, Min(0)] private int maxDots = 3;

        private RectTransform rawImageRect;
        private Camera renderCamera;
        private readonly List<Label> labels = new List<Label>();
        private readonly StringBuilder builder = new StringBuilder(24);

        private sealed class Label
        {
            public Transform anchor;      // 대상 캐릭터
            public RectTransform rect;    // 라벨 UI
            public TextMeshProUGUI text;
            public CanvasGroup group;
            public string baseText;
            public float phase;           // bob 위상
            public int lastDots;          // 점 개수가 바뀔 때만 텍스트를 다시 만든다.
        }

        // ------------------------------------------------------------------
        // 자동 설치
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SelectSceneName) return;
            if (FindFirstObjectByType<ComingSoonPresenter>() != null) return;

            new GameObject(nameof(ComingSoonPresenter)).AddComponent<ComingSoonPresenter>();
        }

        private void Start()
        {
            if (raySelector == null) raySelector = FindFirstObjectByType<CharacterSunRaySelector>();
            if (selectController == null) selectController = FindFirstObjectByType<CharacterSelectController>();

            if (raySelector == null)
            {
                Debug.LogWarning("[ComingSoon] CharacterSunRaySelector를 찾지 못해 라벨을 만들지 못했습니다.", this);
                return;
            }

            renderCamera = raySelector.RenderCamera;
            rawImageRect = raySelector.transform as RectTransform; // SunRaySelector는 RawImage 위에 있습니다.
            var characters = raySelector.Characters;
            var catalog = selectController != null ? selectController.Catalog : null;

            if (renderCamera == null || rawImageRect == null || characters == null || catalog == null)
            {
                Debug.LogWarning("[ComingSoon] 카메라 · RawImage · 캐릭터 · 카탈로그 중 빠진 참조가 있습니다.", this);
                return;
            }

            var font = ResolveFont();
            var count = Mathf.Min(characters.Length, catalog.Count);
            for (var i = 0; i < count; i++)
            {
                var data = catalog.Get(i);
                var anchor = characters[i];
                if (data == null || anchor == null || data.IsPlayable) continue; // 해금된 캐릭터는 건너뛴다.

                var text = string.IsNullOrWhiteSpace(data.LockedLabel) ? "COMING SOON" : data.LockedLabel;
                labels.Add(CreateLabel(anchor, text, font, i * 0.7f));
            }
        }

        private void OnDestroy()
        {
            foreach (var label in labels)
                if (label != null && label.rect != null)
                    Destroy(label.rect.gameObject);
            labels.Clear();
        }

        private void LateUpdate()
        {
            if (renderCamera == null || rawImageRect == null || labels.Count == 0) return;

            var time = Time.time;
            var visibleDots = maxDots > 0 ? Mathf.FloorToInt(time / dotInterval) % (maxDots + 1) : 0;
            var canvasSize = rawImageRect.rect.size;

            foreach (var label in labels)
            {
                if (label == null || label.rect == null) continue;
                if (label.anchor == null)
                {
                    if (label.group != null) label.group.alpha = 0f;
                    continue;
                }

                // 캐릭터 머리 위 지점을 RenderTexture 카메라 뷰포트(0~1)로 투영 -> RawImage 안의 위치로 변환.
                var world = label.anchor.position + Vector3.up * worldHeightOffset;
                var viewport = renderCamera.WorldToViewportPoint(world);
                var visible = viewport.z > 0f;
                if (label.group != null) label.group.alpha = visible ? 1f : 0f;
                if (!visible) continue;

                var bob = Mathf.Sin(time * bobSpeed + label.phase) * bobAmplitude;
                label.rect.anchoredPosition = new Vector2(
                    viewport.x * canvasSize.x,
                    viewport.y * canvasSize.y + screenYOffset + bob);

                UpdateDots(label, visibleDots);
            }
        }

        private void UpdateDots(Label label, int visibleDots)
        {
            if (label.text == null || visibleDots == label.lastDots) return;
            label.lastDots = visibleDots;

            builder.Clear();
            builder.Append(label.baseText);
            builder.Append('.', visibleDots);
            if (maxDots - visibleDots > 0)
            {
                // 남는 점은 투명 처리해서 글자 폭이 흔들리지 않게 한다.
                builder.Append("<alpha=#00>");
                builder.Append('.', maxDots - visibleDots);
            }

            label.text.text = builder.ToString();
        }

        private Label CreateLabel(Transform anchor, string baseText, TMP_FontAsset font, float phase)
        {
            var go = new GameObject($"ComingSoon_{anchor.name}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(rawImageRect, false);
            rect.anchorMin = Vector2.zero; // 좌하단 기준 -> viewport(0~1) * 크기로 바로 배치.
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color = labelColor;
            text.text = baseText;

            if (outlineColor.a > 0f)
            {
                // fontMaterial에 접근하면 이 텍스트 전용 머티리얼 인스턴스가 생겨,
                // 외곽선이 같은 폰트를 쓰는 다른 UI 텍스트로 번지지 않는다.
                var material = text.fontMaterial;
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            }

            return new Label
            {
                anchor = anchor,
                rect = rect,
                text = text,
                group = group,
                baseText = baseText,
                phase = phase,
                lastDots = -1
            };
        }

        /// <summary>
        /// "Outline" TMP 폰트를 우선 사용합니다. 인스펙터에서 지정했으면 그것을, 아니면 프로젝트에서 찾습니다.
        /// </summary>
        private TMP_FontAsset ResolveFont()
        {
            if (labelFont != null) return labelFont;

            // 이미 메모리에 올라온 폰트 중 이름이 "Outline"인 것을 찾는다.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (candidate != null && candidate.name == "Outline")
                    return candidate;

#if UNITY_EDITOR
            // 아직 로드되지 않았으면 에디터에서 직접 불러온다.
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("Outline t:TMP_FontAsset"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null && font.name == "Outline") return font;
            }
#endif

            // 마지막 수단: 씬에서 쓰는 폰트, 그것도 없으면 기본 폰트.
            var sample = FindFirstObjectByType<TMP_Text>();
            if (sample != null && sample.font != null) return sample.font;
            return TMP_Settings.defaultFontAsset;
        }
    }
}
