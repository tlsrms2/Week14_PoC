using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class BossDashAction : BossAction, ISerializationCallbackReceiver
    {
        [Header("Windup")]
        [Tooltip("차징 총 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float windupSeconds = 1.5f;
        [Tooltip("대쉬 시작 몇 초 전에 방향을 고정할지 설정합니다.")]
        [SerializeField, Min(0f)] private float lockSeconds = 0.5f;
        [SerializeField, BossGraphSfxId] private string windupSfxId;
        [SerializeField] private BossGraphEffectSettings windupEffects = new();

        [Header("Dash")]
        [SerializeField, Min(0.05f)] private float dashDuration = 0.4f;
        [SerializeField, Min(0f)] private float dashSpeed = 15f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField, BossGraphSfxId] private string dashSfxId;

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

            float trackDuration = Mathf.Max(0f, windupSeconds - lockSeconds);
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            Vector2 dashDirection = context.GetDirectionToPlayer(context.OriginPosition);

            BossDashTrajectoryVfx trajectoryVfx = SpawnTrajectoryVfx();

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
                dashDirection = context.GetDirectionToPlayer(context.OriginPosition);
                context.PlaySmokeIfDue(ref nextSmokeAt, windupEffects, context.OriginPosition);
                trajectoryVfx?.UpdateVfx(context.OriginPosition, dashDirection, elapsed / windupSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 방향 잠금 페이즈
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
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (trajectoryVfx != null)
            {
                UnityEngine.Object.Destroy(trajectoryVfx.gameObject);
            }

            // 대쉬 페이즈
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
                context.Boss.Body.linearVelocity = dashDirection * (dashSpeed * speedCurve.Evaluate(t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            context.Stop();
        }

        public void OnBeforeSerialize() => EnsureSpeedCurve();
        public void OnAfterDeserialize() => EnsureSpeedCurve();

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
