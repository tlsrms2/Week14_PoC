using System;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.UI;
using Week14.Weapons;

namespace Week14.Combat
{
    internal sealed class PlayerParryController
    {
        internal static event Action ProjectileParried;

        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerAimController aimController;
        private readonly PlayerCombatRig rig;
        private EnemyProjectile projectileLockOnTarget;
        private EnemyProjectile visibleProjectileLockOnIndicator;
        private Vector3 mouseParryReticleBaseLocalScale = Vector3.one;
        private float mouseParryCurrentRangeScale = 1f;
        private float mouseParryRangeRecoveryStartsAt;
        private bool hasMouseParryReticleBaseLocalScale;

        internal PlayerParryController(
            PlayerCombatController.PlayerCombatContext context,
            PlayerAimController aimController,
            PlayerCombatRig rig)
        {
            this.context = context;
            this.aimController = aimController;
            this.rig = rig;
        }

        internal void PlayParryImpact(Vector3 position)
        {
            PlayParryImpact(position, Vector2.right);
        }

        internal void PlayParryImpact(Vector3 position, Vector2 direction)
        {
            PlayerCombatConfig config = context.Config;
            BulletGauge bullets = context.Bullets;
            if (config == null)
            {
                return;
            }

            if (bullets != null && bullets.Restore(config.ParryBulletRecovery, BulletChangeSource.Parry))
            {
                PlayerBulletAudio.PlayBulletRestoreSfx(bullets.CurrentBullets, bullets.MaxBullets);
            }

            ProjectileVfx.PlayParry(
                position,
                direction,
                config.ParrySparkColor,
                config.ParryRingColor,
                config.ParryRingGlitterColor,
                config.ParrySparkCount,
                config.ParryRingGlitterCount,
                config.ParrySparkSeconds,
                config.ParryRingSeconds,
                config.ParryRingGlitterSeconds,
                config.ParryFlameCount,
                config.ParryEffectScale);
            context.CameraFollow?.PlayImpact(direction, 0.32f, 0.24f, 0.22f);
        }

        internal bool TryParryProjectile()
        {
            if (!HasValidParryConfig())
            {
                return false;
            }

            EnemyProjectile target = projectileLockOnTarget != null
                    && projectileLockOnTarget.CanBeIntercepted
                    && IsProjectileInMouseParryRange(projectileLockOnTarget)
                ? projectileLockOnTarget
                : FindClosestInterceptTarget();
            if (target == null)
            {
                return false;
            }

            return ExecuteParry(target);
        }

        internal int AutoParryProjectilesNear(Vector2 center, float radius)
        {
            return AutoParryProjectilesNear(center, radius, RollSkillVfxSettings.Default);
        }

        internal int AutoParryProjectilesNear(Vector2 center, float radius, RollSkillVfxSettings vfxSettings)
        {
            if (!HasValidParryConfig() || radius <= 0f)
            {
                return 0;
            }

            RollSkillVfxSettings sanitizedVfxSettings = vfxSettings.Sanitized;
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;
            float sqrRadius = radius * radius;
            int parriedCount = 0;

            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                EnemyProjectile target = activeProjectiles[i];
                if (target == null || !target.CanBeIntercepted)
                {
                    continue;
                }

                Vector2 targetPosition = target.transform.position;
                if ((targetPosition - center).sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                if (ExecuteParry(target, playSfx: false))
                {
                    PlayerDashVfx.PlayProjectileAbsorb(
                        context.CoroutineHost,
                        target,
                        context.CombatCenterOrigin.position,
                        sanitizedVfxSettings.AutoParryAbsorbSeconds,
                        sanitizedVfxSettings.AutoParryAbsorbColor);
                    parriedCount++;
                }
            }

            return parriedCount;
        }

        private bool HasValidParryConfig()
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

            return true;
        }

        private bool ExecuteParry(EnemyProjectile target, bool playSfx = true)
        {
            PlayerCombatConfig config = context.Config;
            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 firePosition = fireOrigin != null ? fireOrigin.position : context.PlayerTransform.position;
            Vector2 direction = (Vector2)target.transform.position - firePosition;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = target.IncomingDirection.sqrMagnitude > 0.0001f
                    ? -target.IncomingDirection
                    : Vector2.right;
            }

            if (!target.TryReserveIntercept())
            {
                return false;
            }

            PlayerProjectile parryShot = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                firePosition,
                direction.normalized,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                config.ParryEffectColor,
                false,
                true);
            if (parryShot == null)
            {
                target.CancelInterceptReservation();
                return false;
            }

            parryShot.SetForcedParryTarget(target);
            ProjectileVfx.PlayMuzzleFlash(firePosition, direction.normalized, config.ParryEffectColor, 1f);
            context.Visual?.PlayIntercept();

            if (playSfx)
            {
                int currentBullets = context.Bullets != null ? context.Bullets.CurrentBullets : 0;
                int maxBullets = context.Bullets != null ? context.Bullets.MaxBullets : currentBullets;
                SoundManager.PlaySfx("Parry2", PlayerBulletAudio.GetBulletCountPitch(currentBullets, maxBullets, 1.3f));
            }

            ProjectileParried?.Invoke();
            return true;
        }

        internal void UpdateProjectileLockOnTarget()
        {
            projectileLockOnTarget = FindClosestInterceptTarget();
        }

        internal void ApplyMouseParryMissPenalty()
        {
            rig.ResolveMouseParryReticleReference();
            CacheMouseParryReticleBaseScale();

            float minimumScale = Mathf.Clamp(MouseParryMinimumRangeScale, 0.1f, 1f);
            float loss = Mathf.Clamp01(MouseParryMissRangeScaleLoss);
            mouseParryCurrentRangeScale = Mathf.Max(minimumScale, mouseParryCurrentRangeScale - loss);
            mouseParryRangeRecoveryStartsAt = Time.time + Mathf.Max(0f, MouseParryRangeRecoveryDelay);

            ApplyMouseParryRangeScale();
            context.MouseParryReticle?.PlayMissFeedback(
                MouseParryMissColorSeconds,
                MouseParryMissShakeSeconds,
                MouseParryMissShakeAmplitude,
                MouseParryMissShakeFrequency);
        }

        internal void UpdateMouseParryRangeRecovery()
        {
            if (mouseParryCurrentRangeScale >= 0.999f || Time.time < mouseParryRangeRecoveryStartsAt)
            {
                return;
            }

            mouseParryCurrentRangeScale = Mathf.MoveTowards(
                mouseParryCurrentRangeScale,
                1f,
                Mathf.Max(0f, MouseParryRangeRecoveryPerSecond) * Time.deltaTime);
            ApplyMouseParryRangeScale();
        }

        internal void CacheMouseParryReticleBaseScale()
        {
            SpriteRenderer renderer = context.MouseParryReticleRenderer;
            if (hasMouseParryReticleBaseLocalScale || renderer == null)
            {
                return;
            }

            mouseParryReticleBaseLocalScale = renderer.transform.localScale;
            hasMouseParryReticleBaseLocalScale = true;
        }

        private void ApplyMouseParryRangeScale()
        {
            SpriteRenderer renderer = context.MouseParryReticleRenderer;
            if (renderer == null)
            {
                return;
            }

            CacheMouseParryReticleBaseScale();
            if (!hasMouseParryReticleBaseLocalScale)
            {
                return;
            }

            float scale = Mathf.Clamp(mouseParryCurrentRangeScale, Mathf.Clamp(MouseParryMinimumRangeScale, 0.1f, 1f), 1f);
            renderer.transform.localScale = mouseParryReticleBaseLocalScale * scale * WeaponParryRangeMultiplier;
        }

        internal void UpdateMouseParryReticle()
        {
            if (!CanShowMouseParryReticle())
            {
                SetMouseParryReticleVisible(false);
                return;
            }

            SpriteRenderer renderer = context.MouseParryReticleRenderer;
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = true;
            rig.ResolveMouseParryReticleReference();
            context.MouseParryReticle?.SetVisible(true);
            ApplyMouseParryRangeScale();

            Vector2 cursorPosition = GetParryCursorWorldPosition();
            Transform reticleTransform = renderer.transform;
            reticleTransform.position = new Vector3(cursorPosition.x, cursorPosition.y, reticleTransform.position.z);
        }

        internal void UpdateMouseParryReticleThreat()
        {
            context.MouseParryReticle?.SetThreatened(
                projectileLockOnTarget != null && IsProjectileInMouseParryRange(projectileLockOnTarget));
        }

        internal void SetMouseParryReticleVisible(bool visible)
        {
            rig.ResolveMouseParryReticleReference();
            SpriteRenderer renderer = context.MouseParryReticleRenderer;
            if (renderer != null)
            {
                renderer.enabled = visible;
            }

            MouseParryReticle reticle = context.MouseParryReticle;
            if (reticle != null)
            {
                reticle.SetVisible(visible);
                if (!visible)
                {
                    reticle.SetThreatened(false);
                }
            }
        }

        internal void UpdateProjectileLockOnIndicator()
        {
            if (projectileLockOnTarget == null || !projectileLockOnTarget.CanBeIntercepted)
            {
                SetProjectileLockOnIndicatorTarget(null);
                return;
            }

            SetProjectileLockOnIndicatorTarget(projectileLockOnTarget);
        }

        internal void SetProjectileLockOnIndicatorVisible(bool visible)
        {
            SetProjectileLockOnIndicatorTarget(visible ? projectileLockOnTarget : null);

            if (!visible)
            {
                projectileLockOnTarget = null;
            }
        }

        private void SetProjectileLockOnIndicatorTarget(EnemyProjectile target)
        {
            if (visibleProjectileLockOnIndicator == target)
            {
                if (visibleProjectileLockOnIndicator != null)
                {
                    visibleProjectileLockOnIndicator.SetParryLockOnIndicatorVisible(true);
                }

                return;
            }

            if (visibleProjectileLockOnIndicator != null)
            {
                visibleProjectileLockOnIndicator.SetParryLockOnIndicatorVisible(false);
            }

            visibleProjectileLockOnIndicator = target;
            if (visibleProjectileLockOnIndicator != null)
            {
                visibleProjectileLockOnIndicator.SetParryLockOnIndicatorVisible(true);
            }
        }

        internal bool TryGetMouseParryDiamondCorners(out Vector3 top, out Vector3 right, out Vector3 bottom, out Vector3 left)
        {
            top = right = bottom = left = Vector3.zero;
            SpriteRenderer renderer = context.MouseParryReticleRenderer;
            if (renderer == null || renderer.sprite == null)
            {
                return false;
            }

            Bounds spriteBounds = renderer.sprite.bounds;
            Vector3 center = spriteBounds.center;
            Vector3 extents = spriteBounds.extents;
            if (extents.x <= 0.0001f || extents.y <= 0.0001f)
            {
                return false;
            }

            Transform reticleTransform = renderer.transform;
            top = reticleTransform.TransformPoint(center + Vector3.up * extents.y);
            right = reticleTransform.TransformPoint(center + Vector3.right * extents.x);
            bottom = reticleTransform.TransformPoint(center + Vector3.down * extents.y);
            left = reticleTransform.TransformPoint(center + Vector3.left * extents.x);
            return true;
        }

        private EnemyProjectile FindClosestInterceptTarget()
        {
            Vector2 cursorPosition = GetParryCursorWorldPosition();
            EnemyProjectile bestTarget = null;
            float bestDistance = float.PositiveInfinity;
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;

            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                EnemyProjectile source = activeProjectiles[i];
                if (source == null || !source.CanBeIntercepted)
                {
                    continue;
                }

                if (!IsProjectileInMouseParryRange(source))
                {
                    continue;
                }

                Vector2 sourcePosition = source.transform.position;
                float distance = Vector2.Distance(cursorPosition, sourcePosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestTarget = source;
                bestDistance = distance;
            }

            return bestTarget;
        }

        private bool IsProjectileInMouseParryRange(EnemyProjectile projectile)
        {
            if (projectile == null || !projectile.CanBeIntercepted)
            {
                return false;
            }

            return IsPointInsideMouseParryDiamond(projectile.transform.position);
        }

        private bool IsPointInsideMouseParryDiamond(Vector2 worldPoint)
        {
            SpriteRenderer renderer = context.MouseParryReticleRenderer;
            if (renderer == null || renderer.sprite == null)
            {
                return false;
            }

            Bounds spriteBounds = renderer.sprite.bounds;
            Vector2 center = spriteBounds.center;
            Vector2 halfSize = spriteBounds.extents;
            if (halfSize.x <= 0.0001f || halfSize.y <= 0.0001f)
            {
                return false;
            }

            Vector2 local = renderer.transform.InverseTransformPoint(worldPoint) - (Vector3)center;
            return Mathf.Abs(local.x) / halfSize.x + Mathf.Abs(local.y) / halfSize.y <= 1f;
        }

        private Vector2 GetParryCursorWorldPosition()
        {
            return aimController.GetMouseWorldPosition();
        }

        private bool CanShowMouseParryReticle()
        {
            Health health = context.Health;
            return context.Config != null
                && !GameModalState.BlocksGameplayInput
                && !context.IsExecuting
                && health != null
                && !health.IsDead;
        }

        private Transform GetLeftFireOrigin()
        {
            return context.LeftGunFireOrigin != null
                ? context.LeftGunFireOrigin
                : (context.LeftGunOrigin != null ? context.LeftGunOrigin : context.PlayerTransform);
        }

        private float MouseParryMinimumRangeScale => context.Config != null ? context.Config.MouseParryMinimumRangeScale : 0.5f;
        private float MouseParryMissRangeScaleLoss => context.Config != null ? context.Config.MouseParryMissRangeScaleLoss : 0.12f;
        private float MouseParryRangeRecoveryDelay => context.Config != null ? context.Config.MouseParryRangeRecoveryDelay : 0.8f;
        private float MouseParryRangeRecoveryPerSecond => context.Config != null ? context.Config.MouseParryRangeRecoveryPerSecond : 0.45f;
        private float MouseParryMissColorSeconds => context.Config != null ? context.Config.MouseParryMissColorSeconds : 0.16f;
        private float MouseParryMissShakeSeconds => context.Config != null ? context.Config.MouseParryMissShakeSeconds : 0.18f;
        private float MouseParryMissShakeAmplitude => context.Config != null ? context.Config.MouseParryMissShakeAmplitude : 0.035f;
        private float MouseParryMissShakeFrequency => context.Config != null ? context.Config.MouseParryMissShakeFrequency : 42f;

        private static float WeaponParryRangeMultiplier
        {
            get
            {
                BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null ? WeaponLoadoutManager.Instance.CurrentWeapon : null;
                return weapon != null ? Mathf.Max(0.01f, weapon.ParryingRange) : 1f;
            }
        }
    }
}
