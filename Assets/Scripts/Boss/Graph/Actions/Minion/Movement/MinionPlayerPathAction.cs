using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionPlayerPathAction : BossAction
    {
        [SerializeField] private MinionGraphPlayerPathMode mode;
        [SerializeField, Min(0.1f)] private float distanceFromPlayer = 2.8f;
        [SerializeField, Min(0f)] private float moveToStartSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float moveSeconds = 2f;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            MinionGraphCommandRequest request = MinionGraphCommandRequest.PlayerPath(
                mode,
                distanceFromPlayer,
                moveToStartSeconds,
                moveSeconds);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
