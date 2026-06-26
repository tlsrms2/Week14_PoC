using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionSummonAction : BossAction
    {
        [SerializeField, Min(0)] private int summonCount;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            yield return host.SummonMinions(summonCount);
        }
    }
}
