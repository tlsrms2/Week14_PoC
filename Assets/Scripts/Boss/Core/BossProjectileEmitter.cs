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
                settings.ChargingColor,
                settings.LaunchedColor,
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
            Color chargeColor,
            Color projectileColor,
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
            bool homingEnabled = settings.HomingEnabled && !suppressHoming;
            EnemyProjectile spawnPrefab = spawnPrefabOverride != null
                ? spawnPrefabOverride
                : homingEnabled && chargeSeconds > 0f && settings.HomingChargePrefab != null
                    ? settings.HomingChargePrefab
                    : settings.Prefab;
            EnemyProjectile projectile = spawnProjectile(
                spawnPrefab,
                origin,
                direction,
                settings.BulletDamage,
                chargeSeconds,
                settings.Speed,
                settings.Lifetime,
                radius,
                projectileColor,
                settings.TrailSeconds,
                settings.TrailWidthMultiplier,
                homingEnabled,
                settings.HomingSeconds,
                settings.HomingTurnDegreesPerSecond,
                muzzleFlashPosition,
                muzzleFlashScale);

            Color homingBlinkColor = settings.HasHomingBlinkColor ? settings.HomingBlinkColor : projectileColor;
            projectile?.ConfigureStateColors(chargeColor, projectileColor, homingBlinkColor);
            if (settings.HasTrailColor)
            {
                projectile?.ConfigureTrailColor(settings.TrailColor);
            }

            if (settings.HasIndicatorColor)
            {
                projectile?.ConfigureIndicatorColor(settings.IndicatorColor);
            }

            projectile?.ConfigureLaunchReplacementPrefab(spawnPrefab != settings.Prefab ? settings.Prefab : null);
            projectile?.ConfigureChargeMotion(settings.ChargeDriftSpeed, aimAtPlayerWhileCharging, aimAtPlayerOnLaunch);
            return projectile;
        }
    }
}
