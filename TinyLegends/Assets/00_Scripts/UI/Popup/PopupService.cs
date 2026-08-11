using System;
using System.Collections.Generic;
using IdleBattle.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// 게임 전역 팝업 진입점입니다. 게임을 켤 때 코드로 한 번 생성되어 DontDestroyOnLoad로 살아남기 때문에
    /// Title · Select · Main 어느 씬에서도 배치 없이 바로 쓸 수 있습니다.
    ///
    /// - <see cref="Toast"/>       : One_Line_Popup. 버튼 없이 잠깐 떴다가 위로 올라가며 사라집니다.
    /// - <see cref="Alert"/>       : Multi_Line_Popup. 확인 버튼 하나(OneBtn을 끄고 TwoBtn만 남깁니다).
    /// - <see cref="Confirm"/>     : Multi_Line_Popup. 취소(OneBtn) + 확인(TwoBtn) 두 개.
    ///
    /// 팝업은 씬 UI보다 위, 씬 전환 페이드(<see cref="SceneTransition"/>)보다는 아래에 그려집니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class PopupService : MonoBehaviour
    {
        // 프리팹은 Resources 아래에 있어야 빌드에서도 로드됩니다.
        private const string OneLineResourcePath = "UI/One_Line_Popup";
        private const string MultiLineResourcePath = "UI/Multi_Line_Popup";
        private const string OneLineAssetPath = "Assets/Resources/UI/One_Line_Popup.prefab";
        private const string MultiLineAssetPath = "Assets/Resources/UI/Multi_Line_Popup.prefab";

        /// <summary>씬 전환 페이드(short.MaxValue)보다는 아래, 나머지 UI보다는 위.</summary>
        private const int OverlaySortingOrder = 30000;

        private static PopupService instance;
        private static bool quitting;

        private Canvas canvas;
        private RectTransform toastLayer;
        private RectTransform modalLayer;
        private GameObject oneLinePrefab;
        private GameObject multiLinePrefab;

        private OneLinePopup activeToast;
        private string activeToastMessage;
        private readonly Dictionary<string, float> toastCooldowns = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MultiLinePopup> keyedPopups = new(StringComparer.Ordinal);
        private readonly List<MultiLinePopup> openPopups = new();

        public static PopupService Instance
        {
            get
            {
                if (instance != null) return instance;
                if (quitting) return null;

                instance = FindFirstObjectByType<PopupService>();
                if (instance == null)
                    instance = new GameObject(nameof(PopupService)).AddComponent<PopupService>();
                return instance;
            }
        }

        /// <summary>버튼 있는 팝업이 하나라도 떠 있는지. 뒤로가기 처리 같은 곳에서 씁니다.</summary>
        public static bool HasOpenModal => instance != null && instance.openPopups.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            quitting = false;
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
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnApplicationQuit() => quitting = true;

        // ------------------------------------------------------------------
        // 공개 API
        // ------------------------------------------------------------------

        /// <summary>
        /// 한 줄 알림입니다. 인트로 애니메이션이 끝나면 잠시 머물렀다가 위로 올라가며 사라집니다.
        /// </summary>
        /// <param name="message">한 문장으로 짧게 씁니다.</param>
        /// <param name="cooldownSeconds">
        /// 같은 문구가 이 시간 안에 다시 들어오면 무시합니다.
        /// 마나 부족처럼 매 프레임 터질 수 있는 알림이 화면을 도배하지 않게 합니다.
        /// </param>
        public static void Toast(string message, float cooldownSeconds = 0f)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            var service = Instance;
            if (service == null) return;
            service.ShowToast(message, cooldownSeconds);
        }

        /// <summary>확인 버튼 하나짜리 팝업입니다. OneBtn을 끄고 TwoBtn만 남깁니다.</summary>
        /// <param name="key">
        /// 같은 key를 가진 팝업이 이미 떠 있으면 새로 열지 않고 기존 팝업을 그대로 둡니다.
        /// 네트워크 끊김처럼 여러 곳에서 동시에 터지는 알림에 씁니다. 비워 두면 매번 새로 엽니다.
        /// </param>
        public static MultiLinePopup Alert(
            string title,
            string message,
            Action onConfirm = null,
            string confirmLabel = "확인",
            string key = null)
        {
            var service = Instance;
            if (service == null) return null;
            return service.ShowModal(title, message, onConfirm, null, confirmLabel, null, key);
        }

        /// <summary>취소(OneBtn) + 확인(TwoBtn) 두 개짜리 팝업입니다.</summary>
        public static MultiLinePopup Confirm(
            string title,
            string message,
            Action onConfirm,
            Action onCancel = null,
            string confirmLabel = "확인",
            string cancelLabel = "취소",
            string key = null)
        {
            var service = Instance;
            if (service == null) return null;
            return service.ShowModal(title, message, onConfirm, onCancel ?? (() => { }), confirmLabel, cancelLabel, key);
        }

        /// <summary>key로 열어 둔 팝업을 닫습니다. 이미 닫혔으면 아무 일도 하지 않습니다.</summary>
        public static void CloseKeyed(string key)
        {
            if (string.IsNullOrEmpty(key) || instance == null) return;
            if (!instance.keyedPopups.TryGetValue(key, out var popup)) return;
            instance.keyedPopups.Remove(key);
            if (popup != null) popup.Close();
        }

        public static bool IsKeyedOpen(string key)
        {
            return !string.IsNullOrEmpty(key) && instance != null &&
                   instance.keyedPopups.TryGetValue(key, out var popup) && popup != null;
        }

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private void ShowToast(string message, float cooldownSeconds)
        {
            // 같은 문구가 아직 떠 있는 동안 다시 들어오면 새로 띄우지 않고 머무는 시간만 늘린다.
            if (activeToast != null && string.Equals(activeToastMessage, message, StringComparison.Ordinal))
            {
                activeToast.ExtendHold();
                return;
            }

            // 마나 부족처럼 연달아 터질 수 있는 알림은 문구별 쿨다운으로 흘려보낸다.
            if (cooldownSeconds > 0f)
            {
                if (toastCooldowns.TryGetValue(message, out var openAgainAt) && Time.unscaledTime < openAgainAt) return;
                toastCooldowns[message] = Time.unscaledTime + cooldownSeconds;
            }

            var prefab = ResolveOneLinePrefab();
            if (prefab == null) return;

            // 앞선 알림은 즉시 밀어내고 새 문구를 띄운다.
            if (activeToast != null) activeToast.CloseImmediate();

            var instantiated = Instantiate(prefab, toastLayer, false);
            var popup = instantiated.GetComponent<OneLinePopup>() ?? instantiated.AddComponent<OneLinePopup>();

            activeToast = popup;
            activeToastMessage = message;
            popup.Closed += OnToastClosed;
            AudioManager.Play(SfxId.UiToast);
            popup.Show(message);
        }

        private void OnToastClosed(OneLinePopup popup)
        {
            if (popup != null) popup.Closed -= OnToastClosed;
            if (activeToast != popup) return;
            activeToast = null;
            activeToastMessage = null;
        }

        private MultiLinePopup ShowModal(
            string title,
            string message,
            Action onConfirm,
            Action onCancel,
            string confirmLabel,
            string cancelLabel,
            string key)
        {
            if (!string.IsNullOrEmpty(key) && keyedPopups.TryGetValue(key, out var existing) && existing != null)
                return existing; // 같은 상황을 알리는 팝업이 이미 떠 있다.

            var prefab = ResolveMultiLinePrefab();
            if (prefab == null) return null;

            var instantiated = Instantiate(prefab, modalLayer, false);
            var popup = instantiated.GetComponent<MultiLinePopup>() ?? instantiated.AddComponent<MultiLinePopup>();

            openPopups.Add(popup);
            if (!string.IsNullOrEmpty(key)) keyedPopups[key] = popup;

            popup.Closed += closed =>
            {
                openPopups.Remove(closed);
                if (!string.IsNullOrEmpty(key) && keyedPopups.TryGetValue(key, out var tracked) && tracked == closed)
                    keyedPopups.Remove(key);
            };

            AudioManager.Play(SfxId.UiPopupOpen);
            popup.Show(title, message, onConfirm, onCancel, confirmLabel, cancelLabel);
            return popup;
        }

        private void BuildOverlay()
        {
            var canvasObject = new GameObject("PopupCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = LayerMask.NameToLayer("UI");

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // 메인 UI와 동일하게 기준 화면 전체가 항상 보이도록 합니다.
            // 기기 비율이 달라지면 남는 축만 확장되고 콘텐츠가 잘리지 않습니다.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            canvasObject.AddComponent<GraphicRaycaster>();

            // 알림(토스트)은 클릭을 먹지 않고, 버튼 팝업은 뒤쪽 UI를 막아야 하므로 레이어를 나눠 둔다.
            toastLayer = CreateLayer("ToastLayer", blocksRaycasts: false);
            modalLayer = CreateLayer("ModalLayer", blocksRaycasts: true);

            EnsureEventSystem();
        }

        private RectTransform CreateLayer(string layerName, bool blocksRaycasts)
        {
            var layerObject = new GameObject(layerName, typeof(RectTransform));
            var rect = (RectTransform)layerObject.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (!blocksRaycasts)
            {
                var group = layerObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            return rect;
        }

        /// <summary>버튼 팝업은 EventSystem이 없으면 눌리지 않습니다. 씬에 없으면 만들어 둡니다.</summary>
        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var host = new GameObject("EventSystem");
            host.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            host.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            host.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            DontDestroyOnLoad(host);
        }

        private GameObject ResolveOneLinePrefab()
        {
            return oneLinePrefab ??= LoadPrefab(OneLineResourcePath, OneLineAssetPath);
        }

        private GameObject ResolveMultiLinePrefab()
        {
            return multiLinePrefab ??= LoadPrefab(MultiLineResourcePath, MultiLineAssetPath);
        }

        private static GameObject LoadPrefab(string resourcesPath, string assetPath)
        {
            var prefab = AddressableContent.Load<GameObject>(resourcesPath);
#if UNITY_EDITOR
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#endif
            if (prefab == null)
                Debug.LogError($"팝업 프리팹을 찾지 못했습니다: {assetPath}");
            return prefab;
        }
    }
}
