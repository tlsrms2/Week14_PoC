using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class AssassinExitStealthAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            (context?.Boss as AssassinBossAI)?.RequestStealth(false);
            yield break;
        }
    }
}
