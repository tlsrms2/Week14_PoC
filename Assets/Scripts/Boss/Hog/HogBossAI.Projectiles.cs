using Action = System.Action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        private void FirePattern4Wave(Pattern4Settings settings, float startAngleDegrees)
        {
            if (settings == null)
            {
                return;
            }

            SoundManager.PlaySfx("Smash");
            int count = Mathf.Max(1, settings.BulletCount);
            float step = 360f / count;
            float radius = Mathf.Max(0f, settings.SpawnRadius);
            Vector3 center = GetPattern4ProjectilePosition(settings);
            ProjectileSettings projectileSettings = GetProjectile(settings.ProjectileIndex);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(startAngleDegrees + step * i);
                Vector3 origin = center + (Vector3)(direction * radius);
                FireConfiguredProjectileWithoutPlayerAim(projectileSettings, origin, direction);
            }
        }

        private void FireMachinegunBullet(int bulletIndex)
        {
            bool hasConfiguredOrigin = pattern2.ProjectileOrigins != null && pattern2.ProjectileOrigins.HasAny;
            Vector3 origin = GetAlternatingProjectilePosition(pattern2.ProjectileOrigins, bulletIndex);
            Vector2 direction = GetDirectionToPlayer(origin);
            Vector2 side = new(-direction.y, direction.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern2.SpawnSpacing);
            Vector3 spawnPosition = hasConfiguredOrigin ? origin : origin + offset;
            ProjectileSettings projectileSettings = GetProjectile(pattern2.ProjectileIndex);
            bool aimAtPlayerWhileCharging = projectileSettings != null && projectileSettings.AimAtPlayerWhileCharging;
            EnemyProjectile projectile = FireConfiguredProjectile(
                projectileSettings,
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

            PlayOriginBurst(pattern2.Effects, origin);
            PlaySfxOnLaunch(projectile, "BossSpecialShot");
            SoundManager.PlaySfx("BossNormalShot");
        }

        private void FirePattern7NormalVolley(Vector3 origin, Vector2 lockedDirection)
        {
            if (lockedDirection.sqrMagnitude <= 0.0001f)
            {
                lockedDirection = Vector2.right;
            }

            float baseAngle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
            float halfFanAngle = pattern7.FanAngleDegrees * 0.5f;
            float spacing = Mathf.Max(0f, pattern7.NormalSpawnSpacing);
            ProjectileSettings projectileSettings = GetProjectile(pattern7.NormalProjectileIndex);
            for (int i = 0; i < 3; i++)
            {
                float t = i - 1f;
                Vector2 direction = AngleToDirection(baseAngle + halfFanAngle * t);
                Vector2 side = new(-lockedDirection.y, lockedDirection.x);
                Vector3 spawnPosition = origin + (Vector3)(side * spacing * t);
                FireConfiguredProjectileWithoutPlayerAim(projectileSettings, spawnPosition, direction);
            }

            PlayMuzzleFlashIfEnabled(pattern7.Effects, origin, lockedDirection);
            PlayCameraShakeIfEnabled(pattern7.Effects, lockedDirection);
            SoundManager.PlaySfx("BossNormalShot");
        }

        private void FirePattern7SecondaryProjectiles(Vector2 lockedDirection)
        {
            if (lockedDirection.sqrMagnitude <= 0.0001f)
            {
                lockedDirection = Vector2.right;
            }

            ProjectileSettings projectileSettings = GetProjectile(pattern7.SecondaryProjectileIndex);
            int count = Mathf.Max(0, pattern7.SecondaryBulletCount);
            bool firedAny = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = GetPattern7SecondaryProjectilePosition(i);
                Vector3 spawnPosition = origin + (Vector3)(lockedDirection.normalized * Mathf.Max(0f, pattern7.SecondarySpawnForwardOffset));
                EnemyProjectile projectile = FireConfiguredProjectile(projectileSettings, spawnPosition, lockedDirection);
                if (projectile != null)
                {
                    firedAny = true;
                    PlaySfxOnLaunch(projectile, "BossSpecialShot");
                }
            }

            if (firedAny)
            {
                SoundManager.PlaySfx("BossNormalShot");
            }
        }

        private void FirePattern5Bullet(int bulletIndex, float finalAngleDegrees, Vector3 origin)
        {
            Vector2 finalDirection = AngleToDirection(finalAngleDegrees);
            Vector2 side = new(-finalDirection.y, finalDirection.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern5.SpawnSpacing);
            Vector3 spawnPosition = origin + offset;
            ProjectileSettings projectileSettings = GetProjectile(pattern5.ProjectileIndex);
            EnemyProjectile projectile = FireConfiguredProjectile(
                projectileSettings,
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

            PlayMuzzleFlashIfEnabled(pattern5.Effects, origin, finalDirection);
            PlayCameraShakeIfEnabled(pattern5.Effects, finalDirection);
            SoundManager.PlaySfx("BossNormalShot");
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

        private static void PlayOriginBurst(PatternEffectSettings effects, Vector3 position)
        {
            PlayExplosionIfEnabled(effects, position);
            PlaySmokeIfEnabled(effects, position);
        }

        private static void PlayExplosionIfEnabled(PatternEffectSettings effects, Vector3 position)
        {
            ParticleEffectSettings explosion = effects?.Explosion;
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogExplosion(position, explosion.Color, explosion.Scale, explosion.Count);
        }

        private static void PlaySmokeIfEnabled(PatternEffectSettings effects, Vector3 position)
        {
            ParticleEffectSettings smoke = effects?.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }

        private static void PlayMuzzleFlashIfEnabled(PatternEffectSettings effects, Vector3 origin, Vector2 direction)
        {
            ParticleEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(origin, direction, muzzleFlash.Color, muzzleFlash.Scale);
        }

        private static void PlayCameraShakeIfEnabled(PatternEffectSettings effects, Vector2 direction)
        {
            CameraShakeSettings shake = effects?.CameraShake;
            if (shake == null || !shake.Enabled)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, shake.Seconds, shake.Distance, shake.Frequency);
        }

        private static void PlaySfxOnLaunch(EnemyProjectile projectile, string sfxId)
        {
            if (projectile == null)
            {
                return;
            }

            void HandleLaunched(EnemyProjectile launchedProjectile)
            {
                launchedProjectile.Launched -= HandleLaunched;
                SoundManager.PlaySfx(sfxId);
            }

            projectile.Launched += HandleLaunched;
        }

        private IEnumerator WaitPatternSeconds(float seconds, Action onTick)
        {
            yield return patternMovement.WaitSeconds(seconds, onTick, IsBossExecutionPaused, Stop);
        }

        private IEnumerator WaitWhileExecutionPaused()
        {
            yield return patternMovement.WaitWhileExecutionPaused(IsBossExecutionPaused, Stop);
        }

        private Vector3 GetPattern1SpawnPosition(int shotIndex, Vector2 direction)
        {
            if (pattern1.ProjectileOrigins != null && pattern1.ProjectileOrigins.HasAny)
            {
                return GetAlternatingProjectilePosition(pattern1.ProjectileOrigins, shotIndex);
            }

            float radius = Mathf.Max(0f, pattern1.SpawnRadius);
            if (radius <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return GetDefaultProjectileOrigin();
            }

            return GetDefaultProjectileOrigin() + (Vector3)(direction.normalized * radius);
        }

        private Vector2 GetPattern3Direction(Vector3 origin)
        {
            Vector2 direction = GetDirectionToPlayer(origin);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = pattern3.AimSpreadDegrees * 0.5f;
            return AngleToDirection(baseAngle + Random.Range(-halfSpread, halfSpread));
        }

        private EnemyProjectile FireConfiguredProjectileWithoutPlayerAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return FireConfiguredProjectile(settings, origin, direction, false, false, false, -1f, -1f);
        }

        private EnemyProjectile FireConfiguredProjectileWithPlayerLaunchAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return FireConfiguredProjectile(settings, origin, direction, false, true, false, -1f, -1f);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            if (settings == null)
            {
                return null;
            }

            return FireConfiguredProjectile(
                settings,
                origin,
                direction,
                settings.AimAtPlayerWhileCharging,
                false,
                false,
                -1f,
                -1f);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
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
            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            float chargeSeconds = chargeSecondsOverride >= 0f ? chargeSecondsOverride : settings.ChargeSeconds;
            float radius = radiusOverride > 0f ? radiusOverride : settings.Radius;
            return BossProjectileEmitter.Fire(
                SpawnBossProjectile,
                settings,
                origin,
                direction,
                settings.ChargingColor,
                settings.LaunchedColor,
                aimAtPlayerWhileCharging,
                aimAtPlayerOnLaunch,
                suppressHoming,
                chargeSeconds,
                radius,
                muzzleFlashPosition,
                muzzleFlashScale,
                null);
        }

        private void MoveTowardPlayer(float speedMultiplier)
        {
            if (Player == null || Body == null)
            {
                return;
            }

            Vector2 direction = (Vector2)Player.position - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Body.linearVelocity = Vector2.zero;
                return;
            }

            Body.linearVelocity = direction.normalized * (MoveSpeed * Mathf.Max(0f, speedMultiplier));
        }

        private static bool IsBossExecutionPaused()
        {
            return IsExecutionPaused;
        }

        private Vector3 GetAlternatingProjectilePosition(AlternatingProjectileOrigins origins, int shotIndex)
        {
            Transform origin = origins != null ? origins.Get(shotIndex) : null;
            return origin != null ? origin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetFirePointProjectilePosition(FirePoint firePoint)
        {
            Transform origin = GetFirePointProjectileTransform(firePoint);
            return origin != null ? origin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetPattern7NormalProjectilePosition()
        {
            return GetFirePointProjectilePosition(pattern7.FirePoint);
        }

        private Vector3 GetPattern7SecondaryProjectilePosition(int index)
        {
            IReadOnlyList<Transform> origins = pattern7.SecondaryProjectileOrigins;
            Transform origin = origins != null && index >= 0 && index < origins.Count ? origins[index] : null;
            return origin != null ? origin.position : GetFirePointProjectilePosition(pattern7.FirePoint);
        }

        private Transform GetFirePointProjectileTransform(FirePoint firePoint)
        {
            if (firePoint == null)
            {
                return null;
            }

            return firePoint.ProjectileOrigin != null ? firePoint.ProjectileOrigin : firePoint.FireOrigin;
        }

        private Vector3 GetPattern4ProjectilePosition(Pattern4Settings settings)
        {
            return settings.ProjectileOrigin != null ? settings.ProjectileOrigin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetDefaultProjectileOrigin()
        {
            return BodyRoot != null ? BodyRoot.position : transform.position;
        }

        private void RotateFirePointToPlayer(FirePoint firePoint)
        {
            if (firePoint == null || firePoint.FireOrigin == null || Player == null)
            {
                return;
            }

            Vector3 origin = GetFirePointProjectilePosition(firePoint);
            Vector2 direction = (Vector2)(Player.position - origin);
            RotateFirePoint(firePoint, direction);
        }

        private void RotateFirePoint(FirePoint firePoint, Vector2 direction)
        {
            firePoint?.RotateRight(direction);
        }

        private void SetFirePointActive(FirePoint firePoint, bool active)
        {
            firePoint?.SetActive(active);
        }

        private void DeactivatePatternFirePoints()
        {
            SetFirePointActive(pattern3.FirePoint, false);
            SetFirePointActive(pattern5.FirePoint, false);
            SetFirePointActive(pattern7.FirePoint, false);
        }

    }
}
