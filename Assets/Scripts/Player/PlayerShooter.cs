using UnityEngine;
using Week14.Audio;
using Week14.Weapons;

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

            PlayerProjectile projectilePrefab = WeaponLoadoutManager.Instance != null && WeaponLoadoutManager.Instance.CurrentWeapon != null && WeaponLoadoutManager.Instance.CurrentWeapon.ProjectilePrefab != null
                ? WeaponLoadoutManager.Instance.CurrentWeapon.ProjectilePrefab
                : config.ProjectilePrefab;

            if (projectilePrefab == null)
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
                projectilePrefab,
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

        internal bool TryFireSkillProjectile(int damage, float sizeMultiplier, Color color)
        {
            PlayerCombatConfig config = context.Config;
            if (config == null)
            {
                return false;
            }

            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", context.Owner);
                return false;
            }

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(direction);

            float radius = config.ProjectileRadius * Mathf.Max(0.1f, sizeMultiplier);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                radius,
                damage,
                color,
                true,
                damageStyleBulletNumber: 1,
                isSkillShot: true);

            if (projectile == null)
            {
                return false;
            }

            ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, direction, color, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx("PlayerPowerShot");
            return true;
        }

        internal int CalculateAttackBulletDamage()
        {
            BulletGauge bullets = context.Bullets;
            BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null ? WeaponLoadoutManager.Instance.CurrentWeapon : null;
            if (weapon == null)
            {
                return 1;
            }

            int remainingAmmo = bullets != null ? bullets.CurrentBullets : 1;
            return weapon.GetDamageForAmmo(remainingAmmo);
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
        private const float MinPitch = 1f;
        private const int PitchStepReferenceBulletCount = 5;

        internal static float GetBulletCountPitch(int currentBullets, int maxBullets, float maxPitch = 2f)
        {
            float pitchStepPerBullet = (maxPitch - MinPitch) / (PitchStepReferenceBulletCount - 1);
            int bulletDeficit = maxBullets - currentBullets;
            float pitch = maxPitch - pitchStepPerBullet * bulletDeficit;
            return Mathf.Clamp(pitch, MinPitch, maxPitch);
        }

        internal static void PlayBulletRestoreSfx(int currentBullets, int maxBullets)
        {
            SoundManager.PlaySfx("BulletRestore2", GetBulletCountPitch(currentBullets, maxBullets));
        }
    }
}
