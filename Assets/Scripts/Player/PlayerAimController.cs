using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    internal sealed class PlayerAimController
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private float leftGunAimLockedUntil;
        private Vector2 leftGunLockedDirection;

        internal PlayerAimController(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        internal void RotateToAim()
        {
            Vector2 bodyDirection = GetAimDirection(context.CombatCenterOrigin);
            context.Visual?.SetBodyAimDirection(bodyDirection);

            Vector2 leftDirection = context.LockOnTarget == null && Time.time <= leftGunAimLockedUntil
                ? leftGunLockedDirection
                : GetAimDirection(context.LeftGunOrigin);
            context.Visual?.SetLeftArmAimDirection(leftDirection);
        }

        internal void LockLeftGunAim(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            leftGunLockedDirection = direction.normalized;
            leftGunAimLockedUntil = Time.time + (context.Config != null ? context.Config.GunAimHoldSeconds : 0f);
            context.Visual?.SetLeftArmAimDirection(leftGunLockedDirection);
        }

        internal void AimExecutionPose(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalized = direction.normalized;
            context.Visual?.SetBodyAimDirection(normalized);
            context.Visual?.SetLeftArmAimDirection(normalized);
        }

        internal Vector2 AimGunAndGetDirection(Transform gun, Vector2 desiredDirection)
        {
            Vector2 normalized = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : Vector2.right;
            if (gun == context.LeftGunOrigin)
            {
                context.Visual?.SetLeftArmAimDirection(normalized);
            }

            return normalized;
        }

        internal Vector2 GetAimDirection(Transform origin)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return origin != null ? (Vector2)origin.right : (Vector2)context.PlayerTransform.right;
            }

            Vector2 aimPoint = GetAimPoint();
            Vector2 originPosition = origin != null ? origin.position : context.PlayerTransform.position;
            Vector2 direction = aimPoint - originPosition;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : (origin != null ? (Vector2)origin.right : (Vector2)context.PlayerTransform.right);
        }

        internal Vector2 GetAimPoint()
        {
            Health lockOnTarget = context.LockOnTarget;
            if (lockOnTarget != null && !lockOnTarget.IsDead)
            {
                return lockOnTarget.transform.position;
            }

            return GetMouseWorldPosition();
        }

        internal Vector2 GetMouseWorldPosition()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Vector2.zero;
            }

            return camera.ScreenToWorldPoint(GameInput.MouseScreenPosition);
        }
    }
}
