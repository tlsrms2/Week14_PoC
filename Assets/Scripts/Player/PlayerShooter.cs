using UnityEngine;
using Week14.Audio;
using Week14.Weapons;

namespace Week14.Combat
{
    public sealed class PlayerShooter
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerAimController aimController;

        private float chargeTime;
        private bool isCharging;

        internal PlayerShooter(
            PlayerCombatController.PlayerCombatContext context,
            PlayerAimController aimController)
        {
            this.context = context;
            this.aimController = aimController;
        }

        public int CurrentBullets => context.Bullets != null ? context.Bullets.CurrentBullets : 0;
        public bool IsCharging => isCharging;

        internal void BeginAttack()
        {
            chargeTime = 0f;
            isCharging = true;
            context.SniperChargeIndicator?.SetChargeRatio(0f);
            context.PlayerHpView?.FreezeNewestBullet(true);
            WeaponLoadoutManager.Instance?.CurrentWeapon?.BeginAttack(this);
        }

        internal void HoldAttack(float dt)
        {
            if (!isCharging) return;

            if (CurrentBullets <= 0)
            {
                context.PlayerHpView?.FreezeNewestBullet(false);
                context.SniperChargeIndicator?.SetChargeRatio(0f);
                isCharging = false;
                chargeTime = 0f;
                return;
            }

            chargeTime += dt;
            WeaponLoadoutManager.Instance?.CurrentWeapon?.HoldAttack(this, chargeTime);
        }

        internal void ReleaseAttack()
        {
            if (!isCharging) return;
            context.PlayerHpView?.FreezeNewestBullet(false);
            WeaponLoadoutManager.Instance?.CurrentWeapon?.ReleaseAttack(this, chargeTime);
            context.SniperChargeIndicator?.SetChargeRatio(0f);
            isCharging = false;
            chargeTime = 0f;
        }

        public void UpdateChargeVisual(float ratio)
        {
            context.SniperChargeIndicator?.SetChargeRatio(ratio);
        }

        public bool TrySpendAllBullets()
        {
            BulletGauge bullets = context.Bullets;
            if (bullets == null || bullets.CurrentBullets <= 0) return false;
            return bullets.TrySpend(bullets.CurrentBullets, BulletChangeSource.Attack);
        }

        public bool TrySpendOneBullet()
        {
            BulletGauge bullets = context.Bullets;
            PlayerCombatConfig config = context.Config;
            if (bullets == null || config == null) return false;
            return bullets.TrySpend(config.LeftAttackBulletCost, BulletChangeSource.Attack);
        }

        public void FireSingle(int damage)
        {
            PlayerCombatConfig config = context.Config;
            if (config == null) return;

            PlayerProjectile projectilePrefab = ResolveProjectilePrefab(config);
            if (projectilePrefab == null) return;

            BulletGauge bullets = context.Bullets;
            int firedBulletNumber = bullets != null ? bullets.CurrentBullets : 0;
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
                damage,
                config.AttackEffectColor,
                true);

            if (projectile == null) return;

            ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, direction, config.AttackEffectColor, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx(firedBulletNumber >= 2 ? "PlayerShot" : "PlayerPowerShot");
            SoundManager.PlaySfx("BulletLoss");
        }

        public void FireSpread(int[] damagesPerPellet, float pelletStep, float maxRange)
        {
            if (damagesPerPellet == null || damagesPerPellet.Length == 0) return;

            PlayerCombatConfig config = context.Config;
            if (config == null) return;

            PlayerProjectile projectilePrefab = ResolveProjectilePrefab(config);
            if (projectilePrefab == null) return;

            int pelletCount = damagesPerPellet.Length;
            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 baseDirection = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(baseDirection);

            float lifetime = config.ProjectileSpeed > 0f
                ? maxRange / config.ProjectileSpeed
                : config.ProjectileLifetime;

            float startAngle = -(pelletCount - 1) * pelletStep * 0.5f;
            float angleStep = pelletStep;

            int[] sorted = (int[])damagesPerPellet.Clone();
            System.Array.Sort(sorted);
            System.Array.Reverse(sorted);

            int[] posOrder = BuildCenterHighDamageOrder(pelletCount);
            for (int i = 0; i < pelletCount; i++)
            {
                int spatialIdx = posOrder[i];
                float angle = startAngle + angleStep * spatialIdx;
                Vector2 pelletDir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                PlayerProjectile.Spawn(
                    projectilePrefab,
                    fireOrigin.position,
                    pelletDir,
                    context.Owner,
                    config.ProjectileSpeed,
                    lifetime,
                    config.ProjectileRadius,
                    sorted[i],
                    config.AttackEffectColor,
                    true);
            }

            ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, baseDirection, config.AttackEffectColor, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx("PlayerShot");
            SoundManager.PlaySfx("BulletLoss");
        }

        private static int[] BuildCenterHighDamageOrder(int count)
        {
            int[] order = new int[count];
            int idx = 0;
            if (count % 2 == 1)
            {
                int center = count / 2;
                order[idx++] = center;
                for (int step = 1; idx < count; step++)
                {
                    order[idx++] = center - step;
                    if (idx < count) order[idx++] = center + step;
                }
            }
            else
            {
                int innerLeft = count / 2 - 1;
                int innerRight = count / 2;
                order[idx++] = innerLeft;
                order[idx++] = innerRight;
                for (int step = 1; idx < count; step++)
                {
                    order[idx++] = innerLeft - step;
                    if (idx < count) order[idx++] = innerRight + step;
                }
            }
            return order;
        }

        private PlayerProjectile ResolveProjectilePrefab(PlayerCombatConfig config)
        {
            return config != null ? config.ProjectilePrefab : null;
        }

        public bool TryShootEnemy()
        {
            PlayerCombatConfig config = context.Config;
            BulletGauge bullets = context.Bullets;
            if (config == null)
            {
                return false;
            }

            PlayerProjectile projectilePrefab = ResolveProjectilePrefab(config);

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
                true);

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

        public int CalculateAttackBulletDamage()
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
