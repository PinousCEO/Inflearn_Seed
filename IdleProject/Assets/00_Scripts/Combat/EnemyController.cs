using System.Collections;
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

        public bool IsDead => health <= 0;

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
        }

        public void PrepareForPool()
        {
            StopAllCoroutines();
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
            if (hasOriginalScale)
                transform.localScale = originalScale;
        }

        public void TakeDamage(int value)
        {
            health -= value;
            var healthRatio = maxHealth > 0 ? (float)health / maxHealth : 0f;
            transform.localScale = originalScale * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(healthRatio));
        }

        public void ApplySlow(float duration, float multiplier, GameObject auraPrefab)
        {
            StopCoroutine(nameof(ClearSlow));
            slowMultiplier = Mathf.Clamp(multiplier, .1f, 1f);
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
            if (distance > aggroDistance) return;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    turnSpeed * Time.deltaTime);

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
                owner.TakePlayerDamage(scaledAttackDamage);
                StartCoroutine(AttackPulse());
            }
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
