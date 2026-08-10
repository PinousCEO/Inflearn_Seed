using UnityEngine;

namespace IdleBattle.UI
{
    /// <summary>
    /// Fits this RectTransform inside the device safe area.
    /// Attach it to a full-stretch UI container directly below a Canvas.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        [Header("Edges")]
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize)
                Apply();
        }

        private void OnValidate()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        [ContextMenu("Apply Safe Area")]
        public void Apply()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            var safeArea = Screen.safeArea;
            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if (!applyLeft) anchorMin.x = 0f;
            if (!applyBottom) anchorMin.y = 0f;
            if (!applyRight) anchorMax.x = 1f;
            if (!applyTop) anchorMax.y = 1f;

            anchorMin.x = Mathf.Clamp01(anchorMin.x);
            anchorMin.y = Mathf.Clamp01(anchorMin.y);
            anchorMax.x = Mathf.Clamp01(anchorMax.x);
            anchorMax.y = Mathf.Clamp01(anchorMax.y);

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
