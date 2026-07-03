using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class BossProjectileEmitter
    {
        public delegate EnemyProjectile SpawnProjectile(
            EnemyProjectile prefab,
            Vector3 position,
            Vector2 direction,
            int projectileBulletDamage,
            float chargeSeconds,
            float speed,
            float lifetime,
            float radius,
            Color color,
            float trailSeconds,
            float trailWidth,
            bool homingEnabled,
            float homingSeconds,
            float homingTurnDegrees,
            Vector3? muzzleFlashPosition,
            float muzzleFlashScale);

        public static EnemyProjectile Fire(
            SpawnProjectile spawnProjectile,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            if (settings == null)
            {
                return null;
            }

            return Fire(
                spawnProjectile,
                settings,
                origin,
                direction,
                settings.AimAtPlayerWhileCharging,
                settings.AimAtPlayerOnLaunch,
                false,
                -1f,
                -1f,
                null,
                0f,
                null);
        }

        public static EnemyProjectile Fire(
            SpawnProjectile spawnProjectile,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool aimAtPlayerWhileCharging,
            bool aimAtPlayerOnLaunch,
            bool suppressHoming,
            float chargeSecondsOverride,
            float radiusOverride,
            Vector3? muzzleFlashPosition,
            float muzzleFlashScale,
            EnemyProjectile spawnPrefabOverride)
        {
            if (spawnProjectile == null || settings == null || settings.Prefab == null)
            {
                return null;
            }

            float chargeSeconds = chargeSecondsOverride >= 0f ? chargeSecondsOverride : settings.ChargeSeconds;
            float radius = radiusOverride > 0f ? radiusOverride : settings.Radius;
            EnemyProjectile spawnPrefab = spawnPrefabOverride != null
                ? spawnPrefabOverride
                : settings.Prefab;
            bool homingEnabled = spawnPrefab is HomingEnemyProjectile && !suppressHoming;
            EnemyProjectile projectile = spawnProjectile(
                spawnPrefab,
                origin,
                direction,
                settings.BulletDamage,
                chargeSeconds,
                settings.Speed,
                settings.Lifetime,
                radius,
                Color.clear,
                settings.TrailSeconds,
                settings.TrailWidthMultiplier,
                homingEnabled,
                0f,
                0f,
                muzzleFlashPosition,
                muzzleFlashScale);

            projectile?.ConfigureChargeMotion(settings.ChargeDriftSpeed, aimAtPlayerWhileCharging, aimAtPlayerOnLaunch);
            return projectile;
        }
    }
}
