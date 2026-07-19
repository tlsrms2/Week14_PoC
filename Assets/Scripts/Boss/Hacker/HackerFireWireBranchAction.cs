using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerFireWireBranchAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            // Fire Wire의 유지 bool 해제와 다음 공격 트리거가 같은 프레임에 처리되면
            // Animator 전환 전에 다음 액션의 선딜 시간이 먼저 소모된다.
            yield return null;
        }

        internal static bool IsPlayerGrabbed(BossActionContext context)
        {
            return context?.Boss is HackerBossAI hacker
                && hacker.LastFireWireResult == HackerFireWireResult.PlayerGrabbed;
        }
    }
}
