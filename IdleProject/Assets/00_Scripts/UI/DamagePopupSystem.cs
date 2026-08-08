using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle
{
    /// <summary>
    /// Converts a world-space hit position to the UI canvas and reuses damage texts
    /// from Pool#[Text].
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopupSystem : MonoBehaviour
    {
        private const string PoolName = "Pool#[Text]";
        private const float ShrinkDuration = 0.14f;
        private const float HoldDuration = 0.22f;
        private const float FadeDuration = 0.42f;
        private const float StartScale = 1.55f;
        private const float EndScale = 1f;
        private const float HorizontalSpread = 58f;
        private const float VerticalSpread = 32f;
        private const float GoldenAngle = 137.5f;
        private const float RetryInterval = 1f;

        private readonly Stack<TextMeshProUGUI> available = new Stack<TextMeshProUGUI>();
        private readonly HashSet<TextMeshProUGUI> owned = new HashSet<TextMeshProUGUI>();

        private uint popupSequence;
        private Canvas canvas;
        private RectTransform pool;
        private RectTransform mainBounds;
        private Camera worldCamera;
        private Camera canvasCamera;
        private GameObject damagePrefab;
        private float nextInitializeAttempt;

        public bool Initialize(Camera camera)
        {
            worldCamera = camera != null ? camera : Camera.main;
            canvas = SceneRefs.RootCanvas;
            if (canvas == null)
            {
                Debug.LogWarning("Damage UI를 표시할 Canvas를 찾지 못했습니다.");
                return false;
            }

            mainBounds = SceneRefs.Screen("Main") as RectTransform;
            var poolParent = mainBounds != null ? mainBounds : canvas.transform as RectTransform;
            pool = FindPool(canvas.transform);
            if (pool == null)
            {
                var poolObject = new GameObject(PoolName, typeof(RectTransform));
                poolObject.layer = canvas.gameObject.layer;
                pool = poolObject.GetComponent<RectTransform>();
            }

            // Damage belongs to the battle screen, not to the global Canvas.
            // Parenting it below Main also hides every active popup immediately
            // when another screen (Equipment, Skill, etc.) is selected.
            if (poolParent != null && pool.parent != poolParent)
                pool.SetParent(poolParent, false);
            pool.anchorMin = Vector2.zero;
            pool.anchorMax = Vector2.one;
            pool.offsetMin = Vector2.zero;
            pool.offsetMax = Vector2.zero;
            pool.SetAsLastSibling();
            if (!pool.TryGetComponent<RectMask2D>(out _))
                pool.gameObject.AddComponent<RectMask2D>();

            canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            damagePrefab = LoadDamagePrefab();
            return worldCamera != null;
        }

        public void Show(int damage, Vector3 worldPosition)
        {
            // 피해 숫자 하나마다 씬을 다시 훑지 않도록, 준비가 안 된 동안에도 재시도 간격을 둡니다.
            if (canvas == null || pool == null || worldCamera == null)
            {
                if (Time.unscaledTime < nextInitializeAttempt) return;
                nextInitializeAttempt = Time.unscaledTime + RetryInterval;
                if (!Initialize(Camera.main)) return;
            }

            var viewportPoint = worldCamera.WorldToViewportPoint(worldPosition);
            if (viewportPoint.z <= 0f)
                return;

            var screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    pool, screenPoint, canvasCamera, out var localPoint))
                return;

            var text = GetText();
            if (text == null)
                return;

            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            text.text = damage.ToString();
            text.alpha = 1f;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            var popupPosition = ClampToMain(GetSpreadPosition(localPoint), rect);
            rect.anchoredPosition = popupPosition;
            rect.localScale = Vector3.one * StartScale;
            StartCoroutine(Animate(text, popupPosition));
        }

        private Vector2 ClampToMain(Vector2 position, RectTransform popup)
        {
            if (pool == null || popup == null) return position;
            var bounds = pool.rect;
            var halfWidth = popup.rect.width * Mathf.Max(popup.pivot.x, 1f - popup.pivot.x) * StartScale;
            var halfHeight = popup.rect.height * Mathf.Max(popup.pivot.y, 1f - popup.pivot.y) * StartScale;
            const float padding = 8f;
            position.x = Mathf.Clamp(position.x, bounds.xMin + halfWidth + padding, bounds.xMax - halfWidth - padding);
            position.y = Mathf.Clamp(position.y, bounds.yMin + halfHeight + padding, bounds.yMax - halfHeight - padding);
            return position;
        }

        private Vector2 GetSpreadPosition(Vector2 hitPosition)
        {
            // Stepping by the golden angle keeps consecutive hits apart instead
            // of allowing purely random positions to repeatedly overlap.
            var angle = popupSequence++ * GoldenAngle * Mathf.Deg2Rad;
            var distance = Random.Range(0.72f, 1f);
            var offset = new Vector2(
                Mathf.Cos(angle) * HorizontalSpread,
                Mathf.Sin(angle) * VerticalSpread) * distance;
            return hitPosition + offset;
        }

        private IEnumerator Animate(TextMeshProUGUI text, Vector2 origin)
        {
            var elapsed = 0f;

            // Diablo-style impact: the number appears oversized and quickly
            // settles to its normal size without travelling in an arc.
            while (elapsed < ShrinkDuration)
            {
                if (text == null)
                    yield break;

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / ShrinkDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                text.rectTransform.anchoredPosition = origin;
                text.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(StartScale, EndScale, eased);
                text.alpha = 1f;
                yield return null;
            }

            text.rectTransform.anchoredPosition = origin;
            text.rectTransform.localScale = Vector3.one * EndScale;

            elapsed = 0f;
            while (elapsed < HoldDuration)
            {
                if (text == null)
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                if (text == null)
                    yield break;

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / FadeDuration);
                text.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            Recycle(text);
        }

        private TextMeshProUGUI GetText()
        {
            while (available.Count > 0)
            {
                var reused = available.Pop();
                if (reused != null)
                    return reused;
            }

            GameObject instance;
            if (damagePrefab != null)
            {
                instance = Instantiate(damagePrefab, pool, false);
            }
            else
            {
                instance = new GameObject("Damage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                instance.layer = canvas.gameObject.layer;
                instance.transform.SetParent(pool, false);
                var fallback = instance.GetComponent<TextMeshProUGUI>();
                fallback.alignment = TextAlignmentOptions.Center;
                fallback.fontSize = 36f;
                fallback.fontStyle = FontStyles.Bold;
                fallback.color = Color.white;
                fallback.rectTransform.sizeDelta = new Vector2(200f, 50f);
            }

            var text = instance.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                Debug.LogWarning("Damage 프리팹에 TextMeshProUGUI가 없습니다.", instance);
                Destroy(instance);
                return null;
            }

            owned.Add(text);
            return text;
        }

        private void Recycle(TextMeshProUGUI text)
        {
            if (text == null || !owned.Contains(text))
                return;

            text.gameObject.SetActive(false);
            text.alpha = 1f;
            available.Push(text);
        }

        private static RectTransform FindPool(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == PoolName)
                    return child;
            }
            return null;
        }

        private static GameObject LoadDamagePrefab()
        {
            var prefab = Resources.Load<GameObject>("01_Prefabs/UI/Damage");
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/01_Prefabs/UI/Damage.prefab");
#endif
            return prefab;
        }
    }
}
