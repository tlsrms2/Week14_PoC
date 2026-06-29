using UnityEngine;
using Week14.Input;

namespace Week14.Bootstrap
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float followSpeed = 12f;
        [SerializeField, Min(0f)] private float focusBlendSpeed = 8f;
        [SerializeField, Min(0f)] private float mouseLookMaxOffset = 1.2f;
        [SerializeField, Range(0f, 0.45f)] private float mouseLookDeadZone = 0.08f;
        [SerializeField, Min(0f)] private float mouseLookBlendSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float lockOnMouseLookMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float lockOnTransitionSmoothTime = 0.45f;
        [SerializeField, Min(0f)] private float focusPositionSmoothTime = 0.28f;
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
        private Vector3 currentFocusPosition;
        private Vector3 focusPositionVelocity;
        private Vector2 currentMouseLookOffset;
        private Vector2 mouseLookOffsetVelocity;
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
        private bool hasCurrentFocusPosition;
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
            currentFocusPosition = Vector3.zero;
            focusPositionVelocity = Vector3.zero;
            hasCurrentFocusPosition = false;
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

        public bool IsCinematicZoomSettled(float toleranceRatio = 0.02f)
        {
            if (controlledCamera == null || !controlledCamera.orthographic || baseOrthographicSize <= 0f)
            {
                return true;
            }

            float targetSize = baseOrthographicSize * cinematicZoomMultiplier;
            float tolerance = baseOrthographicSize * Mathf.Max(0f, toleranceRatio);
            return Mathf.Abs(controlledCamera.orthographicSize - targetSize) <= tolerance;
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
            float activeFocusWeight = cinematicFocusActive ? cinematicFocusWeight : 0.5f;
            float targetFocusWeight = focusTarget != null ? activeFocusWeight : 0f;
            float focusSmoothTime = GetLockOnBlendSmoothTime(focusBlendSpeed);
            if (focusSmoothTime <= 0f)
            {
                currentFocusWeight = targetFocusWeight;
            }
            else
            {
                currentFocusWeight = Mathf.SmoothDamp(
                    currentFocusWeight,
                    targetFocusWeight,
                    ref focusWeightVelocity,
                    focusSmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime);
            }

            if (focusTarget == null)
            {
                Vector3 position = currentFocusWeight > 0.001f
                    ? Vector3.Lerp(targetPosition, lastFocusPosition, currentFocusWeight)
                    : targetPosition;
                return position + (Vector3)GetMouseLookOffset(false);
            }

            Vector3 focusPosition = focusBody != null ? focusBody.transform.position : focusTarget.position;
            currentFocusPosition = GetSmoothedFocusPosition(focusPosition);
            lastFocusPosition = currentFocusPosition;
            return Vector3.Lerp(targetPosition, currentFocusPosition, currentFocusWeight)
                + (Vector3)GetMouseLookOffset(true);
        }

        private Vector3 GetSmoothedFocusPosition(Vector3 targetFocusPosition)
        {
            if (currentFocusWeight <= 0.001f || !hasCurrentFocusPosition)
            {
                currentFocusPosition = targetFocusPosition;
                focusPositionVelocity = Vector3.zero;
                hasCurrentFocusPosition = true;
                return currentFocusPosition;
            }

            if (cinematicFocusActive || focusPositionSmoothTime <= 0f)
            {
                currentFocusPosition = targetFocusPosition;
                focusPositionVelocity = Vector3.zero;
                hasCurrentFocusPosition = true;
                return currentFocusPosition;
            }

            currentFocusPosition = Vector3.SmoothDamp(
                currentFocusPosition,
                targetFocusPosition,
                ref focusPositionVelocity,
                focusPositionSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
            hasCurrentFocusPosition = true;
            return currentFocusPosition;
        }

        private Vector2 GetMouseLookOffset(bool hasFocusTarget)
        {
            Vector2 targetOffset = Vector2.zero;
            if (!cinematicFocusActive && controlledCamera != null && mouseLookMaxOffset > 0f)
            {
                Vector2 screenPosition = GameInput.MouseScreenPosition;
                Vector2 screenSize = new Vector2(Screen.width, Screen.height);
                if (screenSize.x > 0f && screenSize.y > 0f)
                {
                    Vector2 normalized = new Vector2(
                        Mathf.Clamp01(screenPosition.x / screenSize.x) - 0.5f,
                        Mathf.Clamp01(screenPosition.y / screenSize.y) - 0.5f) * 2f;
                    float magnitude = normalized.magnitude;
                    if (magnitude > mouseLookDeadZone)
                    {
                        float strength = Mathf.InverseLerp(mouseLookDeadZone, 1f, Mathf.Min(1f, magnitude));
                        float multiplier = hasFocusTarget ? lockOnMouseLookMultiplier : 1f;
                        targetOffset = normalized.normalized * strength * mouseLookMaxOffset * multiplier;
                    }
                }
            }

            if (mouseLookBlendSpeed <= 0f)
            {
                currentMouseLookOffset = targetOffset;
            }
            else
            {
                currentMouseLookOffset = Vector2.SmoothDamp(
                    currentMouseLookOffset,
                    targetOffset,
                    ref mouseLookOffsetVelocity,
                    1f / mouseLookBlendSpeed,
                    Mathf.Infinity,
                    Time.deltaTime);
            }

            return currentMouseLookOffset;
        }

        private float GetLockOnBlendSmoothTime(float blendSpeed)
        {
            float speedSmoothTime = blendSpeed > 0f ? 1f / blendSpeed : 0f;
            if (cinematicFocusActive)
            {
                return speedSmoothTime;
            }

            return Mathf.Max(speedSmoothTime, lockOnTransitionSmoothTime);
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
