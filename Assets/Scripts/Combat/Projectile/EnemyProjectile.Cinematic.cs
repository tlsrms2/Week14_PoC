using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private bool ignoresExecutionPause;
        private Transform cinematicClearanceTarget;
        private float cinematicClearanceDistance;

        public void ConfigureExecutionPauseIgnored(bool ignored)
        {
            ignoresExecutionPause = ignored;
            if (ignored)
            {
                ResumeFromExecutionPause();
            }
        }

        public void ConfigureCinematicPlayerClearance(Transform target, float distance)
        {
            cinematicClearanceTarget = target;
            cinematicClearanceDistance = Mathf.Max(0f, distance);
        }

        public void ReleaseCinematicPlayerClearance()
        {
            cinematicClearanceTarget = null;
            cinematicClearanceDistance = 0f;
        }

        private bool ApplyCinematicPlayerClearance()
        {
            if (cinematicClearanceTarget == null
                || cinematicClearanceDistance <= 0f
                || reflectedByPlayer)
            {
                return false;
            }

            Vector2 projectilePosition = transform.position;
            Vector2 targetPosition = cinematicClearanceTarget.position;
            Vector2 fromTarget = projectilePosition - targetPosition;
            float distance = fromTarget.magnitude;
            Vector2 awayDirection = distance > 0.0001f
                ? fromTarget / distance
                : flightDirection.sqrMagnitude > 0.0001f
                    ? -flightDirection.normalized
                    : Vector2.right;
            bool movingTowardTarget = Vector2.Dot(flightDirection, -awayDirection) > 0f;
            if (!movingTowardTarget)
            {
                return false;
            }

            float nextMovementAllowance = projectileSpeed
                * EnemyTimeScale.Current
                * Mathf.Max(Time.fixedDeltaTime, Time.deltaTime)
                * 1.5f;
            if (distance > cinematicClearanceDistance + nextMovementAllowance)
            {
                return false;
            }

            Vector2 safePosition =
                targetPosition + awayDirection * cinematicClearanceDistance;
            if (body != null)
            {
                body.position = safePosition;
                body.linearVelocity = Vector2.zero;
            }
            else
            {
                Vector3 position = transform.position;
                position.x = safePosition.x;
                position.y = safePosition.y;
                transform.position = position;
            }

            return true;
        }

        private void ResetCinematicRuntimeState()
        {
            ignoresExecutionPause = false;
            cinematicClearanceTarget = null;
            cinematicClearanceDistance = 0f;
        }
    }
}
