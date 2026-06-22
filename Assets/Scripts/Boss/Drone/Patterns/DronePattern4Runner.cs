using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class DronePattern4Runner
    {
        internal static IEnumerator Run(DronePilot.DronePattern4Settings settings, DronePatternContext context)
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
            float offset = Mathf.Max(0f, settings.AimOffsetDegrees);
            for (int i = 0; i < drones.Count; i++)
            {
                float sign = i % 2 == 0 ? 1f : -1f;
                float ring = 1f + i / 2;
                duration = Mathf.Max(duration, drones[i].CommandChargeSideFire(
                    settings.Projectile,
                    settings.ChargeSeconds,
                    settings.ChargeSpeed,
                    sign * offset * ring,
                    settings.SideFireInterval,
                    settings.SideFireAngleDegrees));
            }

            yield return context.WaitDronePatternSeconds(duration);
        }
    }
}
