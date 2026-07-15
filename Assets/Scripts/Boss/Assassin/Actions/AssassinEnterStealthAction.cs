using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class AssassinEnterStealthAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            (context?.Boss as AssassinBossAI)?.RequestStealth(true);
            yield break;
        }
    }
}
