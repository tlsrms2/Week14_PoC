using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class HogPattern3Runner
    {
        internal static IEnumerator Run(HogBossAI.Pattern3Settings settings, HogPatternContext context)
        {
            context.Stop();
            context.SetFirePointActive(settings.FirePoint, true);

            context.RotateFirePointToPlayer(settings.FirePoint);
            Transform projectileAnchor = context.GetFirePointProjectileTransform(settings.FirePoint);
            Vector3 origin = projectileAnchor != null
                ? projectileAnchor.position
                : context.GetFirePointProjectilePosition(settings.FirePoint);
            HogBossAI.ProjectileSettings projectileSettings = context.GetProjectile(settings.ProjectileIndex);
            if (projectileSettings == null)
            {
                context.SetFirePointActive(settings.FirePoint, false);
                yield break;
            }

            float radius = projectileSettings.Radius * settings.ProjectileRadiusMultiplier;

            EnemyProjectile projectile = context.FireConfiguredProjectileWithOptions(
                projectileSettings,
                origin,
                context.GetPattern3Direction(origin),
                true,
                false,
                true,
                Mathf.Max(0f, settings.WindupSeconds),
                radius,
                null,
                0f);
            if (projectile == null)
            {
                context.SetFirePointActive(settings.FirePoint, false);
                yield break;
            }

            context.PlayOriginBurst(settings.Effects, origin);
            context.PlaySfxOnLaunch(projectile, "BossNormalShot");
            context.PlayBossBombOnRadialSplit(projectile);
            projectile.ConfigureChargeAnchor(projectileAnchor);
            projectile.ConfigureProjectileSize(radius);
            projectile.ConfigureChargeMotion(0f, true, false, settings.AimSpreadDegrees);
            projectile.ConfigureChargeGrowth(
                settings.StartScaleMultiplier,
                settings.FinalScaleMultiplier);
            projectile.ConfigureInterceptable(false);
            projectile.ConfigureRadialSplitOnLaunch(
                settings.RadialSplitBulletCount,
                settings.RadialSplitStartAngleOffset,
                settings.SplitDelaySeconds,
                settings.SplitSpeedMultiplier,
                settings.SplitRadiusMultiplier,
                settings.SplitLifetimeMultiplier);
            projectile.ConfigureRadialSplitSfxLead(settings.BombSfxLeadSeconds);

            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            bool trackingStopped = false;
            while (projectile != null && projectile.IsCharging)
            {
                if (context.IsExecutionPaused())
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (!trackingStopped)
                {
                    context.RotateFirePointToPlayer(settings.FirePoint);
                }

                if (!trackingStopped && elapsed >= settings.AimTrackingSeconds)
                {
                    projectile.ConfigureChargeMotion(0f, false, false, settings.AimSpreadDegrees);
                    trackingStopped = true;
                }

                context.PlaySmokeIfDue(
                    ref nextSmokeAt,
                    settings.Effects,
                    context.GetFirePointProjectilePosition(settings.FirePoint));

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (projectile != null)
            {
                Vector3 launchOrigin = context.GetFirePointProjectilePosition(settings.FirePoint);
                Vector2 launchDirection = projectile.IncomingDirection.sqrMagnitude > 0.0001f
                    ? projectile.IncomingDirection
                    : context.GetDirectionToPlayer(launchOrigin);
                context.PlayMuzzleFlashIfEnabled(settings.Effects, launchOrigin, launchDirection);
            }

            context.AdvancePreviewGroup();
            context.SetFirePointActive(settings.FirePoint, false);
        }
    }
}
