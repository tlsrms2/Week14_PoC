using UnityEngine;

namespace Week14.Bootstrap
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float followSpeed = 12f;
        [SerializeField, Range(0f, 1f)] private float focusWeight = 0.5f;
        [SerializeField, Min(0f)] private float focusBlendSpeed = 8f;

        private Rigidbody2D targetBody;
        private Transform focusTarget;
        private Rigidbody2D focusBody;
        private Vector3 followVelocity;
        private Vector3 lastFocusPosition;
        private float currentFocusWeight;
        private float focusWeightVelocity;

        private void Awake()
        {
            CacheTargetBody();
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            followVelocity = Vector3.zero;
            currentFocusWeight = 0f;
            focusWeightVelocity = 0f;
            CacheTargetBody();
        }

        public void SetFocusTarget(Transform nextFocusTarget)
        {
            if (nextFocusTarget != null && focusTarget == nextFocusTarget)
            {
                return;
            }

            focusTarget = nextFocusTarget;
            CacheFocusBody();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = GetTargetPosition() + offset;
            if (followSpeed <= 0f)
            {
                transform.position = desiredPosition;
                followVelocity = Vector3.zero;
                return;
            }

            float smoothTime = 1f / followSpeed;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        private void CacheTargetBody()
        {
            targetBody = target != null ? target.GetComponentInParent<Rigidbody2D>() : null;
            if (targetBody != null)
            {
                targetBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        private void CacheFocusBody()
        {
            focusBody = focusTarget != null ? focusTarget.GetComponentInParent<Rigidbody2D>() : null;
            if (focusBody != null)
            {
                focusBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        private Vector3 GetTargetPosition()
        {
            Vector3 targetPosition = targetBody != null ? targetBody.transform.position : target.position;
            float targetFocusWeight = focusTarget != null ? focusWeight : 0f;
            if (focusBlendSpeed <= 0f)
            {
                currentFocusWeight = targetFocusWeight;
            }
            else
            {
                currentFocusWeight = Mathf.SmoothDamp(
                    currentFocusWeight,
                    targetFocusWeight,
                    ref focusWeightVelocity,
                    1f / focusBlendSpeed,
                    Mathf.Infinity,
                    Time.deltaTime);
            }

            if (focusTarget == null)
            {
                return currentFocusWeight > 0.001f
                    ? Vector3.Lerp(targetPosition, lastFocusPosition, currentFocusWeight)
                    : targetPosition;
            }

            Vector3 focusPosition = focusBody != null ? focusBody.transform.position : focusTarget.position;
            lastFocusPosition = focusPosition;
            return Vector3.Lerp(targetPosition, focusPosition, currentFocusWeight);
        }
    }
}
