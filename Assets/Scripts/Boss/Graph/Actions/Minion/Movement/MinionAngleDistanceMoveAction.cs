using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionAngleDistanceMoveAction : BossAction
    {
        [SerializeField] private List<MinionGraphAngleDistanceSlot> slots = new()
        {
            new MinionGraphAngleDistanceSlot(0f, 2f),
            new MinionGraphAngleDistanceSlot(180f, 2f)
        };
        [SerializeField, Min(0f)] private float speedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            float duration = host.CommandMinionAngleDistanceList(slots, speedMultiplier, settleSeconds);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
