using System.Collections;
using IdleBattle.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// RawImage를 누른 지점을 RenderTexture의 UV로 바꾸고, 그 자리에서 가장 가까운 캐릭터를 고릅니다.
    /// 고른 캐릭터 머리 위로 SunRay 스포트라이트를 옮겨 그 캐릭터만 밝게 비춥니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class CharacterSunRaySelector : MonoBehaviour, IPointerClickHandler
    {
        [Header("연결")]
        [Tooltip("RawImage의 RenderTexture를 그리는 카메라입니다.")]
        [SerializeField] private Camera renderCamera;
        [Tooltip("고른 캐릭터 위로 옮길 스포트라이트입니다.")]
        [SerializeField] private Light sunRayLight;
        [Tooltip("고를 수 있는 캐릭터입니다. 배열 순서는 CharacterCatalog 순서와 같아야 합니다.")]
        [SerializeField] private Transform[] characters = new Transform[5];
        [Tooltip("클릭한 캐릭터로 선택을 바꿀 컨트롤러입니다. CharacterInfo 패널이 함께 갱신됩니다.")]
        [SerializeField] private CharacterSelectController selectController;

        [Header("라이트")]
        [Tooltip("캐릭터 발밑에서 스포트라이트까지의 높이입니다.")]
        [SerializeField, Min(1f)] private float lightHeight = 12f;
        [SerializeField, Min(0f)] private float litIntensity = 200f;
        [Tooltip("빛이 옮겨가는 동안 잠깐 약해지는 정도입니다. 1이면 약해지지 않습니다.")]
        [SerializeField, Range(0.1f, 1f)] private float dipRatio = 0.45f;
        [SerializeField, Min(0f)] private float moveDuration = 0.3f;

        [Header("판정")]
        [Tooltip("클릭 지점에서 이 거리 안에 있는 캐릭터만 고릅니다. RawImage 세로 길이 대비 비율입니다.")]
        [SerializeField, Range(0.05f, 0.6f)] private float pickRadius = 0.22f;
        [Tooltip("발밑이 아니라 몸통 높이를 기준으로 거리를 잽니다.")]
        [SerializeField] private float bodyHeight = 1f;
        [Tooltip("이미 고른 캐릭터를 다시 누르면 무시합니다.")]
        [SerializeField] private bool ignoreRepeatPick = true;

        /// <summary>캐릭터를 새로 골랐을 때 인덱스를 넘깁니다.</summary>
        public UnityEvent<int> onCharacterPicked = new UnityEvent<int>();

        /// <summary>현재 고른 캐릭터 인덱스입니다. 아직 아무것도 고르지 않았으면 -1입니다.</summary>
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>RenderTexture를 그리는 카메라입니다. Coming Soon 라벨 등 외부 연출이 참조합니다.</summary>
        public Camera RenderCamera => renderCamera;

        /// <summary>고를 수 있는 캐릭터 트랜스폼들입니다. 순서는 CharacterCatalog와 같습니다.</summary>
        public Transform[] Characters => characters;

        /// <summary>켜 두면 클릭이 어디서 끊기는지 콘솔에 남깁니다.</summary>
        [Header("디버그")]
        [SerializeField] private bool logClicks;

        private Coroutine moveRoutine;

        // Awake 순서에 기대지 않도록 쓸 때 잡습니다.
        private RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            // 클릭을 받으려면 반드시 켜져 있어야 합니다.
            GetComponent<RawImage>().raycastTarget = true;

            if (renderCamera == null)
                Debug.LogError("renderCamera가 비어 있습니다. RenderTexture를 그리는 카메라를 연결하세요.", this);
            if (sunRayLight == null)
                Debug.LogError("sunRayLight가 비어 있습니다. 스포트라이트를 연결하세요.", this);
        }

        private void Start()
        {
            // 시작할 때 이미 고른 캐릭터가 있으면 그 위로 빛을 붙여 둡니다.
            if (SelectedIndex >= 0) SelectCharacter(SelectedIndex, instant: true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!TryGetUv(eventData, out var uv))
            {
                if (logClicks) Debug.Log("[SunRay] RawImage 밖을 눌렀습니다.", this);
                return;
            }

            var index = PickNearest(uv);
            if (logClicks) Debug.Log($"[SunRay] uv=({uv.x:F3}, {uv.y:F3}) -> index {index}", this);

            if (index < 0) return;
            if (ignoreRepeatPick && index == SelectedIndex) return;

            // 잠긴(Coming Soon) 캐릭터는 포커싱도 선택도 하지 않습니다.
            if (selectController != null && !selectController.IsIndexSelectable(index))
            {
                if (logClicks) Debug.Log($"[SunRay] index {index} 는 잠긴 캐릭터라 선택하지 않습니다.", this);
                // 눌렸지만 아무 일도 일어나지 않았다는 것을 소리로 알립니다.
                AudioManager.Play(SfxId.UiDenied);
                return;
            }

            // 빛이 이동하는 연출 없이, 고른 캐릭터로 곧바로 포커싱합니다.
            SelectCharacter(index, instant: true);
        }

        /// <summary>인덱스로 직접 고릅니다. 버튼 UI 등 다른 곳에서 불러도 됩니다.</summary>
        public void SelectCharacter(int index) => SelectCharacter(index, instant: true);

        public void SelectCharacter(int index, bool instant)
        {
            if (characters == null || index < 0 || index >= characters.Length) return;
            var target = characters[index];
            if (target == null) return;

            SelectedIndex = index;

            // 실제 캐릭터 선택을 바꿉니다. CharacterInfo 패널·이름·스탯이 여기서 갱신됩니다.
            if (selectController != null)
                selectController.SelectByIndex(index);
            else if (logClicks)
                Debug.LogWarning("[SunRay] selectController가 비어 있어 캐릭터 선택이 바뀌지 않습니다.", this);

            if (sunRayLight != null)
            {
                var destination = target.position + Vector3.up * lightHeight;

                if (moveRoutine != null) StopCoroutine(moveRoutine);

                if (instant || moveDuration <= 0f)
                {
                    sunRayLight.transform.position = destination;
                    sunRayLight.intensity = litIntensity;
                }
                else
                {
                    moveRoutine = StartCoroutine(MoveLightRoutine(destination));
                }
            }

            onCharacterPicked?.Invoke(index);
        }

        // ------------------------------------------------------------------

        /// <summary>클릭한 화면 좌표를 RawImage 안에서의 0~1 UV로 바꿉니다.</summary>
        private bool TryGetUv(PointerEventData eventData, out Vector2 uv)
        {
            uv = default;
            var rect = Rect;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out var local))
                return false;

            var r = rect.rect;
            uv = new Vector2(
                Mathf.InverseLerp(r.xMin, r.xMax, local.x),
                Mathf.InverseLerp(r.yMin, r.yMax, local.y));

            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <summary>
        /// UV는 곧 카메라의 뷰포트 좌표입니다. 각 캐릭터를 뷰포트로 투영해 가장 가까운 하나를 고릅니다.
        /// 캐릭터에 콜라이더가 없어도 되기 때문에 Physics.Raycast 대신 이 방식을 씁니다.
        /// 아무도 판정 범위에 없으면 -1을 돌려줍니다.
        /// </summary>
        public int PickNearest(Vector2 uv)
        {
            if (renderCamera == null || characters == null) return -1;

            // 가로세로 비율을 곱해 화면에서 원형으로 판정되게 맞춥니다.
            var aspect = renderCamera.aspect;

            var best = -1;
            var bestSqr = pickRadius * pickRadius;

            for (var i = 0; i < characters.Length; i++)
            {
                var c = characters[i];
                if (c == null) continue;

                var v = renderCamera.WorldToViewportPoint(c.position + Vector3.up * bodyHeight);
                if (v.z <= 0f) continue;   // 카메라 뒤쪽

                var offset = new Vector2((v.x - uv.x) * aspect, v.y - uv.y);
                var sqr = offset.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>빛을 잠깐 약하게 줄였다가 새 자리에서 다시 밝히며 옮깁니다.</summary>
        private IEnumerator MoveLightRoutine(Vector3 destination)
        {
            var from = sunRayLight.transform.position;
            var startIntensity = sunRayLight.intensity;
            var dip = litIntensity * dipRatio;

            var elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / moveDuration);

                sunRayLight.transform.position = Vector3.Lerp(from, destination, EaseInOutCubic(t));

                // 중간에서 가장 어두워졌다가 도착하면서 다시 밝아집니다.
                sunRayLight.intensity = t < 0.5f
                    ? Mathf.Lerp(startIntensity, dip, t * 2f)
                    : Mathf.Lerp(dip, litIntensity, (t - 0.5f) * 2f);

                yield return null;
            }

            sunRayLight.transform.position = destination;
            sunRayLight.intensity = litIntensity;
            moveRoutine = null;
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }
    }
}
