using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class PlayerCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow Settings")]
        [SerializeField] private Vector3 followOffset = new Vector3(10f, 14f, -12f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.28f;
        [SerializeField] private float lookAtHeight = 0.3f;
        [SerializeField] private bool snapOnTargetAssigned = true;

        [Header("Shake")]
        [SerializeField, Min(0f)] private float maxShakeStrength = 0.6f;

        private Vector3 velocity;
        private bool pendingInitialSnap;
        private Vector3 shakeOffset;
        private float shakeStrength;
        private float shakeTimer;
        private float shakeDuration;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            velocity = Vector3.zero;
            pendingInitialSnap = snapOnTargetAssigned;

            if (snapOnTargetAssigned)
                SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            shakeOffset = Vector3.zero;
            transform.position = target.position + followOffset;
            LookAtTarget();
        }

        /// <summary>
        /// 화면을 잠깐 흔듭니다. 겹쳐 들어오면 더 센 쪽/긴 쪽으로 이어 붙습니다.
        /// </summary>
        public void Shake(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f) return;
            if (!GameSettings.ScreenShakeEnabled) return;

            shakeStrength = Mathf.Min(Mathf.Max(shakeStrength, strength), maxShakeStrength);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 다른 컨트롤러의 첫 Update가 끝난 뒤, 첫 렌더 직전에 다시 정렬합니다.
            if (pendingInitialSnap)
            {
                pendingInitialSnap = false;
                SnapToTarget();
                return;
            }

            var desiredPosition = target.position + followOffset;
            // 흔들림은 추적 결과 위에 얹는 값이라, 보간은 흔들리기 전 위치에서 이어 갑니다.
            var followPosition = Vector3.SmoothDamp(
                transform.position - shakeOffset,
                desiredPosition,
                ref velocity,
                smoothTime);

            transform.position = followPosition;
            LookAtTarget();

            shakeOffset = NextShakeOffset();
            transform.position = followPosition + shakeOffset;
        }

        private Vector3 NextShakeOffset()
        {
            if (shakeTimer <= 0f) return Vector3.zero;

            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
            {
                shakeStrength = 0f;
                shakeDuration = 0f;
                return Vector3.zero;
            }

            // 남은 시간에 비례해 잦아들고, 화면에 평행한 방향으로만 흔듭니다.
            var amount = shakeStrength * (shakeTimer / shakeDuration);
            var random = Random.insideUnitCircle * amount;
            return transform.right * random.x + transform.up * random.y;
        }

        private void LookAtTarget()
        {
            var lookPosition = target.position + Vector3.up * lookAtHeight;
            var lookDirection = lookPosition - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}
