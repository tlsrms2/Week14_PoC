using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MoveTowardPlayerAction : BossAction
    {
        [SerializeField, Min(0f)] private float seconds = 1f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.MoveTowardPlayer(speedMultiplier);
                remaining -= Time.deltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }
    }

    [Serializable]
    public sealed class StartMoveTowardPlayerAction : BossAction
    {
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;

        public override IEnumerator Execute(BossActionContext context)
        {
            context?.StartMoveTowardPlayer(speedMultiplier);
            yield break;
        }
    }

    [Serializable]
    public sealed class StopMovementAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            context?.StopMoveTowardPlayer();
            yield break;
        }
    }
}
