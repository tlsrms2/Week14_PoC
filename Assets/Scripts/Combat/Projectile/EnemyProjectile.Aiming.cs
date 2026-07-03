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

            Vector2 direction = toTarget.normalized;
            if (spreadDegrees > 0f)
            {
                direction = RotateDirection(direction, Random.Range(-spreadDegrees * 0.5f, spreadDegrees * 0.5f)).normalized;
            }

            ApplyFlightDirection(direction);
        }
    }
}
