using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class DronePattern3Runner
    {
        internal static IEnumerator Run(DronePilot.DronePattern3Settings settings, DronePatternContext context)
        {
            if (settings == null || context == null)
            {
                yield break;
            }

            yield return context.WaitWhileExecutionPaused();

            List<Drone> drones = context.GetControlledDrones();
            if (!context.EnsureAnyDrone(drones))
            {
                yield break;
            }

            float duration = 0f;
            for (int i = 0; i < drones.Count; i++)
            {
                duration = Mathf.Max(duration, drones[i].CommandRadialBurst(
                    settings.Projectile,
                    settings.VolleyCount,
                    settings.DirectionCount,
                    settings.VolleyInterval,
                    settings.SpreadDegrees,
                    true));
            }

            yield return context.WaitDronePatternSeconds(duration);
        }
    }
}
