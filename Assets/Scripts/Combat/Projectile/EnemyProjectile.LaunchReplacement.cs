using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private bool TryReplaceWithLaunchPrefab()
        {
            if (launchReplacementPrefab == null)
            {
                return false;
            }

            if (ShouldTurnTowardPlayerOnLaunch())
            {
                AimAtPlayerWhileCharging();
            }
            else if (aimAtPlayerOnLaunch)
            {
                AimAtPlayerWhileCharging(aimAtPlayerOnLaunchSpreadDegrees);
            }

            GetHomingSpawnConfig(
                out bool homingEnabled,
                out float homingSeconds,
                out float homingTurnDegrees);

            EnemyProjectile replacement = SpawnInternal(
                launchReplacementPrefab,
                ownerBullets,
                transform.position,
                flightDirection,
                bulletDamage,
                0f,
                projectileSpeed,
                projectileLifetime,
                projectileRadius,
                launchedColor,
                projectileTrailSeconds,
                projectileTrailWidthMultiplier,
                homingEnabled,
                homingSeconds,
                homingTurnDegrees,
                suppressPathIndicator,
                interceptGroupId);

            if (replacement == null)
            {
                return false;
            }

            CopyLaunchRuntimeStateTo(replacement);
            if (growScaleWhileCharging)
            {
                replacement.transform.localScale = chargeGrowthEndScale;
                replacement.baseLocalScale = replacement.transform.localScale;
            }

            if (playSmokeOnLaunch)
            {
                ProjectileVfx.PlayHogSmokeBurst(transform.position, launchSmokeColor, launchSmokeScale, 18);
            }

            replacement.radialSplitAt = replacement.splitRadiallyOnLaunch
                ? Time.time + replacement.radialSplitDelaySeconds
                : 0f;
            replacement.SetChargeVfxVisible(false);
            replacement.BeginPathIndicator();
            if (replacement.body != null)
            {
                replacement.body.linearVelocity = replacement.flightDirection * replacement.projectileSpeed * EnemyTimeScale.Current;
            }

            replacement.Launched?.Invoke(replacement);
            replacement.OnProjectileLaunched();
            RetireAfterLaunchReplacement();
            return true;
        }

        private void CopyLaunchRuntimeStateTo(EnemyProjectile replacement)
        {
            replacement.ConfigureStateColors(chargingColor, launchedColor);
            if (customTrailColorConfigured && projectileTrail != null)
            {
                replacement.ConfigureTrailColor(projectileTrail.startColor);
            }

            if (customIndicatorColorConfigured)
            {
                replacement.ConfigureIndicatorColor(indicatorColor);
            }

            replacement.ConfigureChargeMotion(
                chargeDriftSpeed,
                aimAtPlayerWhileCharging,
                aimAtPlayerOnLaunch,
                aimAtPlayerOnLaunchSpreadDegrees);
            replacement.canBeIntercepted = canBeIntercepted;
            replacement.interceptPending = interceptPending;
            replacement.splitOnObstacle = splitOnObstacle;
            replacement.splitRadiallyOnLaunch = splitRadiallyOnLaunch;
            replacement.splitRemaining = splitRemaining;
            replacement.splitAngleDegrees = splitAngleDegrees;
            replacement.radialSplitBulletCount = radialSplitBulletCount;
            replacement.radialSplitStartAngleDegrees = radialSplitStartAngleDegrees;
            replacement.radialSplitDelaySeconds = radialSplitDelaySeconds;
            replacement.radialSplitSfxLeadSeconds = radialSplitSfxLeadSeconds;
            replacement.splitSpeedMultiplier = splitSpeedMultiplier;
            replacement.splitRadiusMultiplier = splitRadiusMultiplier;
            replacement.splitLifetimeMultiplier = splitLifetimeMultiplier;
            replacement.preserveLaunchDirectionOnLaunch = preserveLaunchDirectionOnLaunch;
            replacement.Launched = Launched;
            replacement.RadialSplit = RadialSplit;
            replacement.RadialSplitImminent = RadialSplitImminent;
            replacement.SetParryLockOnIndicatorVisible(parryLockOnIndicatorVisible);
            CopySpecialRuntimeStateTo(replacement);
        }

        private void RetireAfterLaunchReplacement()
        {
            isDestroying = true;
            activeProjectiles.Remove(this);
            ReleaseOwnerProjectileSlot();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            SetParryLockOnIndicatorVisible(false);

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (projectileTrail != null && renderers[i] == projectileTrail)
                {
                    continue;
                }

                renderers[i].enabled = false;
            }

            float destroyDelay = 0f;
            if (projectileTrail != null)
            {
                projectileTrail.emitting = false;
                destroyDelay = Mathf.Max(0.01f, projectileTrail.time);
            }

            Destroy(gameObject, destroyDelay);
        }

    }
}
