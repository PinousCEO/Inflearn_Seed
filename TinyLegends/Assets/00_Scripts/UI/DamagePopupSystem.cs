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
        // 치명타는 같은 자리에 섞여 뜨므로, 색만으로는 눈에 잘 들어오지 않습니다.
        private const float CriticalStartScale = 1.95f;
        private const float CriticalEndScale = 1.2f;
        private static readonly Color CriticalColor = new Color(1f, .62f, .22f, 1f);
        private const float HorizontalSpread = 58f;
        private const float VerticalSpread = 32f;
        private const float GoldenAngle = 137.5f;
        private const float RetryInterval = 1f;
        private const int MaxPendingPopups = 64;

        private readonly Stack<TextMeshProUGUI> available = new Stack<TextMeshProUGUI>();
        private readonly HashSet<TextMeshProUGUI> owned = new HashSet<TextMeshProUGUI>();
        private readonly HashSet<TextMeshProUGUI> pooled = new HashSet<TextMeshProUGUI>();
        private readonly Dictionary<TextMeshProUGUI, uint> generations = new Dictionary<TextMeshProUGUI, uint>();
        private readonly Queue<PendingPopup> pending = new Queue<PendingPopup>();

        private uint popupSequence;
        private Canvas canvas;
        private RectTransform pool;
        private RectTransform mainBounds;
        private Camera worldCamera;
        private Camera canvasCamera;
        private GameObject damagePrefab;
        private Transform settingsOverlay;
        private Transform mailboxOverlay;
        private Transform rankingOverlay;
        private float nextInitializeAttempt;
        private Color normalColor = Color.white;
        private bool hasNormalColor;

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

            // Main contains nested/safe-area canvases. A plain RectTransform can be active
            // yet render behind those canvases, making valid damage texts invisible.
            // Give damage numbers their own non-interactive canvas above screen content.
            var popupCanvas = pool.GetComponent<Canvas>();
            if (popupCanvas == null) popupCanvas = pool.gameObject.AddComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = GetTopSortingOrder(canvas.transform) + 1;
            popupCanvas.additionalShaderChannels = canvas.additionalShaderChannels;

            canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            damagePrefab = LoadDamagePrefab();
            settingsOverlay = canvas.transform.Find("Settings");
            mailboxOverlay = canvas.transform.Find("Mailbox");
            rankingOverlay = canvas.transform.Find("Ranking");
            return worldCamera != null;
        }

        public void Show(int damage, Vector3 worldPosition, bool isCritical = false)
        {
            Show(damage, null, worldPosition, isCritical);
        }

        public void Show(int damage, Transform target, Vector3 worldPosition, bool isCritical = false)
        {
            if (IsBlockingOverlayOpen()) return;
            // 피해 숫자 하나마다 씬을 다시 훑지 않도록, 준비가 안 된 동안에도 재시도 간격을 둡니다.
            if (!IsReady())
            {
                if (pending.Count >= MaxPendingPopups) pending.Dequeue();
                pending.Enqueue(new PendingPopup(damage, target, worldPosition, isCritical));
                TryInitialize();
                return;
            }

            Display(damage, target, worldPosition, isCritical);
        }

        private void LateUpdate()
        {
            if (IsBlockingOverlayOpen())
            {
                HideActivePopups();
                pending.Clear();
                return;
            }

            if (pending.Count == 0) return;
            if (!IsReady() && !TryInitialize()) return;

            while (pending.Count > 0)
            {
                var popup = pending.Dequeue();
                Display(popup.Damage, popup.Target, popup.WorldPosition, popup.IsCritical);
            }
        }

        private bool IsReady() => canvas != null && pool != null && worldCamera != null;

        private bool IsBlockingOverlayOpen()
        {
            return IsVisible(settingsOverlay) || IsVisible(mailboxOverlay) || IsVisible(rankingOverlay);
        }

        private static bool IsVisible(Transform overlay)
        {
            if (overlay == null || !overlay.gameObject.activeInHierarchy) return false;
            var group = overlay.GetComponent<CanvasGroup>();
            return group == null || group.alpha > 0.001f;
        }

        private void HideActivePopups()
        {
            foreach (var text in owned)
            {
                if (text == null || !text.gameObject.activeSelf) continue;
                var generation = NextGeneration(text);
                text.gameObject.SetActive(false);
                text.alpha = 1f;
                if (pooled.Add(text)) available.Push(text);
            }
        }

        private bool TryInitialize()
        {
            if (Time.unscaledTime < nextInitializeAttempt) return false;
            nextInitializeAttempt = Time.unscaledTime + RetryInterval;
            return Initialize(Camera.main);
        }

        private void Display(int damage, Transform target, Vector3 worldPosition, bool isCritical)
        {
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
            var generation = NextGeneration(text);

            var startScale = isCritical ? CriticalStartScale : StartScale;
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            text.text = damage.ToString();
            // 색은 매번 되돌려 줍니다. 재활용된 텍스트가 앞선 치명타의 주황색을 물고 오면 안 됩니다.
            text.color = isCritical ? CriticalColor : normalColor;
            text.alpha = 1f;
            text.raycastTarget = false;
            text.maskable = false;
            text.canvasRenderer.cull = false;

            var rect = text.rectTransform;
            var spreadOffset = GetSpreadOffset();
            var popupPosition = ClampToMain(localPoint + spreadOffset, rect, startScale);
            rect.anchoredPosition = popupPosition;
            rect.localScale = Vector3.one * startScale;
            var targetLocalPosition = target != null
                ? target.InverseTransformPoint(worldPosition)
                : Vector3.zero;
            StartCoroutine(Animate(
                text, target, targetLocalPosition, popupPosition, spreadOffset,
                startScale, isCritical ? CriticalEndScale : EndScale, generation));
        }

        private readonly struct PendingPopup
        {
            public readonly int Damage;
            public readonly Transform Target;
            public readonly Vector3 WorldPosition;
            public readonly bool IsCritical;

            public PendingPopup(int damage, Transform target, Vector3 worldPosition, bool isCritical)
            {
                Damage = damage;
                Target = target;
                WorldPosition = worldPosition;
                IsCritical = isCritical;
            }
        }

        private Vector2 ClampToMain(Vector2 position, RectTransform popup, float startScale)
        {
            if (pool == null || popup == null) return position;
            var bounds = pool.rect;
            var halfWidth = popup.rect.width * Mathf.Max(popup.pivot.x, 1f - popup.pivot.x) * startScale;
            var halfHeight = popup.rect.height * Mathf.Max(popup.pivot.y, 1f - popup.pivot.y) * startScale;
            const float padding = 8f;
            position.x = Mathf.Clamp(position.x, bounds.xMin + halfWidth + padding, bounds.xMax - halfWidth - padding);
            position.y = Mathf.Clamp(position.y, bounds.yMin + halfHeight + padding, bounds.yMax - halfHeight - padding);
            return position;
        }

        private Vector2 GetSpreadOffset()
        {
            // Stepping by the golden angle keeps consecutive hits apart instead
            // of allowing purely random positions to repeatedly overlap.
            var angle = popupSequence++ * GoldenAngle * Mathf.Deg2Rad;
            var distance = Random.Range(0.72f, 1f);
            var offset = new Vector2(
                Mathf.Cos(angle) * HorizontalSpread,
                Mathf.Sin(angle) * VerticalSpread) * distance;
            return offset;
        }

        private IEnumerator Animate(
            TextMeshProUGUI text,
            Transform target,
            Vector3 targetLocalPosition,
            Vector2 origin,
            Vector2 spreadOffset,
            float startScale,
            float endScale,
            uint generation)
        {
            var elapsed = 0f;

            // Diablo-style impact: the number appears oversized and quickly
            // settles to its normal size without travelling in an arc.
            while (elapsed < ShrinkDuration)
            {
                if (!IsCurrent(text, generation))
                    yield break;

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / ShrinkDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                origin = FollowTarget(text, target, targetLocalPosition, spreadOffset, origin, startScale);
                text.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(startScale, endScale, eased);
                text.alpha = 1f;
                yield return null;
            }

            origin = FollowTarget(text, target, targetLocalPosition, spreadOffset, origin, endScale);
            text.rectTransform.localScale = Vector3.one * endScale;

            elapsed = 0f;
            while (elapsed < HoldDuration)
            {
                if (!IsCurrent(text, generation))
                    yield break;

                elapsed += Time.deltaTime;
                origin = FollowTarget(text, target, targetLocalPosition, spreadOffset, origin, endScale);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                if (!IsCurrent(text, generation))
                    yield break;

                elapsed += Time.deltaTime;
                origin = FollowTarget(text, target, targetLocalPosition, spreadOffset, origin, endScale);
                var t = Mathf.Clamp01(elapsed / FadeDuration);
                text.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            Recycle(text, generation);
        }

        private Vector2 FollowTarget(
            TextMeshProUGUI text,
            Transform target,
            Vector3 targetLocalPosition,
            Vector2 spreadOffset,
            Vector2 fallback,
            float scale)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                text.rectTransform.anchoredPosition = fallback;
                return fallback;
            }

            var screenPoint = worldCamera.WorldToScreenPoint(target.TransformPoint(targetLocalPosition));
            if (screenPoint.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    pool, screenPoint, canvasCamera, out var localPoint))
            {
                text.rectTransform.anchoredPosition = fallback;
                return fallback;
            }

            var position = ClampToMain(localPoint + spreadOffset, text.rectTransform, scale);
            text.rectTransform.anchoredPosition = position;
            return position;
        }

        private static int GetTopSortingOrder(Transform root)
        {
            var top = 0;
            foreach (var nestedCanvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (nestedCanvas != null && nestedCanvas.overrideSorting)
                    top = Mathf.Max(top, nestedCanvas.sortingOrder);
            }
            return top;
        }

        private TextMeshProUGUI GetText()
        {
            while (available.Count > 0)
            {
                var reused = available.Pop();
                if (reused != null && pooled.Remove(reused))
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

            // 평타 색은 프리팹이 정한 색을 그대로 씁니다. 여기서 한 번만 기억해 둡니다.
            if (!hasNormalColor)
            {
                normalColor = text.color;
                normalColor.a = 1f;
                hasNormalColor = true;
            }

            owned.Add(text);
            return text;
        }

        private uint NextGeneration(TextMeshProUGUI text)
        {
            generations.TryGetValue(text, out var generation);
            generation++;
            generations[text] = generation;
            return generation;
        }

        private bool IsCurrent(TextMeshProUGUI text, uint generation)
        {
            return text != null &&
                   generations.TryGetValue(text, out var current) &&
                   current == generation;
        }

        private void Recycle(TextMeshProUGUI text, uint generation)
        {
            if (!IsCurrent(text, generation) || !owned.Contains(text) || pooled.Contains(text))
                return;

            text.gameObject.SetActive(false);
            text.alpha = 1f;
            pooled.Add(text);
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
            var prefab = AddressableContent.Load<GameObject>("01_Prefabs/UI/Damage");
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/01_Prefabs/UI/Damage.prefab");
#endif
            return prefab;
        }
    }
}
