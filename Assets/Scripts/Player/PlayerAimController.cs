using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    internal sealed class PlayerAimController
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private float leftGunAimLockedUntil;
        private Vector2 leftGunLockedDirection;
        private bool leftGunLockIncludesBody;

        internal PlayerAimController(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        internal void RotateToAim()
        {
            bool leftGunLocked = context.LockOnTarget == null && Time.time <= leftGunAimLockedUntil;

            // 레일건처럼 몸통까지 같이 고정해야 하는 무기는(leftGunLockIncludesBody) 빔이 떠 있는 동안
            // 몸통이 마우스를 따라 반전/회전하면 총구에 붙은 빔도 같이 뒤틀려 보이는 문제가 있어,
            // 이 경우엔 몸통도 같은 방향으로 묶어 둡니다.
            Vector2 bodyDirection = leftGunLocked && leftGunLockIncludesBody
                ? leftGunLockedDirection
                : GetAimDirection(context.CombatCenterOrigin);
            context.Visual?.SetBodyAimDirection(bodyDirection);

            Vector2 leftDirection = leftGunLocked ? leftGunLockedDirection : GetAimDirection(context.LeftGunOrigin);
            context.Visual?.SetLeftArmAimDirection(leftDirection);
        }

        internal void LockLeftGunAim(Vector2 direction)
        {
            LockLeftGunAim(direction, context.Config != null ? context.Config.GunAimHoldSeconds : 0f);
        }

        // 무기별로 조준 고정 시간을 직접 지정해야 할 때(예: 레일건 - 빔 이펙트가 사라질 때까지 유지) 쓰는 오버로드입니다.
        // lockBodyToo가 true면 팔뿐 아니라 몸통 페이싱/반전까지 같은 방향으로 묶어 둡니다.
        internal void LockLeftGunAim(Vector2 direction, float holdSeconds, bool lockBodyToo = false)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            leftGunLockedDirection = direction.normalized;
            leftGunAimLockedUntil = Time.time + Mathf.Max(0f, holdSeconds);
            leftGunLockIncludesBody = lockBodyToo;
            context.Visual?.SetLeftArmAimDirection(leftGunLockedDirection);
            if (lockBodyToo)
            {
                context.Visual?.SetBodyAimDirection(leftGunLockedDirection);
            }
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
