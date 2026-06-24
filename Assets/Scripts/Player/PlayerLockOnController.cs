using UnityEngine;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    internal sealed class PlayerLockOnController
    {
        private const float LockOnAcquireViewportPadding = 0f;
        private const float LockOnReleaseViewportPadding = 0.08f;

        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerAimController aimController;

        internal PlayerLockOnController(
            PlayerCombatController.PlayerCombatContext context,
            PlayerAimController aimController)
        {
            this.context = context;
            this.aimController = aimController;
        }

        internal void UpdateLockOnTarget()
        {
            SetLockOnTarget(FindNearestLockOnTarget());
        }

        internal void ClearInvalidLockOnTarget()
        {
            Health lockOnTarget = context.LockOnTarget;
            if (lockOnTarget != null
                && !lockOnTarget.IsDead
                && CanKeepLockOnTarget(lockOnTarget, Camera.main))
            {
                return;
            }

            SetLockOnTarget(null);
        }

        internal void SetLockOnTarget(Health nextTarget)
        {
            if (context.LockOnTarget == nextTarget)
            {
                return;
            }

            context.LockOnTarget = nextTarget;
            CameraFollow2D activeCamera = context.CameraFollow;
            if (activeCamera != null)
            {
                activeCamera.SetFocusTarget(nextTarget != null ? nextTarget.transform : null);
            }
        }

        private Health FindNearestLockOnTarget()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return null;
            }

            Vector2 mousePosition = aimController.GetMouseWorldPosition();
            Health bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            Health[] allTargets = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < allTargets.Length; i++)
            {
                Health targetHealth = allTargets[i];
                ChooseCloserLockOnTarget(targetHealth, camera, mousePosition, ref bestTarget, ref bestDistance);
            }

            return bestTarget;
        }

        private void ChooseCloserLockOnTarget(
            Health targetHealth,
            Camera camera,
            Vector2 mousePosition,
            ref Health bestTarget,
            ref float bestDistance)
        {
            if (!IsValidLockOnTargetInCamera(targetHealth, camera)
                || !CanKeepLockOnTarget(targetHealth, camera))
            {
                return;
            }

            Vector2 targetPoint = GetLockOnMouseComparePoint(targetHealth, mousePosition);
            float distance = Vector2.Distance(mousePosition, targetPoint);
            if (distance >= bestDistance)
            {
                return;
            }

            bestTarget = targetHealth;
            bestDistance = distance;
        }

        private static Vector2 GetLockOnMouseComparePoint(
            Health targetHealth,
            Vector2 mousePosition)
        {
            Collider2D[] colliders = targetHealth != null ? targetHealth.GetComponentsInChildren<Collider2D>() : null;
            if (colliders == null || colliders.Length == 0)
            {
                return targetHealth != null ? targetHealth.transform.position : mousePosition;
            }

            Vector2 bestPoint = targetHealth.transform.position;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 point = collider.ClosestPoint(mousePosition);
                float distance = Vector2.Distance(mousePosition, point);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestPoint = point;
                bestDistance = distance;
            }

            return bestPoint;
        }

        private bool IsValidLockOnTarget(Health targetHealth)
        {
            return targetHealth != null
                && targetHealth != context.Health
                && !targetHealth.IsDead
                && (targetHealth.GetComponent<BossAI>() != null
                    || targetHealth.GetComponentInParent<BossAI>() != null
                    || targetHealth.GetComponent<Minion>() != null
                    || targetHealth.GetComponentInParent<Minion>() != null);
        }

        private bool IsValidLockOnTargetInCamera(Health targetHealth, Camera camera)
        {
            return IsValidLockOnTargetInCamera(targetHealth, camera, LockOnAcquireViewportPadding);
        }

        private bool IsValidLockOnTargetInCamera(Health targetHealth, Camera camera, float viewportPadding)
        {
            return IsValidLockOnTarget(targetHealth)
                && IsTargetVisibleInCamera(targetHealth, camera, viewportPadding);
        }

        private bool CanKeepLockOnTarget(Health targetHealth, Camera camera)
        {
            return IsValidLockOnTarget(targetHealth)
                && CanCameraContainPair(camera, context.PlayerTransform.position, targetHealth.transform.position, LockOnReleaseViewportPadding);
        }

        private static bool CanCameraContainPair(Camera camera, Vector3 firstPosition, Vector3 secondPosition, float viewportPadding)
        {
            if (camera == null || !camera.orthographic)
            {
                return false;
            }

            float padding = Mathf.Clamp01(viewportPadding);
            float availableHalfHeight = camera.orthographicSize * (1f - padding);
            float availableHalfWidth = availableHalfHeight * camera.aspect;
            Vector2 delta = secondPosition - firstPosition;
            return Mathf.Abs(delta.x) * 0.5f <= availableHalfWidth
                && Mathf.Abs(delta.y) * 0.5f <= availableHalfHeight;
        }

        private static bool IsTargetVisibleInCamera(Health targetHealth, Camera camera, float viewportPadding)
        {
            if (targetHealth == null || camera == null)
            {
                return false;
            }

            if (IsWorldPointInCamera(camera, targetHealth.transform.position, viewportPadding))
            {
                return true;
            }

            Collider2D[] colliders = targetHealth.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsBoundsVisibleInCamera(camera, collider.bounds, viewportPadding))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBoundsVisibleInCamera(Camera camera, Bounds bounds, float viewportPadding)
        {
            return IsWorldPointInCamera(camera, bounds.center, viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.min.x, bounds.min.y, bounds.center.z), viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.min.x, bounds.max.y, bounds.center.z), viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.max.x, bounds.min.y, bounds.center.z), viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.max.x, bounds.max.y, bounds.center.z), viewportPadding);
        }

        private static bool IsWorldPointInCamera(Camera camera, Vector3 worldPoint, float viewportPadding)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPoint);
            float padding = Mathf.Max(0f, viewportPadding);
            return viewportPoint.z >= camera.nearClipPlane
                && viewportPoint.z <= camera.farClipPlane
                && viewportPoint.x >= -padding
                && viewportPoint.x <= 1f + padding
                && viewportPoint.y >= -padding
                && viewportPoint.y <= 1f + padding;
        }
    }
}
