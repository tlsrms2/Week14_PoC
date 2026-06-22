using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class HogProjectileEmitter
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

        public static EnemyProjectile FireWithoutPlayerAim(
            SpawnProjectile spawnProjectile,
            HogBossAI.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return Fire(spawnProjectile, settings, origin, direction, false, false, false, -1f, -1f);
        }

        public static EnemyProjectile FireWithPlayerLaunchAim(
            SpawnProjectile spawnProjectile,
            HogBossAI.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return Fire(spawnProjectile, settings, origin, direction, false, true, false, -1f, -1f);
        }

        public static EnemyProjectile Fire(
            SpawnProjectile spawnProjectile,
            HogBossAI.ProjectileSettings settings,
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
                false,
                false,
                -1f,
                -1f);
        }

        public static EnemyProjectile Fire(
            SpawnProjectile spawnProjectile,
            HogBossAI.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool aimAtPlayerWhileCharging,
            bool aimAtPlayerOnLaunch,
            bool suppressHoming,
            float chargeSecondsOverride,
            float radiusOverride,
            Vector3? muzzleFlashPosition = null,
            float muzzleFlashScale = 0f)
        {
            if (spawnProjectile == null || settings == null || settings.Prefab == null)
            {
                return null;
            }

            float chargeSeconds = chargeSecondsOverride >= 0f ? chargeSecondsOverride : settings.ChargeSeconds;
            float radius = radiusOverride > 0f ? radiusOverride : settings.Radius;
            Color chargeColor = settings.ChargingColor;
            Color projectileColor = settings.LaunchedColor;
            Color homingBlinkColor = settings.HomingBlinkColor;
            Color trailColor = settings.TrailColor;
            Color indicatorColor = settings.IndicatorColor;
            bool homingEnabled = settings.HomingEnabled && !suppressHoming;
            EnemyProjectile spawnPrefab = homingEnabled && chargeSeconds > 0f && settings.HomingChargePrefab != null
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

            projectile?.ConfigureStateColors(chargeColor, projectileColor, homingBlinkColor);
            projectile?.ConfigureTrailColor(trailColor);
            projectile?.ConfigureIndicatorColor(indicatorColor);
            projectile?.ConfigureLaunchReplacementPrefab(spawnPrefab != settings.Prefab ? settings.Prefab : null);
            projectile?.ConfigureChargeMotion(settings.ChargeDriftSpeed, aimAtPlayerWhileCharging, aimAtPlayerOnLaunch);
            return projectile;
        }
    }
}
