using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class HogPatternShotEmitter
    {
        public delegate EnemyProjectile FireProjectile(
            HogBossAI.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction);

        public delegate EnemyProjectile FireConfiguredProjectile(
            HogBossAI.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool aimAtPlayerWhileCharging,
            bool aimAtPlayerOnLaunch,
            bool suppressHoming,
            float chargeSecondsOverride,
            float radiusOverride,
            Vector3? muzzleFlashPosition,
            float muzzleFlashScale);

        public delegate Vector3 GetIndexedOrigin(int index);

        public static void FirePattern4Wave(
            HogBossAI.Pattern4Settings settings,
            Vector3 center,
            float startAngleDegrees,
            FireProjectile fireWithoutPlayerAim)
        {
            if (settings == null || fireWithoutPlayerAim == null)
            {
                return;
            }

            SoundManager.PlaySfx("Smash");
            int count = Mathf.Max(1, settings.BulletCount);
            float step = 360f / count;
            float radius = Mathf.Max(0f, settings.SpawnRadius);

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(startAngleDegrees + step * i);
                Vector3 origin = center + (Vector3)(direction * radius);
                fireWithoutPlayerAim(settings.Projectile, origin, direction);
            }
        }

        public static void FireMachinegunBullet(
            HogBossAI.Pattern2Settings pattern2,
            int bulletIndex,
            Vector3 origin,
            Vector2 direction,
            bool hasConfiguredOrigin,
            FireConfiguredProjectile fireConfigured)
        {
            if (pattern2 == null || fireConfigured == null)
            {
                return;
            }

            Vector2 side = new(-direction.y, direction.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern2.SpawnSpacing);
            Vector3 spawnPosition = hasConfiguredOrigin ? origin : origin + offset;
            bool aimAtPlayerWhileCharging = pattern2.Projectile != null && pattern2.Projectile.AimAtPlayerWhileCharging;
            EnemyProjectile projectile = fireConfigured(
                pattern2.Projectile,
                spawnPosition,
                direction,
                aimAtPlayerWhileCharging,
                false,
                false,
                -1f,
                -1f,
                origin,
                0f);
            if (projectile == null)
            {
                return;
            }

            HogPatternEffects.PlayOriginBurst(pattern2.Effects, origin);
            HogPatternEffects.PlaySfxOnLaunch(projectile, "BossSpecialShot");
            SoundManager.PlaySfx("BossNormalShot");
        }

        public static void FirePattern7NormalVolley(
            HogBossAI.Pattern7Settings pattern7,
            Vector3 origin,
            Vector2 lockedDirection,
            FireProjectile fireWithoutPlayerAim)
        {
            if (pattern7 == null || fireWithoutPlayerAim == null)
            {
                return;
            }

            if (lockedDirection.sqrMagnitude <= 0.0001f)
            {
                lockedDirection = Vector2.right;
            }

            float baseAngle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
            float halfFanAngle = pattern7.FanAngleDegrees * 0.5f;
            float spacing = Mathf.Max(0f, pattern7.NormalSpawnSpacing);

            for (int i = 0; i < 3; i++)
            {
                float t = i - 1f;
                Vector2 direction = AngleToDirection(baseAngle + halfFanAngle * t);
                Vector2 side = new(-lockedDirection.y, lockedDirection.x);
                Vector3 spawnPosition = origin + (Vector3)(side * spacing * t);
                fireWithoutPlayerAim(pattern7.NormalProjectile, spawnPosition, direction);
            }

            HogPatternEffects.PlayMuzzleFlashIfEnabled(pattern7.Effects, origin, lockedDirection);
            HogPatternEffects.PlayCameraShakeIfEnabled(pattern7.Effects, lockedDirection);
            SoundManager.PlaySfx("BossNormalShot");
        }

        public static void FirePattern7SpecialProjectiles(
            HogBossAI.Pattern7Settings pattern7,
            Vector2 lockedDirection,
            GetIndexedOrigin getOrigin,
            FireProjectile fireProjectile)
        {
            if (pattern7 == null || getOrigin == null || fireProjectile == null)
            {
                return;
            }

            if (lockedDirection.sqrMagnitude <= 0.0001f)
            {
                lockedDirection = Vector2.right;
            }

            int count = Mathf.Max(0, pattern7.SpecialBulletCount);
            bool firedAny = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = getOrigin(i);
                Vector3 specialOrigin = origin + (Vector3)(lockedDirection.normalized * Mathf.Max(0f, pattern7.SpecialSpawnForwardOffset));
                EnemyProjectile specialProjectile = fireProjectile(pattern7.SpecialProjectile, specialOrigin, lockedDirection);
                if (specialProjectile != null)
                {
                    firedAny = true;
                    HogPatternEffects.PlaySfxOnLaunch(specialProjectile, "BossSpecialShot");
                }
            }

            if (firedAny)
            {
                SoundManager.PlaySfx("BossNormalShot");
            }
        }

        public static void FirePattern5Bullet(
            HogBossAI.Pattern5Settings pattern5,
            int bulletIndex,
            float finalAngleDegrees,
            Vector3 origin,
            FireConfiguredProjectile fireConfigured)
        {
            if (pattern5 == null || fireConfigured == null)
            {
                return;
            }

            Vector2 finalDirection = AngleToDirection(finalAngleDegrees);
            Vector2 side = new(-finalDirection.y, finalDirection.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern5.SpawnSpacing);
            Vector3 spawnPosition = origin + offset;

            EnemyProjectile projectile = fireConfigured(
                pattern5.Projectile,
                spawnPosition,
                finalDirection,
                false,
                false,
                false,
                0f,
                -1f,
                origin,
                0f);
            if (projectile == null)
            {
                return;
            }

            HogPatternEffects.PlayMuzzleFlashIfEnabled(pattern5.Effects, origin, finalDirection);
            HogPatternEffects.PlayCameraShakeIfEnabled(pattern5.Effects, finalDirection);
            SoundManager.PlaySfx("BossNormalShot");
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float GetAlternatingOffset(int index, float spacing)
        {
            if (index <= 0 || spacing <= 0f)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 0 ? -1f : 1f;
            return ring * spacing * sign;
        }
    }
}
