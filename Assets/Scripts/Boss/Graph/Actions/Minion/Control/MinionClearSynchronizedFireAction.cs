using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionClearSynchronizedFireAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            if (MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                host.ClearSynchronizedMinionFire();
            }

            yield break;
        }
    }
}
