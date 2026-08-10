using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class ItemAcquisitionFeed : MonoBehaviour
    {
        private const int PrewarmCount = 10;
        private const float RevealDuration = .18f;
        private const float HoldDuration = 1.22f;
        private const float FadeDuration = .6f;
        private const float RiseDistance = 120f;
        private const float SpawnInterval = .5f;
        private const float TotalDuration = RevealDuration + HoldDuration + FadeDuration;
        private const float RetryInterval = 1f;

        private readonly Queue<AcquiredItem> pending = new();
        private readonly Stack<GameObject> pool = new();
        private RectTransform panel;
        private GameObject template;
        private Vector2 origin;
        private PlayerDataManager playerData;
        private Coroutine playback;
        private float nextInitializeAttempt;

        private readonly struct AcquiredItem
        {
            public readonly ItemData Item;
            public readonly int Amount;

            public AcquiredItem(ItemData item, int amount)
            {
                Item = item;
                Amount = amount;
            }
        }

        /// <summary>
        /// 아직 준비되지 않았을 때만 초기화를 다시 시도합니다.
        /// 실패가 이어지는 동안 아이템을 얻을 때마다 씬을 훑지 않도록 재시도 간격을 둡니다.
        /// </summary>
        private bool EnsureInitialized()
        {
            if (panel != null && template != null) return true;
            if (Time.unscaledTime < nextInitializeAttempt) return false;
            nextInitializeAttempt = Time.unscaledTime + RetryInterval;
            return Initialize();
        }

        public bool Initialize()
        {
            var main = SceneRefs.Screen("Main");
            panel = main != null ? main.Find("SafeArea/GetItemPanel") as RectTransform : null;
            if (panel == null)
            {
                Debug.LogWarning("Canvas/Main/SafeArea/GetItemPanel을 찾지 못했습니다.", this);
                return false;
            }

            template = panel.childCount > 0 ? panel.GetChild(0).gameObject : null;
            if (template == null)
            {
                Debug.LogWarning("GetItemPanel 안에 알림 템플릿 Image가 없습니다.", this);
                return false;
            }

            var templateRect = template.transform as RectTransform;
            origin = templateRect != null ? templateRect.anchoredPosition : Vector2.zero;
            template.SetActive(false);

            if (pool.Count == 0)
            {
                for (var i = 0; i < PrewarmCount; i++)
                    pool.Push(CreatePooledAlert());
            }

            if (playerData != null)
                playerData.ItemAcquired -= Enqueue;
            playerData = PlayerDataManager.Instance;
            playerData.ItemAcquired -= Enqueue;
            playerData.ItemAcquired += Enqueue;
            return true;
        }

        private void OnDestroy()
        {
            if (playerData != null)
                playerData.ItemAcquired -= Enqueue;
        }

        private void Enqueue(ItemData item, int totalAmount)
        {
            if (item == null || !EnsureInitialized()) return;

            pending.Enqueue(new AcquiredItem(item, 1));

            if (playback == null)
                playback = StartCoroutine(PlayQueue());
        }

        private IEnumerator PlayQueue()
        {
            while (pending.Count > 0)
            {
                var acquired = pending.Dequeue();
                var alert = GetAlert();
                Configure(alert, acquired);
                StartCoroutine(AnimateAndReturn(alert));
                yield return new WaitForSeconds(SpawnInterval);
            }

            playback = null;
        }

        private GameObject CreatePooledAlert()
        {
            var alert = Instantiate(template, panel, false);
            alert.name = "GetItem_Pooled";
            var layoutElement = alert.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = alert.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            var canvasGroup = alert.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = alert.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            foreach (var graphic in alert.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            alert.SetActive(false);
            return alert;
        }

        private GameObject GetAlert()
        {
            var alert = pool.Count > 0 ? pool.Pop() : CreatePooledAlert();
            alert.SetActive(true);
            alert.transform.SetAsLastSibling();
            return alert;
        }

        private void Configure(GameObject alert, AcquiredItem acquired)
        {
            alert.name = $"GetItem_{acquired.Item.ItemId}";
            var text = alert.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                var color = ColorUtility.ToHtmlStringRGB(RarityColor(acquired.Item.Rarity));
                text.text = $"<color=#{color}>{acquired.Item.DisplayName}</color>";
                text.SetAllDirty();
                text.ForceMeshUpdate();
            }
            var icon = alert.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.name == "Icon")
                ?? alert.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.gameObject != alert && image.name != "Background");
            if (icon != null)
            {
                icon.sprite = acquired.Item.Icon;
                icon.enabled = acquired.Item.Icon != null;
            }
        }

        private IEnumerator AnimateAndReturn(GameObject alert)
        {
            var rect = alert.transform as RectTransform;
            var canvasGroup = alert.GetComponent<CanvasGroup>();
            rect.anchoredPosition = origin;
            rect.localScale = new Vector3(.82f, .82f, 1f);
            canvasGroup.alpha = 0f;

            var elapsed = 0f;
            while (elapsed < TotalDuration)
            {
                elapsed += Time.deltaTime;
                var lifetime = Mathf.Clamp01(elapsed / TotalDuration);
                // Move from the very first frame so alerts spawned 0.2 seconds
                // apart immediately separate instead of occupying one position.
                rect.anchoredPosition = origin + Vector2.up * (RiseDistance * lifetime);

                var reveal = Mathf.Clamp01(elapsed / RevealDuration);
                var revealEase = 1f - Mathf.Pow(1f - reveal, 3f);
                rect.localScale = Vector3.Lerp(new Vector3(.82f, .82f, 1f), Vector3.one, revealEase);

                var fadeStart = RevealDuration + HoldDuration;
                var fade = elapsed <= fadeStart
                    ? 0f
                    : Mathf.Clamp01((elapsed - fadeStart) / FadeDuration);
                canvasGroup.alpha = revealEase * (1f - Mathf.SmoothStep(0f, 1f, fade));
                yield return null;
            }

            ReturnAlert(alert);
        }

        private void ReturnAlert(GameObject alert)
        {
            if (alert == null) return;
            alert.SetActive(false);
            alert.name = "GetItem_Pooled";
            pool.Push(alert);
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
