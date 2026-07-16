using System.Collections;
using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class Monster : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.25f;
        [SerializeField, Min(0f)] private float turnSpeed = 10f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float attackRange = 1.65f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1.1f;
        [SerializeField, Min(0)] private int attackDamage = 2;

        [Header("Hit Feedback")]
        [SerializeField, Min(1f)] private float pulseScale = 1.12f;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.12f;

        private int health;
        private Transform target;
        private IdleBattleGame owner;
        private Terrain terrain;
        private float groundOffset;
        private float attackTimer;
        private Vector3 originalScale;

        public bool IsDead => health <= 0;

        public void Initialize(int value, Transform player, IdleBattleGame game, Terrain activeTerrain)
        {
            health = value;
            target = player;
            owner = game;
            terrain = activeTerrain;
            groundOffset = terrain != null
                ? transform.position.y - (terrain.SampleHeight(transform.position) + terrain.transform.position.y)
                : transform.position.y;
            originalScale = transform.localScale;
            attackTimer = Random.Range(0.2f, 0.8f);
        }

        public void TakeDamage(int value)
        {
            health -= value;
            transform.localScale = originalScale * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(health));
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
                owner.TakePlayerDamage(attackDamage);
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
