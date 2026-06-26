using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionWanderAction : BossAction
    {
        [SerializeField, Min(0f)] private float wanderSeconds = 1f;
        [SerializeField, Min(0f)] private float speed = 3.2f;
        [SerializeField, Min(0.1f)] private float radius = 2.8f;
        [SerializeField, Min(0.1f)] private float retargetSeconds = 1.5f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            MinionGraphCommandRequest request = MinionGraphCommandRequest.Wander(
                wanderSeconds,
                speed,
                radius,
                retargetSeconds);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
