using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class DroneSummonRunner
    {
        internal static IEnumerator Run(DronePilot.SummonSettings settings, DronePatternContext context)
        {
            if (settings == null || context == null)
            {
                yield break;
            }

            yield return context.WaitWhileExecutionPaused();

            context.RefreshControlledDrones();
            if (settings.Prefab == null)
            {
                yield break;
            }

            int maxOwned = settings.MaxOwnedDrones;
            int currentCount = context.GetControlledDrones().Count;
            int summonCount = Mathf.Max(1, settings.SummonCount);
            if (maxOwned > 0)
            {
                summonCount = Mathf.Min(summonCount, Mathf.Max(0, maxOwned - currentCount));
            }

            context.StopBoss();
            float longestIntro = 0f;
            for (int i = 0; i < summonCount; i++)
            {
                yield return context.WaitWhileExecutionPaused();

                longestIntro = Mathf.Max(longestIntro, context.SpawnDrone(i, currentCount + summonCount));
                if (i < summonCount - 1 && settings.SummonInterval > 0f)
                {
                    yield return context.WaitStoppedSeconds(settings.SummonInterval);
                }
            }

            if (longestIntro > 0f)
            {
                yield return context.WaitStoppedSeconds(longestIntro);
            }
        }
    }
}
