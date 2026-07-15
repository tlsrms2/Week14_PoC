using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class AssassinSpawnCloneShootersAction : BossAction
    {
        [SerializeField, Min(0f)] private float cloneIntroSeconds = 0.2f;
        [Tooltip("분신 등장 연출이 도달하는 목표 알파값(0~255)입니다.")]
        [SerializeField, Range(0, 255)] private int cloneTargetAlpha = 255;
        [Tooltip("켜면 보스 자신도 분신들처럼 사라졌다가 구역 안 랜덤 위치로 순간이동해 발사 순서 후보에 포함됩니다. 끄면 보스는 순서 후보에서 빠집니다.")]
        [SerializeField] private bool includeBossPosition = true;
        [Tooltip("보스가 완전히 사라진 상태로 대기하는 시간(초)입니다. 이 시간이 지난 뒤 분신 등장과 동시에 다시 나타납니다.")]
        [SerializeField, Min(0f)] private float teleportHiddenSeconds = 0f;
        [Tooltip("발사 순서 대기 중(공격 사이 딜레이 동안)인 개체의 지정된 스프라이트가 바뀌는 색상입니다. 그 개체가 실제로 발사하는 순간 원래 색으로 돌아옵니다.")]
        [SerializeField] private Color attackFlashColor = Color.white;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin)
            {
                yield break;
            }

            assassin.SetCloneShooterAttackFlashColor(attackFlashColor);

            (Vector2 pointA, Vector2 pointB) = assassin.GetSeparatedCloneSpawnPositions();

            AssassinClone cloneA = assassin.CreateClone(pointA);
            AssassinClone cloneB = assassin.CreateClone(pointB);

            Vector2 bossDestination = default;
            if (includeBossPosition)
            {
                bossDestination = assassin.GetSeparatedCloneSpawnPosition(pointA, pointB);
                // 보스가 사라진 상태로 순간이동하는 동안에는 분신 등장 연출을 아직 시작하지 않고 기다린다 —
                // 보스가 다시 나타나는 순간에 분신들도 같이 나타나게 맞추기 위함.
                yield return assassin.VanishForTeleport(bossDestination);

                if (teleportHiddenSeconds > 0f)
                {
                    yield return context.WaitSeconds(teleportHiddenSeconds);
                }
            }

            cloneA?.PlayIntro(cloneIntroSeconds, cloneTargetAlpha);
            cloneB?.PlayIntro(cloneIntroSeconds, cloneTargetAlpha);

            if (includeBossPosition)
            {
                yield return assassin.ReappearAfterTeleport();
                assassin.EnqueueCloneShooter(bossDestination, null);
            }

            assassin.EnqueueCloneShooter(pointA, cloneA);
            assassin.EnqueueCloneShooter(pointB, cloneB);

            assassin.ShuffleCloneShooters();
        }
    }
}
