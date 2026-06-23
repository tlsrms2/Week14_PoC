using System.Collections;
using System.Collections.Generic;

namespace Week14.Enemy
{
    internal static class DronePattern1Runner
    {
        internal static IEnumerator Run(
            DronePilot.DronePattern1Settings settings,
            DronePilot.BossBurstSettings bossBurstSettings,
            DronePatternContext context)
        {
            if (settings == null || bossBurstSettings == null || context == null)
            {
                yield break;
            }

            yield return context.WaitWhileExecutionPaused();

            List<Drone> drones = context.GetControlledDrones();
            if (!context.EnsureAnyDrone(drones))
            {
                yield break;
            }

            DronePilot.ProjectileSettings projectile = settings.UseBossProjectile
                ? bossBurstSettings.Projectile
                : settings.DroneProjectile;
            int syncVersion = context.BeginSynchronizedDroneFire(projectile, settings.BulletCount);
            yield return context.WaitSynchronizedDroneFire(syncVersion);
        }
    }
}
