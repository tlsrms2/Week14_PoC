using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogPattern5Runner
    {
        internal static IEnumerator Run(HogBossAI.Pattern5Settings settings, HogPatternContext context)
        {
            context.Stop();
            context.SetFirePointActive(settings.FirePoint, true);

            int bulletCount = Mathf.Max(1, settings.BulletCount);
            float windupSeconds = Mathf.Max(0f, settings.WindupSeconds);
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            while (elapsed < windupSeconds)
            {
                if (context.IsExecutionPaused())
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.Stop();
                context.RotateFirePointToPlayer(settings.FirePoint);
                context.PlaySmokeIfDue(
                    ref nextSmokeAt,
                    settings.Effects,
                    context.GetFirePointProjectilePosition(settings.FirePoint));

                elapsed += Time.deltaTime;
                yield return null;
            }

            float currentSweepOffset = 0f;
            float sweepDirection = 1f;
            bool groupedFire = settings.FireInterval <= 0f;

            for (int i = 0; i < bulletCount; i++)
            {
                yield return context.WaitWhileExecutionPaused();

                context.Stop();
                context.RotateFirePointToPlayer(settings.FirePoint);
                Vector3 currentOrigin = context.GetFirePointProjectilePosition(settings.FirePoint);
                Vector2 dynamicBaseDirection = context.GetDirectionToPlayer(currentOrigin);

                float dynamicBaseAngle = Mathf.Atan2(dynamicBaseDirection.y, dynamicBaseDirection.x) * Mathf.Rad2Deg;
                float finalAngle = dynamicBaseAngle + currentSweepOffset;
                context.RotateFirePoint(settings.FirePoint, context.AngleToDirection(finalAngle));
                currentOrigin = context.GetFirePointProjectilePosition(settings.FirePoint);

                context.FirePattern5Bullet(i, finalAngle, currentOrigin);
                if (!groupedFire)
                {
                    context.AdvancePreviewGroup();
                }

                currentSweepOffset += settings.SweepStepDegrees * sweepDirection;
                if (Mathf.Abs(currentSweepOffset) >= settings.MaxSweepAngle)
                {
                    sweepDirection *= -1f;
                    currentSweepOffset = Mathf.Sign(currentSweepOffset) * settings.MaxSweepAngle;
                }

                if (settings.FireInterval > 0f && i < bulletCount - 1)
                {
                    yield return context.WaitPatternSeconds(settings.FireInterval, context.Stop);
                }
            }

            if (groupedFire)
            {
                context.AdvancePreviewGroup();
            }

            context.SetFirePointActive(settings.FirePoint, false);
        }
    }
}
