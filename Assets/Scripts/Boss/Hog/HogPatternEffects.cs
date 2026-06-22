using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class HogPatternEffects
    {
        public static void PlaySfxOnLaunch(EnemyProjectile projectile, string sfxId)
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

        public static void PlayBossBombOnRadialSplit(EnemyProjectile projectile)
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

        public static void PlayOriginBurst(HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            if (effects == null)
            {
                return;
            }

            PlayExplosionIfEnabled(effects, position);
            PlaySmokeIfEnabled(effects, position);
        }

        public static void PlaySmokeIfDue(
            ref float nextSmokeAt,
            HogBossAI.PatternEffectSettings effects,
            Vector3 position)
        {
            if (effects == null || Time.time < nextSmokeAt)
            {
                return;
            }

            PlaySmokeIfEnabled(effects, position);
            nextSmokeAt = Time.time + Mathf.Max(0.01f, effects.SmokeInterval);
        }

        public static void PlayExplosionIfEnabled(HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            HogBossAI.ParticleEffectSettings explosion = effects?.Explosion;
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogExplosion(position, explosion.Color, explosion.Scale, explosion.Count);
        }

        public static void PlayMuzzleFlashIfEnabled(
            HogBossAI.PatternEffectSettings effects,
            Vector3 position,
            Vector2 direction)
        {
            HogBossAI.ParticleEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(position, direction, muzzleFlash.Color, muzzleFlash.Scale);
        }

        public static void PlayCameraShakeIfEnabled(HogBossAI.PatternEffectSettings effects, Vector2 direction)
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

        private static void PlaySmokeIfEnabled(HogBossAI.PatternEffectSettings effects, Vector3 position)
        {
            HogBossAI.ParticleEffectSettings smoke = effects?.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }
    }
}
