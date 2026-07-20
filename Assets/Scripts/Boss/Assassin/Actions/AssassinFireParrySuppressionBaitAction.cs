using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    // FireParrySuppressionBaitAction과 완전히 동일한 미끼(ParryBaitRewardProjectile) 인프라를 쓰되,
    // 패링 성공 시 Assassin 전용 정리를 추가로 한다:
    //  - 패링 실패(지속시간 안에 패링 못 함): 아무 효과 없이 그대로 다음 액션으로 이어진다.
    //  - 패링 성공: 그 자리에 보상 탄이 원형으로 뿌려지고, 지금 존재하는 분신(발사 대기열에 남아있는
    //    것 + 이미 발사돼 페이드아웃 중인 것 전부)이 보스 위치로 모여들며 서서히 사라지고, 은신을
    //    해제한 뒤, FireParrySuppressionBaitAction과 동일하게 boss.RequestGroggy(Groggy Seconds)로
    //    지금 돌고 있는 패턴 전체를 취소하고 보스가 그로기(무력화) 상태로 들어간다.
    [Serializable]
    public sealed class AssassinFireParrySuppressionBaitAction : BossAction
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
        [Tooltip("패링 성공으로 분신을 정리할 때, 보스 위치로 모여들며 사라지는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float cloneGatherDespawnSeconds = 0.35f;
        [SerializeField, BossGraphSfxId] private string spawnSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin)
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

            context.PlaySfx(spawnSfxId);
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

            // 보상 탄은 ParryBaitRewardProjectile 자신이 Intercepted 처리 중에 이미 스폰한다.
            assassin.ClearActiveClonesGathering(cloneGatherDespawnSeconds);
            assassin.RequestStealth(false);

            // FireParrySuppressionBaitAction(그로기탄)과 동일한 처리: 그로기 진입/패턴 취소는 다음
            // 프레임의 Update로 미뤄지므로(재진입 방지), 그 처리가 실제로 일어날 때까지 한 프레임 더
            // 대기한다. 대기 도중 패턴 자체가 강제 종료되므로 이 코루틴이 "다음 액션"으로 자연스럽게
            // 넘어가는 일은 없다.
            assassin.RequestGroggy(groggySeconds);
            yield return null;
        }
    }
}
