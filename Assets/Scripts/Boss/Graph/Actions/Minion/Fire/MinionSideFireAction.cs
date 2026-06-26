using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionSideFireAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Min(0.05f)] private float fireSeconds = 1f;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.18f;
        [SerializeField, Range(1f, 179f)] private float sideFireAngleDegrees = 90f;
        [SerializeField] private MinionGraphSideFireOriginMode originMode = MinionGraphSideFireOriginMode.BodySides;
        [SerializeField, Min(0f)] private float bodySideSpacing = 0.35f;
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

            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, aim, effects, context);
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);
            MinionGraphCommandRequest request = MinionGraphCommandRequest.SideFire(
                projectile,
                fireSeconds,
                fireInterval,
                fireSpec,
                sideFireAngleDegrees,
                originMode,
                bodySideSpacing);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
