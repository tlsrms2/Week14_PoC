using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class DronePattern2Runner
    {
        internal static IEnumerator Run(DronePilot.DronePattern2Settings settings, DronePatternContext context)
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

            Drone orbitDrone = drones[0];
            bool clockwise = UnityEngine.Random.value > 0.5f;
            float duration = orbitDrone.CommandOrbitFire(
                settings.OrbitProjectile,
                settings.OrbitRadius,
                settings.OrbitSeconds,
                settings.FireAngleStepDegrees,
                clockwise);

            for (int i = 1; i < drones.Count; i++)
            {
                duration = Mathf.Max(duration, drones[i].CommandStopAndFire(
                    settings.StationaryProjectile,
                    settings.StationaryBulletCount,
                    settings.StationaryFireInterval,
                    true));
            }

            yield return context.WaitDronePatternSeconds(duration);
        }
    }
}
