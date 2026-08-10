using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using TMPro;
using IdleBattle.Audio;
using IdleBattle.UI;
using Random = UnityEngine.Random;

namespace IdleBattle
{
    public sealed class BattleGameController : MonoBehaviour
    {
        private readonly List<EnemyController> enemies = new List<EnemyController>();
        // 지금 사냥 중인 몬스터. 죽기 전까지 바꾸지 않습니다(AcquireTarget 참고).
        private EnemyController currentTarget;
        private Transform player, destinationMarker;
        private readonly List<GameObject> skillEffects = new List<GameObject>();
        private Animator playerAnimator;
        private CharacterEquipmentPresenter playerEquipment;
        private float playerGroundY;
        private Vector3 destination;
        private Material playerMaterial, enemyMaterial, markerMaterial;
        private GameObject enemyPrefab;
        private GameObject monsterZonePrefab;
        private GameObject monsterZoneInstance;
        private GameObject stunPrefab;
        private GameObject hitPrefab;
        // 5번 스킬은 Character/Skill0에 VFX가 없어 이 프리팹을 그 자리에 띄웁니다.
        private GameObject rowPrefab;
        private PlayerCameraController playerCameraController;
        // 1번/3번 스킬에서 쓰는 약한 카메라 흔들림.
        private const float SkillShakeStrength = 0.16f;
        private const float SkillShakeDuration = 0.2f;
        // 치명타는 스킬보다 짧고 날카롭게 한 번 칩니다.
        private const float CriticalShakeStrength = 0.13f;
        private const float CriticalShakeDuration = 0.14f;
        private CriticalProfile criticalProfile;
        // 장비까지 반영한 초당 마나 재생량과, 아직 넘겨주지 못한 소수점 이하입니다.
        private float manaRegenerationPerSecond;
        private float manaRegenerationCarry;
        private ItemCatalog itemCatalog;
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
        private Coroutine reviveRoutine;
        private float animatorSpeedBeforeSkill = 1f;
        private float nextFootstepTime;
        private int nextSkillNum;
        private SkillData[] skills = Array.Empty<SkillData>();
        private float[] skillReadyTimes = Array.Empty<float>();
        private float nextSkillAllowedTime;
        private SkillData activeSkill;
        private int activeSkillIndex = -1;
        private string stateText = string.Empty;
        private TMP_Text stageText;
        private TMP_Text waveText;
        private TMP_Text monsterCountText;
        private TMP_Text levelText;
        private TMP_Text playerNameText;
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
        private const int SkillAnimationCount = 5;
        /// <summary>
        /// 마무리 기술(5번, Character 애니메이터의 Skill5)의 번호입니다.
        /// 다른 스킬과 달리 사거리를 보지 않고 살아 있는 모든 몬스터를 칩니다.
        /// </summary>
        private const int UltimateAnimationIndex = 4;
        /// <summary>마무리 기술이 몬스터를 때리는 횟수와 그 간격입니다.</summary>
        private const int UltimateHitCount = 5;
        private const float UltimateHitInterval = .16f;
        private const float SkillAnimationSpeed = 2.1f;
        private const float SkillEventFallbackNormalizedTime = .75f;
        // 스킬이 이 시간을 넘기면 비정상으로 보고 강제 종료해, 멈춤 고착을 막습니다.
        private const float MaxSkillDuration = 6f;
        /// <summary>
        /// 스킬 클립의 마무리 동작을 이만큼 재생했으면, 다음 스킬로 넘어가도 되는 시점입니다.
        ///
        /// 스킬 클립은 타격 뒤에도 자세를 되돌리는 뒤태가 길게 남아 있습니다(1-1은 클립의 60%).
        /// 쓸 수 있는 스킬이 이미 준비돼 있는데 그 뒤태를 끝까지 기다리면 가만히 서 있는 것처럼
        /// 보이므로, 준비된 스킬이 있을 때만 뒤태를 잘라내고 곧바로 이어 갑니다.
        /// 준비된 스킬이 없으면 자르지 않고 끝까지 재생합니다. 그래야 동작이 뚝 끊기지 않습니다.
        /// </summary>
        private const float SkillChainNormalizedTime = .82f;
        /// <summary>
        /// 전투 시작 직후 스킬이 한꺼번에 준비되지 않도록, 스킬마다 조금씩 늦춰 시작합니다.
        /// 쿨타임(1.1·2·2.5·3·8초)이 서로 어긋난 채로 돌기 시작해 한 곳에 몰리지 않습니다.
        /// </summary>
        private const float SkillStartStagger = 1.2f;
        // 쓰러진 뒤 다시 일어나기까지의 시간입니다.
        private const float ReviveDelay = 2f;
        // 달리는 동안 발소리를 내는 간격입니다.
        private const float FootstepInterval = .33f;
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
            PrewarmBattleAudio();
        }

        /// <summary>
        /// 효과음은 처음 재생할 때 합성되므로, 전투가 시작된 뒤 첫 타격에서 한 프레임 튈 수 있습니다.
        /// 전투에서 확실히 쓰이는 것들만 미리 만들어 둡니다.
        /// </summary>
        private void PrewarmBattleAudio()
        {
            AudioManager.Prewarm(
                SfxId.EnemyHit, SfxId.EnemyCritical, SfxId.EnemyDeath, SfxId.EnemyAttack,
                SfxId.Footstep, SfxId.CoinDrop, SfxId.CoinPickup, SfxId.LootPickup,
                SfxId.LootCommon, SfxId.WeaponDraw, SfxId.WeaponSheathe,
                SfxId.SkillCleave, SfxId.SkillGroundSmash, SfxId.SkillLeapCrush, SfxId.SkillEarthshatter,
                SfxId.WaveStart, SfxId.EnemyStun, SfxId.PotionHeal, SfxId.PotionMana);
        }

        /// <summary>
        /// 스킬 효과음은 <see cref="SkillData.AnimationIndex"/>(0~4)로 고릅니다.
        /// 그 번호가 곧 재생되는 애니메이션이자 Character/Skill0의 이펙트 번호(1-1~1-4)이므로,
        /// 스킬 목록의 순서가 바뀌어도 화면에 보이는 동작과 소리가 항상 함께 갑니다.
        /// </summary>
        private static SfxId GetSkillSound(SkillData skill)
        {
            switch (skill != null ? skill.AnimationIndex : 0)
            {
                case 1: return SfxId.SkillGroundSmash;
                case 2: return SfxId.SkillLeapCrush;
                case 3: return SfxId.SkillEarthshatter;
                case 4: return SfxId.SkillBattleRoar;
                default: return SfxId.SkillCleave;
            }
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
            for (var i = 0; i < skillReadyTimes.Length; i++)
                skillReadyTimes[i] = Time.time + i * SkillStartStagger;
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
            hitPrefab = LoadPrefab("01_Prefabs/Effects/Hit", "Assets/01_Prefabs/Effects/Hit.prefab");
            rowPrefab = LoadPrefab("01_Prefabs/Effects/Row", "Assets/01_Prefabs/Effects/Row.prefab");
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
            playerCameraController = follow;
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

            // 스킬 수와 VFX 수가 달라도 스킬을 잘라내지 않습니다.
            // 어떤 VFX를 쓸지는 SkillData.AnimationIndex가 이미 정하고 있으므로,
            // 그 번호로 골라 쓰면 VFX 하나를 여러 스킬이 나눠 쓸 수 있습니다.
            // (예전처럼 잘라내면 하단 스킬 칸 하나가 영영 비어 쿨타임도 돌지 않았습니다)
            //
            // 마무리 기술(AnimationIndex 4)만은 Skill0이 아니라 Row 프리팹을 그 자리에 띄우므로
            // 여기서 세지 않습니다. Skill0에는 1-1~1-4 네 개만 있으면 됩니다.
            if (skillEffects.Count < UltimateAnimationIndex)
                Debug.Log(
                    $"Character/Skill0 자식이 {skillEffects.Count}개입니다({UltimateAnimationIndex}개 필요). " +
                    "AnimationIndex가 같은 스킬끼리 같은 이펙트를 함께 씁니다.",
                    this);
        }

        private void Update()
        {
            enemies.RemoveAll(enemy => enemy == null);
            // 스킬을 쓰는 도중에도 계속 차야 합니다. 아래에서 일찍 return하므로 맨 앞에 둡니다.
            RegenerateMana();
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
                    // 스킬을 쓰는 도중에도 사냥 중인 몬스터 쪽으로 몸을 돌립니다.
                    //
                    // 스킬이 끊김 없이 이어지면서 이 구간이 거의 전부가 되었습니다.
                    // 여기서 그냥 return해 버리면, 때리던 몬스터가 죽어도 그 빈자리를
                    // 계속 바라본 채 허공에 스킬을 씁니다. 방향만은 매 프레임 갱신합니다.
                    // (자리를 옮기는 것은 동작이 미끄러져 보이므로 스킬이 끝난 뒤에 합니다)
                    var facing = AcquireTarget();
                    if (facing != null) Face(facing.transform.position);
                    return;
                }
            }
            if (enemies.Count == 0)
            {
                stateText = Localization.Get("battle.state.moving");
                MoveTowards(destination);
                UpdateTravelProgress();
                if (Vector3.Distance(Flat(player.position), Flat(destination)) < .65f) SpawnWave();
                return;
            }

            var target = AcquireTarget();
            if (target == null) return;
            var distance = Vector3.Distance(Flat(player.position), Flat(target.transform.position));
            if (distance > AttackRange)
            {
                stateText = Localization.Get("battle.state.chasing");
                MoveTowards(target.transform.position);
            }
            else
            {
                stateText = Localization.Get("battle.state.skill_wait");
                SetRunning(false);
                Face(target.transform.position);
                TryActivateSkill();
            }
        }

        /// <summary>
        /// 지금 사냥 중인 몬스터입니다. <b>죽기 전까지는 바꾸지 않습니다.</b>
        ///
        /// 매 프레임 "가장 가까운 몬스터"를 다시 고르면, 몬스터들이 서로 위치를 바꿀 때마다
        /// 대상이 휙휙 바뀌어 캐릭터가 계속 두리번거립니다. 한 마리를 끝까지 잡고,
        /// 그 몬스터가 죽었을 때만 다음 대상을 찾습니다.
        /// </summary>
        private EnemyController AcquireTarget()
        {
            // 쫓던 몬스터가 아직 살아서 이번 웨이브에 남아 있으면 그대로 둡니다.
            // (죽은 몬스터는 DamageEnemy가 enemies에서 빼고 풀로 돌려보냅니다)
            if (currentTarget != null && !currentTarget.IsDead && enemies.Contains(currentTarget))
                return currentTarget;

            currentTarget = FindNearestEnemy();
            return currentTarget;
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

            // 달리는 동안에만 일정 간격으로 발소리를 냅니다.
            if (Time.time >= nextFootstepTime)
            {
                nextFootstepTime = Time.time + FootstepInterval;
                AudioManager.PlayAt(SfxId.Footstep, player.position);
            }
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

        /// <summary>지금 당장 쓸 수 있는 스킬이 하나라도 있는지만 봅니다(마나는 쓰지 않습니다).</summary>
        private bool HasSkillReadyNow()
        {
            if (skills.Length == 0) return false;
            var playerData = PlayerDataManager.Instance;
            for (var i = 0; i < skills.Length; i++)
                if (skills[i] != null &&
                    Time.time >= skillReadyTimes[i] &&
                    playerData.CurrentMana >= Mathf.CeilToInt(skills[i].ResourceCost))
                    return true;
            return false;
        }

        /// <summary>
        /// 쓸 수 있는 스킬 중 <b>가장 센 것</b>을 고릅니다.
        ///
        /// <see cref="skills"/>는 SkillId 순으로 정렬되어 있고 skill_01이 기본기,
        /// skill_05가 마무리 기술이므로 <b>번호가 클수록 센 스킬</b>입니다.
        /// 쿨타임이 아니라 이 번호로 고르는 이유는, 쿨타임 값을 어떻게 조정하더라도
        /// "5번이 제일 세다"는 사실이 흔들리지 않게 하기 위해서입니다.
        ///
        /// 예전에는 쓸 수 있는 것 중 아무거나 골랐습니다. 그러면 쿨타임이 짧은 1번이
        /// 거의 항상 후보에 있으므로, 2~5번이 준비를 마치고도 동전 던지기에서 밀려 계속 놀았습니다.
        /// 센 것부터 쓰면 1번은 자연히 <b>빈틈을 메우는 역할만</b> 맡습니다.
        /// </summary>
        private bool TryActivateSkill()
        {
            if (isSkillActive || skills.Length == 0 || skillEffects.Count == 0) return false;
            // 마무리 기술은 타격이 여러 번 나뉘어 들어가므로, 그 동안만 다음 스킬을 미룹니다.
            if (Time.time < nextSkillAllowedTime) return false;
            var playerData = PlayerDataManager.Instance;
            var index = -1;
            for (var i = skills.Length - 1; i >= 0; i--)
                if (skills[i] != null &&
                    Time.time >= skillReadyTimes[i] &&
                    playerData.CurrentMana >= Mathf.CeilToInt(skills[i].ResourceCost))
                {
                    index = i;
                    break;
                }
            if (index < 0) return false;
            var skill = skills[index];
            if (!playerData.TrySpendMana(Mathf.CeilToInt(skill.ResourceCost))) return false;
            // 쿨타임은 SkillData에 적힌 값을 그대로 씁니다(1.1·2·2.5·3·8초).
            //
            // 1번의 1.1초는 자기 애니메이션 길이(2.4초 ÷ 2.1배속 = 1.14초)보다 짧습니다.
            // 그래서 1번을 쓰고 나면 그 동작이 끝나기 전에 1번이 이미 다시 준비됩니다.
            // 다른 스킬이 전부 쿨타임이어도 1번 혼자 빈틈을 메우므로, 손이 비는 순간이 없습니다.
            // 1번의 쿨타임을 이 1.14초보다 길게 잡으면 그 보장이 깨져 손이 비게 됩니다.
            skillReadyTimes[index] = Time.time + skill.Cooldown;
            activeSkill = skill;
            activeSkillIndex = index;
            SkillCast?.Invoke(skill, index);
            // 시전 순간에 울려야 애니메이션의 도약과 소리가 맞습니다.
            AudioManager.PlayAt(GetSkillSound(skill), player != null ? player.position : Vector3.zero);
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

                    // 타격은 이미 들어갔고 다음 스킬도 준비돼 있으면, 남은 마무리 동작을
                    // 끝까지 기다리지 않고 곧바로 넘깁니다. 이 뒤태를 다 보는 동안이
                    // 가만히 서 있는 것처럼 보이던 구간입니다.
                    if (skillEventApplied &&
                        !playerAnimator.IsInTransition(0) &&
                        state.normalizedTime >= SkillChainNormalizedTime &&
                        HasSkillReadyNow())
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
                // Num이 어떤 상태와도 맞지 않으면 Skill 트리거가 소비되지 않고 남습니다.
                // 그대로 두면 CombatIdle로 되돌린 직후 엉뚱하게 다시 스킬 상태로 튑니다.
                playerAnimator.ResetTrigger("Skill");
                // Skill 전환이 끊겼을 때를 대비해 항상 유효한 기본 전투 대기로 복귀시킵니다.
                // Animator.Update(0)로 직접 평가하면 엔진의 자동 갱신과 겹쳐
                // 그 프레임의 포즈가 기록되지 않을 수 있어 부르지 않습니다.
                playerAnimator.Play("CombatIdle", 0, 0f);
            }
            isSkillActive = false;
            activeSkill = null;
            activeSkillIndex = -1;
            // 여기서 다음 스킬을 막지 않습니다. 준비된 스킬이 있으면 다음 프레임에 바로 이어 갑니다.
            // 무기는 싸움이 끝났을 때만 넣습니다. 스킬마다 넣었다 빼면 이어 쓸 때
            // 무기가 사라졌다 나타나고 뽑는 소리도 계속 겹쳐 납니다.
            if (playerEquipment != null && enemies.Count == 0) playerEquipment.SheatheWeapon();
        }

        // Character의 Skill 애니메이션 Event가 이 시점에 스킬별 범위 피해를 실행합니다.
        public void Skill()
        {
            if (!isSkillActive || skillEventApplied) return;
            skillEventApplied = true;

            if (IsUltimate(activeSkill))
            {
                CastUltimate();
                return;
            }

            PlaySkillEffectOnce();
            var targets = SelectSkillTargets().ToArray();
            // 1번/3번 스킬은 맞은 몬스터를 플레이어 반대쪽으로 살짝 밀어내고 화면을 약하게 흔듭니다.
            var knockbackSkill = activeSkillIndex == 0 || activeSkillIndex == 2;
            var knockbackOrigin = player != null ? player.position : transform.position;
            if (knockbackSkill && playerCameraController != null)
                playerCameraController.Shake(SkillShakeStrength, SkillShakeDuration);
            foreach (var enemy in targets)
            {
                var damage = activeSkill != null ? activeSkill.Damage : 1;
                DamageEnemy(enemy, damage);
                if (enemy == null || enemy.IsDead) continue;
                if (knockbackSkill) enemy.ApplyKnockback(knockbackOrigin);
                if (activeSkillIndex == 2 || activeSkillIndex == 3)
                    ShowStun(enemy);
            }
            legendaryEquipmentSystem?.OnSkill(activeSkillIndex, targets, DamageEnemy);
        }

        private static bool IsUltimate(SkillData skill)
        {
            return skill != null && skill.AnimationIndex == UltimateAnimationIndex;
        }

        /// <summary>
        /// 마무리 기술입니다. 발밑에 Row 충격파를 띄우고, 사거리와 상관없이
        /// 살아 있는 모든 몬스터를 <see cref="UltimateHitCount"/>번 나눠서 칩니다.
        /// </summary>
        private void CastUltimate()
        {
            var origin = player != null ? player.position : transform.position;
            SpawnRowEffect(GroundPoint(origin));
            // 다섯 번을 다 치기 전에 다음 스킬이 들어가면 남은 타격이 겹쳐 보입니다.
            // 여기서만 다음 스킬을 잠깐 미룹니다.
            nextSkillAllowedTime = Time.time + UltimateHitCount * UltimateHitInterval;
            // 피해가 여러 번 들어가므로 타격마다 흔드는 것은 코루틴이 맡습니다.
            StartCoroutine(UltimateStrikes(activeSkill != null ? activeSkill.Damage : 1));
        }

        private IEnumerator UltimateStrikes(int damage)
        {
            for (var hit = 0; hit < UltimateHitCount; hit++)
            {
                if (hit > 0) yield return new WaitForSeconds(UltimateHitInterval);

                if (playerCameraController != null)
                    playerCameraController.Shake(SkillShakeStrength, SkillShakeDuration);

                // 타격마다 목록을 다시 훑습니다. DamageEnemy가 죽은 몬스터를 enemies에서
                // 빼면서 풀로 돌려보내므로, 미리 잡아 둔 배열을 계속 쓰면 안 됩니다.
                foreach (var enemy in enemies.ToArray())
                {
                    if (enemy == null || enemy.IsDead) continue;
                    DamageEnemy(enemy, damage);
                }
            }
        }

        /// <summary>
        /// Row 충격파는 캐릭터에 붙이지 않고 그 자리에 띄웁니다.
        /// 캐릭터가 다음 목표로 달려가도 충격파는 친 자리에 남아 있어야 합니다.
        /// </summary>
        private void SpawnRowEffect(Vector3 groundPosition)
        {
            if (rowPrefab == null) return;

            var effect = Instantiate(rowPrefab, groundPosition + Vector3.up * .05f, Quaternion.identity);
            effect.name = "Row_Active";
            var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            var lifetime = 2f;
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
                lifetime = Mathf.Max(lifetime, particle.main.duration + particle.main.startLifetime.constantMax);
            }
            Destroy(effect, lifetime);
        }

        /// <summary>
        /// 피격 이펙트는 몬스터에 붙이지 않고 그 자리에 띄웁니다.
        /// 죽은 몬스터가 곧바로 풀로 돌아가면 자식 이펙트도 같이 꺼지기 때문입니다.
        /// </summary>
        private void ShowHit(EnemyController target)
        {
            if (hitPrefab == null || target == null) return;

            var hit = Instantiate(hitPrefab, GetBodyWorldPosition(target.gameObject), Quaternion.identity);
            hit.name = "Hit_Active";
            var particles = hit.GetComponentsInChildren<ParticleSystem>(true);
            var lifetime = 1f;
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
                lifetime = Mathf.Max(lifetime, particle.main.duration + particle.main.startLifetime.constantMax);
            }
            Destroy(hit, lifetime);
        }

        private void ShowStun(EnemyController target)
        {
            if (stunPrefab == null || target == null || target.IsDead) return;

            var stun = Instantiate(stunPrefab);
            stun.name = "Stun_Active";
            stun.transform.position = GetHeadWorldPosition(target.gameObject);
            AudioManager.PlayAt(SfxId.EnemyStun, stun.transform.position);
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
                // 3번: 한 마리만 타격합니다. 캐릭터가 바라보고 있는 그 몬스터여야
                // 때리는 대상과 맞는 대상이 어긋나지 않습니다.
                if (currentTarget != null && !currentTarget.IsDead)
                {
                    yield return currentTarget;
                    yield break;
                }

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
            if (skillEffects.Count == 0 || activeSkillIndex < 0) return;
            // 스킬이 어떤 VFX로 보이는지는 AnimationIndex가 정합니다.
            var effectIndex = Mathf.Clamp(
                activeSkill != null ? activeSkill.AnimationIndex : activeSkillIndex,
                0,
                skillEffects.Count - 1);
            var effect = skillEffects[effectIndex];
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

            // 스킬이든 전설 장비의 추가 타격이든 전부 여기를 지나므로, 치명타 판정도 여기서 한 번만 합니다.
            var isCritical = criticalProfile.Roll();
            if (isCritical)
            {
                damage = criticalProfile.Apply(damage);
                if (playerCameraController != null)
                    playerCameraController.Shake(CriticalShakeStrength, CriticalShakeDuration);
            }

            if (damagePopupSystem != null)
                damagePopupSystem.Show(damage, GetHeadWorldPosition(target.gameObject), isCritical);
            var impactPosition = target.transform.position;
            ShowHit(target);
            target.TakeDamage(damage);
            if (!target.IsDead)
            {
                // 죽는 순간에는 사망 소리만 냅니다. 두 소리가 겹치면 서로를 가립니다.
                AudioManager.PlayAt(isCritical ? SfxId.EnemyCritical : SfxId.EnemyHit, impactPosition);
                return;
            }

            AudioManager.PlayAt(SfxId.EnemyDeath, impactPosition);
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
            AudioManager.Play(SfxId.StageAdvance);
        }

        private static Vector3 GetHeadWorldPosition(GameObject target)
        {
            return TryGetVisualBounds(target, out var headBounds)
                ? new Vector3(headBounds.center.x, headBounds.max.y + 0.2f, headBounds.center.z)
                : target.transform.position + Vector3.up * 1.5f;
        }

        /// <summary>
        /// 피격 이펙트는 머리 위가 아니라 몸통 한가운데에서 터지도록 합니다.
        /// </summary>
        private static Vector3 GetBodyWorldPosition(GameObject target)
        {
            return TryGetVisualBounds(target, out var bodyBounds)
                ? bodyBounds.center
                : target.transform.position + Vector3.up * 0.9f;
        }

        private static bool TryGetVisualBounds(GameObject target, out Bounds result)
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
            {
                result = default;
                return false;
            }

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

            result = bounds;
            return true;
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
                ? Localization.Get("battle.round.boss")
                : Localization.Format("battle.round.normal", completedNormalRounds + 1);
            // 보스는 화면 전체에 울리도록 2D로, 일반 라운드는 북 두 번으로 알립니다.
            AudioManager.Play(activeEncounterIsBoss ? SfxId.BossSpawn : SfxId.WaveStart);
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
            // 등장 소리는 마리마다 내지 않습니다. 한 웨이브에 여덟 마리까지 나오기 때문에
            // 개당 울리면 딸랑거림이 몰립니다. 웨이브 시작음(SpawnWave)이 이미 알려 줍니다.
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
            // 씬의 프로필은 레벨 글자를 "LV"로 두고 있습니다. 예전 이름(LevelTxT)도 함께 봅니다.
            levelText = profileRoot != null
                ? FindTextByName(profileRoot, "LV") ?? FindTextByName(profileRoot, "LevelTxT")
                : FindSceneText("LevelTxT");
            playerNameText = profileRoot != null ? FindTextByName(profileRoot, "Name") : FindSceneText("Name");
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
            data.Died += OnPlayerDied;
            // 장비를 갈아입으면 치명타 수치도 바뀝니다. 타격마다 다시 계산하지 않고 그때만 갱신합니다.
            data.InventoryChanged += RefreshCriticalProfile;
            RefreshCriticalProfile();
            // 전투는 항상 가득 찬 체력/마나로 시작합니다.
            // PlayerDataManager는 씬을 넘어 살아남으므로, 되돌리지 않으면
            // 지난 판에서 쓰러진 0/0 상태 그대로 다시 시작하게 됩니다.
            data.ResetVitals();
            UpdateBattleHud();
        }

        private void OnEnable() => Localization.LanguageChanged += UpdateBattleHud;

        private void OnDisable() => Localization.LanguageChanged -= UpdateBattleHud;

        private void OnDestroy()
        {
            if (PlayerDataManager.Instance == null) return;
            PlayerDataManager.Instance.ExperienceChanged -= OnExperienceChanged;
            PlayerDataManager.Instance.CoinsChanged -= OnCoinsChanged;
            PlayerDataManager.Instance.Died -= OnPlayerDied;
            PlayerDataManager.Instance.InventoryChanged -= RefreshCriticalProfile;
        }

        private void RefreshCriticalProfile()
        {
            if (itemCatalog == null) itemCatalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
            criticalProfile = CombatPower.GetCritical(PlayerDataManager.Instance, itemCatalog);
            manaRegenerationPerSecond = CombatPower.GetManaRegeneration(PlayerDataManager.Instance, itemCatalog);
        }

        /// <summary>
        /// 마나를 초당 <see cref="manaRegenerationPerSecond"/>만큼 되돌립니다.
        ///
        /// 마나는 정수라 프레임마다 바로 더할 수 없습니다. 소수점 아래를 모아 두었다가
        /// 1이 넘을 때만 넘겨줍니다. 이게 없으면 매 프레임 0으로 반올림되어 영영 차지 않습니다.
        /// </summary>
        private void RegenerateMana()
        {
            if (manaRegenerationPerSecond <= 0f) return;

            var data = PlayerDataManager.Instance;
            if (data == null || data.CurrentMana >= data.MaxMana)
            {
                manaRegenerationCarry = 0f;
                return;
            }

            manaRegenerationCarry += manaRegenerationPerSecond * Time.deltaTime;
            var whole = Mathf.FloorToInt(manaRegenerationCarry);
            if (whole <= 0) return;

            manaRegenerationCarry -= whole;
            data.RestoreMana(whole);
        }

        /// <summary>
        /// 쓰러진 채로 두면 체력·마나가 0에 묶여 자동 물약도 스킬도 돌지 않습니다.
        /// 방치형이므로 사망은 잠깐 멈추는 것으로만 처리하고 곧바로 되살립니다.
        /// </summary>
        private void OnPlayerDied()
        {
            if (reviveRoutine == null) reviveRoutine = StartCoroutine(ReviveRoutine());
        }

        private IEnumerator ReviveRoutine()
        {
            stateText = Localization.Get("battle.state.down");
            PopupService.Toast(Localization.Get("battle.toast.revive"));
            yield return new WaitForSeconds(ReviveDelay);
            AudioManager.Play(SfxId.PlayerRevive);
            PlayerDataManager.Instance.ResetVitals();
            reviveRoutine = null;
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
            if (waveText != null) waveText.text = Localization.Format("main.stage.wave", shownWave, totalWaves);
            if (monsterCountText != null)
                monsterCountText.text = Localization.Format("main.stage.monster_left", enemies.Count);
            UpdatePlayerHud();
        }

        private void UpdatePlayerHud()
        {
            var data = PlayerDataManager.Instance;
            var required = data.ExperienceToNextLevel;
            var ratio = required > 0 ? data.Experience / (float)required : 0f;
            if (levelText != null) levelText.text = $"Lv.{data.Level}";
            if (experienceText != null) experienceText.text = $"EXP {ratio * 100f:0.0}%";
            // 이름이 아직 안 들어왔을 때는 씬에 그려 둔 글자를 그대로 둡니다.
            if (playerNameText != null && !string.IsNullOrWhiteSpace(data.PlayerName))
                playerNameText.text = data.PlayerName;
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
            return Localization.Format("main.stage.name", chapter, section);
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
