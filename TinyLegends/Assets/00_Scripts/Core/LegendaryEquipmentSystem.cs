using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleBattle.Audio;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>테스트를 위해 전설 9종을 기본 보유/장착하고, 스킬 특수효과를 한 곳에서 관리합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class LegendaryEquipmentSystem : MonoBehaviour
    {
        private static LegendaryEquipmentSystem instance;

        /// <summary>
        /// 특수효과 1~9번이 붙는 전설 장비입니다. 순서는 장비 화면의 9개 슬롯 순서
        /// (무기·투구·갑옷·견갑·건틀릿·허리띠·장화·반지·목걸이)와 같습니다.
        /// 장비 화면도 이 목록을 그대로 쓰므로, 두 곳이 서로 어긋날 일이 없습니다.
        /// </summary>
        public static readonly string[] LegendaryIds =
        {
            "equipment-016", // 전설의 지옥불 도끼
            "equipment-018", // 전설의 사자수호 투구
            "equipment-021", // 전설의 뼈감시자 갑옷
            "equipment-022", // 전설의 폭풍 견갑
            "equipment-024", // 전설의 용암 건틀릿
            "equipment-026", // 전설의 거신 허리띠
            "equipment-028", // 전설의 비익 장화
            "equipment-030", // 전설의 용안 반지
            "equipment-032"  // 전설의 불사조 목걸이
        };

        /// <summary>
        /// 예전에 이 자리에 있던 장비입니다. 장비표를 다시 만들면서 같은 번호가
        /// 일반 등급으로 밀려났습니다. 이것을 끼고 있던 저장 데이터는 같은 자리의
        /// 전설 장비로 한 번만 옮겨 줍니다.
        /// </summary>
        private static readonly string[] LegacyIds =
        {
            "equipment-003", "equipment-004", "equipment-005", "equipment-006", "equipment-007",
            "equipment-008", "equipment-009", "equipment-010", "equipment-011"
        };

        private readonly Dictionary<string, GameObject> effects = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool[] equipped = new bool[9];
        // 몬스터 목록을 매번 새로 할당하지 않기 위한 재사용 버퍼입니다.
        private readonly List<EnemyController> damageBuffer = new();
        private readonly List<EnemyController> targetBuffer = new();
        private ItemCatalog catalog;
        private Transform player;
        private BattleGameController battle;
        private Action<EnemyController, int> damageCallback;
        private bool isSyncing;

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
            catalog = AddressableContent.Load<ItemCatalog>("Data/ItemCatalog");

            // 전투 시작 시점에는 아직 Firestore 로드가 끝나지 않았을 수 있습니다.
            // 그때 한 번 읽고 마는 대신, 저장 데이터가 바뀔 때마다 다시 맞춥니다.
            // (로드가 끝나면 PlayerDataManager가 InventoryChanged를 한 번 쏩니다)
            var save = PlayerDataManager.Instance;
            if (save != null)
            {
                save.InventoryChanged -= SyncWithSave;
                save.InventoryChanged += SyncWithSave;
            }
            SyncWithSave();
            LoadEffects();
        }

        private void OnDestroy()
        {
            // 종료 중에는 싱글턴을 새로 생성하지 않고 기존 인스턴스가 있을 때만 해제합니다.
            var save = PlayerDataManager.Existing;
            if (save != null) save.InventoryChanged -= SyncWithSave;
        }

        private void SyncWithSave()
        {
            var data = PlayerDataManager.Instance;
            // 아래에서 아이템을 주거나 착용을 옮기면 InventoryChanged가 다시 날아옵니다.
            if (data == null || isSyncing) return;

            isSyncing = true;
            try
            {
                if (data.IsLoaded)
                {
                    EnsureDefaultLoadout(data);
                    MigrateLegacyLoadout(data);
                }
                for (var i = 0; i < LegendaryIds.Length; i++)
                    equipped[i] = data.IsItemEquipped(LegendaryIds[i]);
            }
            finally
            {
                isSyncing = false;
            }
        }

        private void EnsureDefaultLoadout(PlayerDataManager data)
        {
            if (catalog == null) return;
            foreach (var id in LegendaryIds)
                if (catalog.TryGet(id, out var item) && data.GetItemCount(item) <= 0)
                    data.AddItem(item, 1, false);
        }

        /// <summary>
        /// 예전 번호의 장비를 끼고 있었다면, 같은 자리의 전설 장비로 바꿔 끼웁니다.
        /// 저장된 착용 정보만 옮기므로 한 번 지나가면 다시 실행되지 않습니다.
        /// </summary>
        private void MigrateLegacyLoadout(PlayerDataManager data)
        {
            for (var i = 0; i < LegacyIds.Length; i++)
            {
                if (!data.IsItemEquipped(LegacyIds[i])) continue;
                data.SetItemEquipped(LegacyIds[i], false);
                if (!data.IsItemEquipped(LegendaryIds[i]))
                    data.SetItemEquipped(LegendaryIds[i], true);
            }
        }

        private void LoadEffects()
        {
            effects["sand"] = LoadPrefab("01_Prefabs/Effects/Tornado_sand", "Assets/Resources/01_Prefabs/Effects/Tornado_sand.prefab");
            effects["snow"] = LoadPrefab("01_Prefabs/Effects/Tornado_snow", "Assets/Resources/01_Prefabs/Effects/Tornado_snow.prefab");
            effects["quake"] = LoadPrefab("01_Prefabs/Effects/EarthQuake", "Assets/Resources/01_Prefabs/Effects/EarthQuake.prefab");
            effects["slow"] = LoadPrefab("01_Prefabs/Effects/Aura_slowdown", "Assets/Resources/01_Prefabs/Effects/Aura_slowdown.prefab");
            effects["chain8"] = LoadPrefab("01_Prefabs/Effects/8", "Assets/Resources/01_Prefabs/Effects/8.prefab");
            effects["chain9"] = LoadPrefab("01_Prefabs/Effects/9", "Assets/Resources/01_Prefabs/Effects/9.prefab");
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
                // 틱마다 씬을 훑는 대신 전투 컨트롤러가 이미 들고 있는 생존 목록을 재사용합니다.
                CollectActiveEnemies(damageBuffer);
                var centerPosition = center.transform.position;
                foreach (var enemy in damageBuffer)
                    if (enemy != null && !enemy.IsDead && (enemy.transform.position - centerPosition).sqrMagnitude <= radius * radius)
                        damageCallback?.Invoke(enemy, 8);
                yield return new WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// 살아 있는 몬스터 목록을 얻습니다. 전투 컨트롤러가 연결돼 있으면 그 목록을 쓰고,
        /// 아직 Initialize 전이라면 그때 한정으로만 씬을 탐색합니다.
        /// </summary>
        private void CollectActiveEnemies(List<EnemyController> buffer)
        {
            if (battle != null)
            {
                battle.CopyActiveEnemies(buffer);
                return;
            }

            buffer.Clear();
            buffer.AddRange(FindObjectsByType<EnemyController>(FindObjectsSortMode.None));
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

        private EnemyController[] GetNearbyTargets(float radius, EnemyController[] fallback, Vector3 center)
        {
            CollectActiveEnemies(targetBuffer);
            var result = targetBuffer.Where(enemy => enemy != null && !enemy.IsDead &&
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
            AudioManager.PlayAt(GetEffectSound(key), position);
            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true)) { particle.Clear(true); particle.Play(true); }
            return effect;
        }

        /// <summary>전설 장비가 부르는 이펙트 종류에 맞는 효과음입니다.</summary>
        private static SfxId GetEffectSound(string key)
        {
            switch (key)
            {
                case "sand":
                case "snow": return SfxId.LegendaryTornado;
                case "quake": return SfxId.LegendaryQuake;
                case "chain8": return SfxId.LegendaryExplosion;
                case "chain9": return SfxId.LegendarySky;
                default: return SfxId.None;
            }
        }

        private static IEnumerator DestroyAfter(GameObject target, float seconds)
        { yield return new WaitForSeconds(seconds); if (target != null) Destroy(target); }

        private static GameObject LoadPrefab(string resourcesPath, string assetPath)
        {
            var prefab = AddressableContent.Load<GameObject>(resourcesPath);
#if UNITY_EDITOR
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#endif
            return prefab;
        }
    }
}
