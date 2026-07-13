using System;
using System.Collections;
using UnityEngine;

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
        [Tooltip("궤적 표시에 사용할 스프라이트입니다. 비워두면 궤적 VFX를 표시하지 않습니다.")]
        [SerializeField] private Sprite trajectorySprite;
        [SerializeField, Min(0.01f)] private float trajectoryWidth = 0.3f;
        [SerializeField] private Color trajectoryBackgroundColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color trajectoryFillColor = new Color(1f, 0.4f, 0.1f, 0.6f);
        [Tooltip("스프라이트 렌더러 Sorting Order입니다.")]
        [SerializeField] private int trajectorySortingOrder = 5;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            EnsureSpeedCurve();
            context.PlaySfx(windupSfxId);
            context.PlayAnimationTrigger(chargeTriggerName);

            float trackDuration = Mathf.Max(0f, windupSeconds - lockSeconds);
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            Vector2 dashDirection = context.GetDirectionToPlayer(context.OriginPosition);

            BossDashTrajectoryVfx trajectoryVfx = SpawnTrajectoryVfx();
            if (trajectoryVfx != null)
            {
                context.RegisterTransientVisual(trajectoryVfx.gameObject);
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
                trajectoryVfx?.UpdateVfx(context.OriginPosition, dashDirection, elapsed / windupSeconds);
                elapsed += EnemyTimeScale.DeltaTime;
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
                trajectoryVfx?.UpdateVfx(context.OriginPosition, dashDirection, elapsed / windupSeconds);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (trajectoryVfx != null)
            {
                context.UnregisterTransientVisual(trajectoryVfx.gameObject);
                UnityEngine.Object.Destroy(trajectoryVfx.gameObject);
            }

            // 대쉬 페이즈
            context.SetAnimationBool(chargeBoolName, true);
            context.SetDashing(true);
            context.PlaySfx(dashSfxId);
            elapsed = 0f;
            while (elapsed < dashDuration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / dashDuration);
                context.Boss.SetMovementVelocity(dashDirection * (dashSpeed * speedCurve.Evaluate(t)));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.SetAnimationBool(chargeBoolName, false);
            context.SetDashing(false);
            context.SetFacingLocked(false);
            context.Stop();
        }

        public void OnBeforeSerialize() => EnsureSpeedCurve();
        public void OnAfterDeserialize() => EnsureSpeedCurve();

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + Mathf.Max(0.05f, dashDuration);
            return seconds > 0f;
        }

        private BossDashTrajectoryVfx SpawnTrajectoryVfx()
        {
            if (trajectorySprite == null)
            {
                return null;
            }

            return BossDashTrajectoryVfx.Spawn(
                trajectorySprite,
                dashSpeed * dashDuration,
                trajectoryWidth,
                trajectoryBackgroundColor,
                trajectoryFillColor,
                trajectorySortingOrder);
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
