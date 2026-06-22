using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class HogPattern1Runner
    {
        internal static IEnumerator Run(HogBossAI.Pattern1Settings settings, HogPatternContext context)
        {
            float elapsed = 0f;
            float nextBurstAt = 0f;
            int fired = 0;
            int totalBullets = Mathf.Max(1, settings.RadialBulletCount);

            float currentAngle = Random.Range(0f, 360f);
            while (fired < totalBullets)
            {
                if (context.IsExecutionPaused())
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float t = totalBullets <= 1 ? 1f : Mathf.Clamp01((float)fired / (totalBullets - 1));
                float speedMultiplier = Mathf.Lerp(
                    settings.InitialChaseSpeedMultiplier,
                    settings.FinalChaseSpeedMultiplier,
                    t);
                context.MoveTowardPlayer(speedMultiplier);

                if (elapsed >= nextBurstAt)
                {
                    Vector2 direction = context.AngleToDirection(currentAngle);
                    Vector3 origin = context.GetPattern1SpawnPosition(fired, direction);
                    EnemyProjectile projectile = context.FireConfiguredProjectileWithPlayerLaunchAim(
                        settings.Projectile,
                        origin,
                        direction);

                    if (projectile != null)
                    {
                        context.PlayOriginBurst(settings.Effects, origin);
                        context.PlaySfxOnLaunch(projectile, "BossSpecialShot");
                        context.PlaySfx("BossNormalShot");
                    }

                    fired++;
                    context.AdvancePreviewGroup();
                    nextBurstAt += Mathf.Max(0.01f, settings.BurstInterval);
                    currentAngle += settings.AngleStepDegrees;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
