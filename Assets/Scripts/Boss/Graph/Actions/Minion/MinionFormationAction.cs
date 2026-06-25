using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionFormationAction : BossAction
    {
        [SerializeField, Min(0.1f)] private float radius = 2.8f;
        [SerializeField, Min(1f)] private float angleSpacingDegrees = 28f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            MinionGraphCommandRequest request = MinionGraphCommandRequest.Formation(
                radius,
                angleSpacingDegrees,
                speedMultiplier,
                settleSeconds);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
