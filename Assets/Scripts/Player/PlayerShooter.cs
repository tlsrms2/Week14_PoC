using UnityEngine;
using Week14.Audio;

namespace Week14.Combat
{
    internal sealed class PlayerShooter
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerAimController aimController;

        internal PlayerShooter(
            PlayerCombatController.PlayerCombatContext context,
            PlayerAimController aimController)
        {
            this.context = context;
            this.aimController = aimController;
        }

        internal bool TryShootEnemy()
        {
            PlayerCombatConfig config = context.Config;
            BulletGauge bullets = context.Bullets;
            if (config == null)
            {
                return false;
            }

            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", context.Owner);
                return false;
            }

            int firedBulletNumber = bullets != null ? bullets.CurrentBullets : 0;
            int dynamicDamage = CalculateAttackBulletDamage();

            if (bullets == null || !bullets.TrySpend(config.LeftAttackBulletCost, BulletChangeSource.Attack))
            {
                return false;
            }

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(direction);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                dynamicDamage,
                config.AttackEffectColor,
                true,
                damageStyleBulletNumber: firedBulletNumber);

            if (projectile == null)
            {
                bullets.Restore(config.LeftAttackBulletCost, BulletChangeSource.Attack);
                return false;
            }

            ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, direction, config.AttackEffectColor, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx(firedBulletNumber >= 2 ? "PlayerShot" : "PlayerPowerShot");
            SoundManager.PlaySfx("BulletLoss");
            return true;
        }

        internal int CalculateAttackBulletDamage()
        {
            PlayerCombatConfig config = context.Config;
            BulletGauge bullets = context.Bullets;
            if (bullets == null || config == null)
            {
                return config != null ? config.AttackBulletDamage : 1;
            }

            return config.GetAttackDamageForRemainingBullets(bullets.CurrentBullets);
        }

        private Transform GetLeftFireOrigin()
        {
            return context.LeftGunFireOrigin != null
                ? context.LeftGunFireOrigin
                : (context.LeftGunOrigin != null ? context.LeftGunOrigin : context.PlayerTransform);
        }
    }

    internal static class PlayerBulletAudio
    {
        private const int BulletRestorePitchReferenceMax = 5;

        internal static float GetBulletCountPitch(int currentBullets, float maxPitch = 2f)
        {
            float t = Mathf.Clamp01((currentBullets - 1f) / (BulletRestorePitchReferenceMax - 1f));
            return Mathf.Lerp(1f, maxPitch, t);
        }

        internal static void PlayBulletRestoreSfx(int currentBullets)
        {
            SoundManager.PlaySfx("BulletRestore2", GetBulletCountPitch(currentBullets));
        }
    }
}
