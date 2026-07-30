using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    // 단검을 회수하기 전에 패링 전용 미끼(ParryBaitRewardProjectile)를 먼저 스폰한다.
    //  - 패링 실패(지속시간 안에 못 함): 단검이 (실패 시점) 플레이어 방향으로 직진한다(유도 없음,
    //    닿으면 플레이어에게 데미지) — 발사 후 플레이어가 움직여도 방향을 다시 잡지 않고, 특정
    //    지점에서 멈추는 게 아니라 Failed Recall Flight Seconds가 지나면 그 자리에서 사라진다.
    //    탄환처럼 궤적도 표시한다. 전부 사라지면 이 액션은 그대로 끝나 뒤에 있는 패턴이 이어서 진행된다.
    //    이 경우 은신은 자동으로 풀리지 않는다 — 회수 뒤에 이어지는 노드(들)까지 다 실행된 뒤,
    //    그래프에 직접 배치한 AssassinExitStealthAction으로 해제 타이밍을 지정한다.
    //  - 패링 성공: 그 자리에 보상 탄이 원형으로 뿌려지고, 단검이 보스 자신에게 날아가(궤적 없음)
    //    보스에게 데미지를 준다. 그 뒤 FireParrySuppressionBaitAction(그로기탄)과 완전히 동일하게
    //    boss.RequestGroggy(groggySeconds)로 패턴을 취소하고 보스가 그로기(무력화) 상태로 들어간다.
    //    은신 해제는 이 경우에 한해 회수가 끝나는 시점에 자동으로 처리된다.
    [Serializable]
    public sealed class AssassinRecallDaggersWithParryBaitAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [Tooltip("패링 가능한 시간(초)입니다. 이 시간 안에 패링되지 않으면 단검이 그대로 회수됩니다.")]
        [SerializeField, Min(0.1f)] private float baitDurationSeconds = 3f;
        [Tooltip("패링 성공 시 사방으로 뿌릴 보상 탄 개수입니다.")]
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [Tooltip("보상 탄이 배치될 원의 반지름입니다.")]
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [Tooltip("보상 탄의 지속시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float rewardLifetimeSeconds = 2f;
        [Tooltip("패링 성공 시 보스가 무력화(그로기)되는 시간(초)입니다. FireParrySuppressionBaitAction과 동일한 그로기 시스템을 씁니다.")]
        [SerializeField, Min(0f)] private float groggySeconds = 3f;
        [Tooltip("패링 실패 시 단검이 플레이어 방향으로 직진하다가 사라지기까지 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.1f)] private float failedRecallFlightSeconds = 2f;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin || !assassin.HasEnoughDaggersForRecallPattern)
            {
                // 단검이 충분히 모이지 않았으면 이 패턴은 아무 것도 하지 않고 즉시 끝난다.
                // 그래프 에디터에서 이 패턴의 CooldownPatternCount는 0으로 둬야
                // 스킵된 턴이 쿨다운을 소모해 다음 기회를 막지 않는다.
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            Vector3 spawnOrigin = originSpec.GetAimOrigin(context, 0);
            Vector2 direction = aimSpec.GetDirection(context, spawnOrigin);

            EnemyProjectile spawned = context.FireProjectile(
                projectile,
                spawnOrigin,
                direction,
                0f,
                projectileName: projectileName);

            if (spawned is not ParryBaitRewardProjectile bait)
            {
                yield return assassin.RecallAllDaggersRoutine(true);
                yield break;
            }

            bait.ConfigureBaitDuration(baitDurationSeconds);
            bait.ConfigureRewardOverrides(rewardBulletCount, rewardCircleRadius, rewardLifetimeSeconds);

            context.PlaySfx(SoundEvent.Boss_CreateParrySuppressionBait);
            context.PlayOriginBurst(effects, spawnOrigin);

            bool parried = false;
            bool resolved = false;

            void OnBaitDestroyed(EnemyProjectile _, EnemyProjectileDestroyReason reason, Vector3 __)
            {
                parried = reason == EnemyProjectileDestroyReason.Intercepted;
                resolved = true;
            }

            bait.Destroyed += OnBaitDestroyed;

            while (!resolved)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                }

                yield return null;
            }

            bait.Destroyed -= OnBaitDestroyed;

            if (parried)
            {
                // 보상 탄은 ParryBaitRewardProjectile 자신이 Intercepted 처리 중에 이미 스폰한다.
                yield return assassin.RecallAllDaggersRoutine(false);
                assassin.RequestStealth(false);

                // FireParrySuppressionBaitAction(그로기탄)과 동일한 처리: 그로기 진입/패턴 취소는
                // 다음 프레임의 Update로 미뤄지므로(재진입 방지), 그 처리가 실제로 일어날 때까지
                // 한 프레임 더 대기한다. 대기 도중 패턴 자체가 강제 종료되므로 이 코루틴이 "다음
                // 액션"으로 자연스럽게 넘어가는 일은 없다.
                assassin.RequestGroggy(groggySeconds);
                yield return null;
            }
            else
            {
                // 패링 실패 시에는 여기서 은신을 자동으로 해제하지 않는다 — 회수 뒤에 이어지는
                // 노드(들)까지 전부 실행되도록, 은신 해제 타이밍은 그래프에 배치하는
                // AssassinExitStealthAction으로 직접 지정한다.
                // 단검은 보스가 아니라 (발동 시점) 플레이어 방향으로 직진한다 — 유도 없이 그 방향으로만
                // 계속 날아가다가 failedRecallFlightSeconds가 지나면 사라진다.
                yield return assassin.RecallDaggersTowardPlayerRoutine(failedRecallFlightSeconds);
            }
        }
    }
}
