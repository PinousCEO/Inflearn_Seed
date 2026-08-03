using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace IdleBattle
{
    public sealed class BattleGameController : MonoBehaviour
    {
        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private Transform player, destinationMarker;
        private readonly List<GameObject> skillEffects = new List<GameObject>();
        private Animator playerAnimator;
        private CharacterEquipmentPresenter playerEquipment;
        private float playerGroundY;
        private Vector3 destination;
        private Material playerMaterial, enemyMaterial, markerMaterial;
        private GameObject enemyPrefab;
        private ObjectPool<EnemyController> enemyPool;
        private Transform enemyPoolRoot;
        private DamagePopupSystem damagePopupSystem;
        private Terrain terrain;
        private Vector2 worldMin = new Vector2(-37f, -37f);
        private Vector2 worldMax = new Vector2(37f, 37f);
        private int wave, defeated;
        private int currentStage = 1;
        private int completedNormalRounds;
        private bool activeEncounterIsBoss;
        private StageData stageData;
        private StageRule currentStageRule;
        private RectTransform stageSlider;
        private RectTransform stageSliderFill;
        private float travelDistance;
        private float stageProgress;
        private int playerMana;
        private bool isSkillActive;
        private bool skillEventApplied;
        private float animatorSpeedBeforeSkill = 1f;
        private int nextSkillNum;
        private SkillData[] skills = Array.Empty<SkillData>();
        private float[] skillReadyTimes = Array.Empty<float>();
        private SkillData activeSkill;
        private int activeSkillIndex = -1;
        private string stateText = "지역 탐색 중";

        private const float SpawnRadius = 10f;
        private const float MoveSpeed = 4.2f;
        private const float AttackRange = 2.1f;
        private const int SkillAnimationCount = 4;
        private const float SkillAnimationSpeed = 1.35f;
        private const float SkillEventFallbackNormalizedTime = .75f;
        private const float StageStatGrowth = 1.1f;
        private const int NormalEnemyBaseHealth = 50;
        private const int NormalEnemyBaseDamage = 20;
        private const int BossHealthMultiplier = 4;
        private const int BossDamageMultiplier = 2;

        private void Awake()
        {
            LoadSkills();
            SetupWorld();
            SetupStage();
            SetupStageSlider();
            CreateDestination();
        }

        public event Action<SkillData, int> SkillCast;
        public IReadOnlyList<SkillData> Skills => skills;
        public float GetCooldownRemaining(int index) =>
            index >= 0 && index < skillReadyTimes.Length
                ? Mathf.Max(0f, skillReadyTimes[index] - Time.time)
                : 0f;

        private void LoadSkills()
        {
            skills = Resources.LoadAll<SkillData>("Data/Skills");
            Array.Sort(skills, (a, b) => string.CompareOrdinal(a.SkillId, b.SkillId));
            skillReadyTimes = new float[skills.Length];
            if (skills.Length != 5)
                Debug.LogWarning($"5개의 SkillData가 필요합니다. 현재 {skills.Length}개입니다.", this);
        }

        private void SetupStage()
        {
            stageData = Resources.Load<StageData>("Data/StageData");
            if (stageData != null && stageData.TryGetRule(currentStage, out currentStageRule))
                return;

            Debug.LogError("Resources/StageData를 불러오지 못했거나 Stage 1 규칙이 없습니다.", this);
            enabled = false;
        }

        private void SetupStageSlider()
        {
            var sliderObject = GameObject.Find("Canvas/Top/Slider");
            if (sliderObject == null) return;

            stageSlider = sliderObject.GetComponent<RectTransform>();
            var fill = sliderObject.transform.Find("Fill");
            stageSliderFill = fill != null ? fill.GetComponent<RectTransform>() : null;
            if (stageSliderFill != null)
            {
                var fillImage = stageSliderFill.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.type = Image.Type.Simple;
                    fillImage.fillAmount = 1f;
                    fillImage.raycastTarget = false;
                }
                stageSliderFill.anchorMin = new Vector2(0f, .5f);
                stageSliderFill.anchorMax = new Vector2(0f, .5f);
                stageSliderFill.pivot = new Vector2(0f, .5f);
                stageSliderFill.anchoredPosition = Vector2.zero;
            }

            var oldHorizontal = sliderObject.transform.Find("Horizontal");
            if (oldHorizontal != null)
                oldHorizontal.gameObject.SetActive(false);

            UpdateStageSlider(0f);
        }

        private void UpdateStageSlider(float progress)
        {
            stageProgress = Mathf.Clamp01(progress);
            if (stageSliderFill != null && stageSlider != null)
                stageSliderFill.sizeDelta = new Vector2(stageSlider.rect.width * stageProgress, stageSliderFill.sizeDelta.y);
        }

        private void SetupWorld()
        {
            terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                var terrainPosition = terrain.transform.position;
                var terrainSize = terrain.terrainData.size;
                // 가장자리 산과 장식물 쪽으로 전투가 번지지 않도록 약간의 여백을 둡니다.
                var margin = Mathf.Min(12f, Mathf.Min(terrainSize.x, terrainSize.z) * .08f);
                worldMin = new Vector2(terrainPosition.x + margin, terrainPosition.z + margin);
                worldMax = new Vector2(terrainPosition.x + terrainSize.x - margin, terrainPosition.z + terrainSize.z - margin);
            }
            else
            {
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.gameObject.SetActive(false);
            }

            playerMaterial = MakeMaterial(new Color(.15f, .65f, 1f));
            enemyMaterial = MakeMaterial(new Color(.9f, .2f, .25f));
            markerMaterial = MakeMaterial(new Color(.95f, .73f, .12f, .75f));
            enemyPrefab = LoadPrefab("01_Prefabs/Enemy", "Assets/01_Prefabs/Enemy.prefab");
            CreateEnemyPool();
            damagePopupSystem = GetComponent<DamagePopupSystem>();
            if (damagePopupSystem == null)
                damagePopupSystem = gameObject.AddComponent<DamagePopupSystem>();
            damagePopupSystem.Initialize(Camera.main);

            if (terrain == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Battle Ground";
                ground.transform.localScale = new Vector3(8, 1, 8);
                ground.GetComponent<Renderer>().material = MakeMaterial(new Color(.17f, .27f, .2f));
            }

            var characterPrefab = LoadPrefab("01_Prefabs/Character", "Assets/01_Prefabs/Character.prefab");
            GameObject hero;
            if (characterPrefab != null)
            {
                hero = Instantiate(characterPrefab);
                hero.name = "Character";
                // Skill 파티클의 큰 Renderer bounds가 캐릭터의 지면 배치에 포함되지 않게
                // 높이를 계산하기 전에 먼저 비활성화합니다.
                var initialSkillRoot = FindChildByName(hero.transform, "Skill0");
                if (initialSkillRoot != null) initialSkillRoot.SetActive(false);
                hero.transform.position = GroundPoint(Vector3.zero);
                PlaceCharacterOnGround(hero, hero.transform.position.y);
            }
            else
            {
                Debug.LogWarning("Assets/01_Prefabs/Character.prefab을 찾지 못해 Capsule을 대신 사용합니다.");
                hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hero.name = "Character (Fallback)";
                hero.transform.position = GroundPoint(Vector3.zero) + Vector3.up;
                hero.GetComponent<Renderer>().material = playerMaterial;
            }
            player = hero.transform;
            CollectCharacterSkillEffects(hero.transform);
            playerAnimator = hero.GetComponentInChildren<Animator>();
            playerEquipment = hero.GetComponentInChildren<CharacterEquipmentPresenter>(true);
            if (playerAnimator == null)
                Debug.LogWarning("Character 프리팹에서 Animator를 찾지 못했습니다.");
            else
            {
                var relay = playerAnimator.gameObject.GetComponent<CharacterAnimationEventRelay>();
                if (relay == null) relay = playerAnimator.gameObject.AddComponent<CharacterAnimationEventRelay>();
                relay.owner = this;
                playerAnimator.SetBool("Run", false);
            }
            playerGroundY = player.position.y - GroundPoint(player.position).y;
            ConfigurePlayerCamera();

            if (terrain == null)
            {
                var lightObject = new GameObject("Sun");
                var sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.25f;
                sun.color = new Color(1f, .94f, .82f);
                lightObject.transform.rotation = Quaternion.Euler(48, -35, 0);
            }

        }

        private void ConfigurePlayerCamera()
        {
            var playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogWarning("MainCamera 태그가 지정된 기존 카메라를 찾지 못했습니다.");
                return;
            }

            var follow = playerCamera.GetComponent<PlayerCameraController>();
            if (follow == null) follow = playerCamera.gameObject.AddComponent<PlayerCameraController>();
            follow.SetTarget(player);
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

        private void CreateEnemyPool()
        {
            enemyPoolRoot = new GameObject("Pool#[Enemy]").transform;
            enemyPool = new ObjectPool<EnemyController>(
                CreatePooledEnemy,
                enemy => enemy.gameObject.SetActive(true),
                ReleasePooledEnemy,
                enemy =>
                {
                    if (enemy != null) Destroy(enemy.gameObject);
                },
                true,
                9,
                18);
        }

        private EnemyController CreatePooledEnemy()
        {
            GameObject enemy;
            if (enemyPrefab != null)
            {
                enemy = Instantiate(enemyPrefab);
            }
            else
            {
                Debug.LogWarning("Assets/01_Prefabs/Enemy.prefab을 찾지 못해 Sphere를 대신 사용합니다.");
                enemy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                enemy.transform.localScale = Vector3.one * 1.25f;
                enemy.GetComponent<Renderer>().material = enemyMaterial;
            }

            var controller = enemy.GetComponent<EnemyController>();
            if (controller == null)
                controller = enemy.AddComponent<EnemyController>();
            return controller;
        }

        private void ReleasePooledEnemy(EnemyController enemy)
        {
            enemy.PrepareForPool();
            enemy.transform.SetParent(enemyPoolRoot, false);
            enemy.gameObject.SetActive(false);
        }

        private static void PlaceCharacterOnGround(GameObject hero, float groundHeight = 0f)
        {
            var renderers = hero.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            hero.transform.position += Vector3.up * (groundHeight - bounds.min.y);
        }

        private static GameObject FindChildByName(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName) return child.gameObject;
            }
            return null;
        }

        private void CollectCharacterSkillEffects(Transform heroRoot)
        {
            skillEffects.Clear();
            var skillRoot = FindChildByName(heroRoot, "Skill0");
            if (skillRoot == null)
            {
                Debug.LogWarning("Character 프리팹에서 Skill0 스킬 목록을 찾지 못했습니다.", this);
                return;
            }

            skillRoot.SetActive(true);
            for (var i = 0; i < skillRoot.transform.childCount; i++)
            {
                var effect = skillRoot.transform.GetChild(i).gameObject;
                effect.SetActive(false);
                skillEffects.Add(effect);
            }

            if (skillEffects.Count != skills.Length)
            {
                Debug.LogWarning(
                    $"SkillData({skills.Length})와 Character/Skill0 자식({skillEffects.Count}) 수가 다릅니다. " +
                    "자식 오브젝트가 존재하는 스킬만 전투에서 사용합니다.",
                    this);

                var usableSkillCount = Mathf.Min(skills.Length, skillEffects.Count);
                Array.Resize(ref skills, usableSkillCount);
                Array.Resize(ref skillReadyTimes, usableSkillCount);
            }
        }

        private void Update()
        {
            enemies.RemoveAll(enemy => enemy == null);
            if (isSkillActive)
            {
                SetRunning(false);
                return;
            }
            if (enemies.Count == 0)
            {
                stateText = "다음 전투 지역으로 이동 중";
                MoveTowards(destination);
                UpdateTravelProgress();
                if (Vector3.Distance(Flat(player.position), Flat(destination)) < .65f) SpawnWave();
                return;
            }

            var target = FindNearestEnemy();
            if (target == null) return;
            var distance = Vector3.Distance(Flat(player.position), Flat(target.transform.position));
            if (distance > AttackRange)
            {
                stateText = "몬스터 추적 중";
                MoveTowards(target.transform.position);
            }
            else
            {
                stateText = "스킬 사용 대기 중";
                SetRunning(false);
                Face(target.transform.position);
                TryActivateRandomSkill();
            }
        }

        private EnemyController FindNearestEnemy()
        {
            EnemyController nearest = null;
            var distance = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                var d = (enemy.transform.position - player.position).sqrMagnitude;
                if (d >= distance) continue;
                distance = d;
                nearest = enemy;
            }
            return nearest;
        }

        private void MoveTowards(Vector3 target)
        {
            if (!isSkillActive) SetRunning(true);
            var flatTarget = GroundPoint(target) + Vector3.up * playerGroundY;
            Face(flatTarget);
            player.position = Vector3.MoveTowards(player.position, flatTarget, MoveSpeed * Time.deltaTime);
            var clamped = ClampToWorld(player.position);
            player.position = GroundPoint(clamped) + Vector3.up * playerGroundY;
        }

        private void Face(Vector3 target)
        {
            var direction = Flat(target) - Flat(player.position);
            if (direction.sqrMagnitude > .001f)
                player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(direction), 12 * Time.deltaTime);
        }

        private void SetRunning(bool value)
        {
            if (playerAnimator != null) playerAnimator.SetBool("Run", value);
        }

        // 예전 Attack 클립의 Animation Event가 남아 있어도 기본 공격은 실행하지 않습니다.
        public void ATK()
        {
        }

        private bool TryActivateRandomSkill()
        {
            if (isSkillActive || skills.Length == 0 || skillEffects.Count == 0) return false;
            var candidates = new List<int>();
            var playerData = PlayerDataManager.Instance;
            for (var i = 0; i < skills.Length; i++)
                if (skills[i] != null &&
                    Time.time >= skillReadyTimes[i] &&
                    playerData.CurrentMana >= Mathf.CeilToInt(skills[i].ResourceCost))
                    candidates.Add(i);
            if (candidates.Count == 0) return false;
            var index = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var skill = skills[index];
            if (!playerData.TrySpendMana(Mathf.CeilToInt(skill.ResourceCost))) return false;
            skillReadyTimes[index] = Time.time + skill.Cooldown;
            activeSkill = skill;
            activeSkillIndex = index;
            SkillCast?.Invoke(skill, index);
            StartCoroutine(ActivateSkill());
            return true;
        }

        private IEnumerator ActivateSkill()
        {
            isSkillActive = true;
            skillEventApplied = false;
            SetRunning(false);
            if (playerEquipment != null) playerEquipment.DrawWeapon();

            if (playerAnimator == null)
            {
                Skill();
                FinishSkill();
                yield break;
            }

            var previousState = playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            animatorSpeedBeforeSkill = playerAnimator.speed;
            playerAnimator.speed = animatorSpeedBeforeSkill * SkillAnimationSpeed;
            playerAnimator.SetInteger("Num", activeSkill != null
                ? activeSkill.AnimationIndex
                : nextSkillNum);
            nextSkillNum = (nextSkillNum + 1) % SkillAnimationCount;
            playerAnimator.ResetTrigger("Skill");
            playerAnimator.SetTrigger("Skill");

            var enterTimeout = 1f;
            while (enterTimeout > 0f && playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == previousState)
            {
                enterTimeout -= Time.deltaTime;
                yield return null;
            }

            var skillState = playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var finishTimeout = 5f;
            while (finishTimeout > 0f)
            {
                finishTimeout -= Time.deltaTime;
                var state = playerAnimator.GetCurrentAnimatorStateInfo(0);
                // Every authored skill clip has a Skill Animation Event at its
                // actual contact frame. Only fall back late in the clip when an
                // imported event is missing; firing at .3 made VFX/damage lead
                // the weapon contact by a visible amount.
                if (!skillEventApplied &&
                    !playerAnimator.IsInTransition(0) &&
                    state.normalizedTime >= SkillEventFallbackNormalizedTime)
                    Skill();

                if (state.fullPathHash != skillState ||
                    (!playerAnimator.IsInTransition(0) && state.normalizedTime >= .98f))
                    break;
                yield return null;
            }

            if (!skillEventApplied)
                Skill();
            FinishSkill();
        }

        private void FinishSkill()
        {
            if (playerAnimator != null) playerAnimator.speed = animatorSpeedBeforeSkill;
            isSkillActive = false;
            activeSkill = null;
            activeSkillIndex = -1;
            if (playerEquipment != null) playerEquipment.SheatheWeapon();
        }

        // Character의 Skill 애니메이션 Event가 이 시점에 이펙트와 광역 피해를 실행합니다.
        public void Skill()
        {
            if (!isSkillActive || skillEventApplied) return;
            skillEventApplied = true;

            PlaySkillEffectOnce();
            var targets = enemies.ToArray();
            foreach (var enemy in targets)
                DamageEnemy(enemy, activeSkill != null ? activeSkill.Damage : 1);
        }

        private void PlaySkillEffectOnce()
        {
            if (activeSkillIndex < 0 || activeSkillIndex >= skillEffects.Count) return;
            var effect = skillEffects[activeSkillIndex];
            if (effect == null) return;

            effect.SetActive(true);
            var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
            }
            StartCoroutine(HideSkillEffectWhenFinished(effect, particles));
        }

        private static IEnumerator HideSkillEffectWhenFinished(
            GameObject effect,
            ParticleSystem[] particles)
        {
            yield return null;
            while (effect != null && AreParticlesAlive(particles))
                yield return null;

            if (effect != null) effect.SetActive(false);
        }

        private static bool AreParticlesAlive(ParticleSystem[] particles)
        {
            foreach (var particle in particles)
            {
                if (particle != null && particle.IsAlive(true)) return true;
            }
            return false;
        }

        private void DamageEnemy(EnemyController target, int damage)
        {
            if (target == null || target.IsDead) return;
            if (damagePopupSystem != null)
                damagePopupSystem.Show(damage, GetHeadWorldPosition(target.gameObject));
            target.TakeDamage(damage);
            if (!target.IsDead) return;

            enemies.Remove(target);
            defeated++;
            enemyPool.Release(target);
            if (enemies.Count == 0)
            {
                if (activeEncounterIsBoss)
                    AdvanceStage();
                else
                    completedNormalRounds++;
                CreateDestination();
            }
        }

        private void AdvanceStage()
        {
            currentStage++;
            completedNormalRounds = 0;
            if (!stageData.TryGetRule(currentStage, out currentStageRule))
            {
                Debug.LogError($"Stage {currentStage}에 적용할 StageData 규칙이 없습니다.", this);
                enabled = false;
                return;
            }
            UpdateStageSlider(0f);
        }

        private static Vector3 GetHeadWorldPosition(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return target.transform.position + Vector3.up * 1.5f;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z);
        }

        private void CreateDestination()
        {
            if (destinationMarker != null) Destroy(destinationMarker.gameObject);
            var angle = Random.Range(0f, Mathf.PI * 2);
            destination = Flat(player != null ? player.position : Vector3.zero)
                + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * SpawnRadius;
            destination = ClampToWorld(destination);
            destination = GroundPoint(destination);
            travelDistance = Mathf.Max(.001f,
                Vector3.Distance(
                    Flat(player != null ? player.position : Vector3.zero),
                    Flat(destination)));
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Next Enemy Area";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = destination + Vector3.up * .045f;
            marker.transform.localScale = new Vector3(2.2f, .04f, 2.2f);
            marker.GetComponent<Renderer>().material = markerMaterial;
            destinationMarker = marker.transform;
        }

        private void SpawnWave()
        {
            wave++;
            activeEncounterIsBoss = completedNormalRounds >= currentStageRule.NormalRoundCount;
            if (destinationMarker != null) Destroy(destinationMarker.gameObject);
            var count = activeEncounterIsBoss ? 1 : Mathf.Min(4 + completedNormalRounds, 9);
            var stageMultiplier = Mathf.Pow(StageStatGrowth, currentStage - 1);
            var health = Mathf.Max(1, Mathf.CeilToInt(NormalEnemyBaseHealth * stageMultiplier));
            var damage = Mathf.Max(1, Mathf.CeilToInt(NormalEnemyBaseDamage * stageMultiplier));
            if (activeEncounterIsBoss)
            {
                health *= BossHealthMultiplier;
                damage *= BossDamageMultiplier;
            }
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2 / count + Random.Range(-.2f, .2f);
                var radius = Random.Range(2.7f, 4.4f);
                var position = destination + new Vector3(Mathf.Cos(angle) * radius, .65f, Mathf.Sin(angle) * radius);
                var enemyController = enemyPool.Get();
                var enemy = enemyController.gameObject;
                enemy.transform.SetParent(null, true);
                enemy.transform.position = position;
                PlaceCharacterOnGround(enemy, GroundPoint(position).y);
                if (enemyPrefab == null)
                {
                    Debug.LogWarning("Assets/01_Prefabs/Enemy.prefab을 찾지 못해 Sphere를 대신 사용합니다.");
                }
                enemy.name = activeEncounterIsBoss
                    ? $"Stage {currentStage} Boss"
                    : $"Stage {currentStage} Enemy {i + 1}";
                enemyController.Initialize(health, damage, player, this, terrain);
                enemies.Add(enemyController);
            }
            UpdateStageSlider(GetEncounterProgress());
            stateText = activeEncounterIsBoss
                ? "보스 등장"
                : $"일반 라운드 {completedNormalRounds + 1}";
        }

        private float GetEncounterProgress()
        {
            return currentStageRule == null
                ? 0f
                : (completedNormalRounds + 1f) / (currentStageRule.NormalRoundCount + 1f);
        }

        private void UpdateTravelProgress()
        {
            if (currentStageRule == null || player == null) return;

            var remainingDistance = Vector3.Distance(
                Flat(player.position),
                Flat(destination));
            var travelRatio = 1f - Mathf.Clamp01(remainingDistance / travelDistance);
            var encounterCount = currentStageRule.NormalRoundCount + 1f;
            var segmentStart = completedNormalRounds / encounterCount;

            UpdateStageSlider(segmentStart + travelRatio / encounterCount);
        }

        private void LateUpdate()
        {
            if (destinationMarker != null) destinationMarker.Rotate(0, 35 * Time.deltaTime, 0, Space.World);
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0, value.z);

        private Vector3 ClampToWorld(Vector3 value)
        {
            value.x = Mathf.Clamp(value.x, worldMin.x, worldMax.x);
            value.z = Mathf.Clamp(value.z, worldMin.y, worldMax.y);
            return value;
        }

        private Vector3 GroundPoint(Vector3 value)
        {
            if (terrain != null)
                value.y = terrain.SampleHeight(value) + terrain.transform.position.y;
            else
                value.y = 0f;
            return value;
        }

        public void TakePlayerDamage(int damage)
        {
            PlayerDataManager.Instance.TakeDamage(damage);
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (color.a < 1f)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            return material;
        }
    }

}
