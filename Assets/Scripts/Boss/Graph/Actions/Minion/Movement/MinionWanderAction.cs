using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionWanderAction : BossAction, IConductorCueDurationCompanion
    {
        [SerializeField, Min(0f)] private float wanderSeconds = 1f;
        [SerializeField, Min(0f)] private float speed = 3.2f;
        [SerializeField, Min(0.1f)] private float radius = 2.8f;
        [SerializeField, Min(0.1f)] private float retargetSeconds = 1.5f;
        [SerializeField] private bool waitForDuration = true;

        private float minimumConductorCueDuration;

        public void SetMinimumConductorCueDuration(float seconds)
        {
            minimumConductorCueDuration = Mathf.Max(0f, seconds);
        }

        public void ClearMinimumConductorCueDuration()
        {
            minimumConductorCueDuration = 0f;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            float effectiveWanderSeconds = Mathf.Max(wanderSeconds, minimumConductorCueDuration);
            MinionGraphCommandRequest request = MinionGraphCommandRequest.Wander(
                effectiveWanderSeconds,
                speed,
                radius,
                retargetSeconds);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
