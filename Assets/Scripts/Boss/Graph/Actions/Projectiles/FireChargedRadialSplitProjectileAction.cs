using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class SpawnChargedProjectileAction : BossAction
    {
        [SerializeField] private string handleKey = "Projectile";
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float chargeSeconds = 1.6f;
        [SerializeField, Min(0.01f)] private float projectileRadiusMultiplier = 1f;
        [SerializeField, Range(0f, 180f)] private float aimSpreadDegrees = 24f;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            Transform anchor = originSpec.GetAimOriginTransform(context, 0);
            Vector3 spawnOrigin = anchor != null ? anchor.position : originSpec.GetAimOrigin(context, 0);
            Vector2 direction = GetSpreadDirection(aimSpec.GetDirection(context, spawnOrigin));
            BossProjectileSettings projectileSettings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            float radius = Mathf.Max(0.01f, projectileSettings.Radius * projectileRadiusMultiplier);
            EnemyProjectile spawned = context.FireProjectile(
                projectile,
                spawnOrigin,
                direction,
                0f,
                true,
                false,
                chargeSeconds,
                radius,
                true,
                projectileName);

            if (spawned == null)
            {
                yield break;
            }

            spawned.ConfigureChargeAnchor(anchor);
            spawned.ConfigureProjectileSize(radius);
            spawned.ConfigureChargeMotion(0f, true, false, aimSpreadDegrees);
            spawned.ConfigureInterceptable(false);
            context.SetProjectileHandle(handleKey, spawned);
            context.PlaySfxOnLaunch(spawned, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
        }

        private Vector2 GetSpreadDirection(Vector2 direction)
        {
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = aimSpreadDegrees * 0.5f;
            return BossActionContext.AngleToDirection(baseAngle + UnityEngine.Random.Range(-halfSpread, halfSpread));
        }
    }

    [Serializable]
    public sealed class ConfigureProjectileGrowthAction : BossAction
    {
        [SerializeField] private string handleKey = "Projectile";
        [SerializeField, Min(0.01f)] private float startScaleMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float finalScaleMultiplier = 4f;

        public override IEnumerator Execute(BossActionContext context)
        {
            EnemyProjectile projectile = context?.GetProjectileHandle(handleKey);
            projectile?.ConfigureChargeGrowth(startScaleMultiplier, finalScaleMultiplier);
            yield break;
        }
    }

    [Serializable]
    public sealed class ConfigureRadialSplitAction : BossAction
    {
        [SerializeField] private string handleKey = "Projectile";
        [SerializeField, Min(1)] private int radialSplitBulletCount = 12;
        [SerializeField] private float radialSplitStartAngleOffset;
        [SerializeField, Min(0f)] private float splitDelaySeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float splitSpeedMultiplier = 0.92f;
        [SerializeField, Range(0.05f, 1f)] private float splitRadiusMultiplier = 0.62f;
        [SerializeField, Range(0.05f, 1f)] private float splitLifetimeMultiplier = 0.85f;
        [SerializeField, Min(0f)] private float splitSfxLeadSeconds = 0.15f;
        [SerializeField, BossGraphSfxId] private string splitImminentSfxId;

        public override IEnumerator Execute(BossActionContext context)
        {
            EnemyProjectile projectile = context?.GetProjectileHandle(handleKey);
            if (projectile == null)
            {
                yield break;
            }

            projectile.ConfigureRadialSplitOnLaunch(
                radialSplitBulletCount,
                radialSplitStartAngleOffset,
                splitDelaySeconds,
                splitSpeedMultiplier,
                splitRadiusMultiplier,
                splitLifetimeMultiplier);
            projectile.ConfigureRadialSplitSfxLead(splitSfxLeadSeconds);
            context.PlaySfxOnRadialSplitImminent(projectile, splitImminentSfxId);
        }
    }

    [Serializable]
    public sealed class WaitProjectileChargeEndAction : BossAction
    {
        [SerializeField] private string handleKey = "Projectile";
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float aimTrackingSeconds = 1f;
        [SerializeField, Range(0f, 180f)] private float aimSpreadDegrees = 24f;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            EnemyProjectile projectile = context?.GetProjectileHandle(handleKey);
            if (context == null || projectile == null)
            {
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            bool trackingStopped = false;
            while (projectile != null && projectile.IsCharging)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (!trackingStopped && elapsed >= aimTrackingSeconds)
                {
                    projectile.ConfigureChargeMotion(0f, false, false, aimSpreadDegrees);
                    trackingStopped = true;
                }

                Vector3 smokeOrigin = originSpec.GetAimOrigin(context, 0);
                context.PlaySmokeIfDue(ref nextSmokeAt, effects, smokeOrigin);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (projectile != null)
            {
                Vector3 launchOrigin = originSpec.GetAimOrigin(context, 0);
                Vector2 launchDirection = projectile.IncomingDirection.sqrMagnitude > 0.0001f
                    ? projectile.IncomingDirection
                    : aimSpec.GetDirection(context, launchOrigin);
                context.PlayMuzzleFlashIfEnabled(effects, launchOrigin, launchDirection);
            }
        }
    }
}
