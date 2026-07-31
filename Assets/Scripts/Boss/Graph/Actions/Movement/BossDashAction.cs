using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class BossDashAction : BossAction, ISerializationCallbackReceiver, IBossActionDurationProvider
    {
        [Header("Windup")]
        [Tooltip("차징 총 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float windupSeconds = 1.5f;
        [Tooltip("대쉬 시작 몇 초 전에 방향을 고정할지 설정합니다.")]
        [SerializeField, Min(0f)] private float lockSeconds = 0.5f;
        [Tooltip("켜져 있으면 방향 잠금 전까지 플레이어를 계속 추적합니다. 끄면 차징 시작 시점의 방향으로 즉시 고정됩니다.")]
        [SerializeField] private bool trackPlayerDuringWindup = true;
        [Tooltip("추적 중 방향을 초당 최대 몇 도까지 회전시킬지 제한합니다. 0이면 제한 없이 즉시 플레이어 방향으로 스냅합니다. " +
            "EnemyTimeScale이 적용되어, 시간 슬로우 스킬로 보스 이동/투사체가 느려질 때 이 회전 속도도 똑같이 느려집니다.")]
        [SerializeField, Min(0f)] private float maxTrackTurnDegreesPerSecond = 0f;
        [SerializeField, BossGraphSfxId] private string windupSfxId;
        [SerializeField] private BossGraphEffectSettings windupEffects = new();

        [Header("Dash")]
        [SerializeField, Min(0.05f)] private float dashDuration = 0.4f;
        [SerializeField, Min(0f)] private float dashSpeed = 15f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField, BossGraphSfxId] private string dashSfxId;

        [Header("Animation")]
        [Tooltip("Execute 시작 시 발동할 애니메이터 트리거 이름입니다. 비워두면 호출하지 않습니다.")]
        [SerializeField] private string chargeTriggerName = "Charge";
        [Tooltip("윈드업이 끝나고 실제 대쉬 이동이 시작될 때 true, 대쉬 이동이 끝나면 false로 설정할 애니메이터 Bool 파라미터 이름입니다. 비워두면 호출하지 않습니다.")]
        [SerializeField] private string chargeBoolName = "isCharge";

        [Header("Trajectory VFX")]
        [Tooltip("경로를 이어 붙일 정사각형 화살표 인디케이터 프리팹입니다. 프리팹의 Animator와 Sorting 설정을 그대로 사용합니다.")]
        [SerializeField] private GameObject trajectoryIndicatorPrefab;
        [Tooltip("인디케이터 한 칸이 차지할 경로 길이입니다. 0이면 프리팹 SpriteRenderer의 가로 크기를 자동으로 사용합니다.")]
        [SerializeField, Min(0f)] private float trajectoryTileSpacing;
        [Tooltip("프리팹 화살표가 오른쪽을 향하지 않을 때 보정할 Z축 회전값입니다.")]
        [SerializeField] private float trajectoryTileRotationOffset;
        [Tooltip("대기 시작 시 인디케이터 색입니다.")]
        [SerializeField] private Color trajectoryReadyColor = Color.white;
        [Tooltip("대기가 끝났을 때 인디케이터 색입니다.")]
        [SerializeField] private Color trajectoryChargedColor = Color.red;

        [Header("Dash Dust VFX")]
        [Tooltip("실제 대쉬가 시작될 때 보스 뒤에 한 번 생성할 먼지 이펙트 프리팹입니다. 오른쪽 대쉬 방향을 기준으로 제작된 프리팹을 사용합니다.")]
        [SerializeField] private GameObject dashDustEffectPrefab;
        [Tooltip("Boss Graph Editor 하이어러키에서 먼지 생성 위치를 드래그해 지정합니다.")]
        [SerializeField, BossGraphBossChildPath] private string dashDustSpawnPointPath;
        [Tooltip("먼지 이펙트 프리팹 원본 스케일에 곱할 배율입니다.")]
        [SerializeField, Min(0.01f)] private float dashDustEffectScale = 1f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            EnsureSpeedCurve();
            SoundManager.SfxPlaybackHandle windupSfxHandle = null;
            if (windupSeconds > 0f)
            {
                windupSfxHandle = context.PlaySfx(string.IsNullOrWhiteSpace(windupSfxId)
                    ? GameplaySfxIds.BossBeforeDash
                    : windupSfxId);
            }
            context.PlayAnimationTrigger(chargeTriggerName);

            float trackDuration = Mathf.Max(0f, windupSeconds - lockSeconds);
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            Vector2 dashDirection = context.GetDirectionToPlayer(context.OriginPosition);

            float dashDistance = ComputeDashDistance();
            BossDashTrajectoryVfx trajectoryVfx = SpawnTrajectoryVfx(dashDistance);
            BossDashAttackArea attackArea = trajectoryVfx != null
                ? trajectoryVfx.AttackArea
                : default;
            if (trajectoryVfx != null)
            {
                context.RegisterTransientVisual(trajectoryVfx.gameObject);
                trajectoryVfx.UpdateVfx(context.OriginPosition, dashDirection, 0f);
            }

            // 추적 페이즈
            while (elapsed < trackDuration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.Stop();
                if (trackPlayerDuringWindup)
                {
                    Vector2 targetDirection = context.GetDirectionToPlayer(context.OriginPosition);
                    dashDirection = maxTrackTurnDegreesPerSecond > 0f
                        ? RotateTowards(dashDirection, targetDirection, maxTrackTurnDegreesPerSecond * EnemyTimeScale.DeltaTime)
                        : targetDirection;
                }
                context.PlaySmokeIfDue(ref nextSmokeAt, windupEffects, context.OriginPosition);
                elapsed += EnemyTimeScale.DeltaTime;
                trajectoryVfx?.UpdateVfx(context.OriginPosition, dashDirection, elapsed / windupSeconds);
                yield return null;
            }

            // 방향 잠금 페이즈
            context.SetFacingLocked(true);
            while (elapsed < windupSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.Stop();
                context.PlaySmokeIfDue(ref nextSmokeAt, windupEffects, context.OriginPosition);
                elapsed += EnemyTimeScale.DeltaTime;
                trajectoryVfx?.UpdateVfx(context.OriginPosition, dashDirection, elapsed / windupSeconds);
                yield return null;
            }

            if (trajectoryVfx != null)
            {
                context.UnregisterTransientVisual(trajectoryVfx.gameObject);
                trajectoryVfx.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(trajectoryVfx.gameObject);
            }
            context.StopSfx(windupSfxHandle);

            // 대쉬 페이즈
            context.SetAnimationBool(chargeBoolName, true);
            context.SetDashing(true);
            context.SetAutomaticDashContactDamageSuppressed(attackArea.IsValid);
            context.PlaySfx(dashSfxId);
            PlayDashDustEffect(context, dashDirection);
            Vector2 attackOrigin = context.OriginPosition;
            float previousAttackDistance = 0f;
            elapsed = 0f;
            try
            {
                while (elapsed < dashDuration)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    float t = Mathf.Clamp01(elapsed / dashDuration);
                    float speedMultiplier = Mathf.Max(0f, speedCurve.Evaluate(t));
                    context.Boss.SetMovementVelocity(dashDirection * (dashSpeed * speedMultiplier));

                    float nextElapsed = Mathf.Min(dashDuration, elapsed + EnemyTimeScale.DeltaTime);
                    float nextAttackDistance = BossDashMotion.GetDistanceAtProgress(
                        dashSpeed,
                        dashDuration,
                        speedCurve,
                        nextElapsed / dashDuration);
                    ApplyDashContactDamage(
                        context,
                        attackArea,
                        attackOrigin,
                        dashDirection,
                        previousAttackDistance,
                        nextAttackDistance);
                    previousAttackDistance = nextAttackDistance;
                    elapsed = nextElapsed;
                    yield return null;
                }
            }
            finally
            {
                context.SetAnimationBool(chargeBoolName, false);
                context.SetDashing(false);
                context.SetFacingLocked(false);
                context.Stop();
            }
        }

        public void OnBeforeSerialize() => EnsureSpeedCurve();
        public void OnAfterDeserialize() => EnsureSpeedCurve();

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + Mathf.Max(0.05f, dashDuration);
            return seconds > 0f;
        }

        private BossDashTrajectoryVfx SpawnTrajectoryVfx(float indicatorLength)
        {
            if (trajectoryIndicatorPrefab == null)
            {
                return null;
            }

            return BossDashTrajectoryVfx.Spawn(
                trajectoryIndicatorPrefab,
                indicatorLength,
                trajectoryTileSpacing,
                trajectoryTileRotationOffset,
                trajectoryReadyColor,
                trajectoryChargedColor);
        }

        private void PlayDashDustEffect(BossActionContext context, Vector2 dashDirection)
        {
            if (context == null || dashDustEffectPrefab == null)
            {
                return;
            }

            Transform spawnPoint = context.GetBossChildTransform(dashDustSpawnPointPath);
            if (spawnPoint == null)
            {
                return;
            }

            float yRotation = dashDirection.x < 0f ? 180f : 0f;
            ProjectileVfx.PlayPrefab(
                dashDustEffectPrefab,
                spawnPoint.position,
                Quaternion.Euler(0f, yRotation, 0f),
                null,
                Mathf.Max(0.01f, dashDustEffectScale),
                false);
        }

        // speedCurve가 등속(1)이 아니면 dashSpeed * dashDuration은 실제 이동 거리와 다르므로,
        // 커브를 적분해 실제로 이동할 거리를 근사한다. 대쉬 1회당 한 번만 호출됨.
        private float ComputeDashDistance()
        {
            return BossDashMotion.GetDistance(dashSpeed, dashDuration, speedCurve);
        }

        private static void ApplyDashContactDamage(
            BossActionContext context,
            BossDashAttackArea attackArea,
            Vector2 origin,
            Vector2 direction,
            float fromDistance,
            float toDistance)
        {
            if (!attackArea.TryGetSegmentBox(
                    origin,
                    direction,
                    fromDistance,
                    toDistance,
                    out Vector2 center,
                    out Vector2 size,
                    out float angle))
            {
                return;
            }

            context.Boss.ApplyDashContactDamageInBox(center, size, angle);
        }

        private static Vector2 RotateTowards(Vector2 current, Vector2 target, float maxDegreesDelta)
        {
            float currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxDegreesDelta) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(newAngle), Mathf.Sin(newAngle));
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            speedCurveInitialized = true;
        }
    }
}
