using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    // 보스가 사라졌다가 플레이어로부터 일정 거리(Teleport Radius) 떨어진 원 위의 무작위 지점으로
    // 순간이동해 다시 나타난다. 사라짐/재등장 연출은 AssassinSpawnCloneShootersAction이 보스 자신을
    // 순간이동시킬 때 쓰는 것과 완전히 같은 인프라(VanishForTeleport/ReappearAfterTeleport, 은신
    // 알파 페이드 재사용)를 그대로 쓴다. 목적지는 분신 소환 구역(cloneSpawnZone) 밖으로 나가지 않는다.
    [Serializable]
    public sealed class AssassinTeleportAroundPlayerAction : BossAction
    {
        [Tooltip("플레이어로부터 순간이동할 거리(반지름)입니다.")]
        [SerializeField, Min(0.1f)] private float teleportRadius = 4f;
        [Tooltip("사라지는(페이드 아웃) 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float vanishSeconds = 0.3f;
        [Tooltip("완전히 사라진 상태로 대기하는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float hiddenSeconds = 0f;
        [Tooltip("다시 나타나는(페이드 인) 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float reappearSeconds = 0.3f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin)
            {
                yield break;
            }

            Vector2 destination = assassin.GetRandomTeleportPositionAroundPlayer(teleportRadius);

            yield return assassin.VanishForTeleport(destination, vanishSeconds);

            if (hiddenSeconds > 0f)
            {
                yield return context.WaitSeconds(hiddenSeconds);
            }

            yield return assassin.ReappearAfterTeleport(reappearSeconds);
        }
    }
}
