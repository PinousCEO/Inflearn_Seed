using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    public sealed class IdleBattleGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<IdleBattleGame>() == null)
                new GameObject("Idle Battle Game").AddComponent<IdleBattleGame>();
        }

        private readonly List<Monster> monsters = new List<Monster>();
        private Transform player, destinationMarker;
        private Animator playerAnimator;
        private Monster attackTarget;
        private bool isAttacking;
        private float playerGroundY;
        private Vector3 destination;
        private Material playerMaterial, monsterMaterial, markerMaterial;
        private GameObject monsterPrefab;
        private Terrain terrain;
        private Vector2 worldMin = new Vector2(-37f, -37f);
        private Vector2 worldMax = new Vector2(37f, 37f);
        private int wave, defeated;
        private int playerHealth = 100;
        private float attackTimer;
        private string stateText = "지역 탐색 중";
        private GUIStyle titleStyle, labelStyle, centerStyle;

        private const float SpawnRadius = 10f;
        private const float MoveSpeed = 4.2f;
        private const float AttackRange = 2.1f;

        private void Awake()
        {
            SetupWorld();
            CreateDestination();
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
            monsterMaterial = MakeMaterial(new Color(.9f, .2f, .25f));
            markerMaterial = MakeMaterial(new Color(.95f, .73f, .12f, .75f));
            monsterPrefab = LoadPrefab("01_Prefabs/Monster", "Assets/01_Prefabs/Monster.prefab");

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
            playerAnimator = hero.GetComponentInChildren<Animator>();
            if (playerAnimator == null)
                Debug.LogWarning("Character 프리팹에서 Animator를 찾지 못했습니다.");
            else
            {
                var relay = playerAnimator.gameObject.GetComponent<AttackAnimationEventRelay>();
                if (relay == null) relay = playerAnimator.gameObject.AddComponent<AttackAnimationEventRelay>();
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

            var follow = playerCamera.GetComponent<PlayerCameraFollow>();
            if (follow == null) follow = playerCamera.gameObject.AddComponent<PlayerCameraFollow>();
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

        private static void PlaceCharacterOnGround(GameObject hero, float groundHeight = 0f)
        {
            var renderers = hero.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            hero.transform.position += Vector3.up * (groundHeight - bounds.min.y);
        }

        private void Update()
        {
            monsters.RemoveAll(m => m == null);
            attackTimer -= Time.deltaTime;
            if (isAttacking)
            {
                // 공격 애니메이션 중에도 움직이는 대상의 방향을 계속 보정합니다.
                if (attackTarget != null) Face(attackTarget.transform.position);
                return;
            }
            if (monsters.Count == 0)
            {
                stateText = "다음 전투 지역으로 이동 중";
                MoveTowards(destination);
                if (Vector3.Distance(Flat(player.position), Flat(destination)) < .65f) SpawnWave();
                return;
            }

            var target = FindNearestMonster();
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

        private Monster FindNearestMonster()
        {
            Monster nearest = null;
            var distance = float.MaxValue;
            foreach (var monster in monsters)
            {
                if (monster == null) continue;
                var d = (monster.transform.position - player.position).sqrMagnitude;
                if (d >= distance) continue;
                distance = d;
                nearest = monster;
            }
            return nearest;
        }

        private void MoveTowards(Vector3 target)
        {
            SetRunning(true);
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

        private IEnumerator PlayAttack(Monster target)
        {
            if (target == null) yield break;
            isAttacking = true;
            attackTarget = target;
            SetRunning(false);
            FaceImmediately(target.transform.position);

            if (playerAnimator == null)
            {
                ATK();
                yield return new WaitForSeconds(.42f);
                isAttacking = false;
                attackTarget = null;
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
        }

        private void SetRunning(bool value)
        {
            if (playerAnimator != null) playerAnimator.SetBool("Run", value);
        }

        // Character의 Attack 애니메이션 Event(ATK)가 이 메서드까지 전달됩니다.
        public void ATK()
        {
            if (!isAttacking || attackTarget == null) return;
            StartCoroutine(ApplyAttack(attackTarget));
        }

        private IEnumerator ApplyAttack(Monster target)
        {
            if (target == null) yield break;
            var start = player.position + Vector3.up * .35f + player.forward * .65f;
            var slash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            slash.name = "Attack Effect";
            Destroy(slash.GetComponent<Collider>());
            slash.transform.localScale = Vector3.one * .32f;
            slash.GetComponent<Renderer>().material = markerMaterial;
            var elapsed = 0f;
            while (elapsed < .14f && target != null)
            {
                elapsed += Time.deltaTime;
                slash.transform.position = Vector3.Lerp(start, target.transform.position + Vector3.up * .45f, elapsed / .14f);
                yield return null;
            }
            Destroy(slash);
            if (target == null) yield break;
            target.TakeDamage(1);
            if (!target.IsDead) yield break;
            monsters.Remove(target);
            defeated++;
            Destroy(target.gameObject);
            if (monsters.Count == 0) CreateDestination();
        }

        private void CreateDestination()
        {
            if (destinationMarker != null) Destroy(destinationMarker.gameObject);
            var angle = Random.Range(0f, Mathf.PI * 2);
            destination = Flat(player != null ? player.position : Vector3.zero)
                + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * SpawnRadius;
            destination = ClampToWorld(destination);
            destination = GroundPoint(destination);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Next Monster Area";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = destination + Vector3.up * .045f;
            marker.transform.localScale = new Vector3(2.2f, .04f, 2.2f);
            marker.GetComponent<Renderer>().material = markerMaterial;
            destinationMarker = marker.transform;
        }

        private void SpawnWave()
        {
            wave++;
            if (destinationMarker != null) Destroy(destinationMarker.gameObject);
            var count = Mathf.Min(4 + wave, 9);
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2 / count + Random.Range(-.2f, .2f);
                var radius = Random.Range(2.7f, 4.4f);
                var position = destination + new Vector3(Mathf.Cos(angle) * radius, .65f, Mathf.Sin(angle) * radius);
                GameObject enemy;
                if (monsterPrefab != null)
                {
                    enemy = Instantiate(monsterPrefab);
                    enemy.transform.position = position;
                    PlaceCharacterOnGround(enemy, GroundPoint(position).y);
                }
                else
                {
                    Debug.LogWarning("Assets/01_Prefabs/Monster.prefab을 찾지 못해 Sphere를 대신 사용합니다.");
                    enemy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    enemy.transform.position = GroundPoint(position) + Vector3.up * .625f;
                    enemy.transform.localScale = Vector3.one * 1.25f;
                    enemy.GetComponent<Renderer>().material = monsterMaterial;
                }
                enemy.name = "Monster " + (i + 1);
                var monster = enemy.AddComponent<Monster>();
                monster.Initialize(1 + wave / 3, player, this, terrain);
                monsters.Add(monster);
            }
            stateText = "새 몬스터 무리 발견";
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
            GUI.Label(new Rect(42, 72, 245, 26), $"WAVE {wave}   처치 {defeated}   HP {playerHealth}", labelStyle);
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
            playerHealth = Mathf.Max(0, playerHealth - damage);
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
