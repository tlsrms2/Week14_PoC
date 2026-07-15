using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    // 은신 상태(isStealthed)는 그대로 유지한 채, 보스 스프라이트만 시각적으로 완전히 보이게(또는 다시
    // 반투명하게) 만든다. 예를 들어 회수 패턴에서 패링 미끼를 스폰하기 전에 이 액션으로 잠깐 보스를
    // 노출시키는 식으로 쓸 수 있다. 그래프 패턴/쿨다운 등 게임플레이에는 영향을 주지 않는다.
    [Serializable]
    public sealed class AssassinSetStealthVisibilityAction : BossAction
    {
        [Tooltip("체크하면 보스가 완전히 보이게, 해제하면 다시 은신 알파값으로 돌아갑니다.")]
        [SerializeField] private bool visible = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            (context?.Boss as AssassinBossAI)?.SetStealthVisibilityOverride(visible);
            yield break;
        }
    }
}
