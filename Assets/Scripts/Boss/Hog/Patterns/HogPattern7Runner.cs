using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogPattern7Runner
    {
        internal static IEnumerator Run(HogBossAI.Pattern7Settings settings, HogPatternContext context)
        {
            context.Stop();
            context.SetFirePointActive(settings.FirePoint, true);

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
                Vector3 guideOrigin = context.GetPattern7NormalProjectilePosition();
                Vector2 guideDirection = context.GetDirectionToPlayer(guideOrigin);
                context.UpdateGuideLines(guideOrigin, guideDirection);
                context.PlaySmokeIfDue(ref nextSmokeAt, settings.Effects, guideOrigin);

                elapsed += Time.deltaTime;
                yield return null;
            }

            context.HideGuideLines();
            yield return context.WaitWhileExecutionPaused();

            Vector3 aimOrigin = context.GetPattern7NormalProjectilePosition();
            Vector2 lockedDirection = context.GetDirectionToPlayer(aimOrigin);
            context.RotateFirePoint(settings.FirePoint, lockedDirection);

            int volleyCount = Mathf.Max(1, settings.NormalVolleyCount);
            bool groupedFire = settings.NormalVolleyInterval <= 0f;
            for (int volleyIndex = 0; volleyIndex < volleyCount; volleyIndex++)
            {
                yield return context.WaitWhileExecutionPaused();

                context.Stop();
                context.FirePattern7NormalVolley(context.GetPattern7NormalProjectilePosition(), lockedDirection);
                if (volleyIndex == 0)
                {
                    context.FirePattern7SecondaryProjectiles(lockedDirection);
                }

                if (!groupedFire)
                {
                    context.AdvancePreviewGroup();
                }

                if (settings.NormalVolleyInterval > 0f && volleyIndex < volleyCount - 1)
                {
                    yield return context.WaitPatternSeconds(settings.NormalVolleyInterval, context.Stop);
                }
            }

            if (groupedFire)
            {
                context.AdvancePreviewGroup();
            }

            context.SetFirePointActive(settings.FirePoint, false);
            context.HideGuideLines();
        }
    }
}
