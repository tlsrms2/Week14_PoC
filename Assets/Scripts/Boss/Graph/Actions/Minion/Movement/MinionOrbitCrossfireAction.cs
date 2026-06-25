using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionOrbitCrossfireAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string orbitProjectileName = "Default";
        [SerializeField, BossGraphProjectileName] private string stationaryProjectileName = "Default";
        [SerializeField] private bool useOrbitProjectileForStationary;
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0)] private int minimumMinionCount = 2;
        [SerializeField, Min(0.1f)] private float orbitRadius = 2.6f;
        [SerializeField, Min(0.1f)] private float orbitSeconds = 3f;
        [SerializeField, Min(1f)] private float fireAngleStepDegrees = 30f;
        [SerializeField] private bool randomizeDirection = true;
        [SerializeField] private bool clockwise;
        [SerializeField, Min(1)] private int stationaryBulletCount = 5;
        [SerializeField, Min(0f)] private float stationaryFireInterval = 0.25f;
        [SerializeField] private bool resumeIdle = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryResolveProjectile(
                context,
                orbitProjectileName,
                out IMinionPatternHost host,
                out BossProjectileSettings orbitProjectile))
            {
                yield break;
            }

            BossProjectileSettings stationaryProjectile = orbitProjectile;
            if (!useOrbitProjectileForStationary
                && !MinionGraphActionHost.TryResolveProjectile(host, stationaryProjectileName, out stationaryProjectile))
            {
                yield break;
            }

            bool resolvedClockwise = randomizeDirection ? UnityEngine.Random.value > 0.5f : clockwise;
            MinionGraphOrbitCrossfireRequest request = new(
                orbitProjectile,
                stationaryProjectile,
                minimumMinionCount,
                orbitRadius,
                orbitSeconds,
                fireAngleStepDegrees,
                resolvedClockwise,
                stationaryBulletCount,
                stationaryFireInterval,
                new MinionGraphProjectileFireSpec(minionOrigin, aim, effects, context),
                resumeIdle);

            yield return host.RunOrbitCrossfire(request);
        }
    }
}
