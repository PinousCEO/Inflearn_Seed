using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using TMPro;
using IdleBattle.UI;
using Random = UnityEngine.Random;

namespace IdleBattle
{
    public sealed class BattleGameController : MonoBehaviour
    {
        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private Transform player, destinationMarker;
        private readonly List<GameObject> skillEffects = new List<GameObject>();
        // 사용 가능한 스킬 후보를 매번 새로 할당하지 않도록 재사용하는 버퍼.
        private readonly List<int> skillCandidates = new List<int>();
        private Animator playerAnimator;
        private CharacterEquipmentPresenter playerEquipment;
        private float playerGroundY;
        private Vector3 destination;
        private Material playerMaterial, enemyMaterial, markerMaterial;
        private GameObject enemyPrefab;
        private GameObject monsterZonePrefab;
        private GameObject monsterZoneInstance;
        private GameObject stunPrefab;
        private ObjectPool<EnemyController> enemyPool;
        private Transform enemyPoolRoot;
        private DamagePopupSystem damagePopupSystem;
        private ItemDropSystem itemDropSystem;
        private ItemAcquisitionFeed itemAcquisitionFeed;
        private CoinDropSystem coinDropSystem;
        private TargetNavigationUI targetNavigationUI;
        private LegendaryEquipmentSystem legendaryEquipmentSystem;
        private Terrain terrain;
        private Vector2 worldMin = new Vector2(-37f, -37f);
        private Vector2 worldMax = new Vector2(37f, 37f);
        private int wave, stageWave, defeated;
        private int currentStage = 1;
        private int completedNormalRounds;
        private bool activeEncounterIsBoss;
        private StageData stageData;
        private StageRule currentStageRule;
        private RectTransform stageSlider;
        private RectTransform stageSliderFill;
        private Image stageSliderFillImage;
        private float travelDistance;
        private float stageProgress;
        private int playerMana;
        private bool isSkillActive;
        private bool skillEventApplied;
        private float skillStartTime;
        private Coroutine skillRoutine;
        private float animatorSpeedBeforeSkill = 1f;
        private int nextSkillNum;
        private SkillData[] skills = Array.Empty<SkillData>();
        private float[] skillReadyTimes = Array.Empty<float>();
        private SkillData activeSkill;
        private int activeSkillIndex = -1;
        private string stateText = "지역 탐색 중";
        private TMP_Text stageText;
        private TMP_Text waveText;
        private TMP_Text monsterCountText;
        private TMP_Text levelText;
        private TMP_Text experienceText;
        private TMP_Text coinText;
        private Slider experienceSlider;
        private RectTransform experienceFill;
        private Image experienceFillImage;
        // Awake의 UI 셋업 동안에만 유지하는 씬 스캔 결과입니다.
        private TMP_Text[] sceneTexts = Array.Empty<TMP_Text>();
        private RectTransform[] sceneRects = Array.Empty<RectTransform>();

        private const float SpawnRadius = 14f;
        private const float MoveSpeed = 4.2f;
        private const float AttackRange = 2.1f;
        private const int SkillAnimationCount = 4;
        private const float SkillAnimationSpeed = 2.1f;
        private const float SkillEventFallbackNormalizedTime = .75f;
        // 스킬이 이 시간을 넘기면 비정상으로 보고 강제 종료해, T-Pose·멈춤 고착을 막습니다.
        private const float MaxSkillDuration = 6f;
        private const float StageStatGrowth = 1.1f;
        private const int NormalEnemyBaseHealth = 1000;
        private const int NormalEnemyBaseDamage = 20;
        private const int BossHealthMultiplier = 4;
        private const int BossDamageMultiplier = 2;

        private void Awake()
        {
            // UI가 씬 전체를 훑지 않고 전투 컨트롤러를 찾을 수 있게 등록합니다.
            SceneRefs.Register(this);
            LoadSkills();
            SetupWorld();
            SetupStage();
            // 아래 셋업은 씬의 UI 오브젝트를 이름으로 찾습니다.
            // 호출마다 씬을 훑지 않도록 한 번만 수집해서 공유하고, 끝나면 버립니다.
            BeginSceneLookup();
            SetupStageSlider();
            CreateDestination();
            SetupHud();
            EndSceneLookup();
        }

        private void BeginSceneLookup()
        {
            sceneTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sceneRects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void EndSceneLookup()
        {
            sceneTexts = Array.Empty<TMP_Text>();
            sceneRects = Array.Empty<RectTransform>();
        }

        public event Action<SkillData, int> SkillCast;
        public IReadOnlyList<SkillData> Skills => skills;

        /// <summary>
        /// 현재 전장에 살아 있는 몬스터를 buffer로 복사합니다.
        /// 살아 있는 목록은 이미 여기서 관리하므로, 다른 시스템이 FindObjectsByType으로
        /// 씬 전체를 다시 훑을 필요가 없습니다. 피해를 주면 원본 목록이 바뀌므로 항상 복사본을 순회합니다.
        /// </summary>
        public void CopyActiveEnemies(List<EnemyController> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            buffer.AddRange(enemies);
        }
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
            var stageRoot = FindSceneObject("Stage");
            var sliderObject = stageRoot != null ? FindChildByName(stageRoot.transform, "Slider") : null;
            if (sliderObject == null) return;

            stageSlider = sliderObject.GetComponent<RectTransform>();
            var fill = sliderObject.transform.Find("Fill");
            stageSliderFill = fill != null ? fill.GetComponent<RectTransform>() : null;
            if (stageSliderFill != null)
            {
                stageSliderFillImage = stageSliderFill.GetComponent<Image>();
                if (stageSliderFillImage != null)
                {
                    stageSliderFillImage.type = Image.Type.Filled;
                    stageSliderFillImage.fillMethod = Image.FillMethod.Horizontal;
                    stageSliderFillImage.fillOrigin = 0;
                    stageSliderFillImage.fillAmount = 0f;
                    stageSliderFillImage.raycastTarget = false;
                }
            }

            var oldHorizontal = sliderObject.transform.Find("Horizontal");
            if (oldHorizontal != null)
                oldHorizontal.gameObject.SetActive(false);

            UpdateStageSlider(0f);
        }

        private void UpdateStageSlider(float progress)
        {
            stageProgress = Mathf.Clamp01(progress);
            if (stageSliderFillImage != null)
                stageSliderFillImage.fillAmount = stageProgress;
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
            monsterZonePrefab = LoadPrefab("01_Prefabs/Effects/Monsterzone", "Assets/01_Prefabs/Effects/Monsterzone.prefab");
            stunPrefab = LoadPrefab("01_Prefabs/Effects/Stun", "Assets/01_Prefabs/Effects/Stun.prefab");
            CreateEnemyPool();
            damagePopupSystem = GetComponent<DamagePopupSystem>();
            if (damagePopupSystem == null)
                damagePopupSystem = gameObject.AddComponent<DamagePopupSystem>();
            damagePopupSystem.Initialize(Camera.main);
            itemDropSystem = GetComponent<ItemDropSystem>();
            if (itemDropSystem == null)
                itemDropSystem = gameObject.AddComponent<ItemDropSystem>();
            itemDropSystem.Initialize(Camera.main);
            itemAcquisitionFeed = GetComponent<ItemAcquisitionFeed>();
            if (itemAcquisitionFeed == null)
                itemAcquisitionFeed = gameObject.AddComponent<ItemAcquisitionFeed>();
            itemAcquisitionFeed.Initialize();

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
            legendaryEquipmentSystem = LegendaryEquipmentSystem.Instance;
            legendaryEquipmentSystem.Initialize(this, player);
            coinDropSystem = GetComponent<CoinDropSystem>();
            if (coinDropSystem == null) coinDropSystem = gameObject.AddComponent<CoinDropSystem>();
            coinDropSystem.Initialize(player);
            var canvas = SceneRefs.RootCanvas;
            if (canvas != null)
            {
                targetNavigationUI = canvas.GetComponent<TargetNavigationUI>();
                if (targetNavigationUI == null) targetNavigationUI = canvas.gameObject.AddComponent<TargetNavigationUI>();
                targetNavigationUI.Initialize(Camera.main, player);
            }
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
                playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                playerAnimator.updateMode = AnimatorUpdateMode.Normal;
                playerAnimator.enabled = true;
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
                // 스킬 코루틴이 어떤 이유로든 끝내지 못하고 오래 고착되면(T-Pose·멈춤) 강제로 되돌린다.
                if (Time.time - skillStartTime > MaxSkillDuration)
                {
                    Debug.LogWarning("스킬이 비정상적으로 오래 지속되어 강제 종료합니다.", this);
                    if (skillRoutine != null) StopCoroutine(skillRoutine);
                    skillRoutine = null;
                    FinishSkill();
                }
                else
                {
                    SetRunning(false);
                    return;
                }
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
            var candidates = skillCandidates;
            candidates.Clear();
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
            skillReadyTimes[index] = Time.time + skill.Cooldown * .55f;
            activeSkill = skill;
            activeSkillIndex = index;
            SkillCast?.Invoke(skill, index);
            skillRoutine = StartCoroutine(ActivateSkill());
            return true;
        }

        private IEnumerator ActivateSkill()
        {
            isSkillActive = true;
            skillEventApplied = false;
            skillStartTime = Time.time;

            // try/finally로 감싸, 중간에 어떤 예외(스킬 데미지·이펙트 등)가 나더라도
            // 반드시 FinishSkill()이 실행되어 isSkillActive가 풀리고 CombatIdle로 복귀합니다.
            // 이 복구가 빠지면 캐릭터가 T-Pose/멈춤 상태로 고착됩니다.
            try
            {
                SetRunning(false);
                if (playerEquipment != null) playerEquipment.DrawWeapon();

                if (playerAnimator == null)
                {
                    SafeSkill();
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
                        SafeSkill();

                    if (state.fullPathHash != skillState ||
                        (!playerAnimator.IsInTransition(0) && state.normalizedTime >= .98f))
                        break;
                    yield return null;
                }

                if (!skillEventApplied)
                    SafeSkill();
            }
            finally
            {
                FinishSkill();
            }
        }

        /// <summary>스킬 피해/이펙트 처리에서 예외가 나도 전투 흐름이 끊기지 않도록 감쌉니다.</summary>
        private void SafeSkill()
        {
            try
            {
                Skill();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void FinishSkill()
        {
            if (playerAnimator != null)
            {
                playerAnimator.speed = animatorSpeedBeforeSkill;
                playerAnimator.SetBool("Run", false);
                // Skill 전환이 끊겼을 때 바인드 포즈(T-Pose)에 남지 않도록
                // 항상 유효한 기본 전투 대기 상태로 복귀시킵니다.
                playerAnimator.Play("CombatIdle", 0, 0f);
                playerAnimator.Update(0f);
            }
            isSkillActive = false;
            activeSkill = null;
            activeSkillIndex = -1;
            if (playerEquipment != null) playerEquipment.SheatheWeapon();
        }

        // Character의 Skill 애니메이션 Event가 이 시점에 스킬별 범위 피해를 실행합니다.
        public void Skill()
        {
            if (!isSkillActive || skillEventApplied) return;
            skillEventApplied = true;

            PlaySkillEffectOnce();
            var targets = SelectSkillTargets().ToArray();
            foreach (var enemy in targets)
            {
                var damage = activeSkill != null ? activeSkill.Damage : 1;
                DamageEnemy(enemy, damage);
                if ((activeSkillIndex == 2 || activeSkillIndex == 3) && enemy != null && !enemy.IsDead)
                    ShowStun(enemy);
            }
            legendaryEquipmentSystem?.OnSkill(activeSkillIndex, targets, DamageEnemy);
        }

        private void ShowStun(EnemyController target)
        {
            if (stunPrefab == null || target == null || target.IsDead) return;

            var stun = Instantiate(stunPrefab);
            stun.name = "Stun_Active";
            stun.transform.position = GetHeadWorldPosition(target.gameObject);
            stun.transform.SetParent(target.transform, true);
            // 같은 몬스터에 이미 표시 중이면 겹치지 않게 이전 것을 정리합니다.
            // 몬스터가 자기 이펙트를 들고 있으므로 자식 계층을 이름으로 뒤지지 않습니다.
            target.SetStunEffect(stun);

            var particles = stun.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
            }
            StartCoroutine(RemoveStunAfter(target, stun, 1.4f));
            legendaryEquipmentSystem?.OnStun(target);
        }

        private static IEnumerator RemoveStunAfter(EnemyController target, GameObject stun, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (target != null) target.ClearStunEffect(stun);
            if (stun != null) Destroy(stun);
        }

        private IEnumerable<EnemyController> SelectSkillTargets()
        {
            var snapshot = enemies.ToArray();
            if (activeSkillIndex == 2)
            {
                // 3번: 가장 가까운 한 마리만 타격합니다.
                EnemyController nearest = null;
                var nearestDistance = float.MaxValue;
                foreach (var enemy in snapshot)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    var distance = (enemy.transform.position - player.position).sqrMagnitude;
                    if (distance < nearestDistance) { nearestDistance = distance; nearest = enemy; }
                }
                if (nearest != null) yield return nearest;
                yield break;
            }

            var radius = activeSkillIndex == 0 ? 5.5f : 6.5f;
            var directional = activeSkillIndex == 1 || activeSkillIndex == 3;
            var forward = Flat(player != null ? player.forward : Vector3.forward).normalized;
            foreach (var enemy in snapshot)
            {
                if (enemy == null || enemy.IsDead) continue;
                var offset = Flat(enemy.transform.position - player.position);
                if (offset.sqrMagnitude > radius * radius) continue;
                if (directional && Vector3.Dot(forward, offset.normalized) < .35f) continue;
                yield return enemy;
            }
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

            var dropPosition = GroundPoint(target.transform.position);
            enemies.Remove(target);
            defeated++;
            var rewardMultiplier = activeEncounterIsBoss ? currentStageRule.BossRewardMultiplier : 1f;
            var experienceReward = Mathf.RoundToInt(currentStageRule.ExperiencePerMonster * rewardMultiplier);
            var coinReward = Mathf.RoundToInt(currentStageRule.CoinPerMonster * rewardMultiplier);
            PlayerDataManager.Instance.AddExperience(experienceReward);
            if (coinDropSystem != null) coinDropSystem.Drop(dropPosition, coinReward);
            if (itemDropSystem != null)
                itemDropSystem.RollDrops(dropPosition);
            enemyPool.Release(target);
            UpdateBattleHud();
            if (enemies.Count == 0)
            {
                if (activeEncounterIsBoss)
                    AdvanceStage();
                else
                    completedNormalRounds++;
                CreateDestination();
            }
        }

        public void DamageFromEquipment(EnemyController target, int damage)
        {
            DamageEnemy(target, damage);
        }

        private void AdvanceStage()
        {
            currentStage++;
            completedNormalRounds = 0;
            stageWave = 0;
            if (!stageData.TryGetRule(currentStage, out currentStageRule))
            {
                Debug.LogError($"Stage {currentStage}에 적용할 StageData 규칙이 없습니다.", this);
                enabled = false;
                return;
            }
            UpdateStageSlider(0f);
            UpdateBattleHud();
        }

        private static Vector3 GetHeadWorldPosition(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            Renderer first = null;
            for (var i = 0; i < renderers.Length; i++)
            {
                // Stun 이펙트 자체의 Renderer를 머리 높이 계산에서 제외합니다.
                // 그렇지 않으면 재생성할 때 이전 이펙트 높이만큼 계속 위로 누적됩니다.
                var current = renderers[i].transform;
                var isStunVisual = false;
                while (current != null && current != target.transform)
                {
                    if (current.name == "Stun_Active") { isStunVisual = true; break; }
                    current = current.parent;
                }
                if (!isStunVisual) { first = renderers[i]; break; }
            }

            if (first == null)
                return target.transform.position + Vector3.up * 1.5f;

            var bounds = first.bounds;
            for (var i = 0; i < renderers.Length; i++)
            {
                var current = renderers[i].transform;
                var isStunVisual = false;
                while (current != null && current != target.transform)
                {
                    if (current.name == "Stun_Active") { isStunVisual = true; break; }
                    current = current.parent;
                }
                if (!isStunVisual && renderers[i] != first)
                    bounds.Encapsulate(renderers[i].bounds);
            }

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
            // 목표 지점은 월드 오브젝트를 만들지 않고 화면 가장자리 네비게이션으로 표시합니다.
            destinationMarker = null;
            if (targetNavigationUI != null) targetNavigationUI.SetTarget(destination);
            SpawnMonsterZone();
        }

        private void SpawnWave()
        {
            wave++;
            stageWave++;
            activeEncounterIsBoss = completedNormalRounds >= currentStageRule.NormalRoundCount;
            if (destinationMarker != null) Destroy(destinationMarker.gameObject);
            if (monsterZoneInstance != null)
            {
                Destroy(monsterZoneInstance);
                monsterZoneInstance = null;
            }
            var count = activeEncounterIsBoss
                ? 1
                : Mathf.Min(currentStageRule.BaseMonsterCount + completedNormalRounds, currentStageRule.MaxMonsterCount);
            var stageMultiplier = Mathf.Pow(StageStatGrowth, currentStage - 1);
            var health = Mathf.Max(1, Mathf.CeilToInt(NormalEnemyBaseHealth * stageMultiplier * currentStageRule.HealthMultiplier));
            var damage = Mathf.Max(1, Mathf.CeilToInt(NormalEnemyBaseDamage * stageMultiplier * currentStageRule.DamageMultiplier));
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
                var targetScale = enemy.transform.localScale;
                enemy.transform.localScale = Vector3.zero;
                StartCoroutine(AnimateEnemySpawn(enemy.transform, targetScale, i * .08f));
                enemies.Add(enemyController);
            }
            UpdateStageSlider(GetEncounterProgress());
            stateText = activeEncounterIsBoss
                ? "보스 등장"
                : $"일반 라운드 {completedNormalRounds + 1}";
            UpdateBattleHud();
        }

        private void SpawnMonsterZone()
        {
            if (monsterZoneInstance != null) Destroy(monsterZoneInstance);
            if (monsterZonePrefab == null) return;
            monsterZoneInstance = Instantiate(monsterZonePrefab, destination + Vector3.up * .03f, Quaternion.identity);
            monsterZoneInstance.name = "Monsterzone_Active";
        }

        private IEnumerator AnimateEnemySpawn(Transform enemy, Vector3 targetScale, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (enemy == null) yield break;
            const float duration = .42f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (enemy == null) yield break;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                enemy.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, eased);
                yield return null;
            }
            if (enemy != null) enemy.localScale = targetScale;
        }

        private void SetupHud()
        {
            waveText = FindSceneText("Wave");
            monsterCountText = FindSceneText("MonsterCount");
            experienceText = FindSceneText("EXP");
            stageText = FindSceneTextByValue("지옥");

            // 레벨/EXP 슬라이더는 EXP 텍스트와 같은 프로필 패널 안의 것을 사용합니다.
            var profileRoot = experienceText != null ? experienceText.transform.parent : null;
            levelText = profileRoot != null ? FindTextByName(profileRoot, "LevelTxT") : FindSceneText("LevelTxT");
            var goldRoot = FindSceneObject("Gold");
            coinText = goldRoot != null ? goldRoot.GetComponentInChildren<TMP_Text>(true) : null;
            var expRoot = profileRoot != null ? FindChildByName(profileRoot, "EXP_Slider") : FindSceneObject("EXP_Slider");
            if (expRoot != null)
            {
                experienceSlider = expRoot.GetComponent<Slider>();
                experienceFillImage = expRoot.GetComponentInChildren<Image>(true);
                foreach (var image in expRoot.GetComponentsInChildren<Image>(true))
                    if (image.transform != expRoot.transform) { experienceFillImage = image; break; }
                experienceFill = experienceFillImage != null ? experienceFillImage.rectTransform : null;
            }

            var data = PlayerDataManager.Instance;
            data.ExperienceChanged += OnExperienceChanged;
            data.CoinsChanged += OnCoinsChanged;
            UpdateBattleHud();
        }

        private void OnDestroy()
        {
            if (PlayerDataManager.Instance == null) return;
            PlayerDataManager.Instance.ExperienceChanged -= OnExperienceChanged;
            PlayerDataManager.Instance.CoinsChanged -= OnCoinsChanged;
        }

        private void OnExperienceChanged(int level, int experience, int required) => UpdatePlayerHud();
        private void OnCoinsChanged(long value) => UpdatePlayerHud();

        private void UpdateBattleHud()
        {
            if (currentStageRule == null) return;
            var totalWaves = currentStageRule.NormalRoundCount + 1;
            // 이동 중에는 enemies.Count가 0이 되므로 completedNormalRounds로 계산하면
            // 화면의 웨이브가 2→1처럼 되돌아갑니다. 스폰 시 증가한 stageWave만 표시합니다.
            var shownWave = Mathf.Clamp(Mathf.Max(1, stageWave), 1, totalWaves);
            if (stageText != null) stageText.text = GetStageDisplayName(currentStage);
            if (waveText != null) waveText.text = $"Wave <color=#D9CA57>{shownWave}/{totalWaves}";
            if (monsterCountText != null)
                monsterCountText.text = $"남은 몬스터 <color=#D9CA57>{enemies.Count}";
            UpdatePlayerHud();
        }

        private void UpdatePlayerHud()
        {
            var data = PlayerDataManager.Instance;
            var required = data.ExperienceToNextLevel;
            var ratio = required > 0 ? data.Experience / (float)required : 0f;
            if (levelText != null) levelText.text = $"Lv.{data.Level}";
            if (experienceText != null) experienceText.text = $"EXP {ratio * 100f:0.0}%";
            if (coinText != null) coinText.text = data.Coins.ToString("N0");
            if (experienceSlider != null)
            {
                experienceSlider.minValue = 0f;
                experienceSlider.maxValue = 1f;
                experienceSlider.value = ratio;
            }
            else if (experienceFill != null)
            {
                if (experienceFillImage != null)
                {
                    experienceFillImage.type = Image.Type.Filled;
                    experienceFillImage.fillMethod = Image.FillMethod.Horizontal;
                    experienceFillImage.fillOrigin = 0;
                    experienceFillImage.fillAmount = Mathf.Clamp01(ratio);
                }
            }
        }

        private static string GetStageDisplayName(int stage)
        {
            var chapter = (Mathf.Max(1, stage) - 1) / 10 + 1;
            var section = (Mathf.Max(1, stage) - 1) % 10 + 1;
            return $"지옥 {chapter}-{section}";
        }

        private static TMP_Text FindTextByName(Transform root, string objectName)
        {
            var child = FindChildByName(root, objectName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static TMP_Text FindTextByInitialValue(Transform root, string prefix)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.text != null && text.text.StartsWith(prefix, StringComparison.Ordinal)) return text;
            return null;
        }

        // 아래 세 메서드는 BeginSceneLookup으로 미리 모아 둔 배열만 훑습니다.
        // 씬 스캔은 Awake에서 종류별로 한 번씩만 일어납니다.
        private TMP_Text FindSceneText(string objectName)
        {
            TMP_Text fallback = null;
            foreach (var text in sceneTexts)
            {
                if (text == null || text.name != objectName || !text.gameObject.scene.IsValid()) continue;
                if (text.gameObject.activeInHierarchy) return text;
                fallback ??= text;
            }
            return fallback;
        }

        private TMP_Text FindSceneTextByValue(string prefix)
        {
            TMP_Text fallback = null;
            foreach (var text in sceneTexts)
            {
                if (text == null || !text.gameObject.scene.IsValid() || text.text == null ||
                    !text.text.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (text.gameObject.activeInHierarchy) return text;
                fallback ??= text;
            }
            return fallback;
        }

        private GameObject FindSceneObject(string objectName)
        {
            GameObject fallback = null;
            foreach (var rect in sceneRects)
            {
                if (rect == null || rect.name != objectName || !rect.gameObject.scene.IsValid()) continue;
                if (rect.gameObject.activeInHierarchy) return rect.gameObject;
                fallback ??= rect.gameObject;
            }
            return fallback;
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
            var material = new Material(RuntimeShaders.Lit) { color = color };
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
