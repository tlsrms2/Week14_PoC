using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireChargedRadialSplitProjectileAction : BossAction
    {
        [SerializeField] private BossProjectileSettings projectile = new();
        [SerializeField] private string firePointPath;
        [SerializeField] private string projectileOriginPath;
        [SerializeField] private bool setFirePointActive = true;
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
        [SerializeField] private string launchSfxId;
        [SerializeField] private string radialSplitImminentSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (setFirePointActive)
            {
                context.SetBossChildActive(firePointPath, true);
            }

            AimFirePointToPlayer(context);
            Transform anchor = context.GetBossChildTransform(projectileOriginPath);
            Vector3 origin = anchor != null ? anchor.position : context.GetBossChildPosition(projectileOriginPath);
            float radius = Mathf.Max(0.01f, projectile.Radius * projectileRadiusMultiplier);

            EnemyProjectile spawned = context.FireProjectile(
                projectile,
                origin,
                GetSpreadDirection(context, origin),
                0f,
                true,
                false,
                Mathf.Max(0f, windupSeconds),
                radius,
                true);

            if (spawned == null)
            {
                if (setFirePointActive)
                {
                    context.SetBossChildActive(firePointPath, false);
                }

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

            if (setFirePointActive)
            {
                context.SetBossChildActive(firePointPath, false);
            }
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

                if (!trackingStopped)
                {
                    AimFirePointToPlayer(context);
                }

                if (!trackingStopped && elapsed >= aimTrackingSeconds)
                {
                    spawned.ConfigureChargeMotion(0f, false, false, aimSpreadDegrees);
                    trackingStopped = true;
                }

                context.PlaySmokeIfDue(ref nextSmokeAt, effects, context.GetBossChildPosition(projectileOriginPath));
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

        private void AimFirePointToPlayer(BossActionContext context)
        {
            Vector3 origin = context.GetBossChildPosition(projectileOriginPath);
            context.RotateBossChildRight(firePointPath, context.GetDirectionToPlayer(origin), true);
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
