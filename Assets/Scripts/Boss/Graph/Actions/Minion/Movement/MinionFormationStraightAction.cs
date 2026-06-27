using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionFormationStraightAction : BossAction
    {
        [SerializeField] private MinionGraphFormationStraightMode mode;
        [SerializeField, Min(0.1f)] private float distanceFromPlayer = 2f;
        [SerializeField, Min(0.1f)] private float spacing = 0.7f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            MinionGraphCommandRequest request = MinionGraphCommandRequest.FormationStraight(
                mode,
                distanceFromPlayer,
                spacing,
                speedMultiplier,
                settleSeconds);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
