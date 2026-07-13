using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerFireWireBranchAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            yield break;
        }

        internal static bool IsPlayerGrabbed(BossActionContext context)
        {
            return context?.Boss is HackerBossAI hacker
                && hacker.LastFireWireResult == HackerFireWireResult.PlayerGrabbed;
        }
    }
}
