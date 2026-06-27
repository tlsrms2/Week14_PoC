using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionGatherAction : BossAction
    {
        [SerializeField] private MinionGraphGatherAnchorMode anchorMode = MinionGraphGatherAnchorMode.ClosestToPlayer;
        [SerializeField, Range(0f, 360f)] private float angleDegrees;
        [SerializeField] private MinionGraphGatherLayout layout = MinionGraphGatherLayout.Circle;
        [SerializeField, Min(0.1f)] private float radius = 2f;
        [SerializeField, Min(0.1f)] private float spacing = 0.75f;
        [SerializeField, Min(0f)] private float moveSpeed = 24f;
        [SerializeField, Min(0f)] private float settleSeconds = 0.5f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            float duration = host.CommandMinionGather(
                anchorMode,
                angleDegrees,
                layout,
                radius,
                spacing,
                moveSpeed,
                settleSeconds);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
