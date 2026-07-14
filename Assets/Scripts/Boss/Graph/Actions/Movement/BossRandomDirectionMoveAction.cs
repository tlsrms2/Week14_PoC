using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class BossRandomDirectionMoveAction : BossAction, ISerializationCallbackReceiver, IBossActionDurationProvider
    {
        [SerializeField, Min(0.01f)] private float distance = 2f;
        [SerializeField, Min(0.01f)] private float duration = 0.3f;
        [Tooltip("켜면 아래 각도 범위 대신, 플레이어를 바라보는 방향 기준 좌/우(90도) 중 하나를 무작위로 골라 이동합니다.")]
        [SerializeField] private bool strafeAroundPlayer;
        [Tooltip("Strafe Around Player가 꺼져 있을 때 사용하는 랜덤 방향의 각도 범위입니다. 0~360이면 전방위, 예를 들어 -60~60이면 보스의 현재 오른쪽 기준 부채꼴 범위로 제한됩니다.")]
        [SerializeField] private float minAngleDegrees;
        [SerializeField] private float maxAngleDegrees = 360f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            Vector2 direction = GetDirection(context);
            float baseSpeed = distance / Mathf.Max(0.01f, duration);
            AnimationCurve curve = GetSpeedCurve();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                context.Boss.SetMovementVelocity(direction * (baseSpeed * curve.Evaluate(t)));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        private Vector2 GetDirection(BossActionContext context)
        {
            if (strafeAroundPlayer)
            {
                Vector2 toPlayer = context.GetDirectionToPlayer(context.OriginPosition);
                Vector2 side = new(-toPlayer.y, toPlayer.x);
                return UnityEngine.Random.value < 0.5f ? side : -side;
            }

            float lowAngle = Mathf.Min(minAngleDegrees, maxAngleDegrees);
            float highAngle = Mathf.Max(minAngleDegrees, maxAngleDegrees);
            return BossActionContext.AngleToDirection(UnityEngine.Random.Range(lowAngle, highAngle));
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, duration);
            return seconds > 0f;
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }
}
