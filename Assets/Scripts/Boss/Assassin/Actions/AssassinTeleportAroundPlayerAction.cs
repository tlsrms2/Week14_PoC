using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    // 보스가 사라졌다가 플레이어로부터 일정 거리(Teleport Radius) 떨어진 원 위의 무작위 지점으로
    // 순간이동해 다시 나타난다. 원래 자리에서 vanishSeconds 동안 사라지고, 그 자리에서 hiddenSeconds만큼
    // 대기한 뒤에야 실제로 위치가 바뀌고, 곧바로 reappearSeconds 동안 새 자리에서 페이드 인한다.
    // 은신 알파 페이드 인프라(VanishForTeleportInPlace/TeleportImmediate/ReappearAfterTeleport)는
    // AssassinSpawnCloneShootersAction이 보스 자신을 순간이동시킬 때 쓰는 것과 공유한다. 목적지는
    // 분신 소환 구역(cloneSpawnZone) 밖으로 나가지 않는다.
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

            // 원래 자리에서 안 보이게 된 뒤(vanishSeconds), 그 자리에서 hiddenSeconds만큼 대기하고,
            // 그게 다 끝난 뒤에야 실제로 순간이동한다 — 새 목적지에서 곧바로 페이드 인이 시작된다.
            yield return assassin.VanishForTeleportInPlace(vanishSeconds);

            if (hiddenSeconds > 0f)
            {
                // 이 구간(hiddenSeconds) 동안은 보스가 맵에서 아예 사라진 것으로 취급한다 —
                // 콜라이더를 꺼서 총알/근접공격이 안 맞고, 락온 대상에서도 제외된다.
                assassin.SetHiddenFromMap(true);
                yield return context.WaitSeconds(hiddenSeconds);
                assassin.SetHiddenFromMap(false);
            }

            assassin.TeleportImmediate(destination);

            yield return assassin.ReappearAfterTeleport(reappearSeconds);
        }
    }
}
