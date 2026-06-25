using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionEnsureCountAction : BossAction
    {
        [SerializeField, Min(0)] private int targetCount = 1;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            yield return host.EnsureMinionCount(targetCount);
        }
    }
}
