using System.Collections;
using IdleBattle.Audio;
using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.25f;
        [SerializeField, Min(0f)] private float turnSpeed = 10f;
        [SerializeField, Min(0f)] private float aggroDistance = 6.5f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float attackRange = 1.65f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1.1f;
        [Header("Hit Feedback")]
        [SerializeField, Min(1f)] private float pulseScale = 1.12f;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.12f;
        /// <summary>
        /// 맞은 순간 몸 전체를 덮는 색입니다.
        ///
        /// 붉은색은 몬스터의 원래 색과 섞여 "붉은 몬스터"처럼 보였습니다.
        /// 흰색은 어떤 몬스터에게든 같은 밝기로 얹혀 맞았다는 것만 알려 줍니다.
        ///
        /// 다만 그냥 흰색(1,1,1)이면 안 됩니다. 이 값은 몬스터의 원래 색에 곱해지므로,
        /// 흰 눈알처럼 이미 밝은 재질은 아무 변화가 없습니다.
        /// 1을 넘겨서 원래 색과 상관없이 하얗게 뜨도록 만듭니다.
        /// 대신 2를 넘기면 명암까지 다 날아가 실루엣만 남으니 이 정도가 상한입니다.
        /// </summary>
        [SerializeField, ColorUsage(false, true)] private Color hitColor = new Color(1.8f, 1.8f, 1.8f, 1f);
        [SerializeField, Min(0.01f)] private float hitColorDuration = 0.1f;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float knockbackDistance = 0.7f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.22f;
        // 밀려난 직후 곧바로 때리지 않고 다시 다가오는 모습을 보여 주기 위한 여유 시간.
        [SerializeField, Min(0f)] private float knockbackRecovery = 0.25f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private int health;
        private int maxHealth;
        private int scaledAttackDamage;
        private Transform target;
        private BattleGameController owner;
        private Terrain terrain;
        private float groundOffset;
        private float attackTimer;
        private float slowMultiplier = 1f;
        private Vector3 originalScale;
        private bool hasOriginalScale;
        private GameObject stunEffect;
        private Vector3 knockbackDirection;
        private float knockbackTimer;
        private Renderer[] bodyRenderers;
        private MaterialPropertyBlock colorBlock;
        private bool isHitColorActive;

        public bool IsDead => health <= 0;

        private void Awake()
        {
            CacheBodyRenderers();
        }

        /// <summary>
        /// 색을 바꿀 몸통 렌더러만 미리 모아 둡니다.
        /// 파티클(스턴/오라/피격 이펙트)은 같이 물들면 안 되므로 제외합니다.
        /// </summary>
        private void CacheBodyRenderers()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            var count = 0;
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] is ParticleSystemRenderer) continue;
                all[count++] = all[i];
            }
            bodyRenderers = new Renderer[count];
            System.Array.Copy(all, bodyRenderers, count);
            colorBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 스턴 이펙트를 이 몬스터가 직접 들고 있게 해서,
        /// 표시할 때마다 자식 계층을 이름으로 뒤지지 않도록 합니다.
        /// </summary>
        public void SetStunEffect(GameObject effect)
        {
            if (stunEffect != null && stunEffect != effect) Destroy(stunEffect);
            stunEffect = effect;
        }

        public void ClearStunEffect(GameObject effect)
        {
            if (stunEffect == effect) stunEffect = null;
        }

        public void Initialize(int healthValue, int attackDamageValue, Transform player, BattleGameController game, Terrain activeTerrain)
        {
            StopAllCoroutines();
            // 하얗게 물든 채로 코루틴이 끊겼을 수 있으니 원래 색으로 되돌립니다.
            ApplyHitColor(false);
            if (!hasOriginalScale)
            {
                originalScale = transform.localScale;
                hasOriginalScale = true;
            }
            transform.localScale = originalScale;
            maxHealth = Mathf.Max(1, healthValue);
            health = maxHealth;
            scaledAttackDamage = Mathf.Max(1, attackDamageValue);
            target = player;
            owner = game;
            terrain = activeTerrain;
            groundOffset = terrain != null
                ? transform.position.y - (terrain.SampleHeight(transform.position) + terrain.transform.position.y)
                : transform.position.y;
            attackTimer = Random.Range(0.2f, 0.8f);
            slowMultiplier = 1f;
            knockbackTimer = 0f;
        }

        public void PrepareForPool()
        {
            StopAllCoroutines();
            ApplyHitColor(false);
            if (stunEffect != null) Destroy(stunEffect);
            stunEffect = null;
            health = 0;
            maxHealth = 0;
            scaledAttackDamage = 0;
            target = null;
            owner = null;
            terrain = null;
            attackTimer = 0f;
            slowMultiplier = 1f;
            knockbackTimer = 0f;
            if (hasOriginalScale)
                transform.localScale = originalScale;
        }

        public void TakeDamage(int value)
        {
            health -= value;
            var healthRatio = maxHealth > 0 ? (float)health / maxHealth : 0f;
            transform.localScale = originalScale * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(healthRatio));
            if (IsDead) return;

            StopCoroutine(nameof(FlashHitColor));
            StartCoroutine(nameof(FlashHitColor));
        }

        private IEnumerator FlashHitColor()
        {
            ApplyHitColor(true);
            yield return new WaitForSeconds(hitColorDuration);
            ApplyHitColor(false);
        }

        private void ApplyHitColor(bool active)
        {
            if (bodyRenderers == null || isHitColorActive == active) return;
            isHitColorActive = active;

            for (var i = 0; i < bodyRenderers.Length; i++)
            {
                var visual = bodyRenderers[i];
                if (visual == null) continue;
                if (!active)
                {
                    visual.SetPropertyBlock(null);
                    continue;
                }

                visual.GetPropertyBlock(colorBlock);
                colorBlock.SetColor(BaseColorId, hitColor);
                colorBlock.SetColor(ColorId, hitColor);
                visual.SetPropertyBlock(colorBlock);
            }
        }

        /// <summary>
        /// 타격 지점 반대 방향으로 잠깐 밀려납니다.
        /// 밀리는 동안에는 추격/공격을 멈추고, 끝나면 스스로 다시 다가와 공격합니다.
        /// </summary>
        public void ApplyKnockback(Vector3 sourcePosition)
        {
            if (IsDead || knockbackDistance <= 0f) return;

            var away = transform.position - sourcePosition;
            away.y = 0f;
            // 완전히 겹쳐 있으면 방향을 잡을 수 없으니 바라보는 반대쪽으로 밀어냅니다.
            if (away.sqrMagnitude < 0.0001f)
            {
                away = -transform.forward;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            }
            knockbackDirection = away.normalized;
            knockbackTimer = knockbackDuration;
            // 밀리는 도중과 직후 잠깐은 공격하지 않게 해서 되돌아오는 동작이 보이도록 합니다.
            attackTimer = Mathf.Max(attackTimer, knockbackDuration + knockbackRecovery);
        }

        private void UpdateKnockback()
        {
            knockbackTimer -= Time.deltaTime;
            // 남은 시간에 비례해 감속시키면 총 이동량이 knockbackDistance와 같아지고,
            // 끝부분이 부드럽게 멈춰 보입니다.
            var speed = 2f * knockbackDistance / knockbackDuration
                        * Mathf.Clamp01(knockbackTimer / knockbackDuration);
            var next = transform.position + knockbackDirection * (speed * Time.deltaTime);
            if (terrain != null)
                next.y = terrain.SampleHeight(next) + terrain.transform.position.y + groundOffset;
            transform.position = next;
        }

        public void ApplySlow(float duration, float multiplier, GameObject auraPrefab)
        {
            // 죽은 적은 풀로 돌아가며 즉시 비활성화될 수 있습니다.
            // 비활성 오브젝트에서는 코루틴을 시작할 수 없으므로 아무 효과도 적용하지 않습니다.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || IsDead) return;

            StopCoroutine(nameof(ClearSlow));
            slowMultiplier = Mathf.Clamp(multiplier, .1f, 1f);
            AudioManager.PlayAt(SfxId.EnemySlow, transform.position);
            if (auraPrefab != null)
            {
                var aura = Instantiate(auraPrefab, transform);
                aura.name = "Aura_Slow_Down_Active";
                aura.transform.localPosition = Vector3.zero;
                foreach (var particle in aura.GetComponentsInChildren<ParticleSystem>(true)) { particle.Clear(true); particle.Play(true); }
                Destroy(aura, duration);
            }
            StartCoroutine(ClearSlow(duration));
        }

        private IEnumerator ClearSlow(float duration)
        {
            yield return new WaitForSeconds(duration);
            slowMultiplier = 1f;
        }

        private void Update()
        {
            if (target == null || owner == null || IsDead) return;

            attackTimer -= Time.deltaTime;
            var direction = target.position - transform.position;
            direction.y = 0f;
            var distance = direction.magnitude;

            if (knockbackTimer > 0f)
            {
                // 뒤로 밀리는 중에도 플레이어를 계속 바라보게 둡니다.
                FaceTarget(direction);
                UpdateKnockback();
                return;
            }

            if (distance > aggroDistance) return;

            FaceTarget(direction);

            if (distance > attackRange)
            {
                var targetY = terrain != null
                    ? terrain.SampleHeight(transform.position) + terrain.transform.position.y + groundOffset
                    : groundOffset;
                var destination = new Vector3(target.position.x, targetY, target.position.z);
                transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * slowMultiplier * Time.deltaTime);
            }
            else if (attackTimer <= 0f)
            {
                attackTimer = attackInterval;
                AudioManager.PlayAt(SfxId.EnemyAttack, transform.position);
                owner.TakePlayerDamage(scaledAttackDamage);
                StartCoroutine(AttackPulse());
            }
        }

        private void FaceTarget(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f) return;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                turnSpeed * Time.deltaTime);
        }

        private IEnumerator AttackPulse()
        {
            var startScale = transform.localScale;
            transform.localScale = startScale * pulseScale;
            yield return new WaitForSeconds(pulseDuration);
            if (this != null) transform.localScale = startScale;
        }
    }
}
