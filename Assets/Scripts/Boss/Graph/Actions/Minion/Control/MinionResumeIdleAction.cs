using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionResumeIdleAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            if (MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                host.ResumeAllMinions();
            }

            yield break;
        }
    }
}
