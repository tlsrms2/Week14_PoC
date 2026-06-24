using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionOrbitFireAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, Min(0.1f)] private float orbitRadius = 2.6f;
        [SerializeField, Min(0.1f)] private float orbitSeconds = 3f;
        [SerializeField, Min(1f)] private float fireAngleStepDegrees = 30f;
        [SerializeField] private bool randomizeDirection = true;
        [SerializeField] private bool clockwise;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryResolveProjectile(
                context,
                projectileName,
                out IMinionPatternHost host,
                out BossProjectileSettings projectile))
            {
                yield break;
            }

            bool resolvedClockwise = randomizeDirection ? UnityEngine.Random.value > 0.5f : clockwise;
            MinionGraphCommandRequest request = MinionGraphCommandRequest.OrbitFire(
                projectile,
                orbitRadius,
                orbitSeconds,
                fireAngleStepDegrees,
                resolvedClockwise);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
