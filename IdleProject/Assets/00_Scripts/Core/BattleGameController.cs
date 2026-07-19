using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace IdleBattle
{
    public sealed class BattleGameController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<BattleGameController>() == null)
                new GameObject("Battle Game Controller").AddComponent<BattleGameController>();
        }

        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private Transform player, destinationMarker;
        private GameObject skillEffect;
        private Animator playerAnimator;
        private CharacterEquipmentPresenter playerEquipment;
        private EnemyController attackTarget;
        private bool isAttacking;
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
        private RectTransform stageMarkerRoot;
        private readonly List<Image> stageMarkers = new List<Image>();
        private Sprite circleSprite;
        private float travelDistance;
        private float stageProgress;
        private int playerMana;
        private float attackTimer;
        private bool isSkillActive;
        private bool skillEventApplied;
        private string stateText = "지역 탐색 중";
        private GUIStyle titleStyle, labelStyle, centerStyle;

        private const float SpawnRadius = 10f;
        private const float MoveSpeed = 4.2f;
        private const float AttackRange = 2.1f;
        private const int PlayerAttackDamage = 1;
        private const int MaxMana = 30;
        private const int ManaPerAttack = 10;
        private const float StageStatGrowth = 1.1f;
        private const int NormalEnemyBaseHealth = 3;
        private const int NormalEnemyBaseDamage = 2;
        private const int BossHealthMultiplier = 4;
        private const int BossDamageMultiplier = 2;

        private void Awake()
        {
            SetupWorld();
            SetupStage();
            SetupStageSlider();
            CreateDestination();
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

            var markerRootObject = new GameObject("Normal Round Markers", typeof(RectTransform));
            markerRootObject.layer = sliderObject.layer;
            stageMarkerRoot = markerRootObject.GetComponent<RectTransform>();
            stageMarkerRoot.SetParent(stageSlider, false);
            stageMarkerRoot.anchorMin = Vector2.zero;
            stageMarkerRoot.anchorMax = Vector2.one;
            stageMarkerRoot.offsetMin = Vector2.zero;
            stageMarkerRoot.offsetMax = Vector2.zero;
            circleSprite = CreateCircleSprite();
            RebuildStageMarkers();
            UpdateStageSlider(0f);
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Stage Round Circle";
            var pixels = new Color32[size * size];
            var center = (size - 1) * .5f;
            var radiusSquared = center * center;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                pixels[y * size + x] = dx * dx + dy * dy <= radiusSquared
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }

        private void RebuildStageMarkers()
        {
            foreach (var marker in stageMarkers)
                if (marker != null) Destroy(marker.gameObject);
            stageMarkers.Clear();
            if (stageMarkerRoot == null || currentStageRule == null) return;

            var count = currentStageRule.NormalRoundCount + 1;
            for (var i = 0; i < count; i++)
            {
                var markerObject = new GameObject(
                    i == count - 1 ? "Boss Round" : $"Normal Round {i + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                markerObject.layer = stageMarkerRoot.gameObject.layer;
                var rect = markerObject.GetComponent<RectTransform>();
                rect.SetParent(stageMarkerRoot, false);
                var position = (i + 1f) / count;
                rect.anchorMin = new Vector2(position, .5f);
                rect.anchorMax = new Vector2(position, .5f);
                rect.sizeDelta = i == count - 1
                    ? new Vector2(30f, 30f)
                    : new Vector2(22f, 22f);
                rect.anchoredPosition = Vector2.zero;
                var image = markerObject.GetComponent<Image>();
                image.sprite = circleSprite;
                image.raycastTarget = false;
                stageMarkers.Add(image);
            }
            RefreshMarkerColors();
        }

        private void UpdateStageSlider(float progress)
        {
            stageProgress = Mathf.Clamp01(progress);
            if (stageSliderFill != null && stageSlider != null)
                stageSliderFill.sizeDelta = new Vector2(stageSlider.rect.width * stageProgress, stageSliderFill.sizeDelta.y);
            RefreshMarkerColors();
        }

        private void RefreshMarkerColors()
        {
            for (var i = 0; i < stageMarkers.Count; i++)
            {
                if (stageMarkers[i] == null) continue;
                var markerProgress = (i + 1f) / stageMarkers.Count;
                stageMarkers[i].color = stageProgress >= markerProgress
                    ? new Color(.25f, .9f, .45f, 1f)
                    : new Color(1f, .82f, .2f, 1f);
            }
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
                var initialSkillEffect = FindChildByName(hero.transform, "Skill");
                if (initialSkillEffect != null) initialSkillEffect.SetActive(false);
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
            skillEffect = FindChildByName(hero.transform, "Skill");
            if (skillEffect != null)
                skillEffect.SetActive(false);
            else
                Debug.LogWarning("Character 프리팹에서 Skill을 찾지 못했습니다.");
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

        private void Update()
        {
            enemies.RemoveAll(enemy => enemy == null);
            attackTimer -= Time.deltaTime;
            if (isSkillActive)
            {
                SetRunning(false);
                return;
            }
            if (isAttacking)
            {
                // 공격 애니메이션 중에도 움직이는 대상의 방향을 계속 보정합니다.
                if (attackTarget != null) Face(attackTarget.transform.position);
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
                stateText = "몬스터 공격 중";
                SetRunning(false);
                Face(target.transform.position);
                if (attackTimer <= 0)
                {
                    attackTimer = .42f;
                    StartCoroutine(PlayAttack(target));
                }
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

        private void FaceImmediately(Vector3 target)
        {
            var direction = Flat(target) - Flat(player.position);
            if (direction.sqrMagnitude > .001f)
                player.rotation = Quaternion.LookRotation(direction);
        }

        private IEnumerator PlayAttack(EnemyController target)
        {
            if (target == null) yield break;
            isAttacking = true;
            attackTarget = target;
            if (playerEquipment != null) playerEquipment.DrawWeapon();
            SetRunning(false);
            FaceImmediately(target.transform.position);

            if (playerAnimator == null)
            {
                ATK();
                yield return new WaitForSeconds(.42f);
                isAttacking = false;
                attackTarget = null;
                if (playerEquipment != null) playerEquipment.SheatheWeapon();
                yield break;
            }

            var previousState = playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            playerAnimator.ResetTrigger("Attack");
            playerAnimator.SetTrigger("Attack");

            var enterTimeout = 1f;
            while (enterTimeout > 0f && playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == previousState)
            {
                enterTimeout -= Time.deltaTime;
                yield return null;
            }

            var attackState = playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var finishTimeout = 5f;
            while (finishTimeout > 0f)
            {
                finishTimeout -= Time.deltaTime;
                var state = playerAnimator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash != attackState || (!playerAnimator.IsInTransition(0) && state.normalizedTime >= .98f))
                    break;
                yield return null;
            }

            // Attack -> Run 전이 조건이 Run == true이므로 공격 재생이 끝난 뒤 켭니다.
            SetRunning(true);
            var transitionTimeout = 1f;
            while (transitionTimeout > 0f && playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == attackState)
            {
                transitionTimeout -= Time.deltaTime;
                yield return null;
            }

            isAttacking = false;
            attackTarget = null;
            if (playerEquipment != null) playerEquipment.SheatheWeapon();
        }

        private void SetRunning(bool value)
        {
            if (playerAnimator != null) playerAnimator.SetBool("Run", value);
        }

        // Character의 Attack 애니메이션 Event(ATK)가 이 메서드까지 전달됩니다.
        public void ATK()
        {
            if (!isAttacking || attackTarget == null) return;
            ApplyAttack(attackTarget);
        }

        private void ApplyAttack(EnemyController target)
        {
            if (target == null) return;
            DamageEnemy(target, PlayerAttackDamage);
            AddMana(ManaPerAttack);
        }

        private void AddMana(int amount)
        {
            playerMana = Mathf.Min(MaxMana, playerMana + amount);
            if (playerMana < MaxMana || isSkillActive) return;

            playerMana = 0;
            StartCoroutine(ActivateSkill());
        }

        private IEnumerator ActivateSkill()
        {
            isSkillActive = true;
            skillEventApplied = false;
            SetRunning(false);

            if (playerAnimator == null)
            {
                Skill();
                isSkillActive = false;
                yield break;
            }

            var previousState = playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
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
                if (state.fullPathHash != skillState ||
                    (!playerAnimator.IsInTransition(0) && state.normalizedTime >= .98f))
                    break;
                yield return null;
            }

            isSkillActive = false;
        }

        // Character의 Skill 애니메이션 Event가 이 시점에 이펙트와 광역 피해를 실행합니다.
        public void Skill()
        {
            if (!isSkillActive || skillEventApplied) return;
            skillEventApplied = true;

            PlaySkillEffectOnce();
            var targets = enemies.ToArray();
            foreach (var enemy in targets)
                DamageEnemy(enemy, PlayerAttackDamage);
        }

        private void PlaySkillEffectOnce()
        {
            if (skillEffect == null) return;

            skillEffect.SetActive(true);
            var particles = skillEffect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                particle.Clear(true);
                particle.Play(true);
            }
            StartCoroutine(HideSkillEffectWhenFinished(particles));
        }

        private IEnumerator HideSkillEffectWhenFinished(ParticleSystem[] particles)
        {
            yield return null;
            while (skillEffect != null && AreParticlesAlive(particles))
                yield return null;

            if (skillEffect != null) skillEffect.SetActive(false);
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
            RebuildStageMarkers();
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

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = new Color(.75f, .86f, 1f) } };
                centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            GUI.Box(new Rect(22, 20, 285, 126), GUIContent.none);
            GUI.Label(new Rect(42, 31, 245, 38), "IDLE HUNTER", titleStyle);
            GUI.Label(new Rect(42, 72, 245, 26), $"STAGE {currentStage}  ROUND {completedNormalRounds + 1}/{currentStageRule.BossRoundNumber}  HP {PlayerDataManager.Instance.CurrentHealth}", labelStyle);
            GUI.Label(new Rect(42, 105, 245, 26), stateText, centerStyle);
            GUI.Box(new Rect(Screen.width - 270, 20, 248, 68), GUIContent.none);
            GUI.Label(new Rect(Screen.width - 255, 32, 218, 44), "캐릭터가 자동으로\n탐색하고 전투합니다", centerStyle);
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
