using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace IdleBattle.UI
{
    /// <summary>
    /// One_Line_Popup 프리팹에 붙는 한 줄 알림입니다. 버튼이 없고 스스로 사라집니다.
    ///
    /// 흐름: 프리팹의 Animator(CanvasGroupFade / GroupFade)가 등장 연출을 재생
    ///       -> <see cref="holdSeconds"/>만큼 머무름
    ///       -> <see cref="riseDistance"/>만큼 위로 올라가며 페이드 아웃 -> 파괴.
    ///
    /// 직접 만들지 말고 <see cref="PopupService.Toast"/>로 띄우세요.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OneLinePopup : MonoBehaviour
    {
        [Header("타이밍")]
        [Tooltip("등장 애니메이션이 끝난 뒤 사라지기까지 머무는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float holdSeconds = 1.4f;

        [Tooltip("올라가며 사라지는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.05f)] private float exitDuration = 0.4f;

        [Tooltip("Animator가 없을 때 등장 시간으로 대신 쓰는 값(초)입니다.")]
        [SerializeField, Min(0f)] private float introFallbackSeconds = 0.34f;

        [Header("사라질 때")]
        [Tooltip("사라지면서 위로 올라가는 거리(px)입니다.")]
        [SerializeField] private float riseDistance = 90f;

        private RectTransform rect;
        private CanvasGroup group;
        private Animator animator;
        private TMP_Text label;
        private Coroutine routine;
        private float holdUntilUnscaled;
        private bool closing;

        /// <summary>알림이 완전히 사라졌을 때 호출됩니다.</summary>
        public event Action<OneLinePopup> Closed;

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            animator = GetComponent<Animator>();
            label = GetComponentInChildren<TMP_Text>(true);

            // 알림은 클릭을 가로채면 안 된다(뒤에 있는 버튼이 죽는다).
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Show(string message)
        {
            if (label != null) label.text = message;

            closing = false;
            gameObject.SetActive(true);
            group.alpha = 0f;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PlayRoutine());
        }

        /// <summary>같은 문구가 다시 들어왔을 때 머무는 시간만 늘립니다.</summary>
        public void ExtendHold()
        {
            holdUntilUnscaled = Mathf.Max(holdUntilUnscaled, Time.unscaledTime + holdSeconds);
        }

        /// <summary>연출 없이 즉시 치웁니다. 새 알림으로 교체할 때 씁니다.</summary>
        public void CloseImmediate()
        {
            if (closing) return;
            closing = true;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Finish();
        }

        private IEnumerator PlayRoutine()
        {
            var intro = introFallbackSeconds;

            if (animator != null)
            {
                // 타임스케일을 만지는 연출이 있어도 알림 속도는 그대로여야 한다.
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.length > 0f) intro = state.length; // 클립 길이를 바꿔도 코드는 그대로.
            }

            // 등장 연출이 끝날 때까지 기다린다.
            for (var elapsed = 0f; elapsed < intro; elapsed += Time.unscaledDeltaTime)
                yield return null;

            // 그 뒤 holdSeconds만큼 머문다. ExtendHold로 늘어날 수 있다.
            holdUntilUnscaled = Time.unscaledTime + holdSeconds;
            while (Time.unscaledTime < holdUntilUnscaled) yield return null;

            // Animator를 켜 둔 채로 알파를 건드리면 매 프레임 되돌려 놓기 때문에 먼저 끈다.
            if (animator != null) animator.enabled = false;

            closing = true;
            var startPosition = rect.anchoredPosition;
            var endPosition = startPosition + Vector2.up * riseDistance;
            var startAlpha = group.alpha;

            for (var elapsed = 0f; elapsed < exitDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / exitDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
                group.alpha = Mathf.Lerp(startAlpha, 0f, eased);
                yield return null;
            }

            rect.anchoredPosition = endPosition;
            group.alpha = 0f;
            routine = null;
            Finish();
        }

        private void Finish()
        {
            var handler = Closed;
            Closed = null;
            handler?.Invoke(this);
            if (this != null) Destroy(gameObject);
        }
    }
}
