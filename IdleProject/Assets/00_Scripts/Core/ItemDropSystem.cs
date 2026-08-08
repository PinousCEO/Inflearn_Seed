using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class ItemDropSystem : MonoBehaviour
    {
        private const string ItemDesResourcePath = "01_Prefabs/UI/ItemDes";
        private const string EffectResourceRoot = "01_Prefabs/Effects/Loot_";
        private const string PickupResourcePath = "01_Prefabs/Effects/Loot_pick_up";
        private const float DropRadius = 1.15f;
        private const float EffectScale = .4f;
        private const float TossDuration = .55f;
        private const float TossHeight = .9f;
        private const float AutoPickupDelay = 3f;
        private const float RetryInterval = 1f;

        private ItemData[] dropTable = Array.Empty<ItemData>();
        private readonly Dictionary<ItemRarity, GameObject> effectPrefabs = new();
        private GameObject itemDescriptionPrefab;
        private GameObject pickupEffectPrefab;
        private RectTransform mainUi;
        private Camera worldCamera;
        private float nextInitializeAttempt;

        public void Initialize(Camera camera)
        {
            worldCamera = camera != null ? camera : Camera.main;
            mainUi = SceneRefs.Screen("Main") as RectTransform;
            itemDescriptionPrefab = LoadPrefab(ItemDesResourcePath, "Assets/01_Prefabs/UI/ItemDes.prefab");
            pickupEffectPrefab = LoadPrefab(PickupResourcePath, "Assets/01_Prefabs/Effects/Loot_pick_up.prefab");
            dropTable = LoadDropTable();

            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                var rarityName = rarity.ToString();
                var prefab = LoadPrefab(
                    EffectResourceRoot + rarityName,
                    $"Assets/01_Prefabs/Effects/Loot_{rarityName}.prefab");
                if (prefab != null)
                    effectPrefabs[rarity] = prefab;
            }

            if (mainUi == null)
                Debug.LogWarning("ItemDes를 표시할 Canvas/Main을 찾지 못했습니다.", this);
            if (dropTable.Length == 0)
                Debug.LogWarning("ItemData 드랍 테이블이 비어 있습니다.", this);
        }

        public void RollDrops(Vector3 deathPosition)
        {
            // 드랍 테이블이 비어 있다고 몬스터가 죽을 때마다 재초기화하면
            // 씬 탐색과 Resources 로드가 계속 반복됩니다. 재시도 간격을 둡니다.
            if (dropTable.Length == 0)
            {
                if (Time.unscaledTime < nextInitializeAttempt) return;
                nextInitializeAttempt = Time.unscaledTime + RetryInterval;
                Initialize(worldCamera);
                if (dropTable.Length == 0) return;
            }

            var droppedItems = dropTable
                .Where(item => item != null && item.DropRatePercent > 0f &&
                               Random.value * 100f < item.DropRatePercent)
                .ToArray();

            for (var i = 0; i < droppedItems.Length; i++)
            {
                var angle = droppedItems.Length == 1
                    ? Random.Range(0f, Mathf.PI * 2f)
                    : i * Mathf.PI * 2f / droppedItems.Length + Random.Range(-.18f, .18f);
                var radius = droppedItems.Length == 1 ? .35f : DropRadius;
                var landingPosition = deathPosition + new Vector3(
                    Mathf.Cos(angle) * radius, .05f, Mathf.Sin(angle) * radius);
                SpawnDrop(droppedItems[i], deathPosition + Vector3.up * .15f, landingPosition);
            }
        }

        private void SpawnDrop(ItemData item, Vector3 startPosition, Vector3 landingPosition)
        {
            if (!effectPrefabs.TryGetValue(item.Rarity, out var effectPrefab) || effectPrefab == null)
            {
                Debug.LogWarning($"{item.Rarity} 드랍 이펙트가 없습니다.", this);
                return;
            }

            var effect = Instantiate(effectPrefab, startPosition, Quaternion.identity);
            effect.name = $"Drop_{item.ItemId}";
            effect.transform.localScale *= EffectScale;

            var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
            }
            StartCoroutine(TossAndAutoPickup(item, effect, landingPosition, particles));

            if (itemDescriptionPrefab == null || mainUi == null)
                return;

            var description = Instantiate(itemDescriptionPrefab, mainUi, false);
            description.name = $"ItemDes_{item.ItemId}";
            description.transform.SetAsLastSibling();
            var follower = description.GetComponent<DroppedItemLabel>() ?? description.AddComponent<DroppedItemLabel>();
            follower.Initialize(item, effect.transform, mainUi, worldCamera, Vector3.up * .35f);
        }

        private IEnumerator TossAndAutoPickup(
            ItemData item,
            GameObject drop,
            Vector3 landingPosition,
            ParticleSystem[] particles)
        {
            if (drop == null) yield break;
            var startPosition = drop.transform.position;
            var elapsed = 0f;
            while (elapsed < TossDuration)
            {
                if (drop == null) yield break;
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / TossDuration);
                var arc = 4f * TossHeight * t * (1f - t);
                drop.transform.position = Vector3.Lerp(startPosition, landingPosition, t) + Vector3.up * arc;
                yield return null;
            }

            if (drop == null) yield break;
            drop.transform.position = landingPosition;
            foreach (var particle in particles)
            {
                if (particle == null || !particle.gameObject.activeInHierarchy) continue;
                particle.Clear(true);
                particle.Play(true);
            }

            yield return new WaitForSeconds(AutoPickupDelay);
            if (drop == null) yield break;

            PlayerDataManager.Instance.AddItem(item);
            PlayPickupEffect(landingPosition);
            Destroy(drop);
        }

        private void PlayPickupEffect(Vector3 position)
        {
            if (pickupEffectPrefab == null)
            {
                Debug.LogWarning("Loot_pick_up 이펙트를 찾지 못했습니다.", this);
                return;
            }

            var pickup = Instantiate(pickupEffectPrefab, position, Quaternion.identity);
            pickup.name = "Loot_pick_up";
            pickup.transform.localScale *= EffectScale;
            var particles = pickup.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                if (!particle.gameObject.activeInHierarchy) continue;
                particle.Clear(true);
                particle.Play(true);
            }
            StartCoroutine(DestroyWhenFinished(pickup, particles));
        }

        private static IEnumerator DestroyWhenFinished(GameObject effect, ParticleSystem[] particles)
        {
            yield return null;
            var timeout = 5f;
            while (effect != null && timeout > 0f && particles.Any(particle => particle != null && particle.IsAlive(true)))
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (effect != null) Destroy(effect);
        }

        private static ItemData[] LoadDropTable()
        {
            var catalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
            var items = catalog != null
                ? catalog.Items.Where(item => item != null).ToArray()
                : Resources.LoadAll<ItemData>("Data/Items");
#if UNITY_EDITOR
            if (items.Length == 0)
            {
                items = UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/00_Data/Items" })
                    .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>)
                    .Where(item => item != null)
                    .ToArray();
            }
#endif
            Array.Sort(items, (a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
            return items;
        }

        private static GameObject LoadPrefab(string resourcesPath, string assetPath)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#endif
            return prefab;
        }
    }
}
