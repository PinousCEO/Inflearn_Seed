using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class DroppedItemLabel : MonoBehaviour
    {
        private RectTransform rectTransform;
        private RectTransform uiParent;
        private Transform worldTarget;
        private Camera worldCamera;
        private Camera canvasCamera;
        private Vector3 worldOffset;
        private Graphic[] graphics;

        public void Initialize(ItemData item, Transform target, RectTransform parent, Camera camera, Vector3 offset)
        {
            rectTransform = transform as RectTransform;
            uiParent = parent;
            worldTarget = target;
            worldCamera = camera != null ? camera : Camera.main;
            worldOffset = offset;

            var canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            canvasCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            var label = GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = item != null ? item.DisplayName : string.Empty;
                label.color = RarityColor(item != null ? item.Rarity : ItemRarity.Common);
                label.SetAllDirty();
                label.ForceMeshUpdate();
            }

            // 매 프레임 다시 탐색하지 않도록 자식 Graphic들을 한 번만 캐싱한다.
            graphics = GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
                graphic.raycastTarget = false;

            UpdatePosition();
        }

        private void LateUpdate()
        {
            if (worldTarget == null)
            {
                Destroy(gameObject);
                return;
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (rectTransform == null || uiParent == null || worldTarget == null || worldCamera == null)
                return;

            var worldPosition = worldTarget.position + worldOffset;
            var viewport = worldCamera.WorldToViewportPoint(worldPosition);
            var visible = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
            if (graphics != null)
                foreach (var graphic in graphics)
                    if (graphic != null)
                        graphic.enabled = visible;
            if (!visible)
                return;

            var screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(uiParent, screenPoint, canvasCamera, out var localPoint))
                rectTransform.anchoredPosition = localPoint;
        }

        private static Color RarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color32(150, 150, 150, 255),
                ItemRarity.Uncommon => new Color32(74, 159, 232, 255),
                ItemRarity.Rare => new Color32(242, 193, 78, 255),
                ItemRarity.Epic => new Color32(167, 101, 209, 255),
                _ => new Color32(240, 120, 69, 255)
            };
        }
    }
}
