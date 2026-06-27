using UnityEngine;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class WorldSpaceCanvasFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.8f, 0f);
        [SerializeField, Min(0.01f)] private float pullStrength = 6f;
        [SerializeField, Min(0f)] private float maxLagDistance = 1.5f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool snapOnEnable = true;

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
        }

        private void OnEnable()
        {
            if (snapOnEnable && target != null)
            {
                transform.position = target.position + worldOffset;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Vector3 desiredPosition = target.position + worldOffset;
            Vector3 offset = desiredPosition - transform.position;
            float distance = offset.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                return;
            }

            // 거리가 멀수록 끌어당기는 힘(이동량)이 커지는 지수 감쇠 방식.
            float t = 1f - Mathf.Exp(-pullStrength * deltaTime);
            Vector3 nextPosition = transform.position + offset * t;

            if (maxLagDistance > 0f)
            {
                Vector3 nextOffset = desiredPosition - nextPosition;
                if (nextOffset.magnitude > maxLagDistance)
                {
                    nextPosition = desiredPosition - nextOffset.normalized * maxLagDistance;
                }
            }

            transform.position = nextPosition;
        }
    }
}
