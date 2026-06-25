using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionAutoSummonIfNeededAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            yield return host.AutoSummonIfNeeded();
        }
    }
}
