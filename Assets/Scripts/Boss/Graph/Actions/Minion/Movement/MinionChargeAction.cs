using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionChargeAction : BossAction
    {
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0.05f)] private float chargeSeconds = 1f;
        [SerializeField, Min(0f)] private float chargeSpeed = 7f;
        [SerializeField, Range(0f, 85f)] private float aimOffsetDegrees = 22f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            MinionGraphProjectileFireSpec aimSpec = new(null, aim, null, context);
            MinionGraphCommandRequest request = MinionGraphCommandRequest.Charge(
                chargeSeconds,
                chargeSpeed,
                aimOffsetDegrees,
                aimSpec);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
