using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    // 분신 스폰 구역(cloneSpawnZone) 안에 패링 가능한 시한 폭탄을 여러 개 뿌린다. 구역은 분신 소환과
    // 공유하지만, 폭탄끼리 최소 간격/플레이어와 최소거리는 이 액션에서 따로 지정한다(분신과 다른 값을
    // 쓰고 싶을 수 있어서). 이 액션은 소환만 담당하고, 패링 판정이나 터질 때 무슨 일이 일어나는지는
    // 스폰되는 탄 프리팹(예: ParryTimedRadialBurstProjectile) 자신이 담당한다.
    [Serializable]
    public sealed class AssassinSpawnRandomBombsAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [Tooltip("스폰할 폭탄 개수입니다.")]
        [SerializeField, Min(1)] private int bombCount = 3;
        [Tooltip("폭탄끼리 서로 떨어져야 하는 최소 거리입니다.")]
        [SerializeField, Min(0f)] private float minSeparationDistance = 1.5f;
        [Tooltip("폭탄이 플레이어로부터 떨어져야 하는 최소 거리입니다.")]
        [SerializeField, Min(0f)] private float minDistanceFromPlayer = 2f;
        [Tooltip("폭탄을 하나씩 스폰할 때, 스폰 사이에 두는 대기 시간(초)입니다. 0이면 전부 동시에 스폰합니다.")]
        [SerializeField, Min(0f)] private float spawnInterval = 0.2f;
        [Tooltip("보스 위치에서 스폰 위치까지 날아가는 데 걸리는 시간(초)입니다. 날아가는 동안에는 플레이어와 상호작용(피격 판정, 패링)이 되지 않고, 도착하면 정상적으로 상호작용 가능해집니다.")]
        [SerializeField, Min(0f)] private float flightSeconds = 0.4f;
        [Tooltip("(착지 후) 패링되지 않고 버틸 수 있는 시간(초)입니다. 이 시간이 지나면 폭탄이 알아서 터집니다(폭발 방식은 탄 프리팹이 결정합니다).")]
        [SerializeField, Min(0f)] private float chargeSeconds = 1.5f;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin)
            {
                yield break;
            }

            List<Vector2> positions = assassin.GetSeparatedRandomZonePositions(
                Mathf.Max(1, bombCount),
                minSeparationDistance,
                minDistanceFromPlayer);

            for (int i = 0; i < positions.Count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector3 spawnOrigin = positions[i];
                Vector3 bossOrigin = context.OriginPosition;
                Vector2 direction = context.GetDirectionToPlayer(bossOrigin);

                EnemyProjectile spawned = context.FireProjectile(
                    projectile,
                    bossOrigin,
                    direction,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: flightSeconds + chargeSeconds,
                    projectileName: projectileName);

                if (spawned != null)
                {
                    context.PlayOriginBurst(effects, bossOrigin);

                    if (flightSeconds > 0f)
                    {
                        // 보스 위치에서 스폰 위치까지 날아가는 동안에는 플레이어와 상호작용(피격/패링)을
                        // 막아두고, 도착하는 순간 되돌린다. 폭탄 자신의 코루틴으로 돌려서, 다음 폭탄을
                        // 스폰하는 이 액션의 메인 루프(spawnInterval 대기)와 독립적으로 진행된다.
                        spawned.ConfigurePlayerCollisionIgnored(true);
                        spawned.ConfigureInterceptable(false);

                        Transform anchor = FormationAlignAnchor.Create(
                            bossOrigin,
                            spawnOrigin,
                            flightSeconds,
                            null,
                            flightSeconds + chargeSeconds + 0.5f);
                        spawned.ConfigureChargeAnchor(anchor);
                        spawned.ConfigureChargeMotion(0f, false, false);

                        spawned.StartCoroutine(ReenableInteractionAfterFlight(
                            spawned,
                            flightSeconds,
                            GameplaySfxIds.AssassinMine));
                    }
                    else
                    {
                        context.PlaySfx(GameplaySfxIds.AssassinMine);
                    }
                }

                if (i < positions.Count - 1 && spawnInterval > 0f)
                {
                    yield return context.WaitSeconds(spawnInterval);
                }
            }
        }

        private static IEnumerator ReenableInteractionAfterFlight(
            EnemyProjectile bomb,
            float delaySeconds,
            string installSfxId)
        {
            float remaining = delaySeconds;
            while (remaining > 0f)
            {
                remaining -= EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (bomb != null)
            {
                bomb.ConfigurePlayerCollisionIgnored(false);
                bomb.ConfigureInterceptable(true);
                SoundManager.PlayBossSfx(installSfxId);
            }
        }
    }
}
