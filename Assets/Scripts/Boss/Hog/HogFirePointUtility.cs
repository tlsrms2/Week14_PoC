using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogFirePointUtility
    {
        public static void SetActive(HogBossAI.FirePoint firePoint, bool active)
        {
            firePoint?.SetActive(active);
        }

        public static void Rotate(HogBossAI.FirePoint firePoint, Vector2 direction)
        {
            firePoint?.RotateRight(direction);
        }

        public static void RotateToPlayer(
            HogBossAI.FirePoint firePoint,
            Transform player,
            Vector3 fallbackProjectilePosition)
        {
            if (firePoint == null || firePoint.FireOrigin == null || player == null)
            {
                return;
            }

            Vector3 origin = GetProjectilePosition(firePoint, fallbackProjectilePosition);
            Vector2 direction = (Vector2)(player.position - origin);
            Rotate(firePoint, direction);
        }

        public static Transform GetProjectileTransform(HogBossAI.FirePoint firePoint)
        {
            if (firePoint == null)
            {
                return null;
            }

            return firePoint.ProjectileOrigin != null ? firePoint.ProjectileOrigin : firePoint.FireOrigin;
        }

        public static Vector3 GetProjectilePosition(
            HogBossAI.FirePoint firePoint,
            Vector3 fallbackProjectilePosition)
        {
            Transform origin = GetProjectileTransform(firePoint);
            return origin != null ? origin.position : fallbackProjectilePosition;
        }

        public static bool Contains(
            HogBossAI.FirePoint firePoint,
            Transform target,
            Transform bodyRoot)
        {
            return firePoint != null && firePoint.Contains(target, bodyRoot);
        }
    }
}
