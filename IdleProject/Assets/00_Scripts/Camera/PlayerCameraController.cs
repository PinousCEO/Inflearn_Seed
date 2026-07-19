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

        private Vector3 velocity;
        private bool pendingInitialSnap;

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

            transform.position = target.position + followOffset;
            LookAtTarget();
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
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocity,
                smoothTime);

            LookAtTarget();
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
