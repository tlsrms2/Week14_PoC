using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionChargeSideFireAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0.05f)] private float chargeSeconds = 1f;
        [SerializeField, Min(0f)] private float chargeSpeed = 7f;
        [SerializeField, Range(0f, 85f)] private float aimOffsetDegrees = 22f;
        [SerializeField, Min(0.01f)] private float sideFireInterval = 0.18f;
        [SerializeField, Range(1f, 179f)] private float sideFireAngleDegrees = 90f;
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
            MinionGraphCommandRequest request = MinionGraphCommandRequest.ChargeSideFire(
                projectile,
                chargeSeconds,
                chargeSpeed,
                aimOffsetDegrees,
                sideFireInterval,
                fireSpec,
                sideFireAngleDegrees);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
