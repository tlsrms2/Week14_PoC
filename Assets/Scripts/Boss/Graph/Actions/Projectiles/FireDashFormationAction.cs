using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    // Shared by two dash-synced bullet patterns:
    //  - PerpendicularWall: bullets line up across the dash direction, then fire parallel to the dash.
    //  - ParallelLane: bullets drop along the ground in real time as the boss's dash actually moves
    //    through the world (a trail left behind it), then each dropped pair fires outward, perpendicular
    //    to the dash. This requires the node to run in parallel with the paired BossDashAction node
    //    (graph parallel edge) so live boss position is available while it runs.
    public enum BossGraphDashFormationPattern
    {
        PerpendicularWall,
        ParallelLane
    }

    public enum BossGraphDashFormationFireOrder
    {
        Simultaneous,
        SequentialByIndex
    }

    [Serializable]
    public sealed class FireDashFormationAction : BossAction, ISerializationCallbackReceiver
    {
        private const float ParallelLaneSafetyTimeoutSeconds = 3f;
        private const float DashStoppedSpeedThresholdSqr = 0.0025f;

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();

        [SerializeField] private BossGraphDashFormationPattern pattern = BossGraphDashFormationPattern.PerpendicularWall;
        [SerializeField, Min(1)] private int bulletCount = 6;
        [Tooltip("PerpendicularWall: 총알벽 내 인접 총알 간 간격. ParallelLane: 보스가 이 거리만큼 실제로 이동할 때마다 트레일 탄 한 쌍을 흘립니다.")]
        [SerializeField, Min(0f)] private float spacing = 0.6f;
        [Tooltip("PerpendicularWall: 총알벽 전체를 대시 방향으로 얼마나 이동시킬지(원점 기준 고정 거리, 음수면 후방). ParallelLane: 트레일 탄이 중앙선에서 좌/우로 스폰되는 초기 거리(발사 시작점).")]
        [SerializeField] private float centerOffset = 1.2f;
        [Tooltip("페어링된 BossDashAction과 같은 방향이 나오도록 설정하세요. AtPlayer는 BossDashAction의 기본 방향 계산과 동일합니다.")]
        [SerializeField] private BossGraphProjectileAimSpec dashAim = new();

        [Tooltip("ParallelLane 전용. 이 노드를 페어링된 BossDashAction과 그래프에서 병렬로 연결한 뒤, 그 BossDashAction의 windupSeconds와 동일한 값을 넣으세요. 실제 대시 이동이 시작되는 시점부터 지나온 경로에 탄을 흘립니다.")]
        [SerializeField, Min(0f)] private float dashStartDelay;

        [Tooltip("PerpendicularWall 전용. 총알이 스폰 지점에서 포메이션 위치까지 슬라이드하는 시간입니다. ParallelLane은 실제 경로 위치에 즉시 스폰되므로 이 값을 사용하지 않습니다.")]
        [SerializeField, Min(0f)] private float alignDuration = 0.15f;
        [SerializeField] private AnimationCurve alignEase;
        [SerializeField, HideInInspector] private bool alignEaseInitialized;
        [Tooltip("탄이 자리를 잡은(또는 트레일에 스폰된) 뒤 발사까지 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.3f;

        [SerializeField] private BossGraphDashFormationFireOrder fireOrder = BossGraphDashFormationFireOrder.Simultaneous;
        [SerializeField, Min(0f)] private float fireInterval = 0.05f;
        [SerializeField, Min(0f)] private float windupSeconds;

        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public void OnBeforeSerialize() => EnsureAlignEase();
        public void OnAfterDeserialize() => EnsureAlignEase();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || bulletCount <= 0)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            EnsureAlignEase();

            if (pattern == BossGraphDashFormationPattern.ParallelLane)
            {
                yield return ExecuteParallelLaneTrail(context);
                yield break;
            }

            ExecutePerpendicularWall(context);
        }

        private void ExecutePerpendicularWall(BossActionContext context)
        {
            Vector3 originPosition = context.OriginPosition;
            BossGraphProjectileAimSpec aimSpec = dashAim ?? new BossGraphProjectileAimSpec();
            Vector2 dashDirection = aimSpec.GetDirection(context, originPosition);
            Vector2 perpendicular = new(-dashDirection.y, dashDirection.x);

            for (int i = 0; i < bulletCount; i++)
            {
                Vector3 targetPosition = GetWallTargetPosition(originPosition, dashDirection, perpendicular, i);
                float chargeSeconds = alignDuration + GetBulletHoldSeconds(i);

                EnemyProjectile spawned = context.FireProjectile(
                    projectile,
                    originPosition,
                    dashDirection,
                    0f,
                    false,
                    false,
                    chargeSeconds,
                    -1f,
                    false,
                    projectileName);

                if (spawned == null)
                {
                    continue;
                }

                Transform anchor = FormationAlignAnchor.Create(
                    originPosition,
                    targetPosition,
                    alignDuration,
                    alignEase,
                    chargeSeconds + 0.5f);
                spawned.ConfigureChargeAnchor(anchor);
                spawned.ConfigureChargeMotion(0f, false, false);

                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(spawned, launchSfxId);
                context.PlayOriginBurst(effects, originPosition);
                context.PlayMuzzleFlashIfEnabled(effects, targetPosition, dashDirection);
            }

            context.PlayCameraShakeIfEnabled(effects, dashDirection);
        }

        private IEnumerator ExecuteParallelLaneTrail(BossActionContext context)
        {
            float waited = 0f;
            while (waited < dashStartDelay)
            {
                if (!context.IsExecutionPaused)
                {
                    waited += Time.deltaTime;
                }

                yield return null;
            }

            BossGraphProjectileAimSpec aimSpec = dashAim ?? new BossGraphProjectileAimSpec();
            Vector3 startPosition = context.OriginPosition;
            Vector2 dashDirection = aimSpec.GetDirection(context, startPosition);
            Vector2 perpendicular = new(-dashDirection.y, dashDirection.x);

            int pairCount = Mathf.Max(1, bulletCount / 2);
            Vector3 lastSpawnPosition = startPosition;
            int spawnedPairs = 0;
            float elapsed = 0f;

            while (spawnedPairs < pairCount && elapsed < ParallelLaneSafetyTimeoutSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                Vector3 currentPosition = context.OriginPosition;
                if (Vector3.Distance(currentPosition, lastSpawnPosition) >= spacing)
                {
                    SpawnLanePair(context, currentPosition, perpendicular, spawnedPairs);
                    lastSpawnPosition = currentPosition;
                    spawnedPairs++;
                }
                else if (spawnedPairs > 0 && IsDashStopped(context))
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            context.PlayCameraShakeIfEnabled(effects, dashDirection);
        }

        private static bool IsDashStopped(BossActionContext context)
        {
            Rigidbody2D body = context.Boss != null ? context.Boss.Body : null;
            return body != null && body.linearVelocity.sqrMagnitude < DashStoppedSpeedThresholdSqr;
        }

        private void SpawnLanePair(BossActionContext context, Vector3 position, Vector2 perpendicular, int pairIndex)
        {
            for (int side = 0; side < 2; side++)
            {
                int index = pairIndex * 2 + side;
                float sideSign = side == 0 ? -1f : 1f;
                Vector3 spawnPosition = position + (Vector3)(perpendicular * centerOffset * sideSign);
                Vector2 launchDirection = perpendicular * sideSign;
                float chargeSeconds = GetBulletHoldSeconds(index);

                EnemyProjectile spawned = context.FireProjectile(
                    projectile,
                    spawnPosition,
                    launchDirection,
                    0f,
                    false,
                    false,
                    chargeSeconds,
                    -1f,
                    false,
                    projectileName);

                if (spawned == null)
                {
                    continue;
                }

                spawned.ConfigureChargeMotion(0f, false, false);

                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(spawned, launchSfxId);
                context.PlayMuzzleFlashIfEnabled(effects, spawnPosition, launchDirection);
            }

            context.PlayOriginBurst(effects, position);
        }

        private float GetBulletHoldSeconds(int index)
        {
            if (fireOrder == BossGraphDashFormationFireOrder.Simultaneous)
            {
                return holdSeconds;
            }

            int groupIndex = index / 2;
            return holdSeconds + fireInterval * groupIndex;
        }

        private Vector3 GetWallTargetPosition(Vector3 origin, Vector2 dashDirection, Vector2 perpendicular, int index)
        {
            int laneIndex = index / 2;
            float side = index % 2 == 0 ? -1f : 1f;
            float offset = spacing * laneIndex + spacing * 0.5f;
            return origin + (Vector3)(dashDirection * centerOffset) + (Vector3)(perpendicular * offset * side);
        }

        private void EnsureAlignEase()
        {
            if (alignEaseInitialized && alignEase != null && alignEase.length > 0)
            {
                return;
            }

            alignEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            alignEaseInitialized = true;
        }
    }
}
