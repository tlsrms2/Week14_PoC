using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    // 보스가 이동하는 동안(다른 이동 액션과 그래프에서 P 포트로 병렬 연결해서 같이 실행) 실제로 이동한
    // 거리를 기준으로 탄을 스폰한다. Move Distance(=병렬로 도는 이동 액션이 실제로 이동할 총 거리)를
    // Bullet Count개로 균등하게 나눠서, 그 간격만큼 실제로 움직였을 때마다 탄을 하나씩 흘린다.
    // FireProjectileBurstAction처럼 "시간 간격"이 아니라 "이동 거리 간격"이라서, 가속/감속 커브 등으로
    // 이동 속도가 일정하지 않아도 궤적 위에 항상 고르게 탄이 떨어진다.
    // 조준/충전 오버라이드를 강제하지 않으므로, 탄 프리셋의 Aim At Player On Launch 등이 그대로 적용된다
    // (예: 충전이 끝나면 그 순간의 플레이어 방향으로 날아가는 탄을 그대로 궤적에 흘릴 수 있다).
    [Serializable]
    public sealed class FireDistanceTrailProjectilesAction : BossAction
    {
        private const float SafetyTimeoutSeconds = 5f;
        private const float StoppedSpeedThresholdSqr = 0.0025f;
        // 병렬로 같이 도는 이동 액션이 속도를 걸기까지 최소 한 프레임 지연될 수 있어서, 시작 직후에는
        // "멈췄는지" 판정을 건너뛴다 — 안 그러면 아직 이동 액션이 속도를 걸기도 전에 0으로 보여서
        // 첫 탄만 나가고 바로 멈춰버리는 오판이 생긴다.
        private const float StoppedCheckGraceSeconds = 0.1f;

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [Tooltip("함께(병렬로) 실행 중인 이동 액션이 실제로 이동할 총 거리입니다. 이동 액션의 Distance 값과 같게 맞추세요.")]
        [SerializeField, Min(0.01f)] private float moveDistance = 4f;
        [Tooltip("Move Distance 구간에 균등하게 흘릴 탄 개수입니다. 시작 지점에 1발이 바로 스폰되고, 나머지는 이동 거리를 (개수-1)등분한 간격마다 하나씩 스폰됩니다.")]
        [SerializeField, Min(1)] private int bulletCount = 5;
        [Tooltip("0 이상이면 Projectile Settings의 Charge Seconds 대신 이 값을 사용합니다. 음수(-1)면 오버라이드하지 않습니다.")]
        [SerializeField] private float chargeSecondsOverride = -1f;
        [Tooltip("플레이어 방향 조준에 좌우로 무작위 오프셋을 주는 각도 범위(도)입니다. 0이면 정확히 플레이어를 조준합니다.")]
        [SerializeField, Range(0f, 180f)] private float aimSpreadDegrees;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            int count = Mathf.Max(1, bulletCount);
            float spacing = count > 1 ? moveDistance / (count - 1) : 0f;

            Vector3 lastSpawnPosition = context.OriginPosition;
            SpawnTrailBullet(context, lastSpawnPosition);
            int spawnedCount = 1;
            float elapsed = 0f;

            while (spawnedCount < count && elapsed < SafetyTimeoutSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector3 currentPosition = context.OriginPosition;
                if (Vector3.Distance(currentPosition, lastSpawnPosition) >= spacing)
                {
                    SpawnTrailBullet(context, currentPosition);
                    lastSpawnPosition = currentPosition;
                    spawnedCount++;
                }
                else if (elapsed >= StoppedCheckGraceSeconds && IsStopped(context))
                {
                    break;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private void SpawnTrailBullet(BossActionContext context, Vector3 spawnOrigin)
        {
            Vector2 direction = GetSpreadDirectionToPlayer(context, spawnOrigin);
            EnemyProjectile spawned = context.FireProjectile(
                projectile,
                spawnOrigin,
                direction,
                0f,
                chargeSecondsOverride: chargeSecondsOverride,
                projectileName: projectileName);

            if (spawned == null)
            {
                return;
            }

            // 충전 중(발사 전)에는 조준선(경로 인디케이터)을 숨기고, 실제로 발사되는 순간부터 보이게 한다.
            spawned.ConfigurePathIndicatorDelayedUntilLaunch(true);

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(spawned, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
        }

        private static bool IsStopped(BossActionContext context)
        {
            Rigidbody2D body = context.Boss != null ? context.Boss.Body : null;
            return body != null && body.linearVelocity.sqrMagnitude < StoppedSpeedThresholdSqr;
        }

        private Vector2 GetSpreadDirectionToPlayer(BossActionContext context, Vector3 spawnOrigin)
        {
            Vector2 direction = context.GetDirectionToPlayer(spawnOrigin);
            if (aimSpreadDegrees <= 0f)
            {
                return direction;
            }

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = aimSpreadDegrees * 0.5f;
            return BossActionContext.AngleToDirection(baseAngle + UnityEngine.Random.Range(-halfSpread, halfSpread));
        }
    }
}
