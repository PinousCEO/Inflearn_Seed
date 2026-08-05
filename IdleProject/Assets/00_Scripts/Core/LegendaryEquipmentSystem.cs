using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>테스트를 위해 전설 9종을 기본 보유/장착하고, 스킬 특수효과를 한 곳에서 관리합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class LegendaryEquipmentSystem : MonoBehaviour
    {
        private static LegendaryEquipmentSystem instance;
        private static readonly string[] LegendaryIds =
        {
            "equipment-003", "equipment-004", "equipment-005", "equipment-006", "equipment-007",
            "equipment-008", "equipment-009", "equipment-010", "equipment-011"
        };

        private readonly Dictionary<string, GameObject> effects = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool[] equipped = new bool[9];
        private ItemCatalog catalog;
        private Transform player;
        private BattleGameController battle;
        private Action<EnemyController, int> damageCallback;

        public static LegendaryEquipmentSystem Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<LegendaryEquipmentSystem>();
                if (instance == null)
                    instance = new GameObject(nameof(LegendaryEquipmentSystem)).AddComponent<LegendaryEquipmentSystem>();
                return instance;
            }
        }

        public bool IsEquipped(int number) => number >= 1 && number <= equipped.Length && equipped[number - 1];

        public void SetEquipped(int number, bool value)
        {
            if (number >= 1 && number <= equipped.Length) equipped[number - 1] = value;
        }

        public void Initialize(BattleGameController owner, Transform playerTransform)
        {
            battle = owner;
            player = playerTransform;
            damageCallback = owner != null ? owner.DamageFromEquipment : null;
            catalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
            EnsureDefaultLoadout();
            var save = PlayerDataManager.Instance;
            for (var i = 0; i < LegendaryIds.Length; i++)
                equipped[i] = save.IsItemEquipped(LegendaryIds[i]);
            LoadEffects();
        }

        private void EnsureDefaultLoadout()
        {
            var data = PlayerDataManager.Instance;
            if (catalog == null) return;
            foreach (var id in LegendaryIds)
                if (catalog.TryGet(id, out var item) && data.GetItemCount(item) <= 0)
                    data.AddItem(item);
        }

        private void LoadEffects()
        {
            effects["sand"] = LoadPrefab("01_Prefabs/Effects/Tornado_sand", "Assets/01_Prefabs/Effects/Tornado_sand.prefab");
            effects["snow"] = LoadPrefab("01_Prefabs/Effects/Tornado_snow", "Assets/01_Prefabs/Effects/Tornado_snow.prefab");
            effects["quake"] = LoadPrefab("01_Prefabs/Effects/EarthQuake", "Assets/01_Prefabs/Effects/EarthQuake.prefab");
            effects["slow"] = LoadPrefab("01_Prefabs/Effects/Aura_slowdown", "Assets/01_Prefabs/Effects/Aura_slowdown.prefab");
            effects["chain8"] = LoadPrefab("01_Prefabs/Effects/8", "Assets/01_Prefabs/Effects/8.prefab");
            effects["chain9"] = LoadPrefab("01_Prefabs/Effects/9", "Assets/01_Prefabs/Effects/9.prefab");
        }

        public void OnSkill(int skillIndex, EnemyController[] targets, Action<EnemyController, int> damage)
        {
            if (skillIndex == 0)
            {
                // 2번 장비가 있으면 1번 모래 소용돌이를 화염 버전으로 변형합니다.
                var tornado = IsEquipped(2) && effects["snow"] != null ? "snow" : "sand";
                if (IsEquipped(1) || IsEquipped(2)) SpawnMovingEffect(tornado, 4.5f, 2.4f, targets, damage);
            }
            if (skillIndex == 1 && IsEquipped(3)) StartCoroutine(RepeatDamage(targets, damage, 3, .16f, 1));
            if (skillIndex == 2 && IsEquipped(4))
            {
                SpawnAfterimages();
                StartCoroutine(RepeatDamage(targets.OrderBy(_ => UnityEngine.Random.value).Take(2).ToArray(), damage, 2, .2f, 2));
            }
            if (skillIndex == 3 && IsEquipped(6)) SpawnMovingEffect("sand", 3f, 1.8f, targets, damage);
            if (skillIndex == 3 && IsEquipped(8))
            {
                var explosion = SpawnEffect("chain8", player != null ? player.position : Vector3.zero);
                if (explosion != null) StartCoroutine(DestroyAfter(explosion, 2f));
                StartCoroutine(RepeatDamage(targets, damage, 5, .35f, 2));
            }
            if (skillIndex == 3 && IsEquipped(9))
            {
                var sky = SpawnEffect("chain9", player != null ? player.position : Vector3.zero);
                if (sky != null)
                {
                    StartCoroutine(DestroyAfter(sky, 2f));
                }
                StartCoroutine(RepeatDamage(GetNearbyTargets(9f, targets, player != null ? player.position : Vector3.zero), damage, 4, .45f, 3));
            }
            if (skillIndex == 3 && IsEquipped(7))
                foreach (var target in targets) if (target != null) target.ApplySlow(4f, .45f, effects["slow"]);
        }

        public void OnStun(EnemyController target)
        {
            if (!IsEquipped(5) || target == null || UnityEngine.Random.value > .35f) return;
            var position = target.transform.position;
            var quake = SpawnEffect("quake", position);
            if (quake != null) StartCoroutine(DestroyAfter(quake, 2.2f));
            StartCoroutine(AreaDamage(target, 2.2f, 5, .45f));
        }

        private IEnumerator AreaDamage(EnemyController center, float radius, int ticks, float interval)
        {
            for (var i = 0; i < ticks; i++)
            {
                if (center == null) yield break;
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    if (enemy != null && !enemy.IsDead && (enemy.transform.position - center.transform.position).sqrMagnitude <= radius * radius)
                        damageCallback?.Invoke(enemy, 8);
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator RepeatDamage(EnemyController[] targets, Action<EnemyController, int> damage, int count, float interval, int multiplier)
        {
            for (var i = 0; i < count; i++)
            {
                foreach (var target in targets)
                    if (target != null && !target.IsDead) damage(target, multiplier);
                yield return new WaitForSeconds(interval);
            }
        }

        private void SpawnMovingEffect(string key, float duration, float radius, EnemyController[] targets, Action<EnemyController, int> damage)
        {
            var origin = player != null ? player.position : Vector3.zero;
            var effect = SpawnEffect(key, origin + Vector3.up * .05f);
            if (effect != null) StartCoroutine(MoveEffect(effect, duration, radius, targets, damage));
        }

        private void SpawnAfterimages()
        {
            // 캐릭터 프리팹을 복제하면 Animator 바인딩이 풀린 잔상이 T-Pose가 될 수 있으므로
            // 실제 캐릭터 복제는 하지 않습니다. 방패 밀쳐내기 피해만 안전하게 유지합니다.
        }

        private static EnemyController[] GetNearbyTargets(float radius, EnemyController[] fallback, Vector3 center)
        {
            var all = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            var result = all.Where(enemy => enemy != null && !enemy.IsDead &&
                (enemy.transform.position - center).sqrMagnitude <= radius * radius).ToArray();
            return result.Length > 0 ? result : fallback;
        }

        private IEnumerator MoveEffect(GameObject effect, float duration, float radius, EnemyController[] targets, Action<EnemyController, int> damage)
        {
            var elapsed = 0f;
            var nextHitTime = new Dictionary<EnemyController, float>();
            var direction = UnityEngine.Random.insideUnitSphere; direction.y = 0f; direction.Normalize();
            while (effect != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                effect.transform.position += direction * (2.4f * Time.deltaTime);
                foreach (var target in targets)
                    if (target != null && !target.IsDead &&
                        (target.transform.position - effect.transform.position).sqrMagnitude < radius * radius &&
                        (!nextHitTime.TryGetValue(target, out var nextHit) || elapsed >= nextHit))
                    {
                        damage(target, 2);
                        nextHitTime[target] = elapsed + .55f;
                    }
                yield return null;
            }
            if (effect != null) Destroy(effect);
        }

        private GameObject SpawnEffect(string key, Vector3 position)
        {
            if (!effects.TryGetValue(key, out var prefab) || prefab == null) return null;
            var effect = Instantiate(prefab, position, Quaternion.identity);
            effect.name = $"Legendary_{key}";
            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true)) { particle.Clear(true); particle.Play(true); }
            return effect;
        }

        private static IEnumerator DestroyAfter(GameObject target, float seconds)
        { yield return new WaitForSeconds(seconds); if (target != null) Destroy(target); }

        private static GameObject LoadPrefab(string resourcesPath, string assetPath)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
#if UNITY_EDITOR
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#endif
            return prefab;
        }
    }
}
