using System;
using System.Collections;
using IdleBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// Multi_Line_Popup 프리팹에 붙는 제목 + 본문 + 버튼 팝업입니다.
    ///
    /// 버튼은 프리팹 구조 그대로 씁니다.
    /// - OneBtn = 왼쪽(취소), TwoBtn = 오른쪽(확인)
    /// - 버튼이 하나만 필요하면 OneBtn을 끄고 TwoBtn만 남깁니다.
    ///
    /// 직접 만들지 말고 <see cref="PopupService.Alert"/> · <see cref="PopupService.Confirm"/>으로 띄우세요.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MultiLinePopup : MonoBehaviour
    {
        [Header("연출")]
        [Tooltip("닫힐 때 사라지는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.02f)] private float closeDuration = 0.16f;

        [Tooltip("닫힐 때 줄어드는 비율입니다.")]
        [SerializeField, Min(0.5f)] private float closeScale = 0.92f;

        private CanvasGroup group;
        private Animator animator;
        private RectTransform box;
        private TMP_Text titleText;
        private TMP_Text descriptionText;
        private GameObject oneButtonRoot;
        private GameObject twoButtonRoot;
        private Button oneButton;
        private Button twoButton;
        private TMP_Text oneLabel;
        private TMP_Text twoLabel;

        private Action confirmAction;
        private Action cancelAction;
        private bool closing;

        public event Action<MultiLinePopup> Closed;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            animator = GetComponent<Animator>();

            box = FindChild(transform, "Box") as RectTransform;
            var textRoot = box != null ? box : (RectTransform)transform;

            // 프리팹의 오브젝트 이름이 'TItle'이라 두 표기를 모두 받아 준다.
            titleText = FindText(textRoot, "TItle") ?? FindText(textRoot, "Title");
            descriptionText = FindText(textRoot, "Des") ?? FindText(textRoot, "Description");

            var one = FindChild(textRoot, "OneBtn");
            var two = FindChild(textRoot, "TwoBtn");
            oneButtonRoot = one != null ? one.gameObject : null;
            twoButtonRoot = two != null ? two.gameObject : null;
            oneButton = EnsureButton(one);
            twoButton = EnsureButton(two);
            oneLabel = one != null ? one.GetComponentInChildren<TMP_Text>(true) : null;
            twoLabel = two != null ? two.GetComponentInChildren<TMP_Text>(true) : null;

            if (oneButton != null) oneButton.onClick.AddListener(OnCancelClicked);
            if (twoButton != null) twoButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDestroy()
        {
            if (oneButton != null) oneButton.onClick.RemoveListener(OnCancelClicked);
            if (twoButton != null) twoButton.onClick.RemoveListener(OnConfirmClicked);
        }

        /// <param name="onCancel">
        /// null이면 버튼 하나짜리(확인만)로 열립니다. OneBtn은 꺼지고 TwoBtn만 남습니다.
        /// </param>
        public void Show(
            string title,
            string message,
            Action onConfirm,
            Action onCancel,
            string confirmLabel,
            string cancelLabel)
        {
            confirmAction = onConfirm;
            cancelAction = onCancel;
            closing = false;

            if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;
            if (descriptionText != null && !string.IsNullOrEmpty(message)) descriptionText.text = message;

            var twoButtonMode = onCancel != null;
            if (oneButtonRoot != null) oneButtonRoot.SetActive(twoButtonMode);
            if (twoButtonRoot != null) twoButtonRoot.SetActive(true);

            if (twoButtonMode && oneLabel != null && !string.IsNullOrEmpty(cancelLabel)) oneLabel.text = cancelLabel;
            if (twoLabel != null && !string.IsNullOrEmpty(confirmLabel)) twoLabel.text = confirmLabel;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            if (animator != null)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        /// <summary>콜백 없이 닫습니다. 네트워크가 복구돼 알림이 더 이상 필요 없을 때처럼 밖에서 치울 때 씁니다.</summary>
        public void Close()
        {
            if (closing) return;
            closing = true;
            confirmAction = null;
            cancelAction = null;
            StartCoroutine(CloseRoutine());
        }

        private void OnConfirmClicked()
        {
            AudioManager.Play(SfxId.UiConfirm);
            CloseWith(confirmAction);
        }

        private void OnCancelClicked()
        {
            AudioManager.Play(SfxId.UiCancel);
            CloseWith(cancelAction);
        }

        private void CloseWith(Action callback)
        {
            if (closing) return;
            closing = true;

            // 콜백이 다시 팝업을 열 수 있으므로, 닫는 연출을 먼저 시작한 뒤에 부른다.
            StartCoroutine(CloseRoutine());
            callback?.Invoke();
        }

        private IEnumerator CloseRoutine()
        {
            group.interactable = false;
            group.blocksRaycasts = false;

            // Animator가 켜져 있으면 알파와 스케일을 매 프레임 되돌려 놓는다.
            if (animator != null) animator.enabled = false;

            var target = box != null ? box : (RectTransform)transform;
            var startScale = target.localScale;
            var endScale = startScale * closeScale;
            var startAlpha = group.alpha;

            for (var elapsed = 0f; elapsed < closeDuration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / closeDuration);
                target.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
                group.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            group.alpha = 0f;

            var handler = Closed;
            Closed = null;
            handler?.Invoke(this);
            if (this != null) Destroy(gameObject);
        }

        private static Button EnsureButton(Transform target)
        {
            if (target == null) return null;
            if (!target.TryGetComponent(out Button button))
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
            }

            if (button.targetGraphic == null && target.TryGetComponent(out Image graphic))
            {
                graphic.raycastTarget = true;
                button.targetGraphic = graphic;
            }

            button.interactable = true;
            return button;
        }

        private static TMP_Text FindText(Transform root, string objectName)
        {
            var found = FindChild(root, objectName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
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
}
