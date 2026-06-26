using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionOrbitAction : BossAction
    {
        [SerializeField, Min(0.1f)] private float orbitRadius = 2.6f;
        [SerializeField, Min(0.1f)] private float orbitSeconds = 3f;
        [SerializeField, Min(0f)] private float moveSpeed = 24f;
        [SerializeField] private bool randomizeDirection = true;
        [SerializeField] private bool clockwise;
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            bool resolvedClockwise = randomizeDirection ? UnityEngine.Random.value > 0.5f : clockwise;
            MinionGraphCommandRequest request = MinionGraphCommandRequest.Orbit(
                orbitRadius,
                orbitSeconds,
                moveSpeed,
                resolvedClockwise);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
