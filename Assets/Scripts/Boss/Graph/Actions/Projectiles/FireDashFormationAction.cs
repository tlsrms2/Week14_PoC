using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum BossGraphDashFormationPattern
    {
        PerpendicularWall,
        ParallelLane
    }

    public enum BossGraphDashFormationFireOrder
    {
        Simultaneous,
        SequentialByIndex,
        SequentialOutsideIn
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
        [Tooltip("PerpendicularWall: 총알벽 내 인접 총알 간 간격. ParallelLane: 보스가 이 거리만큼 실제 이동할 때마다 트레일 탄 한 쌍을 흘립니다.")]
        [SerializeField, Min(0f)] private float spacing = 0.6f;
        [Tooltip("PerpendicularWall: 총알벽 전체를 대시 방향으로 이동시킬 거리. ParallelLane: 트레일 탄이 중앙선 좌우로 스폰되는 초기 거리입니다.")]
        [SerializeField] private float centerOffset = 1.2f;
        [Tooltip("페어링된 BossDashAction과 같은 방향이 나오도록 설정하세요. AtPlayer는 BossDashAction의 기본 방향 계산과 동일합니다.")]
        [SerializeField] private BossGraphProjectileAimSpec dashAim = new();

        [Tooltip("ParallelLane 전용. 병렬 BossDashAction의 windupSeconds와 같은 값을 넣어 실제 대시 시작 시점부터 탄을 흘립니다.")]
        [SerializeField, Min(0f)] private float dashStartDelay;

        [Tooltip("PerpendicularWall 전용. 총알이 스폰 지점에서 포메이션 위치까지 이동하는 시간입니다.")]
        [SerializeField, Min(0f)] private float alignDuration = 0.15f;
        [SerializeField] private AnimationCurve alignEase;
        [SerializeField, HideInInspector] private bool alignEaseInitialized;
        [Tooltip("탄이 자리를 잡은 뒤 발사까지 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.3f;

        [Tooltip("PerpendicularWall 전용. Simultaneous는 동시 발사, SequentialByIndex는 중심→바깥, SequentialOutsideIn은 바깥→중심 순서로 페어씩 발사합니다.")]
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

            yield return ExecutePerpendicularWall(context);
        }

        private IEnumerator ExecutePerpendicularWall(BossActionContext context)
        {
            Vector3 originPosition = context.OriginPosition;
            BossGraphProjectileAimSpec aimSpec = dashAim ?? new BossGraphProjectileAimSpec();
            Vector2 dashDirection = aimSpec.GetDirection(context, originPosition);
            Vector2 perpendicular = new(-dashDirection.y, dashDirection.x);

            bool reverseOrder = fireOrder == BossGraphDashFormationFireOrder.SequentialOutsideIn;
            bool sequential = reverseOrder || fireOrder == BossGraphDashFormationFireOrder.SequentialByIndex;

            for (int step = 0; step < bulletCount; step++)
            {
                int i = reverseOrder ? bulletCount - 1 - step : step;
                SpawnWallBullet(context, originPosition, dashDirection, perpendicular, i);

                bool isLaneBoundary = step % 2 == 1 || step == bulletCount - 1;
                if (sequential && isLaneBoundary && step < bulletCount - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }

            context.PlayCameraShakeIfEnabled(effects, dashDirection);
        }

        private void SpawnWallBullet(BossActionContext context, Vector3 originPosition, Vector2 dashDirection, Vector2 perpendicular, int index)
        {
            Vector3 targetPosition = GetWallTargetPosition(originPosition, dashDirection, perpendicular, index);
            float chargeSeconds = alignDuration + holdSeconds;

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
                return;
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
            context.PlayMuzzleFlashIfEnabled(effects, spawned, dashDirection);
        }

        private IEnumerator ExecuteParallelLaneTrail(BossActionContext context)
        {
            float waited = 0f;
            while (waited < dashStartDelay)
            {
                if (!context.IsExecutionPaused)
                {
                    waited += EnemyTimeScale.DeltaTime;
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

                elapsed += EnemyTimeScale.DeltaTime;
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
                context.PlayMuzzleFlashIfEnabled(effects, spawned, launchDirection);
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
