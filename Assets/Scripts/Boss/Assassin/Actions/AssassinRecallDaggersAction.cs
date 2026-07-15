using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class AssassinRecallDaggersAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin || !assassin.HasEnoughDaggersForRecallPattern)
            {
                // 단검이 충분히 모이지 않았으면 이 패턴은 아무 것도 하지 않고 즉시 끝난다.
                // 그래프 에디터에서 이 패턴의 CooldownPatternCount는 0으로 둬야
                // 스킵된 턴이 쿨다운을 소모해 다음 기회를 막지 않는다.
                yield break;
            }

            yield return assassin.RecallAllDaggersRoutine(true);
        }
    }
}
