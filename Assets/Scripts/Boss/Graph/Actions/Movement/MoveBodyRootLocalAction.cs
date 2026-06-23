using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MoveBodyRootLocalAction : BossAction
    {
        [SerializeField] private Vector3 targetLocalOffset;
        [SerializeField, Min(0f)] private float duration = 0.1f;
        [SerializeField] private bool releaseBaseAfterMove;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return context.MoveBodyRootLocalOffset(targetLocalOffset, duration, releaseBaseAfterMove);
        }
    }

    [Serializable]
    public sealed class ResetBodyRootLocalAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            context?.ResetBodyRootLocalOffset();
            yield break;
        }
    }
}
