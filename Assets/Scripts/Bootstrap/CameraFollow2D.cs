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
        [SerializeField, Min(0f)] private float shakeFrequency = 34f;
        [SerializeField, Min(0f)] private float zoomBlendSpeed = 10f;

        private Camera controlledCamera;
        private Rigidbody2D targetBody;
        private Transform focusTarget;
        private Transform pendingFocusTarget;
        private Rigidbody2D focusBody;
        private Vector3 followVelocity;
        private Vector3 currentBasePosition;
        private Vector3 lastFocusPosition;
        private float currentFocusWeight;
        private float focusWeightVelocity;
        private float baseOrthographicSize;
        private float zoomVelocity;
        private float zoomKick;
        private float zoomKickDuration;
        private float zoomKickEndsAt;
        private float shakeAmplitude;
        private float shakeDuration;
        private float shakeEndsAt;
        private float shakeSeed;
        private Vector2 shakeDirection = Vector2.right;
        private bool hasBasePosition;
        private bool cinematicFocusActive;
        private float cinematicFocusWeight = 0.5f;
        private float cinematicZoomMultiplier = 1f;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            if (controlledCamera != null)
            {
                baseOrthographicSize = controlledCamera.orthographicSize;
            }

            CacheTargetBody();
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            followVelocity = Vector3.zero;
            currentFocusWeight = 0f;
            focusWeightVelocity = 0f;
            hasBasePosition = false;
            CacheTargetBody();
        }

        public void SetFocusTarget(Transform nextFocusTarget)
        {
            if (cinematicFocusActive)
            {
                pendingFocusTarget = nextFocusTarget;
                return;
            }

            ApplyFocusTarget(nextFocusTarget);
        }

        public void PlayImpact(Vector2 direction, float amplitude, float seconds, float zoomAmount = 0f)
        {
            if (amplitude > 0f && seconds > 0f)
            {
                shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
                shakeDuration = Mathf.Max(0.01f, seconds);
                shakeEndsAt = Time.time + shakeDuration;
                shakeSeed = Random.value * 1000f;
                Vector2 fallbackDirection = Random.insideUnitCircle;
                shakeDirection = direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
            }

            if (zoomAmount > 0f && controlledCamera != null && controlledCamera.orthographic)
            {
                zoomKick = Mathf.Max(zoomKick, zoomAmount);
                zoomKickDuration = Mathf.Max(0.01f, seconds);
                zoomKickEndsAt = Time.time + zoomKickDuration;
            }
        }

        public void BeginCinematicFocus(Transform nextFocusTarget, float weight, float zoomMultiplier)
        {
            if (!cinematicFocusActive)
            {
                pendingFocusTarget = focusTarget;
            }

            cinematicFocusActive = true;
            cinematicFocusWeight = Mathf.Clamp01(weight);
            cinematicZoomMultiplier = Mathf.Clamp(zoomMultiplier, 0.35f, 1f);
            ApplyFocusTarget(nextFocusTarget);
        }

        public void EndCinematicFocus()
        {
            if (!cinematicFocusActive)
            {
                return;
            }

            cinematicFocusActive = false;
            cinematicZoomMultiplier = 1f;
            ApplyFocusTarget(pendingFocusTarget);
            pendingFocusTarget = null;
        }

        private void ApplyFocusTarget(Transform nextFocusTarget)
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
                currentBasePosition = desiredPosition;
                hasBasePosition = true;
                transform.position = currentBasePosition + GetShakeOffset();
                followVelocity = Vector3.zero;
                UpdateCameraZoom();
                return;
            }

            if (!hasBasePosition)
            {
                currentBasePosition = transform.position;
                hasBasePosition = true;
            }

            float smoothTime = 1f / followSpeed;
            currentBasePosition = Vector3.SmoothDamp(
                currentBasePosition,
                desiredPosition,
                ref followVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
            transform.position = currentBasePosition + GetShakeOffset();
            UpdateCameraZoom();
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
            float activeFocusWeight = cinematicFocusActive ? cinematicFocusWeight : focusWeight;
            float targetFocusWeight = focusTarget != null ? activeFocusWeight : 0f;
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

        private Vector3 GetShakeOffset()
        {
            float remaining = shakeEndsAt - Time.time;
            if (remaining <= 0f || shakeDuration <= 0f || shakeAmplitude <= 0f)
            {
                shakeAmplitude = 0f;
                return Vector3.zero;
            }

            float t = 1f - remaining / shakeDuration;
            float fade = 1f - Mathf.Clamp01(t);
            float pulse = Mathf.Sin(t * Mathf.PI * 6f);
            float noise = (Mathf.PerlinNoise(shakeSeed, Time.time * shakeFrequency) - 0.5f) * 2f;
            Vector2 side = new Vector2(-shakeDirection.y, shakeDirection.x);
            Vector2 offset = (shakeDirection * pulse + side * noise * 0.65f) * shakeAmplitude * fade * fade;
            return new Vector3(offset.x, offset.y, 0f);
        }

        private void UpdateCameraZoom()
        {
            if (controlledCamera == null || !controlledCamera.orthographic)
            {
                return;
            }

            if (baseOrthographicSize <= 0f)
            {
                baseOrthographicSize = controlledCamera.orthographicSize;
            }

            float targetSize = baseOrthographicSize * cinematicZoomMultiplier;
            float remaining = zoomKickEndsAt - Time.time;
            if (remaining > 0f && zoomKickDuration > 0f)
            {
                float t = 1f - remaining / zoomKickDuration;
                targetSize -= zoomKick * (1f - Mathf.Clamp01(t));
            }

            targetSize = Mathf.Max(0.5f, targetSize);
            if (zoomBlendSpeed <= 0f)
            {
                controlledCamera.orthographicSize = targetSize;
                return;
            }

            controlledCamera.orthographicSize = Mathf.SmoothDamp(
                controlledCamera.orthographicSize,
                targetSize,
                ref zoomVelocity,
                1f / zoomBlendSpeed,
                Mathf.Infinity,
                Time.deltaTime);
        }
    }
}
