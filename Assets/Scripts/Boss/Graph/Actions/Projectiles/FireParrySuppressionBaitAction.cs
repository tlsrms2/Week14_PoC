using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    // 패링으로 보스 패턴을 억제(취소)시키는 시퀀스의 "시작" 액션이다. ParryBaitRewardProjectile을 소환하고
    // 결과를 기다린다:
    //  - 패링 성공: 지금 돌고 있는 패턴 전체를 취소하고(뒤에 있는 액션들은 실행되지 않는다), 보스가
    //    그로기(무력화) 상태로 들어가 정해진 시간 동안 멈춰있다가 새 패턴을 고른다.
    //  - 패링 실패(지속시간 안에 패링 못 함): 아무 효과 없이 그대로 다음 액션으로 이어진다.
    [Serializable]
    public sealed class FireParrySuppressionBaitAction : BossAction, IBossProjectileEmissionAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [Tooltip("패링 가능한 시간(초)입니다. 이 시간 안에 패링되지 않으면 아무 효과 없이 사라지고 패턴이 그대로 이어집니다.")]
        [SerializeField, Min(0.1f)] private float baitDurationSeconds = 3f;
        [Tooltip("패링 성공 시 사방으로 뿌릴 보상 탄 개수입니다.")]
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [Tooltip("보상 탄이 배치될 원의 반지름입니다.")]
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [Tooltip("패링 성공 시 보스가 무력화(그로기)되는 시간(초)입니다. 보상 탄의 지속시간도 이 값과 같게 맞춰집니다.")]
        [SerializeField, Min(0f)] private float groggySeconds = 3f;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not GraphBossAI boss)
            {
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
                yield break;
            }

            bait.ConfigureBaitDuration(baitDurationSeconds);
            bait.ConfigureRewardOverrides(rewardBulletCount, rewardCircleRadius, groggySeconds);

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

            if (!parried)
            {
                yield break;
            }

            // 패턴을 취소하고 그로기로 들어가는 실제 처리는 다음 프레임의 Update로 미뤄진다(재진입 방지).
            // 그 처리가 실제로 일어날 때까지 한 프레임 더 대기한다 — 대기 도중 패턴 자체가 강제
            // 종료되므로, 이 코루틴이 "다음 액션"으로 자연스럽게 넘어가는 일은 없다.
            boss.RequestGroggy(groggySeconds);
            yield return null;
        }
    }
}
