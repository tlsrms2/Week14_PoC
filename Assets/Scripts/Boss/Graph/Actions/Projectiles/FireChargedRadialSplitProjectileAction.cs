using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireChargedRadialSplitProjectileAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, BossGraphBossChildPath] private string projectileOriginPath;
        [SerializeField, Min(0f)] private float windupSeconds = 1.6f;
        [SerializeField, Min(0f)] private float aimTrackingSeconds = 1f;
        [SerializeField, Min(1f)] private float projectileRadiusMultiplier = 4f;
        [SerializeField, Min(0.01f)] private float startScaleMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float finalScaleMultiplier = 4f;
        [SerializeField, Range(0f, 180f)] private float aimSpreadDegrees = 24f;
        [SerializeField, Min(1)] private int radialSplitBulletCount = 12;
        [SerializeField] private float radialSplitStartAngleOffset;
        [SerializeField, Min(0f)] private float splitDelaySeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float splitSpeedMultiplier = 0.92f;
        [SerializeField, Range(0.05f, 1f)] private float splitRadiusMultiplier = 0.62f;
        [SerializeField, Range(0.05f, 1f)] private float splitLifetimeMultiplier = 0.85f;
        [SerializeField, Min(0f)] private float bombSfxLeadSeconds = 0.15f;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField, BossGraphSfxId] private string radialSplitImminentSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            Transform anchor = context.GetBossChildTransform(projectileOriginPath);
            Vector3 origin = anchor != null ? anchor.position : context.GetBossChildPosition(projectileOriginPath);
            BossProjectileSettings projectileSettings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            float radius = Mathf.Max(0.01f, projectileSettings.Radius * projectileRadiusMultiplier);

            EnemyProjectile spawned = context.FireProjectile(
                projectile,
                origin,
                GetSpreadDirection(context, origin),
                0f,
                true,
                false,
                Mathf.Max(0f, windupSeconds),
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
            spawned.ConfigureChargeGrowth(startScaleMultiplier, finalScaleMultiplier);
            spawned.ConfigureInterceptable(false);
            spawned.ConfigureRadialSplitOnLaunch(
                radialSplitBulletCount,
                radialSplitStartAngleOffset,
                splitDelaySeconds,
                splitSpeedMultiplier,
                splitRadiusMultiplier,
                splitLifetimeMultiplier);
            spawned.ConfigureRadialSplitSfxLead(bombSfxLeadSeconds);
            context.PlaySfxOnLaunch(spawned, launchSfxId);
            context.PlaySfxOnRadialSplitImminent(spawned, radialSplitImminentSfxId);
            context.PlayOriginBurst(effects, origin);

            yield return TrackCharge(context, spawned);
        }

        private IEnumerator TrackCharge(BossActionContext context, EnemyProjectile spawned)
        {
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            bool trackingStopped = false;
            while (spawned != null && spawned.IsCharging)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (!trackingStopped && elapsed >= aimTrackingSeconds)
                {
                    spawned.ConfigureChargeMotion(0f, false, false, aimSpreadDegrees);
                    trackingStopped = true;
                }

                Vector3 origin = context.GetBossChildPosition(projectileOriginPath);
                context.PlaySmokeIfDue(ref nextSmokeAt, effects, origin);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (spawned != null)
            {
                Vector3 launchOrigin = context.GetBossChildPosition(projectileOriginPath);
                Vector2 launchDirection = spawned.IncomingDirection.sqrMagnitude > 0.0001f
                    ? spawned.IncomingDirection
                    : context.GetDirectionToPlayer(launchOrigin);
                context.PlayMuzzleFlashIfEnabled(effects, launchOrigin, launchDirection);
            }
        }

        private Vector2 GetSpreadDirection(BossActionContext context, Vector3 origin)
        {
            Vector2 direction = context.GetDirectionToPlayer(origin);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = aimSpreadDegrees * 0.5f;
            return BossActionContext.AngleToDirection(baseAngle + UnityEngine.Random.Range(-halfSpread, halfSpread));
        }
    }
}
