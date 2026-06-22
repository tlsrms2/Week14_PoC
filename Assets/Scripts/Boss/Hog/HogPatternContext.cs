using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class HogPatternContext
    {
        internal delegate EnemyProjectile ProjectileFire(
            HogBossAI.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction);

        internal delegate EnemyProjectile ConfiguredProjectileFire(
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

        internal HogPatternContext(
            Action stop,
            Func<bool> isExecutionPaused,
            Func<IEnumerator> waitWhileExecutionPaused,
            Func<float, Action, IEnumerator> waitPatternSeconds,
            Func<int, HogBossAI.ProjectileSettings> getProjectile,
            Action<float> moveTowardPlayer,
            Action<HogBossAI.Pattern4Settings, float> firePattern4Wave,
            Action<int> fireMachinegunBullet,
            Action<int, float, Vector3> firePattern5Bullet,
            Action<Vector3, Vector2> firePattern7NormalVolley,
            Action<Vector2> firePattern7SecondaryProjectiles,
            Func<HogBossAI.Pattern4Settings, IEnumerator> slamPattern4BodyRoot,
            Func<HogBossAI.Pattern4Settings, IEnumerator> recoverPattern4BodyRoot,
            Func<HogBossAI.PatternKind, float, IEnumerator> reloadWavePreview,
            Action resetPattern4BodyRoot,
            Action advancePreviewGroup,
            Action<HogBossAI.FirePoint, bool> setFirePointActive,
            Action<HogBossAI.FirePoint> rotateFirePointToPlayer,
            Action<HogBossAI.FirePoint, Vector2> rotateFirePoint,
            Func<HogBossAI.FirePoint, Vector3> getFirePointProjectilePosition,
            Func<HogBossAI.FirePoint, Transform> getFirePointProjectileTransform,
            Func<Vector3, Vector2> getDirectionToPlayer,
            Func<float, Vector2> angleToDirection,
            Func<int, Vector2, Vector3> getPattern1SpawnPosition,
            Func<Vector3, Vector2> getPattern3Direction,
            Func<Vector3> getPattern7NormalProjectilePosition,
            Action<Vector3, Vector2> updatePattern7GuideLines,
            Action hidePattern7GuideLines,
            ProjectileFire fireConfiguredProjectileWithoutPlayerAim,
            ProjectileFire fireConfiguredProjectileWithPlayerLaunchAim,
            ProjectileFire fireConfiguredProjectile,
            ConfiguredProjectileFire fireConfiguredProjectileWithOptions)
        {
            Stop = stop;
            IsExecutionPaused = isExecutionPaused;
            WaitWhileExecutionPaused = waitWhileExecutionPaused;
            WaitPatternSeconds = waitPatternSeconds;
            GetProjectile = getProjectile;
            MoveTowardPlayer = moveTowardPlayer;
            FirePattern4Wave = firePattern4Wave;
            FireMachinegunBullet = fireMachinegunBullet;
            FirePattern5Bullet = firePattern5Bullet;
            FirePattern7NormalVolley = firePattern7NormalVolley;
            FirePattern7SecondaryProjectiles = firePattern7SecondaryProjectiles;
            SlamPattern4BodyRoot = slamPattern4BodyRoot;
            RecoverPattern4BodyRoot = recoverPattern4BodyRoot;
            ReloadWavePreview = reloadWavePreview;
            ResetPattern4BodyRoot = resetPattern4BodyRoot;
            AdvancePreviewGroup = advancePreviewGroup;
            SetFirePointActive = setFirePointActive;
            RotateFirePointToPlayer = rotateFirePointToPlayer;
            RotateFirePoint = rotateFirePoint;
            GetFirePointProjectilePosition = getFirePointProjectilePosition;
            GetFirePointProjectileTransform = getFirePointProjectileTransform;
            GetDirectionToPlayer = getDirectionToPlayer;
            AngleToDirection = angleToDirection;
            GetPattern1SpawnPosition = getPattern1SpawnPosition;
            GetPattern3Direction = getPattern3Direction;
            GetPattern7NormalProjectilePosition = getPattern7NormalProjectilePosition;
            UpdatePattern7GuideLines = updatePattern7GuideLines;
            HidePattern7GuideLines = hidePattern7GuideLines;
            FireConfiguredProjectileWithoutPlayerAim = fireConfiguredProjectileWithoutPlayerAim;
            FireConfiguredProjectileWithPlayerLaunchAim = fireConfiguredProjectileWithPlayerLaunchAim;
            FireConfiguredProjectile = fireConfiguredProjectile;
            FireConfiguredProjectileWithOptions = fireConfiguredProjectileWithOptions;
        }

        internal Action Stop { get; }
        internal Func<bool> IsExecutionPaused { get; }
        internal Func<IEnumerator> WaitWhileExecutionPaused { get; }
        internal Func<float, Action, IEnumerator> WaitPatternSeconds { get; }
        internal Func<int, HogBossAI.ProjectileSettings> GetProjectile { get; }
        internal Action<float> MoveTowardPlayer { get; }
        internal Action<HogBossAI.Pattern4Settings, float> FirePattern4Wave { get; }
        internal Action<int> FireMachinegunBullet { get; }
        internal Action<int, float, Vector3> FirePattern5Bullet { get; }
        internal Action<Vector3, Vector2> FirePattern7NormalVolley { get; }
        internal Action<Vector2> FirePattern7SecondaryProjectiles { get; }
        internal Func<HogBossAI.Pattern4Settings, IEnumerator> SlamPattern4BodyRoot { get; }
        internal Func<HogBossAI.Pattern4Settings, IEnumerator> RecoverPattern4BodyRoot { get; }
        internal Func<HogBossAI.PatternKind, float, IEnumerator> ReloadWavePreview { get; }
        internal Action ResetPattern4BodyRoot { get; }
        internal Action AdvancePreviewGroup { get; }
        internal Action<HogBossAI.FirePoint, bool> SetFirePointActive { get; }
        internal Action<HogBossAI.FirePoint> RotateFirePointToPlayer { get; }
        internal Action<HogBossAI.FirePoint, Vector2> RotateFirePoint { get; }
        internal Func<HogBossAI.FirePoint, Vector3> GetFirePointProjectilePosition { get; }
        internal Func<HogBossAI.FirePoint, Transform> GetFirePointProjectileTransform { get; }
        internal Func<Vector3, Vector2> GetDirectionToPlayer { get; }
        internal Func<float, Vector2> AngleToDirection { get; }
        internal Func<int, Vector2, Vector3> GetPattern1SpawnPosition { get; }
        internal Func<Vector3, Vector2> GetPattern3Direction { get; }
        internal Func<Vector3> GetPattern7NormalProjectilePosition { get; }
        internal Action<Vector3, Vector2> UpdatePattern7GuideLines { get; }
        internal Action HidePattern7GuideLines { get; }
        internal ProjectileFire FireConfiguredProjectileWithoutPlayerAim { get; }
        internal ProjectileFire FireConfiguredProjectileWithPlayerLaunchAim { get; }
        internal ProjectileFire FireConfiguredProjectile { get; }
        internal ConfiguredProjectileFire FireConfiguredProjectileWithOptions { get; }

        internal IEnumerator SlamBodyRoot(HogBossAI.Pattern4Settings settings)
        {
            return SlamPattern4BodyRoot(settings);
        }

        internal IEnumerator RecoverBodyRoot(HogBossAI.Pattern4Settings settings)
        {
            return RecoverPattern4BodyRoot(settings);
        }

        internal void ResetBodyRoot()
        {
            ResetPattern4BodyRoot();
        }

        internal void UpdateGuideLines(Vector3 origin, Vector2 direction)
        {
            UpdatePattern7GuideLines(origin, direction);
        }

        internal void HideGuideLines()
        {
            HidePattern7GuideLines();
        }

        internal void PlayOriginBurst(HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            PlayExplosionIfEnabled(effects, position);
            PlaySmokeIfEnabled(effects, position);
        }

        internal void PlaySmokeIfDue(ref float nextSmokeAt, HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            if (effects == null || Time.time < nextSmokeAt)
            {
                return;
            }

            PlaySmokeIfEnabled(effects, position);
            nextSmokeAt = Time.time + Mathf.Max(0.01f, effects.SmokeInterval);
        }

        internal void PlayMuzzleFlashIfEnabled(HogBossAI.PatternEffectSettings effects, Vector3 origin, Vector2 direction)
        {
            HogBossAI.ParticleEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(origin, direction, muzzleFlash.Color, muzzleFlash.Scale);
        }

        internal void PlaySfxOnLaunch(EnemyProjectile projectile, string sfxId)
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

        internal void PlaySfx(string sfxId)
        {
            SoundManager.PlaySfx(sfxId);
        }

        internal void PlayBossBombOnRadialSplit(EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            static void HandleRadialSplitImminent(EnemyProjectile splitProjectile)
            {
                splitProjectile.RadialSplitImminent -= HandleRadialSplitImminent;
                SoundManager.PlaySfx("BossBomb");
            }

            projectile.RadialSplitImminent += HandleRadialSplitImminent;
        }

        private static void PlayExplosionIfEnabled(HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            HogBossAI.ParticleEffectSettings explosion = effects?.Explosion;
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogExplosion(position, explosion.Color, explosion.Scale, explosion.Count);
        }

        private static void PlaySmokeIfEnabled(HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            HogBossAI.ParticleEffectSettings smoke = effects?.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }

        internal void PlayCameraShakeIfEnabled(HogBossAI.PatternEffectSettings effects, Vector2 direction)
        {
            HogBossAI.CameraShakeSettings shake = effects?.CameraShake;
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
    }
}
