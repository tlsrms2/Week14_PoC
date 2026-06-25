using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionCommandAction : BossAction
    {
        [SerializeField] private MinionGraphCommandMode mode;
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(1)] private int repeatCount = 3;
        [SerializeField, Min(0f)] private float fireInterval = 0.2f;
        [SerializeField, Min(1)] private int directionCount = 5;
        [SerializeField, Range(0f, 360f)] private float spreadDegrees = 75f;
        [SerializeField, Min(0.1f)] private float orbitRadius = 2.6f;
        [SerializeField, Min(0.1f)] private float orbitSeconds = 3f;
        [SerializeField, Min(1f)] private float fireAngleStepDegrees = 30f;
        [SerializeField] private bool randomizeOrbitDirection = true;
        [SerializeField] private bool clockwise;
        [SerializeField, Min(0.05f)] private float chargeSeconds = 1f;
        [SerializeField, Min(0f)] private float chargeSpeed = 7f;
        [SerializeField, Range(0f, 85f)] private float aimOffsetDegrees = 22f;
        [SerializeField, Min(0.01f)] private float sideFireInterval = 0.18f;
        [SerializeField, Range(1f, 179f)] private float sideFireAngleDegrees = 90f;
        [SerializeField, Min(0.1f)] private float formationRadius = 2.8f;
        [SerializeField, Min(1f)] private float formationAngleSpacingDegrees = 28f;
        [SerializeField, Min(0f)] private float formationSpeedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField] private bool resumeIdle = true;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            BossProjectileSettings projectile = null;
            if (mode != MinionGraphCommandMode.Formation
                && !MinionGraphActionHost.TryResolveProjectile(host, projectileName, out projectile))
            {
                yield break;
            }

            bool resolvedClockwise = randomizeOrbitDirection ? UnityEngine.Random.value > 0.5f : clockwise;
            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, aim, effects, context);
            MinionGraphCommandRequest request = new(
                mode,
                projectile,
                Mathf.Max(1, repeatCount),
                Mathf.Max(0f, fireInterval),
                Mathf.Max(1, directionCount),
                spreadDegrees,
                orbitRadius,
                orbitSeconds,
                fireAngleStepDegrees,
                resolvedClockwise,
                chargeSeconds,
                chargeSpeed,
                aimOffsetDegrees,
                sideFireInterval,
                sideFireAngleDegrees,
                formationRadius,
                formationAngleSpacingDegrees,
                formationSpeedMultiplier,
                settleSeconds,
                fireSpec,
                resumeIdle);

            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
