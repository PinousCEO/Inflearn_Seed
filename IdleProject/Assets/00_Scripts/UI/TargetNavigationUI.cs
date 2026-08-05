using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>목표 지점이 화면 밖에 있을 때만 화면 가장자리에 몬스터 방향을 표시합니다.</summary>
    public sealed class TargetNavigationUI : MonoBehaviour
    {
        private RectTransform indicator;
        private Image hexImage;
        private Image monsterImage;
        private Image arrowImage;
        private Camera worldCamera;
        private Transform player;
        private Vector3 target;
        private bool hasTarget;

        public void Initialize(Camera camera, Transform playerTransform)
        {
            worldCamera = camera;
            player = playerTransform;
            BuildVisual();
        }

        public void SetTarget(Vector3 worldPosition)
        {
            target = worldPosition;
            hasTarget = true;
        }

        public void Hide() => hasTarget = false;

        private void BuildVisual()
        {
            if (indicator != null) return;
            var root = new GameObject("TargetNavigation", typeof(RectTransform), typeof(CanvasRenderer));
            root.transform.SetParent(transform, false);
            indicator = root.GetComponent<RectTransform>();
            indicator.anchorMin = new Vector2(.5f, .5f);
            indicator.anchorMax = new Vector2(.5f, .5f);
            indicator.pivot = new Vector2(.5f, .5f);
            indicator.sizeDelta = new Vector2(92f, 92f);

            hexImage = root.AddComponent<Image>();
            hexImage.raycastTarget = false;
            hexImage.color = new Color(1f, .78f, .25f, .96f);
            hexImage.sprite = LoadSprite("Assets/Pinous/03_Design/data_Ui_equip_2/data_Ui_equip_2/hexagon.png");

            var icon = new GameObject("MonsterIcon", typeof(RectTransform), typeof(CanvasRenderer));
            icon.transform.SetParent(root.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(20f, 20f);
            iconRect.offsetMax = new Vector2(-20f, -20f);
            monsterImage = icon.AddComponent<Image>();
            monsterImage.raycastTarget = false;
            monsterImage.color = Color.white;
            monsterImage.sprite = LoadSprite("Assets/Pinous/03_Design/data_Ui_inventory_1/data_Ui_maingame_1/skull_icon.png");

            var arrowObject = new GameObject("DirectionArrow", typeof(RectTransform), typeof(CanvasRenderer));
            arrowObject.transform.SetParent(root.transform, false);
            var arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(.5f, .5f);
            arrowRect.anchorMax = new Vector2(.5f, .5f);
            arrowRect.sizeDelta = new Vector2(34f, 42f);
            arrowImage = arrowObject.AddComponent<Image>();
            arrowImage.raycastTarget = false;
            arrowImage.color = new Color(1f, .95f, .7f, 1f);
            arrowImage.sprite = LoadSprite("Assets/Lana Studio/Casual RPG VFX/Textures/arrow04.png");
            root.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!hasTarget || worldCamera == null || player == null || indicator == null) return;
            var screen = worldCamera.WorldToViewportPoint(target);
            var direction = new Vector2(screen.x - .5f, screen.y - .5f);
            var onScreen = screen.z > 0f && screen.x > .04f && screen.x < .96f && screen.y > .06f && screen.y < .94f;
            if (onScreen)
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            if (screen.z < 0f) direction = -direction;
            if (direction.sqrMagnitude < .001f) direction = Vector2.up;
            direction.Normalize();
            var edge = new Vector2(.5f, .5f) + direction * .47f;
            edge.x = Mathf.Clamp(edge.x, .08f, .92f);
            edge.y = Mathf.Clamp(edge.y, .10f, .90f);
            indicator.anchoredPosition = new Vector2(
                (edge.x - .5f) * ((RectTransform)transform).rect.width,
                (edge.y - .5f) * ((RectTransform)transform).rect.height);
            if (arrowImage != null)
                arrowImage.rectTransform.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            indicator.gameObject.SetActive(true);
        }

        private static Sprite LoadSprite(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }
    }
}
