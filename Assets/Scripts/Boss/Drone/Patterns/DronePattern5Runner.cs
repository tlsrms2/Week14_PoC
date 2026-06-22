using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class DronePattern5Runner
    {
        internal static IEnumerator Run(DronePilot.DronePattern5Settings settings, DronePatternContext context)
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

            float settleSeconds = Mathf.Max(0f, settings.SettleSeconds);
            float formationDelaySeconds = settleSeconds * 0.5f;
            if (formationDelaySeconds > 0f)
            {
                yield return context.WaitDronePatternSeconds(formationDelaySeconds);
            }

            for (int i = 0; i < drones.Count; i++)
            {
                drones[i].CommandFormation(
                    context.GetFormationAngle(i, settings.FormationAngleSpacingDegrees),
                    settings.FormationRadius,
                    settings.FormationSpeedMultiplier);
            }

            float formationSettleSeconds = settleSeconds - formationDelaySeconds;
            if (formationSettleSeconds > 0f)
            {
                yield return context.WaitDronePatternSeconds(formationSettleSeconds);
            }

            int fireCount = Mathf.Max(1, settings.DroneFireCount);
            for (int i = 0; i < fireCount; i++)
            {
                yield return context.WaitWhileExecutionPaused();

                context.FireAllDrones(settings.DroneProjectile);
                if (i < fireCount - 1 && settings.DroneFireInterval > 0f)
                {
                    yield return context.WaitDronePatternSeconds(settings.DroneFireInterval);
                }
            }

            context.ResumeAllDrones();
        }
    }
}
