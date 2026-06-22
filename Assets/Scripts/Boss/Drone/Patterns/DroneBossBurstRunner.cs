using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class DroneBossBurstRunner
    {
        internal static IEnumerator Run(
            DronePilot.BossBurstSettings settings,
            bool notifyDrones,
            DronePilot.ProjectileSettings bossProjectile,
            int bulletCount,
            float fireInterval,
            float spawnSpacing,
            DronePilot.ProjectileSettings droneProjectile,
            DronePatternContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (settings != null && settings.WindupSeconds > 0f)
            {
                yield return context.WaitPatternSeconds(settings.WindupSeconds);
            }

            int count = Mathf.Max(1, bulletCount);
            for (int i = 0; i < count; i++)
            {
                yield return context.WaitWhileExecutionPaused();

                Vector3 origin = context.GetProjectileOrigin();
                Vector2 direction = context.GetDirectionToPlayer(origin);
                Vector2 side = new(-direction.y, direction.x);
                Vector3 spawnPosition = origin + (Vector3)(side * context.GetAlternatingOffset(i, spawnSpacing));
                context.FireBossProjectile(bossProjectile, spawnPosition, direction, origin);

                if (notifyDrones)
                {
                    context.FireAllDrones(droneProjectile);
                }

                context.TryFireSynchronizedDrones();
                if (i < count - 1 && fireInterval > 0f)
                {
                    yield return context.WaitPatternSeconds(fireInterval);
                }
            }
        }
    }
}
