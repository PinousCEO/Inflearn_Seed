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
        private Vector3 originalScale;
        private bool hasOriginalScale;

        public bool IsDead => health <= 0;

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
        }

        public void PrepareForPool()
        {
            StopAllCoroutines();
            health = 0;
            maxHealth = 0;
            scaledAttackDamage = 0;
            target = null;
            owner = null;
            terrain = null;
            attackTimer = 0f;
            if (hasOriginalScale)
                transform.localScale = originalScale;
        }

        public void TakeDamage(int value)
        {
            health -= value;
            var healthRatio = maxHealth > 0 ? (float)health / maxHealth : 0f;
            transform.localScale = originalScale * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(healthRatio));
        }

        private void Update()
        {
            if (target == null || owner == null || IsDead) return;

            attackTimer -= Time.deltaTime;
            var direction = target.position - transform.position;
            direction.y = 0f;
            var distance = direction.magnitude;

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
                transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
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
