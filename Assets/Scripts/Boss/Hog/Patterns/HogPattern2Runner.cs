using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogPattern2Runner
    {
        internal static IEnumerator Run(HogBossAI.Pattern2Settings settings, HogPatternContext context)
        {
            int fired = 0;

            IReadOnlyList<HogBossAI.Pattern2Settings.VolleySettings> volleys = settings.Volleys;
            if (volleys == null || volleys.Count == 0)
            {
                yield break;
            }

            void MoveDuringDelay()
            {
                context.MoveTowardPlayer(settings.MoveSpeedMultiplier);
            }

            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                HogBossAI.Pattern2Settings.VolleySettings volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                int volleyBulletCount = Mathf.Max(1, volley.BulletCount);
                bool groupedVolley = volley.FireInterval <= 0f;
                for (int bulletIndex = 0; bulletIndex < volleyBulletCount; bulletIndex++)
                {
                    yield return context.WaitWhileExecutionPaused();

                    context.MoveTowardPlayer(settings.MoveSpeedMultiplier);
                    context.FireMachinegunBullet(fired);
                    fired++;
                    if (!groupedVolley)
                    {
                        context.AdvancePreviewGroup();
                    }

                    if (bulletIndex < volleyBulletCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return context.WaitPatternSeconds(volley.FireInterval, MoveDuringDelay);
                    }
                }

                if (groupedVolley)
                {
                    context.AdvancePreviewGroup();
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return context.WaitPatternSeconds(volley.RestSeconds, MoveDuringDelay);
                }
            }
        }
    }
}
