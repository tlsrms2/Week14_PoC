using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void AimAtPlayerWhileCharging(float spreadDegrees = 0f)
        {
            PlayerCombatController target = PlayerCombatController.Active;
            if (target == null || target.Health == null || target.Health.IsDead)
            {
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            flightDirection = toTarget.normalized;
            if (spreadDegrees > 0f)
            {
                flightDirection = RotateDirection(flightDirection, Random.Range(-spreadDegrees * 0.5f, spreadDegrees * 0.5f)).normalized;
            }

            float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void TickHoming()
        {
            if (!homingEnabled || resolved || isDestroying || Time.time >= homingEndsAt || homingTurnDegreesPerSecond <= 0f || projectileSpeed <= 0f)
            {
                return;
            }

            PlayerCombatController target = PlayerCombatController.Active;
            if (target == null || target.Health == null || target.Health.IsDead)
            {
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float maxRadians = homingTurnDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
            Vector3 nextDirection = Vector3.RotateTowards(flightDirection, toTarget.normalized, maxRadians, 0f);
            flightDirection = ((Vector2)nextDirection).normalized;
            body.linearVelocity = flightDirection * projectileSpeed;
            float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

    }
}
